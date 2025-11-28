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
        // Check if the Account data is loaded
        if (BlockfrostManager != null && BlockfrostManager.CurrentAccount != null)
        {
            // 1. Get raw Lovelace (string)
            string rawAmount = BlockfrostManager.CurrentAccount.ControlledAmount;

            // 2. Convert to ADA (Game Logic)
            if (long.TryParse(rawAmount, out long lovelace))
            {
                double ada = lovelace / 1000000.0;
                BalanceText.text = $"{ada:N2} ₳"; // Format as "1,234.56 ₳"
            }
            else
            {
                BalanceText.text = "0 ₳";
            }

            // 3. Update Status
            bool isActive = BlockfrostManager.CurrentAccount.Active;
        }
    }
}