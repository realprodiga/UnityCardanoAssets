using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;

namespace Prodiga.CardanoSDK
{
    public class BlockfrostIntegration : MonoBehaviour
    {
        [Header("Blockfrost Configuration")]
        [Tooltip("Your Blockfrost project ID. Its prefix (mainnet/preprod/preview) selects the network. Never commit or publish a real ID.")]
        public string ProjectId;
        public string StakeAddressToFetch;
        public string AddressToFetch;
        public string AssetIdToFetch;
        public string TxHashToFetch;
        // [FRAMEWORK STEP 1: Add public Data Class here]
        //[System.Serializable] public class Blockfrost____Data { public string ____;}
        [System.Serializable] public class BlockfrostAccountData { public string StakeAddress; public bool Active; public string ControlledAmount; }
        [System.Serializable] public class BlockfrostAddressData { public string Address; public string Type; public List<BlockfrostAssetAmount> Assets; }
        [System.Serializable] public class BlockfrostAssetDetails { public string AssetNameAscii; public string AssetNameHex; public string AssetId; public string Quantity; }
        [System.Serializable] public class BlockfrostTxData { public string Hash; public string Fees; public int BlockHeight; }
        // A single entry from an address's "amount" array -- either lovelace or a native token/NFT,
        // identified by Blockfrost's "unit" (policy ID + asset name hex, concatenated).
        [System.Serializable] public class BlockfrostAssetAmount { public string unit; public string quantity; }
        // [FRAMEWORK STEP 2: Add JSON Mapper here]
        //[System.Serializable] class Blockfrost____Raw { public string ____;}
        [System.Serializable] class BlockfrostAccountRaw { public string stake_address; public bool active; public string controlled_amount; }
        [System.Serializable] class BlockfrostAddressRaw { public string address; public string type; public List<BlockfrostAssetAmount> amount; }
        [System.Serializable] class BlockfrostAssetRaw { public string asset; public string asset_name; public string quantity; }
        [System.Serializable] class BlockfrostTxRaw { public string hash; public string fees; public int block_height; }
        [Header("--- LOADED DATA ---")]
        // [FRAMEWORK STEP 3: Add new public variables here]
        //public Blockfrost____Data Current____;
        public BlockfrostAccountData CurrentAccount;
        public BlockfrostAddressData CurrentAddress;
        public BlockfrostAssetDetails CurrentAssetDetails;
        public BlockfrostTxData CurrentTransaction;

