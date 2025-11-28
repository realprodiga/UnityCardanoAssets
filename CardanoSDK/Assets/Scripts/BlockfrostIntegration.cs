using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;

public class BlockfrostIntegration : MonoBehaviour
{
    [Header("Blockfrost Configuration")]
    public string ProjectId;
    public string StakeAddressToFetch;
    public string AddressToFetch;
    public string AssetIdToFetch; 
    public string TxHashToFetch;

    // [FRAMEWORK STEP 1: Add public Data Class here]
    //[System.Serializable] public class Blockfrost____Data { public string ____;}
    [System.Serializable] public class BlockfrostAccountData { public string StakeAddress; public bool Active; public string ControlledAmount; }
    [System.Serializable] public class BlockfrostAddressData { public string Address; public string Type; }
    [System.Serializable] public class BlockfrostAssetDetails { public string AssetNameAscii; public string AssetNameHex; public string AssetId; public string Quantity; }
    [System.Serializable] public class BlockfrostTxData { public string Hash; public string Fees; public int BlockHeight; }

    // [FRAMEWORK STEP 2: Add JSON Mapper here]
    //[System.Serializable] class Blockfrost____Raw { public string ____;}
    [System.Serializable] class BlockfrostAccountRaw { public string stake_address; public bool active; public string controlled_amount; }
    [System.Serializable] class BlockfrostAddressRaw { public string address; public string type; }
    [System.Serializable] class BlockfrostAssetRaw { public string asset; public string asset_name; public string quantity; }
    [System.Serializable] class BlockfrostTxRaw { public string hash; public string fees; public int block_height; }

    [Header("--- LOADED DATA ---")]
    // [FRAMEWORK STEP 3: Add new public variables here]
    //public Blockfrost____Data Current____;
    public BlockfrostAccountData CurrentAccount;
    public BlockfrostAddressData CurrentAddress;
    public BlockfrostAssetDetails CurrentAssetDetails; 
    public BlockfrostTxData CurrentTransaction;

    private readonly string MainnetUrl = "https://cardano-mainnet.blockfrost.io/api/v0";

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
        string url = $"{MainnetUrl}/____";
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
        string url = $"{MainnetUrl}/accounts/{StakeAddressToFetch}";
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
        string url = $"{MainnetUrl}/addresses/{AddressToFetch}";
        UnityWebRequest request = CreateRequest(url);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            var info = JsonUtility.FromJson<BlockfrostAddressRaw>(request.downloadHandler.text);
            CurrentAddress.Address = info.address;
            CurrentAddress.Type = info.type;
        }
    }

    private IEnumerator FetchAssetDetails()
    {
        string url = $"{MainnetUrl}/assets/{AssetIdToFetch}";
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
        string url = $"{MainnetUrl}/txs/{TxHashToFetch}";
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
        if (string.IsNullOrEmpty(ProjectId)) return;

        // [FRAMEWORK STEP 5: Initialize new data classes here]
        //Current____ = new Blockfrost____Data();
        CurrentAccount = new BlockfrostAccountData();
        CurrentAddress = new BlockfrostAddressData();
        CurrentAssetDetails = new BlockfrostAssetDetails();
        CurrentTransaction = new BlockfrostTxData();
        
        StopAllCoroutines();

        // [FRAMEWORK STEP 6: Start new Coroutines here]
        //StartCoroutine(Fetch____())
        StartCoroutine(FetchAccount());
        StartCoroutine(FetchAddress());
        StartCoroutine(FetchAssetDetails());
        StartCoroutine(FetchTransaction());
    }

}