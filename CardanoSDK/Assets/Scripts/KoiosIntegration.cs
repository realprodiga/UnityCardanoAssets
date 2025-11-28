using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;

public class KoiosIntegration : MonoBehaviour
{
    [Header("Koios Configuration")]
    public string BaseUrl = "https://api.koios.rest/api/v1";
    public string StakeAddressToFetch;
    public string AddressToFetch;
    public string AssetPolicyToFetch; 
    public string AssetNameHexToFetch; 
    public string TxHashToFetch;

    // [FRAMEWORK STEP 1: Add public Data Class here]
    //[System.Serializable] public class Koios____Data { public string ____;}
    [System.Serializable] public class KoiosAccountData { public string StakeAddress; public string Status; public string TotalBalance; }
    [System.Serializable] public class KoiosAddressData { public string Address; public string Type; }
    [System.Serializable] public class KoiosAssetInfoData { public string AssetNameAscii; public string AssetNameHex; public string PolicyId; public string TotalSupply; }
    [System.Serializable] public class KoiosTxData { public string TxHash; public string Fee; public int BlockHeight; }


    // [FRAMEWORK STEP 2: Add JSON Mapper here]
    //[System.Serializable] class Koios____Raw { public string ____;}
    [System.Serializable] class Wrapper<T> { public List<T> items; } // Koios specific wrapper
    [System.Serializable] class KoiosAccountRaw { public string stake_address; public string status; public string total_balance; }
    [System.Serializable] class KoiosAddressRaw { public string address; public string stake_address; }
    [System.Serializable] class KoiosAssetInfoRaw { public string policy_id; public string asset_name; public string total_supply; }
    [System.Serializable] class KoiosTxRaw { public string tx_hash; public string fee; public int block_height; }


    [Header("--- LOADED DATA ---")]
    // [FRAMEWORK STEP 3: Add new public variables here]
    //public Koios____Data Current____;
    public KoiosAccountData CurrentAccount;
    public KoiosAddressData CurrentAddress;
    public KoiosAssetInfoData CurrentAssetInfo; 
    public KoiosTxData CurrentTransaction;

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
}