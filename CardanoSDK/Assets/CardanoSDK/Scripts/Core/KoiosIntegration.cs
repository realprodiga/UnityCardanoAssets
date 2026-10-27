using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;

namespace Prodiga.CardanoSDK
{
    public class KoiosIntegration : MonoBehaviour
    {
        [Header("Koios Configuration")]
        [Tooltip("Mainnet: https://api.koios.rest/api/v1  |  Preprod: https://preprod.koios.rest/api/v1  |  Preview: https://preview.koios.rest/api/v1")]
        public string BaseUrl = "https://api.koios.rest/api/v1";
        public string StakeAddressToFetch;
        public string AddressToFetch;
        public string AssetPolicyToFetch;
        public string AssetNameHexToFetch;
        public string TxHashToFetch;
        // [FRAMEWORK STEP 1: Add public Data Class here]
        //[System.Serializable] public class Koios____Data { public string ____;}
        [System.Serializable] public class KoiosAccountData { public string StakeAddress; public string Status; public string TotalBalance; }
        [System.Serializable] public class KoiosAddressData { public string Address; public string Type; public List<KoiosAssetListItem> Assets; }
        [System.Serializable] public class KoiosAssetInfoData { public string AssetNameAscii; public string AssetNameHex; public string PolicyId; public string TotalSupply; }
        [System.Serializable] public class KoiosTxData { public string TxHash; public string Fee; public int BlockHeight; }
        // A single native token/NFT entry as it appears inside a UTXO's "asset_list".
        [System.Serializable] public class KoiosAssetListItem { public string policy_id; public string asset_name; public string quantity; }
        // [FRAMEWORK STEP 2: Add JSON Mapper here]
        //[System.Serializable] class Koios____Raw { public string ____;}
        [System.Serializable] class Wrapper<T> { public List<T> items; } // Koios specific wrapper
        [System.Serializable] class KoiosAccountRaw { public string stake_address; public string status; public string total_balance; }
        // Koios doesn't return assets directly on the address -- they're nested per-UTXO, so we
        // pull in utxo_set and aggregate each UTXO's asset_list.
        [System.Serializable] class KoiosUtxoRaw { public string tx_hash; public int tx_index; public string value; public List<KoiosAssetListItem> asset_list; }
        [System.Serializable] class KoiosAddressRaw { public string address; public string stake_address; public List<KoiosUtxoRaw> utxo_set; }
        [System.Serializable] class KoiosAssetInfoRaw { public string policy_id; public string asset_name; public string total_supply; }
        [System.Serializable] class KoiosTxRaw { public string tx_hash; public string fee; public int block_height; }
        [Header("--- LOADED DATA ---")]
        // [FRAMEWORK STEP 3: Add new public variables here]
        //public Koios____Data Current____;
        public KoiosAccountData CurrentAccount;
        public KoiosAddressData CurrentAddress;
        public KoiosAssetInfoData CurrentAssetInfo;
        public KoiosTxData CurrentTransaction;

