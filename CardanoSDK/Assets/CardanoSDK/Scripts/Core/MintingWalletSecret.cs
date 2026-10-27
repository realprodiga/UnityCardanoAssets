using UnityEngine;

namespace Prodiga.CardanoSDK
{
    /// <summary>
    /// Holds the minting wallet's key material. Deliberately NOT a field on a MonoBehaviour --
    /// MonoBehaviour fields get serialized straight into the scene's .unity YAML, which is exactly
    /// what you don't want a mnemonic sitting inside when the project gets pushed to GitHub.
    ///
    /// This asset is meant to live at Assets/_CardanoSDK_LocalSecrets/MintingWalletSecret.asset (create it via
    /// Tools > Cardano SDK > Minting Wallet Setup rather than by hand) and be added to .gitignore.
    /// A regular serialized reference from MintingController.WalletSecret is enough to get it
    /// included in a WebGL build -- it doesn't need to live in a Resources folder.
    ///
    /// SECURITY: a WebGL build ships its JS/wasm to every visitor's browser. Anything embedded in
    /// it -- including whatever this asset holds -- is extractable by anyone who opens dev tools.
    /// That's an acceptable tradeoff for a testnet/demo wallet holding trivial funds. It is NOT
    /// acceptable for a wallet holding real mainnet funds -- that key belongs behind a server you
    /// control, signing on request, never shipped to the client. This asset only supports the
    /// former. See MintingController's class summary for how a server-relay signer could replace
    /// this later without changing anything else.
    /// </summary>
    [CreateAssetMenu(fileName = "MintingWalletSecret", menuName = "Cardano SDK/Minting Wallet Secret (manual -- prefer Tools > Cardano SDK > Minting Wallet Setup)")]
    public class MintingWalletSecret : ScriptableObject
    {
        [Tooltip("24-word recovery phrase for the minting wallet, space-separated. TESTNET/DEMO USE ONLY -- see the class summary.")]
        [TextArea(3, 5)]
        public string Mnemonic;

        [Tooltip("Must match the network your Blockfrost project ID and connected wallets are using. \"mainnet\", \"preprod\", or \"preview\".")]
        public string Network = "preprod";

        [Tooltip("Optional, manual -- nothing derives or fills this in automatically. The address only actually becomes known at runtime (MintingController.MintingWalletAddress, once InitMintingWallet's callback lands from a real WebGL build). Paste it in yourself here for reference/funding purposes if you want a record of it -- e.g. by importing the mnemonic into any wallet app just to read its address, or by copying it from the demo's status text after a test run.")]
        public string DerivedAddressForReference;
    }
}
