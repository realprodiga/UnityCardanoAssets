using UnityEngine;
using TMPro; // Standard Unity Text

public class WalletDisplay : MonoBehaviour
{
    [Header("SDK Reference")]
    public BlockfrostIntegration BlockfrostManager;
    // You could add public KoiosIntegration KoiosManager; here too

    [Header("Game UI")]
    public TextMeshProUGUI BalanceText;

    void Update()
    {
        // Check if we are disconnected (using the address as the source of truth)
        if (BlockfrostManager == null || string.IsNullOrEmpty(BlockfrostManager.AddressToFetch))
        {
            BalanceText.text = "0 ₳";
            return;
        }

        // Only update if we have a valid stake address string
        if (BlockfrostManager.CurrentAccount != null)
        {
            string rawAmount = BlockfrostManager.CurrentAccount.ControlledAmount;
            
            if (long.TryParse(rawAmount, out long lovelace))
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