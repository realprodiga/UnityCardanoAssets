using UnityEngine;

namespace Prodiga.CardanoSDK
{
    using TMPro; // Standard Unity Text

    public class WalletDisplay : MonoBehaviour
    {
        [Header("SDK Reference")]
        [Tooltip("If both providers are assigned, Blockfrost is used as the primary source and Koios is used as a fallback.")]
        public BlockfrostIntegration BlockfrostManager;
        public KoiosIntegration KoiosManager;

        [Header("Game UI")]
        public TextMeshProUGUI BalanceText;

        void Awake()
        {
            if (BlockfrostManager == null) BlockfrostManager = FindObjectOfType<BlockfrostIntegration>();
            if (KoiosManager == null) KoiosManager = FindObjectOfType<KoiosIntegration>();
        }

        void Update()
        {
            // Prefer Blockfrost if it's wired up and actively tracking a connected address
            if (BlockfrostManager != null && !string.IsNullOrEmpty(BlockfrostManager.AddressToFetch))
            {
                SetBalanceFromLovelace(BlockfrostManager.CurrentAccount?.ControlledAmount);
                return;
            }

            // Fall back to Koios if it's wired up and actively tracking a connected address
            if (KoiosManager != null && !string.IsNullOrEmpty(KoiosManager.AddressToFetch))
            {
                SetBalanceFromLovelace(KoiosManager.CurrentAccount?.TotalBalance);
                return;
            }

            // Neither provider is connected
            if (BalanceText != null) BalanceText.text = "0 ₳";
        }

        private void SetBalanceFromLovelace(string rawLovelaceAmount)
        {
            // Bail out once, up front, instead of guarding every assignment below -- this component
            // is meant to work fine with no UI wired at all (e.g. CardanoManager used standalone with
            // a developer's own custom balance display).
            if (BalanceText == null) return;

            if (long.TryParse(rawLovelaceAmount, out long lovelace))
            {
                double ada = lovelace / 1000000.0;
                BalanceText.text = $"{ada:N2} ₳";
            }
            else
            {
                BalanceText.text = "0 ₳";
            }
        }
    }
}
