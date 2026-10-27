using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Prodiga.CardanoSDK
{
    /// <summary>
    /// Milestone 3 - Minting Tokens.
    ///
    /// Attach to a "minter" GameObject, assign a MintingWalletSecret (create one via
    /// Tools > Cardano SDK > Wallet Setup), configure Token/Policy/Sponsorship, and call
    /// TriggerMint() from a button or other UnityEvent.
    ///
    /// Payer model: by default the PLAYER pays their own mint transaction's fee -- their connected
    /// wallet signs to authorize spending their own ADA, in addition to the minting wallet's
    /// required signature under its own policy (two signers on one transaction). If
    /// Sponsorship.AllowSponsorship is on, TriggerMint() checks the minting wallet's balance plus
    /// any configured ConditionGroup rules (evaluated against the PLAYER's wallet, same engine as
    /// GatedAccessController) and, if everything passes, the minting wallet covers the fee alone
    /// instead -- no signature required from the player at all. Falls back to player-pays whenever
    /// sponsorship isn't allowed or its conditions aren't met.
    ///
    /// SECURITY: the minting wallet's key lives in a WebGL build's own JS, which is extractable by
    /// anyone who opens dev tools -- see MintingWalletSecret's class summary. This is fine for a
    /// testnet/demo wallet, not for one holding real funds. The signing step is intentionally
    /// isolated behind the .jslib bridge (InitMintingWallet / MintToken) rather than scattered
    /// through this file, specifically so a future server-relay signer (the minting wallet's key
    /// living on a server instead, signing on request) could replace just that bridge without
    /// touching this controller or its public API.
    ///
    /// Requires the "com.unity.nuget.newtonsoft-json" package (already used by GatedAccessController)
    /// and cardano-mint-sdk.bundle.js loaded in the WebGL template before this runs.
    /// </summary>
    public class MintingController : MonoBehaviour
    {
        [System.Serializable] public class MintResultEvent : UnityEvent<string> { }

        [Header("SDK Reference (reads the PLAYER's wallet)")]
        [Tooltip("Reused for its ProjectId when talking to Blockfrost for the minting wallet too, so you don't have to configure it twice.")]
        public BlockfrostIntegration BlockfrostManager;
        public KoiosIntegration KoiosManager;

        [Header("Minting Wallet")]
        [Tooltip("Create via Tools > Cardano SDK > Wallet Setup. Never commit this asset -- see its class summary.")]
        public MintingWalletSecret WalletSecret;

        [Header("What To Mint")]
        public MintableTokenConfig Token;
        public MintPolicyConfig Policy;

        [Header("Sponsorship (optional)")]
        public MintSponsorshipConfig Sponsorship;

        [Header("UI (optional)")]
        public TMP_Text StatusText;
        [Tooltip("Read-only TMP_InputField showing the tx hash in selectable/copyable form after a successful mint. Turn on its \"Read Only\" checkbox in the Inspector -- this component doesn't set it for you.")]
        public TMP_InputField TxHashField;
        [Tooltip("Optional: a parent GameObject wrapping TxHashField + its copy button, shown after a successful mint. Leave unassigned to manage that row's visibility yourself.")]
        public GameObject TxHashRow;

        [Header("Events")]
        [Tooltip("Invoked with the transaction hash on a successful mint.")]
        public MintResultEvent OnMintSucceeded;
        [Tooltip("Invoked with an error message if the mint fails for any reason.")]
        public MintResultEvent OnMintFailed;

        [DllImport("__Internal")] private static extern void InitMintingWallet(string mnemonic, string network, string blockfrostProjectId, string objectName, string methodName);
        [DllImport("__Internal")] private static extern void GetMintingWalletBalance(string objectName, string methodName);
        [DllImport("__Internal")] private static extern void MintToken(string playerAddress, string assetName, string metadataJson, string quantity, string sponsored, string objectName, string methodName);

        /// <summary>The minting wallet's own address, filled in once InitMintingWallet's callback lands. Empty until then.</summary>
        public string MintingWalletAddress { get; private set; } = "";

        private bool _mintingWalletReady = false;
        private bool _mintInProgress = false;

        private bool PlayerIsConnected =>
            (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch)) ||
            (KoiosManager != null && !string.IsNullOrEmpty(KoiosManager.AddressToFetch));

        void OnEnable()
        {
            if (BlockfrostManager == null) BlockfrostManager = FindObjectOfType<BlockfrostIntegration>();
            if (KoiosManager == null) KoiosManager = FindObjectOfType<KoiosIntegration>();
            if (TxHashRow != null) TxHashRow.SetActive(false);

    #if UNITY_WEBGL && !UNITY_EDITOR
            if (WalletSecret == null || string.IsNullOrEmpty(WalletSecret.Mnemonic))
            {
                SetStatus("Minting wallet not configured -- run Tools > Cardano SDK > Wallet Setup.");
                return;
            }
            if (BlockfrostManager == null || string.IsNullOrEmpty(BlockfrostManager.ProjectId))
            {
                SetStatus("No Blockfrost Project ID available (set it on the BlockfrostManager this component references).");
                return;
            }

            InitMintingWallet(WalletSecret.Mnemonic, WalletSecret.Network, BlockfrostManager.ProjectId, gameObject.name, nameof(OnMintingWalletInitialized));
    #else
            SetStatus("Minting requires a WebGL build.");
    #endif
        }

        /// <summary>jslib callback for InitMintingWallet. "OK|<address>" or "ERROR: ...".</summary>
        public void OnMintingWalletInitialized(string data)
        {
            if (data.StartsWith("ERROR"))
            {
                Debug.LogError("[Cardano SDK] " + data);
                SetStatus(data);
                return;
            }

            MintingWalletAddress = data.Substring(3); // strip "OK|"
            _mintingWalletReady = true;
            SetStatus("Ready to mint.");
        }

        /// <summary>Call this from a button's OnClick (or any other trigger) to start a mint.</summary>
        public void TriggerMint()
        {
            if (_mintInProgress)
            {
                SetStatus("A mint is already in progress.");
                return;
            }
            if (!_mintingWalletReady)
            {
                Fail("Minting wallet isn't ready yet.");
                return;
            }
            if (!PlayerIsConnected)
            {
                Fail("Connect a wallet before minting.");
                return;
            }
            if (string.IsNullOrEmpty(Token?.AssetName))
            {
                Fail("No AssetName configured on Token.");
                return;
            }
            if (Token.EnforceUniqueness)
            {
                // Deliberately not implemented tonight -- see MintableTokenConfig's class summary.
                // Minting under this flag without the numbering/prior-mint-check in place would
                // silently produce something other than what "EnforceUniqueness" promises, which is
                // worse than just refusing outright.
                Fail("EnforceUniqueness isn't implemented yet -- turn it off to mint the duplicate-permitted way.");
                return;
            }

            _mintInProgress = true;
            SetStatus("Checking sponsorship...");

            if (Sponsorship != null && Sponsorship.AllowSponsorship)
            {
                GetMintingWalletBalance(gameObject.name, nameof(OnBalanceCheckedForSponsorship));
            }
            else
            {
                BuildAndSendMint(sponsored: false);
            }
        }

        // Conservative flat estimate covering a simple mint transaction's fee plus the ~1-1.5 ADA
        // minimum-UTXO a native-asset output requires. Deliberately not computed exactly (that would
        // mean building the transaction twice -- once to check, once for real); this constant just
        // needs to comfortably cover the real cost, not match it precisely.
        private const long EstimatedMintCostLovelace = 2_500_000;

        /// <summary>jslib callback for GetMintingWalletBalance. "OK|<lovelace>" or "ERROR: ...".</summary>
        public void OnBalanceCheckedForSponsorship(string data)
        {
            if (data.StartsWith("ERROR"))
            {
                Debug.LogWarning("[Cardano SDK] Balance check failed, falling back to player-pays: " + data);
                BuildAndSendMint(sponsored: false);
                return;
            }

            long balance = 0;
            long.TryParse(data.Substring(3), out balance);

            bool balanceOk = (balance - EstimatedMintCostLovelace) >= Sponsorship.MinimumRemainingLovelace;
            bool conditionsOk = EvaluateSponsorshipConditions();

            BuildAndSendMint(sponsored: balanceOk && conditionsOk);
        }

        // AssetPresent/QuantityThreshold only -- MetadataMatch isn't supported here (it needs async
        // metadata fetching, which doesn't fit a one-shot mint-button check). Mirrors
        // GatedAccessController's evaluation logic against the PLAYER's wallet rather than the gate.
        private bool EvaluateSponsorshipConditions()
        {
            var groups = Sponsorship?.Conditions;
            if (groups == null || groups.Length == 0) return true; // no conditions configured = unconditional (balance check still applies separately)

            foreach (var group in groups)
            {
                if (group?.Conditions == null || group.Conditions.Length == 0) continue;

                bool groupPasses = true;
                foreach (var c in group.Conditions)
                {
                    bool result;
                    switch (c.ConditionType)
                    {
                        case AssetConditionType.AssetPresent:
                            result = CheckAssetPresent(c.PolicyId, c.AssetNameHex);
                            break;
                        case AssetConditionType.QuantityThreshold:
                            result = CheckQuantityThreshold(c);
                            break;
                        default:
                            Debug.LogWarning("[Cardano SDK] MetadataMatch sponsorship conditions aren't supported -- treating as failed.");
                            result = false;
                            break;
                    }
                    if (c.Invert) result = !result;
                    if (!result) { groupPasses = false; break; }
                }
                if (groupPasses) return true;
            }
            return false;
        }

        private bool CheckAssetPresent(string policyId, string assetNameHex)
        {
            if (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch))
            {
                return string.IsNullOrEmpty(assetNameHex)
                    ? BlockfrostManager.HasAnyAssetUnderPolicy(policyId)
                    : BlockfrostManager.HasAsset(policyId + assetNameHex, 1);
            }
            if (KoiosManager != null && !string.IsNullOrEmpty(KoiosManager.AddressToFetch))
            {
                return KoiosManager.HasAsset(policyId, assetNameHex, 1);
            }
            return false;
        }

        private bool CheckQuantityThreshold(AssetCondition c)
        {
            long qty = 0;
            if (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch))
                qty = BlockfrostManager.GetAssetQuantity(c.PolicyId, c.AssetNameHex);
            else if (KoiosManager != null && !string.IsNullOrEmpty(KoiosManager.AddressToFetch))
                qty = KoiosManager.GetAssetQuantity(c.PolicyId, c.AssetNameHex);

            long effectiveMin = c.UseMinQuantity ? c.MinQuantity : 1;
            if (qty < effectiveMin) return false;
            if (c.UseMaxQuantity && qty > c.MaxQuantity) return false;
            return true;
        }

        private void BuildAndSendMint(bool sponsored)
        {
            string playerAddress = (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch))
                ? BlockfrostManager.AddressToFetch
                : KoiosManager?.AddressToFetch;

            string metadataJson = BuildCip25MetadataJson();
            string quantity = Token.Quantity.ToString();

            SetStatus(sponsored ? "Minting (sponsored)..." : "Minting -- check your wallet to sign...");

    #if UNITY_WEBGL && !UNITY_EDITOR
            MintToken(playerAddress, Token.AssetName, metadataJson, quantity, sponsored ? "1" : "0", gameObject.name, nameof(OnMintResult));
    #else
            Fail("Minting requires a WebGL build.");
    #endif
        }

        private string BuildCip25MetadataJson()
        {
            var metadata = new JObject
            {
                ["name"] = Token.DisplayName,
                ["image"] = Token.ImageUri,
                ["mediaType"] = Token.ImageMediaType,
                ["description"] = Token.Description,
            };

            if (Token.Attributes != null && Token.Attributes.Length > 0)
            {
                var attrs = new JObject();
                foreach (var attr in Token.Attributes)
                {
                    if (!string.IsNullOrEmpty(attr.Key)) attrs[attr.Key] = attr.Value;
                }
                metadata["attributes"] = attrs;
            }

            return metadata.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>jslib callback for MintToken. "OK|<txHash>" or "ERROR: ...".</summary>
        public void OnMintResult(string data)
        {
            _mintInProgress = false;

            if (data.StartsWith("ERROR"))
            {
                Fail(data);
                return;
            }

            string txHash = data.Substring(3);
            SetStatus("Minted!");
            if (TxHashField != null) TxHashField.text = txHash;
            if (TxHashRow != null) TxHashRow.SetActive(true);
            OnMintSucceeded?.Invoke(txHash);
        }

        /// <summary>Opens the last minted tx on Cardanoscan, in the network matching WalletSecret.Network. Wire this to a button's OnClick.</summary>
        public void OpenTransactionOnExplorer()
        {
            if (TxHashField == null || string.IsNullOrEmpty(TxHashField.text))
            {
                Debug.LogWarning("[Cardano SDK] No transaction hash to open yet.");
                return;
            }

            string network = WalletSecret != null ? WalletSecret.Network : "mainnet";
            string baseUrl = network.Equals("preprod", System.StringComparison.OrdinalIgnoreCase) ? "https://preprod.cardanoscan.io/transaction/"
                : network.Equals("preview", System.StringComparison.OrdinalIgnoreCase) ? "https://preview.cardanoscan.io/transaction/"
                : "https://cardanoscan.io/transaction/";

            Application.OpenURL(baseUrl + TxHashField.text);
        }

        private void Fail(string message)
        {
            _mintInProgress = false;
            Debug.LogWarning("[Cardano SDK] " + message);
            SetStatus(message);
            OnMintFailed?.Invoke(message);
        }

        private void SetStatus(string message)
        {
            if (StatusText != null) StatusText.text = message;
        }
    }
}
