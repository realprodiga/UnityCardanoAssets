using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Prodiga.CardanoSDK.EditorTools
{
    /// <summary>
    /// Tools > Cardano SDK > Wallet Setup.
    ///
    /// Generalized from the original "Minting Wallet Setup" window -- MintingWalletSecret isn't
    /// actually minting-specific (it's just a mnemonic + network holder kept out of scene YAML), so
    /// the same asset type and the same wizard now serve BOTH the minting wallet and the treasury
    /// wallet used by AwardingController. Pick which slot you're editing from the toolbar at the
    /// top; each slot is backed by its own asset file by default (MintingWalletSecret.asset /
    /// TreasuryWalletSecret.asset), but if your project uses ONE wallet for both jobs, just create
    /// it under either slot and drag that same asset onto both MintingController.WalletSecret and
    /// AwardingController.TreasuryWalletSecret in the Inspector -- this window intentionally doesn't
    /// automate that assignment, since silently rewriting component references felt like more
    /// surprise than it's worth for a two-field drag.
    ///
    /// This REPLACES MintingWalletSetupWindow.cs -- delete that file (and update the "Tools > Cardano
    /// SDK > Minting Wallet Setup" string in MintingController's warning message to say "Wallet
    /// Setup" instead) after adding this one, so there isn't a leftover duplicate menu item.
    ///
    /// Token/Policy/Sponsorship configuration is still just normal Inspector fields on
    /// MintingController/AwardingController -- no wizard needed for those, this window would only be
    /// duplicating the Inspector.
    ///
    /// Also handles two easy-to-forget things: adding whichever secret asset(s) exist to .gitignore,
    /// and checking that each wallet's configured network actually matches the Blockfrost Project ID
    /// it'll be paired with (Blockfrost project IDs conventionally start with their network name,
    /// e.g. "mainnetAbc...", "preprodAbc...").
    /// </summary>
    public class CardanoWalletSetupWindow : EditorWindow
    {
        private const string MintingSecretAssetPath = "Assets/_CardanoSDK_LocalSecrets/MintingWalletSecret.asset";
        private const string TreasurySecretAssetPath = "Assets/_CardanoSDK_LocalSecrets/TreasuryWalletSecret.asset";

        private enum WalletSlot { Minting, Treasury }
        private WalletSlot _slot = WalletSlot.Minting;

        private MintingWalletSecret _secret;
        private bool _revealMnemonic = false;
        private string _mnemonicInput = "";
        private string _networkInput = "preprod";

        private string CurrentAssetPath => _slot == WalletSlot.Minting ? MintingSecretAssetPath : TreasurySecretAssetPath;

        [MenuItem("Tools/Cardano SDK/Wallet Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<CardanoWalletSetupWindow>("Cardano Wallet Setup");
            window.minSize = new Vector2(440, 460);
        }

        private void OnEnable()
        {
            LoadSlot();
        }

        private void LoadSlot()
        {
            _secret = AssetDatabase.LoadAssetAtPath<MintingWalletSecret>(CurrentAssetPath);
            if (_secret != null)
            {
                _mnemonicInput = _secret.Mnemonic;
                _networkInput = string.IsNullOrEmpty(_secret.Network) ? "preprod" : _secret.Network;
            }
            else
            {
                _mnemonicInput = "";
                _networkInput = "preprod";
            }
            _revealMnemonic = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            WalletSlot newSlot = (WalletSlot)GUILayout.Toolbar((int)_slot, new[] { "Minting Wallet", "Treasury Wallet" });
            if (newSlot != _slot)
            {
                _slot = newSlot;
                LoadSlot();
            }
            EditorGUILayout.Space();

            if (_slot == WalletSlot.Minting)
            {
                EditorGUILayout.HelpBox(
                    "The wallet MintingController mints tokens from. Referenced by MintingController.WalletSecret.",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "The wallet AwardingController sends existing tokens from. Referenced by " +
                    "AwardingController.TreasuryWalletSecret. If your project uses the SAME wallet for both " +
                    "minting and awarding, don't fill this slot in -- just drag the Minting Wallet's asset " +
                    "(Assets/_CardanoSDK_LocalSecrets/MintingWalletSecret.asset) directly onto AwardingController's " +
                    "Treasury Wallet Secret field in the Inspector instead.",
                    MessageType.None);
            }

            EditorGUILayout.LabelField("Wallet", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Create a NEW, DEDICATED wallet for this in any Cardano wallet app (Eternl, Nami, etc.) -- " +
                "don't reuse a wallet holding other funds -- and paste its 24-word recovery phrase below.\n\n" +
                "SECURITY: this key ends up embedded in your WebGL build's JS, extractable by anyone who " +
                "opens dev tools. Fine for a testnet/demo wallet with trivial funds. NOT safe for a wallet " +
                "holding real funds -- that belongs behind a server, never shipped to the client.",
                MessageType.Warning);

            EditorGUILayout.Space();

            _networkInput = EditorGUILayout.TextField(
                new GUIContent("Network", "\"mainnet\", \"preprod\", or \"preview\" -- must match your Blockfrost project ID and whatever wallets you test with."),
                _networkInput);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Mnemonic (24 words)", GUILayout.Width(140));
            _revealMnemonic = EditorGUILayout.ToggleLeft("Show", _revealMnemonic, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            if (_revealMnemonic)
            {
                _mnemonicInput = EditorGUILayout.TextArea(_mnemonicInput, GUILayout.Height(60));
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    string masked = string.IsNullOrEmpty(_mnemonicInput) ? "" : new string('\u2022', Mathf.Min(_mnemonicInput.Length, 60));
                    EditorGUILayout.TextArea(masked, GUILayout.Height(60));
                }
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Save " + (_slot == WalletSlot.Minting ? "Minting Wallet" : "Treasury Wallet"), GUILayout.Height(28)))
            {
                SaveSecret();
            }

            if (_secret != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Checks", EditorStyles.boldLabel);
                DrawGitignoreCheck();
                EditorGUILayout.Space();
                DrawNetworkConsistencyCheck();
            }
        }

        private void SaveSecret()
        {
            var words = (_mnemonicInput ?? "").Trim().Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length != 24)
            {
                EditorUtility.DisplayDialog("Cardano Wallet Setup", $"Expected 24 words, found {words.Length}. Not saving.", "OK");
                return;
            }

            string assetPath = CurrentAssetPath;

            if (_secret == null)
            {
                string dir = Path.GetDirectoryName(assetPath);
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    Directory.CreateDirectory(dir);
                    AssetDatabase.Refresh();
                }
                _secret = ScriptableObject.CreateInstance<MintingWalletSecret>();
                AssetDatabase.CreateAsset(_secret, assetPath);
            }

            _secret.Mnemonic = string.Join(" ", words);
            _secret.Network = _networkInput.Trim();
            EditorUtility.SetDirty(_secret);
            AssetDatabase.SaveAssets();

            EnsureGitignoreEntry(assetPath, promptFirst: true);

            Debug.Log($"[Cardano SDK] {(_slot == WalletSlot.Minting ? "Minting" : "Treasury")} wallet secret saved to " + assetPath);
        }

        private static string GitignorePath => Path.Combine(Directory.GetParent(Application.dataPath).FullName, ".gitignore");

        private void EnsureGitignoreEntry(string assetPath, bool promptFirst)
        {
            string existing = File.Exists(GitignorePath) ? File.ReadAllText(GitignorePath) : "";
            if (existing.Contains(assetPath)) return;

            bool add = !promptFirst || EditorUtility.DisplayDialog(
                "Cardano Wallet Setup",
                $"Add \"{assetPath}\" to .gitignore so the mnemonic doesn't get committed?",
                "Add it", "Skip");

            if (add)
            {
                string separator = (existing.Length > 0 && !existing.EndsWith("\n")) ? "\n" : "";
                File.AppendAllText(GitignorePath, separator + assetPath + "\n");
                AssetDatabase.Refresh();
            }
        }

        private void DrawGitignoreCheck()
        {
            string assetPath = CurrentAssetPath;
            bool covered = File.Exists(GitignorePath) && File.ReadAllText(GitignorePath).Contains(assetPath);

            if (covered)
            {
                EditorGUILayout.HelpBox(".gitignore covers this asset.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(".gitignore does NOT cover this asset yet.", MessageType.Warning);
                if (GUILayout.Button("Add to .gitignore"))
                {
                    EnsureGitignoreEntry(assetPath, promptFirst: false);
                }
            }
        }

        private void DrawNetworkConsistencyCheck()
        {
            if (_slot == WalletSlot.Minting)
            {
                var controllers = FindObjectsOfType<MintingController>();
                if (controllers.Length == 0)
                {
                    EditorGUILayout.HelpBox("No MintingController in the open scene to cross-check against yet.", MessageType.None);
                    return;
                }
                foreach (var controller in controllers)
                {
                    CheckOne(controller.name, controller.BlockfrostManager);
                }
            }
            else
            {
                var controllers = FindObjectsOfType<AwardingController>();
                if (controllers.Length == 0)
                {
                    EditorGUILayout.HelpBox("No AwardingController in the open scene to cross-check against yet.", MessageType.None);
                    return;
                }
                foreach (var controller in controllers)
                {
                    CheckOne(controller.name, controller.BlockfrostManager);
                }
            }
        }

        private void CheckOne(string controllerName, BlockfrostIntegration blockfrost)
        {
            if (blockfrost == null || string.IsNullOrEmpty(blockfrost.ProjectId))
            {
                EditorGUILayout.HelpBox($"\"{controllerName}\": no Blockfrost Project ID set yet to check against.", MessageType.None);
                return;
            }

            string projectId = blockfrost.ProjectId;
            bool matches = projectId.StartsWith(_networkInput, StringComparison.OrdinalIgnoreCase);

            EditorGUILayout.HelpBox(
                matches
                    ? $"\"{controllerName}\": network looks consistent (\"{_networkInput}\")."
                    : $"\"{controllerName}\": MISMATCH -- wallet is set to \"{_networkInput}\" but its Blockfrost Project ID (\"{projectId}\") doesn't start with that network name.",
                matches ? MessageType.Info : MessageType.Error);
        }
    }
}