        // Raised whenever CurrentAddress finishes refreshing (including being cleared on
        // disconnect), so listeners like GatedAccessController can react immediately instead of
        // waiting for their own next poll.
        public event System.Action OnDataUpdated;
        // Blockfrost project IDs are prefixed with their network ("mainnet...", "preprod...", "preview..."),
        // so the matching API host is picked automatically.
        private string BaseUrl =>
            ProjectId != null && ProjectId.StartsWith("preprod") ? "https://cardano-preprod.blockfrost.io/api/v0" :
            ProjectId != null && ProjectId.StartsWith("preview") ? "https://cardano-preview.blockfrost.io/api/v0" :
            "https://cardano-mainnet.blockfrost.io/api/v0";
        private UnityWebRequest CreateRequest(string url)
        {
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("project_id", ProjectId);
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
            if (string.IsNullOrEmpty(____ToFetch)) yield break;
            string url = $"{BaseUrl}/____";
            UnityWebRequest request = CreateRequest(url);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var info = JsonUtility.FromJson<Blockfrost____Raw>(request.downloadHandler.text);
                Current____.____ = info.address;
                Current____.Type = info.type;
            }
        }
        */
        private IEnumerator FetchAccount()
        {
            if (string.IsNullOrEmpty(StakeAddressToFetch)) yield break;
            string url = $"{BaseUrl}/accounts/{StakeAddressToFetch}";
            UnityWebRequest request = CreateRequest(url);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var info = JsonUtility.FromJson<BlockfrostAccountRaw>(request.downloadHandler.text);
                CurrentAccount.StakeAddress = info.stake_address;
                CurrentAccount.Active = info.active;
                CurrentAccount.ControlledAmount = info.controlled_amount;
            }
        }
        private IEnumerator FetchAddress()
        {
            if (string.IsNullOrEmpty(AddressToFetch)) yield break;
            string url = $"{BaseUrl}/addresses/{AddressToFetch}";
            UnityWebRequest request = CreateRequest(url);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var info = JsonUtility.FromJson<BlockfrostAddressRaw>(request.downloadHandler.text);
                CurrentAddress.Address = info.address;
                CurrentAddress.Type = info.type;
                // Capture the full asset list so HasAsset() can check it.
                CurrentAddress.Assets = info.amount != null ? info.amount : new List<BlockfrostAssetAmount>();
                OnDataUpdated?.Invoke();
            }
        }
        private IEnumerator FetchAssetDetails()
        {
            if (string.IsNullOrEmpty(AssetIdToFetch)) yield break;
            string url = $"{BaseUrl}/assets/{AssetIdToFetch}";
            UnityWebRequest request = CreateRequest(url);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var info = JsonUtility.FromJson<BlockfrostAssetRaw>(request.downloadHandler.text);
                CurrentAssetDetails.AssetId = info.asset;
                CurrentAssetDetails.AssetNameHex = info.asset_name;
                CurrentAssetDetails.AssetNameAscii = HexToAscii(info.asset_name);
                CurrentAssetDetails.Quantity = info.quantity;
            }
        }
        private IEnumerator FetchTransaction()
        {
            if (string.IsNullOrEmpty(TxHashToFetch)) yield break;
            string url = $"{BaseUrl}/txs/{TxHashToFetch}";
            UnityWebRequest request = CreateRequest(url);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                var info = JsonUtility.FromJson<BlockfrostTxRaw>(request.downloadHandler.text);
                CurrentTransaction.Hash = info.hash;
                CurrentTransaction.Fees = info.fees;
                CurrentTransaction.BlockHeight = info.block_height;
            }
        }
        private void OnEnable()
        {
            // [FRAMEWORK STEP 5: Initialize new data classes here]
            //Current____ = new Blockfrost____Data();
            CurrentAccount = new BlockfrostAccountData();
            CurrentAddress = new BlockfrostAddressData();
            CurrentAssetDetails = new BlockfrostAssetDetails();
            CurrentTransaction = new BlockfrostTxData();

            RefreshData();
        }
        public void RefreshData()
        {
            if (string.IsNullOrEmpty(ProjectId)) return;

            StopAllCoroutines();

            // [FRAMEWORK STEP 6: Start new Coroutines here]
            //StartCoroutine(Fetch____())
            StartCoroutine(FetchAccount());
            StartCoroutine(FetchAddress());
            StartCoroutine(FetchAssetDetails());
            StartCoroutine(FetchTransaction());
        }
        public void ClearData()
        {
            StopAllCoroutines();
            CurrentAccount = new BlockfrostAccountData();
            CurrentAddress = new BlockfrostAddressData();
            CurrentAssetDetails = new BlockfrostAssetDetails();
            CurrentTransaction = new BlockfrostTxData();

            StakeAddressToFetch = "";
            AddressToFetch = "";
            AssetIdToFetch = "";
            TxHashToFetch = "";

            OnDataUpdated?.Invoke();
        }

        // Returns true if the connected address currently holds at least `minQuantity` of the given asset.
        // `assetId` must be in Blockfrost's "unit" format: the 56-character policy ID hex followed
        // directly by the asset name hex, with no separator (e.g. policyId + assetNameHex).
        public bool HasAsset(string assetId, long minQuantity = 1)
        {
            if (CurrentAddress == null || CurrentAddress.Assets == null || string.IsNullOrEmpty(assetId)) return false;

            foreach (var asset in CurrentAddress.Assets)
            {
                if (asset.unit == assetId)
                {
                    return long.TryParse(asset.quantity, out long qty) && qty >= minQuantity;
                }
            }
            return false;
        }

        // Returns true if the address holds ANY asset under the given policy, regardless of asset name/quantity > 0.
        public bool HasAnyAssetUnderPolicy(string policyId)
        {
            if (CurrentAddress == null || CurrentAddress.Assets == null || string.IsNullOrEmpty(policyId) || policyId.Length != 56) return false;

            foreach (var asset in CurrentAddress.Assets)
            {
                if (asset.unit != null && asset.unit.Length >= 56 && asset.unit.Substring(0, 56) == policyId)
                {
                    if (long.TryParse(asset.quantity, out long qty) && qty > 0) return true;
                }
            }
            return false;
        }

        // Returns the held quantity for a specific asset (assetNameHex provided), or the summed
        // quantity across every asset under the policy if assetNameHex is left empty.
        public long GetAssetQuantity(string policyId, string assetNameHex)
        {
            if (CurrentAddress == null || CurrentAddress.Assets == null || string.IsNullOrEmpty(policyId) || policyId.Length != 56) return 0;

            long total = 0;
            foreach (var asset in CurrentAddress.Assets)
            {
                if (asset.unit == null || asset.unit.Length < 56 || asset.unit.Substring(0, 56) != policyId) continue;
                string nameHex = asset.unit.Substring(56);
                if (!string.IsNullOrEmpty(assetNameHex) && nameHex != assetNameHex) continue;
                if (long.TryParse(asset.quantity, out long qty)) total += qty;
            }
            return total;
        }

        // Fetches the full asset JSON (including "onchain_metadata") for a specific asset and hands the
        // raw response text back via onComplete (null on failure). The metadata's shape is arbitrary and
        // collection-defined, so it's intentionally NOT parsed with JsonUtility here -- see AssetCondition/
        // GatedAccessController, which use Newtonsoft Json.Net to read it with a dot-path lookup.
        // `assetId` must be in Blockfrost's "unit" format: policyId + assetNameHex concatenated.
        public IEnumerator FetchAssetMetadataRaw(string assetId, System.Action<string> onComplete)
        {
            if (string.IsNullOrEmpty(assetId))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            string url = $"{BaseUrl}/assets/{assetId}";
            UnityWebRequest request = CreateRequest(url);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                onComplete?.Invoke(request.downloadHandler.text);
            }
            else
            {
                Debug.LogWarning($"[Cardano SDK] Failed to fetch metadata for asset '{assetId}': {request.error}");
                onComplete?.Invoke(null);
            }
        }
    }
}