        // Raised whenever CurrentAddress finishes refreshing (including being cleared on
        // disconnect), so listeners like GatedAccessController can react immediately instead of
        // waiting for their own next poll.
        public event System.Action OnDataUpdated;
        // --- HELPERS ---
        private UnityWebRequest CreatePost(string url, string body)
        {
            UnityWebRequest request = new UnityWebRequest(url, "POST");
            byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            return request;
        }
        private string HexToAscii(string hexString)
        {
            if (string.IsNullOrEmpty(hexString)) return "";
            try
            {
                byte[] bytes = new byte[hexString.Length / 2];
                for (int i = 0; i < bytes.Length; i++)
                    bytes[i] = System.Convert.ToByte(hexString.Substring(i * 2, 2), 16);
                return Encoding.UTF8.GetString(bytes);
            }
            catch { return "Invalid Hex"; }
        }
        // [FRAMEWORK STEP 4: Implement new Coroutines here]
        /*
        private IEnumerator Fetch____()
        {
            string url = $"{BaseUrl}/____";
            // Note: Koios returns arrays, so we wrap the JSON
            UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = "{ \"items\": " + request.downloadHandler.text + "}";
                var wrapper = JsonUtility.FromJson<Wrapper<Koios____Raw>>(json);

                if(wrapper.items != null && wrapper.items.Count > 0)
                {
                    var info = wrapper.items[0];
                    Current____.____ = info.____;
                }
            }
        }
        */
        private IEnumerator FetchAccount()
        {
            string url = $"{BaseUrl}/account_info";
            string jsonBody = "{\"_stake_addresses\":[\"" + StakeAddressToFetch + "\"]}";
            UnityWebRequest request = CreatePost(url, jsonBody);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<Wrapper<KoiosAccountRaw>>("{ \"items\": " + request.downloadHandler.text + "}");
                if (wrapper.items != null && wrapper.items.Count > 0)
                {
                    var raw = wrapper.items[0];
                    CurrentAccount.StakeAddress = raw.stake_address;
                    CurrentAccount.Status = raw.status;
                    CurrentAccount.TotalBalance = raw.total_balance;
                }
            }
        }
        private IEnumerator FetchAddress()
        {
            string url = $"{BaseUrl}/address_info";
            string jsonBody = "{\"_addresses\":[\"" + AddressToFetch + "\"]}";
            UnityWebRequest request = CreatePost(url, jsonBody);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<Wrapper<KoiosAddressRaw>>("{ \"items\": " + request.downloadHandler.text + "}");
                if (wrapper.items != null && wrapper.items.Count > 0)
                {
                    var raw = wrapper.items[0];
                    CurrentAddress.Address = raw.address;
                    CurrentAddress.Type = string.IsNullOrEmpty(raw.stake_address) ? "Enterprise" : "Shelley";

                    // Aggregate every UTXO's asset_list into one flat list so HasAsset() doesn't
                    // need to know about UTXOs at all.
                    var assets = new List<KoiosAssetListItem>();
                    if (raw.utxo_set != null)
                    {
                        foreach (var utxo in raw.utxo_set)
                        {
                            if (utxo.asset_list != null) assets.AddRange(utxo.asset_list);
                        }
                    }
                    CurrentAddress.Assets = assets;
                    OnDataUpdated?.Invoke();
                }
            }
        }
        private IEnumerator FetchAssetInfo()
        {
            string url = $"{BaseUrl}/asset_info?_asset_policy={AssetPolicyToFetch}&_asset_name={AssetNameHexToFetch}";
            UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<Wrapper<KoiosAssetInfoRaw>>("{ \"items\": " + request.downloadHandler.text + "}");
                if(wrapper.items != null && wrapper.items.Count > 0)
                {
                    var raw = wrapper.items[0];
                    CurrentAssetInfo.PolicyId = raw.policy_id;
                    CurrentAssetInfo.AssetNameHex = raw.asset_name;
                    CurrentAssetInfo.AssetNameAscii = HexToAscii(raw.asset_name);
                    CurrentAssetInfo.TotalSupply = raw.total_supply;
                }
            }
        }
        private IEnumerator FetchTransaction()
        {
            string url = $"{BaseUrl}/tx_info";
            string jsonBody = "{\"_tx_hashes\":[\"" + TxHashToFetch + "\"]}";
            UnityWebRequest request = CreatePost(url, jsonBody);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var wrapper = JsonUtility.FromJson<Wrapper<KoiosTxRaw>>("{ \"items\": " + request.downloadHandler.text + "}");
                if (wrapper.items != null && wrapper.items.Count > 0)
                {
                    var raw = wrapper.items[0];
                    CurrentTransaction.TxHash = raw.tx_hash;
                    CurrentTransaction.Fee = raw.fee;
                    CurrentTransaction.BlockHeight = raw.block_height;
                }
            }
        }
        private void OnEnable()
        {
            // [FRAMEWORK STEP 5: Initialize new data classes here]
            //Current____ = new Koios____Data();
            CurrentAccount = new KoiosAccountData();
            CurrentAddress = new KoiosAddressData();
            CurrentAssetInfo = new KoiosAssetInfoData();
            CurrentTransaction = new KoiosTxData();
            StopAllCoroutines();
            // [FRAMEWORK STEP 6: Start new Coroutines here]
            //StartCoroutine(Fetch____())
            StartCoroutine(FetchAccount());
            StartCoroutine(FetchAddress());
            StartCoroutine(FetchAssetInfo());
            StartCoroutine(FetchTransaction());
        }

        // Resets all tracked data and stops in-flight requests. Call this on wallet disconnect --
        // without it, a stale AddressToFetch here keeps this provider looking "connected" even
        // after the wallet is gone.
        public void ClearData()
        {
            StopAllCoroutines();
            CurrentAccount = new KoiosAccountData();
            CurrentAddress = new KoiosAddressData();
            CurrentAssetInfo = new KoiosAssetInfoData();
            CurrentTransaction = new KoiosTxData();

            StakeAddressToFetch = "";
            AddressToFetch = "";
            AssetPolicyToFetch = "";
            AssetNameHexToFetch = "";
            TxHashToFetch = "";

            OnDataUpdated?.Invoke();
        }

        // Returns true if the connected address currently holds at least `minQuantity` of the given asset.
        // Koios identifies assets with a separate policyId (56-char hex) and assetNameHex, rather than
        // Blockfrost's single concatenated "unit". Pass an empty assetNameHex to match any asset under the policy.
        public bool HasAsset(string policyId, string assetNameHex, long minQuantity = 1)
        {
            return GetAssetQuantity(policyId, assetNameHex) >= minQuantity;
        }

        // Returns the held quantity for a specific asset (assetNameHex provided), or the summed
        // quantity across every asset under the policy if assetNameHex is left empty.
        public long GetAssetQuantity(string policyId, string assetNameHex)
        {
            if (CurrentAddress == null || CurrentAddress.Assets == null || string.IsNullOrEmpty(policyId)) return 0;

            long total = 0;
            foreach (var asset in CurrentAddress.Assets)
            {
                bool policyMatches = asset.policy_id == policyId;
                bool nameMatches = string.IsNullOrEmpty(assetNameHex) || asset.asset_name == assetNameHex;
                if (policyMatches && nameMatches && long.TryParse(asset.quantity, out long qty))
                {
                    total += qty;
                }
            }
            return total;
        }

        // Fetches the raw asset_info response (a JSON array with one entry) for a specific asset and
        // hands the raw response text back via onComplete (null on failure). As with Blockfrost, the
        // metadata shape is arbitrary/collection-defined, so it's read with Newtonsoft Json.Net in
        // GatedAccessController rather than JsonUtility here.
        //
        // NOTE: Koios's exact field name/shape for on-chain (CIP-25) metadata within asset_info hasn't
        // been verified against a live response -- check the actual JSON structure before relying on
        // this for MetadataMatch conditions. Blockfrost's onchain_metadata field is the more
        // well-trodden path if you need metadata matching today; Koios requests may also require a
        // CORS proxy when called directly from a WebGL build.
        public IEnumerator FetchAssetMetadataRaw(string policyId, string assetNameHex, System.Action<string> onComplete)
        {
            if (string.IsNullOrEmpty(policyId))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            string url = $"{BaseUrl}/asset_info?_asset_policy={policyId}&_asset_name={assetNameHex}";
            UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(request.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning($"[Cardano SDK] Failed to fetch metadata for policy '{policyId}' asset '{assetNameHex}': {request.error}");
                onComplete?.Invoke(null);
            }
        }
    }
}
