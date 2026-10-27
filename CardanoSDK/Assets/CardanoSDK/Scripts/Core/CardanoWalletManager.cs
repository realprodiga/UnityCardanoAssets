using UnityEngine;
using System.Runtime.InteropServices;
using TMPro;

namespace Prodiga.CardanoSDK
{
    public class CardanoWalletManager : MonoBehaviour
    {
        [DllImport("__Internal")]
        private static extern void ConnectWallet(string objectName, string methodName);

        [DllImport("__Internal")]
        private static extern void SetWalletUI(string btnText, string statusText);

        [Header("Blockchain Providers")]
        public BlockfrostIntegration blockfrostProvider;
        public KoiosIntegration koiosProvider;

        [Header("UI References")]
        public TextMeshProUGUI statusText;

        // Only read inside UNITY_WEBGL blocks, so the Editor compiler reports it as unused (CS0414).
#pragma warning disable 0414
        private bool isConnected = false;
#pragma warning restore 0414

        public void RequestConnection()
        {
    #if UNITY_WEBGL && !UNITY_EDITOR
            if (isConnected) {
                Disconnect();
            } else {
                SetStatus("Requesting Connection...");
                ConnectWallet(gameObject.name, "OnWalletConnected");
            }
    #else
            SetStatus("WebGL Only");
    #endif
        }

        public void OnWalletConnected(string data)
        {
            if (data.StartsWith("ERROR"))
            {
                Debug.LogError(data);
                SetStatus("<color=red>" + data + "</color>");
                isConnected = false;
            }
            else
            {
                try 
                {
                    string[] parts = data.Split('|');
                    string addressHex = parts[0];
                    int netId = int.Parse(parts.Length > 1 ? parts[1] : "1");
                    string stakeHex = parts.Length > 2 ? parts[2] : "";

                    bool isTestnet = (netId == 0);

                    // Native C# Extraction Fallback: If JS data stream drops the reward address 
                    // array segment but provides a valid 57-byte Base Address (114 hex characters), 
                    // slice the 28-byte stake credential directly from the payload.
                    if (string.IsNullOrEmpty(stakeHex) && addressHex.Length == 114)
                    {
                        string stakeHash = addressHex.Substring(58, 56);
                        string header = isTestnet ? "e0" : "e1"; // e1 = Mainnet Reward, e0 = Testnet Reward
                        stakeHex = header + stakeHash;
                    }

                    string netName = isTestnet ? "Testnet" : "Mainnet";

                    string hrpAddr = isTestnet ? "addr_test" : "addr";
                    string hrpStake = isTestnet ? "stake_test" : "stake";

                    byte[] addressBytes = Bech32.HexStringToByteArray(addressHex);
                    string bech32Address = Bech32.Encode(addressBytes, hrpAddr);

                    string bech32Stake = "";
                    if (!string.IsNullOrEmpty(stakeHex)) 
                    {
                        byte[] stakeBytes = Bech32.HexStringToByteArray(stakeHex);
                        bech32Stake = Bech32.Encode(stakeBytes, hrpStake);
                    }

                    string shortAddr = "";
                    if (bech32Address.Length > 16)
                    {
                        shortAddr = bech32Address.Substring(0, 10) + "..." + bech32Address.Substring(bech32Address.Length - 6);
                    }
                    else
                    {
                        shortAddr = bech32Address;
                    }

                    SetStatus($"<color=#00FF00><b>CONNECTED</b></color>\n" +
                              $"<size=80%>{netName}</size>\n" +
                              $"<size=70%>{shortAddr}</size>");

                    Debug.Log("Hex Address: " + addressHex);
                    Debug.Log("Bech32 Address: " + bech32Address);
                    Debug.Log("Bech32 Stake: " + bech32Stake);

                    if (blockfrostProvider != null)
                    {
                        blockfrostProvider.AddressToFetch = bech32Address;
                        blockfrostProvider.StakeAddressToFetch = bech32Stake; 
                        blockfrostProvider.RefreshData(); 
                    }

                    if (koiosProvider != null)
                    {
                        koiosProvider.AddressToFetch = bech32Address;
                        koiosProvider.StakeAddressToFetch = bech32Stake;
                        koiosProvider.enabled = false;
                        koiosProvider.enabled = true;
                    }

                    isConnected = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Error converting address: " + e.Message);
                    SetStatus("Address Error");
                }
            }
        }

        public void Disconnect()
        {
            isConnected = false;
            SetStatus("Disconnected");

            if (blockfrostProvider != null)
            {
                blockfrostProvider.ClearData();
            }

            if (koiosProvider != null)
            {
                koiosProvider.ClearData();
            }

    #if UNITY_WEBGL && !UNITY_EDITOR
            SetWalletUI("Connect Wallet", "Disconnected");
    #endif
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
