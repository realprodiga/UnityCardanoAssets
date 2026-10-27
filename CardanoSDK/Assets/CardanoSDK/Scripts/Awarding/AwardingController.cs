using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Prodiga.CardanoSDK
{
    /// <summary>
    /// Milestone 3 - Awarding Tokens.
    ///
    /// Sends an EXISTING on-chain token (already minted, already sitting in a treasury wallet) to
    /// the connected player's wallet -- as opposed to MintingController, which forges a brand new
    /// token on demand. This is the shape for "here's a token I already have plenty of, hand one to
    /// whoever completed the objective": achievement badges pre-minted in bulk, in-game currency,
    /// treasure-hunt rewards, etc.
    ///
    /// Attach to a "treasury" GameObject, assign a wallet secret (Tools > Cardano SDK > Wallet
    /// Setup -- the SAME MintingWalletSecret asset type used for the minting wallet; point this at
    /// a fresh asset if the treasury should be a separate wallet from the minting wallet, or drag
    /// the minting wallet's own asset onto this field if one wallet is doing both jobs), configure
    /// Token/Sponsorship, and call TriggerAward() from a button or other UnityEvent (a UI button
    /// here in the demo scene; in the treasure-hunt vision this would instead fire when a player
    /// turns in accumulated in-world tokens at an exchange point).
    ///
    /// Payer model: mirrors MintingController. By default the PLAYER pays the transfer's fee --
    /// their wallet signs to authorize spending their own ADA, alongside the treasury wallet's
    /// signature authorizing the spend of its own UTXO holding the token (two signers on one
    /// transaction). If Sponsorship.AllowSponsorship is on, TriggerAward() checks the treasury
    /// wallet's balance plus any configured ConditionGroup rules (evaluated against the PLAYER's
    /// wallet, same engine as GatedAccessController/MintingController) and, if everything passes,
    /// the treasury wallet covers the fee alone instead -- no signature required from the player.
    /// Falls back to player-pays whenever sponsorship isn't allowed or its conditions aren't met.
    ///
    /// SECURITY: same caveat as MintingWalletSecret -- the treasury wallet's key lives in the WebGL
    /// build's own JS and is extractable by anyone who opens dev tools. Fine for a testnet/demo
    /// treasury with modest funds; NOT safe for a treasury holding funds you can't afford to lose.
    /// A community-sponsored treasury (publicizing the address so supporters can send in funds or
    /// tokens) inherits this same exposure -- publicizing the address is fine, but whatever's in it
    /// is only as safe as "anyone can read the client-side key."
    ///
    /// Requires the "com.unity.nuget.newtonsoft-json" package (already used by MintingController)
    /// and cardano-mint-sdk.bundle.js loaded in the WebGL template before this runs --
    /// Cardanotreasuryinterop.jslib reuses the same window.CardanoMintSDK bundle as the minting
    /// bridge, it does not need a second copy of the library.
    /// </summary>
    public class AwardingController : MonoBehaviour
    {
        [System.Serializable] public class AwardResultEvent : UnityEvent<string> { }

        [Header("SDK Reference (reads the PLAYER's wallet)")]
        [Tooltip("Reused for its ProjectId when talking to Blockfrost for the treasury wallet too, so you don't have to configure it twice.")]
        public BlockfrostIntegration BlockfrostManager;
        public KoiosIntegration KoiosManager;

        [Header("Treasury Wallet")]
        [Tooltip("Create/edit via Tools > Cardano SDK > Wallet Setup (Treasury Wallet tab). Can be the SAME asset as MintingController's WalletSecret if one wallet handles both jobs. Never commit this asset -- see its class summary.")]
        public MintingWalletSecret TreasuryWalletSecret;

        [Header("What To Award")]
        public AwardableTokenConfig Token;

        [Header("Sponsorship (optional)")]
        [Tooltip("Reuses the same sponsorship model as MintingController: unconditional balance protection plus optional player-side conditions, evaluated with the same engine as GatedAccessController.")]
        public MintSponsorshipConfig Sponsorship;

        [Header("UI (optional)")]
        public TMP_Text StatusText;
        [Tooltip("Read-only TMP_InputField showing the tx hash in selectable/copyable form after a successful award. Turn on its \"Read Only\" checkbox in the Inspector -- this component doesn't set it for you.")]
        public TMP_InputField TxHashField;
        [Tooltip("Optional: a parent GameObject wrapping TxHashField + its copy button, shown after a successful award. Leave unassigned to manage that row's visibility yourself.")]
        public GameObject TxHashRow;

        [Header("Events")]
        [Tooltip("Invoked with the transaction hash on a successful award.")]
        public AwardResultEvent OnAwardSucceeded;
        [Tooltip("Invoked with an error message if the award fails for any reason.")]
        public AwardResultEvent OnAwardFailed;

        [DllImport("__Internal")] private static extern void InitTreasuryWallet(string mnemonic, string network, string blockfrostProjectId, string objectName, string methodName);
        [DllImport("__Internal")] private static extern void GetTreasuryWalletBalance(string objectName, string methodName);
        [DllImport("__Internal")] private static extern void AwardToken(string playerAddress, string unit, string quantity, string sponsored, string objectName, string methodName);

        /// <summary>The treasury wallet's own address, filled in once InitTreasuryWallet's callback lands. Empty until then.</summary>
        public string TreasuryWalletAddress { get; private set; } = "";

        private bool _treasuryWalletReady = false;
        private bool _awardInProgress = false;

        private bool PlayerIsConnected =>
            (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch)) ||
            (KoiosManager != null && !string.IsNullOrEmpty(KoiosManager.AddressToFetch));

        void OnEnable()
        {   
            if (BlockfrostManager == null) BlockfrostManager = FindObjectOfType<BlockfrostIntegration>();
            if (KoiosManager == null) KoiosManager = FindObjectOfType<KoiosIntegration>();
            if (TxHashRow != null) TxHashRow.SetActive(false);

    #if UNITY_WEBGL && !UNITY_EDITOR
            if (TreasuryWalletSecret == null || string.IsNullOrEmpty(TreasuryWalletSecret.Mnemonic))
            {
                SetStatus("Treasury wallet not configured -- run Tools > Cardano SDK > Wallet Setup (Treasury Wallet tab).");
                return;
            }
            if (BlockfrostManager == null || string.IsNullOrEmpty(BlockfrostManager.ProjectId))
            {
                SetStatus("No Blockfrost Project ID available (set it on the BlockfrostManager this component references).");
                return;
            }

            InitTreasuryWallet(TreasuryWalletSecret.Mnemonic, TreasuryWalletSecret.Network, BlockfrostManager.ProjectId, gameObject.name, nameof(OnTreasuryWalletInitialized));
    #else
            SetStatus("Awarding requires a WebGL build.");
    #endif
        }

        /// <summary>jslib callback for InitTreasuryWallet. "OK|&lt;address&gt;" or "ERROR: ...".</summary>
        public void OnTreasuryWalletInitialized(string data)
        {
            if (data.StartsWith("ERROR"))
            {
                Debug.LogError("[Cardano SDK] " + data);
                SetStatus(data);
                return;
            }

            TreasuryWalletAddress = data.Substring(3); // strip "OK|"
            _treasuryWalletReady = true;
            SetStatus("Ready to award.");
        }

        /// <summary>Call this from a button's OnClick (or any other trigger) to award Token.QuantityPerAward of Token to the connected player.</summary>
        public void TriggerAward()
        {
            if (_awardInProgress)
            {
                SetStatus("An award is already in progress.");
                return;
            }
            if (!_treasuryWalletReady)
            {
                Fail("Treasury wallet isn't ready yet.");
                return;
            }
            if (!PlayerIsConnected)
            {
                Fail("Connect a wallet before awarding.");
                return;
            }
            if (Token == null || string.IsNullOrEmpty(Token.PolicyId) || string.IsNullOrEmpty(Token.AssetNameHex))
            {
                Fail("No PolicyId/AssetNameHex configured on Token.");
                return;
            }
            if (Token.QuantityPerAward <= 0)
            {
                Fail("QuantityPerAward must be greater than zero.");
                return;
            }

            _awardInProgress = true;
            SetStatus("Checking sponsorship...");

            if (Sponsorship != null && Sponsorship.AllowSponsorship)
            {
                GetTreasuryWalletBalance(gameObject.name, nameof(OnBalanceCheckedForSponsorship));
            }
            else
            {
                BuildAndSendAward(sponsored: false);
            }
        }

        // Conservative flat estimate covering a simple send transaction's fee plus the ~1-1.5 ADA
        // minimum-UTXO a native-asset output requires. Deliberately not computed exactly (that would
        // mean building the transaction twice) -- same reasoning as MintingController's
        // EstimatedMintCostLovelace, just a touch lower since a plain send is cheaper than a mint.
        private const long EstimatedAwardCostLovelace = 1_700_000;

        /// <summary>jslib callback for GetTreasuryWalletBalance. "OK|&lt;lovelace&gt;" or "ERROR: ...".</summary>
        public void OnBalanceCheckedForSponsorship(string data)
        {
            if (data.StartsWith("ERROR"))
            {
                Debug.LogWarning("[Cardano SDK] Balance check failed, falling back to player-pays: " + data);
                BuildAndSendAward(sponsored: false);
                return;
            }

            long balance = 0;
            long.TryParse(data.Substring(3), out balance);

            bool balanceOk = (balance - EstimatedAwardCostLovelace) >= Sponsorship.MinimumRemainingLovelace;
            bool conditionsOk = EvaluateSponsorshipConditions();

            BuildAndSendAward(sponsored: balanceOk && conditionsOk);
        }

        // Mirrors MintingController.EvaluateSponsorshipConditions -- AssetPresent/QuantityThreshold
        // only (MetadataMatch isn't supported here either, for the same reason: it needs async
        // metadata fetching that doesn't fit a one-shot award-button check), evaluated against the
        // PLAYER's wallet.
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

        private void BuildAndSendAward(bool sponsored)
        {
            string playerAddress = (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch))
                ? BlockfrostManager.AddressToFetch
                : KoiosManager?.AddressToFetch;

            string unit = Token.PolicyId + Token.AssetNameHex;
            string quantity = Token.QuantityPerAward.ToString();

            SetStatus(sponsored ? "Awarding (sponsored)..." : "Awarding -- check your wallet to sign...");

    #if UNITY_WEBGL && !UNITY_EDITOR
            AwardToken(playerAddress, unit, quantity, sponsored ? "1" : "0", gameObject.name, nameof(OnAwardResult));
    #else
            Fail("Awarding requires a WebGL build.");
    #endif
        }

        /// <summary>jslib callback for AwardToken. "OK|&lt;txHash&gt;" or "ERROR: ...".</summary>
        public void OnAwardResult(string data)
        {
            _awardInProgress = false;

            if (data.StartsWith("ERROR"))
            {
                Fail(data);
                return;
            }

            string txHash = data.Substring(3);
            SetStatus("Awarded!");
            if (TxHashField != null) TxHashField.text = txHash;
            if (TxHashRow != null) TxHashRow.SetActive(true);
            OnAwardSucceeded?.Invoke(txHash);
        }

        /// <summary>Opens the last award tx on Cardanoscan, in the network matching TreasuryWalletSecret.Network. Wire this to a button's OnClick.</summary>
        public void OpenTransactionOnExplorer()
        {
            if (TxHashField == null || string.IsNullOrEmpty(TxHashField.text))
            {
                Debug.LogWarning("[Cardano SDK] No transaction hash to open yet.");
                return;
            }

            string network = TreasuryWalletSecret != null ? TreasuryWalletSecret.Network : "mainnet";
            string baseUrl = network.Equals("preprod", System.StringComparison.OrdinalIgnoreCase) ? "https://preprod.cardanoscan.io/transaction/"
                : network.Equals("preview", System.StringComparison.OrdinalIgnoreCase) ? "https://preview.cardanoscan.io/transaction/"
                : "https://cardanoscan.io/transaction/";

            Application.OpenURL(baseUrl + TxHashField.text);
        }

        private void Fail(string message)
        {
            _awardInProgress = false;
            Debug.LogWarning("[Cardano SDK] " + message);
            SetStatus(message);
            OnAwardFailed?.Invoke(message);
        }

        private void SetStatus(string message)
        {
            if (StatusText != null) StatusText.text = message;
        }
    }
}
