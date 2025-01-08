// BlockfrostIntegrationBasic.cs
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine.UI; // Required for LayoutRebuilder

public class BlockfrostIntegrationBasic : MonoBehaviour
{
    [Header("Blockfrost Settings")]
    public string ProjectId;
    public string StakeAddress;
    public string SpecificAddress;
    public string SpecificAssetId;
    public string SpecificTransactionHash;

    [Header("UI References")]
    public TMP_InputField AccountsInfoUI;
    public TMP_InputField AddressesInfoUI;
    public TMP_InputField AssetsInfoUI;
    public TMP_InputField SpecificAssetInfoUI;
    public TMP_InputField TransactionsInfoUI;

    private readonly string MainnetUrl = "https://cardano-mainnet.blockfrost.io/api/v0";

    private string AccountsInfo;
    private string AddressesInfo;
    private string AssetsInfo;
    private string SpecificAssetInfo;
    private string TransactionsInfo;

    private void Start()
    {
        if (string.IsNullOrEmpty(ProjectId))
        {
            Debug.LogError("ProjectId is not set.");
            return;
        }

        // Start all coroutines to fetch data concurrently
        StartCoroutine(GetAccountsInfo());
        StartCoroutine(GetAddressesInfo());
        StartCoroutine(GetAssetsInfo());
        StartCoroutine(GetSpecificAssetInfo());
        StartCoroutine(GetTransactionsInfo());
    }

    private IEnumerator GetAccountsInfo()
    {
        string url = $"{MainnetUrl}/accounts/{StakeAddress}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError($"Error fetching accounts info: {request.error}");
        }
        else
        {
            AccountsInfo = request.downloadHandler.text;
            if (AccountsInfoUI != null)
            {
                AccountsInfoUI.text = BeautifyJson(AccountsInfo);
            }
        }
    }

    private IEnumerator GetAddressesInfo()
    {
        string url = $"{MainnetUrl}/addresses/{SpecificAddress}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError($"Error fetching addresses info: {request.error}");
        }
        else
        {
            AddressesInfo = request.downloadHandler.text;
            if (AddressesInfoUI != null)
            {
                AddressesInfoUI.text = BeautifyJson(AddressesInfo);
            }
        }
    }

    private IEnumerator GetAssetsInfo()
    {
        string url = $"{MainnetUrl}/assets?count=10&page=1&order=asc";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError($"Error fetching assets info: {request.error}");
        }
        else
        {
            AssetsInfo = request.downloadHandler.text;
            if (AssetsInfoUI != null)
            {
                AssetsInfoUI.text = BeautifyJson(AssetsInfo);
            }
        }
    }

    private IEnumerator GetSpecificAssetInfo()
    {
        string url = $"{MainnetUrl}/assets/{SpecificAssetId}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError($"Error fetching specific asset info: {request.error}");
        }
        else
        {
            SpecificAssetInfo = request.downloadHandler.text;
            if (SpecificAssetInfoUI != null)
            {
                SpecificAssetInfoUI.text = BeautifyJson(SpecificAssetInfo);
            }
        }
    }

    private IEnumerator GetTransactionsInfo()
    {
        string url = $"{MainnetUrl}/txs/{SpecificTransactionHash}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.ConnectionError ||
            request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError($"Error fetching transaction info: {request.error}");
        }
        else
        {
            TransactionsInfo = request.downloadHandler.text;
            if (TransactionsInfoUI != null)
            {
                TransactionsInfoUI.text = BeautifyJson(TransactionsInfo);
            }
        }
    }

    private string BeautifyJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        var indent = 0;
        var quote = false;
        var result = new StringBuilder();

        for (int i = 0; i < json.Length; i++)
        {
            char ch = json[i];

            if (ch == '\"')
            {
                result.Append(ch);
                // Check if the quote is escaped
                bool escaped = false;
                int index = i;
                while (index > 0 && json[index - 1] == '\\')
                {
                    escaped = !escaped;
                    index--;
                }
                if (!escaped)
                    quote = !quote;
            }
            else if (!quote)
            {
                if (ch == '{' || ch == '[')
                {
                    result.Append(ch);
                    result.Append('\n');
                    indent++;
                    result.Append(new string(' ', indent * 4));
                }
                else if (ch == '}' || ch == ']')
                {
                    result.Append('\n');
                    indent--;
                    result.Append(new string(' ', indent * 4));
                    result.Append(ch);
                }
                else if (ch == ',')
                {
                    result.Append(ch);
                    result.Append('\n');
                    result.Append(new string(' ', indent * 4));
                }
                else if (ch == ':')
                {
                    result.Append(ch);
                    result.Append(' ');
                }
                else if (!char.IsWhiteSpace(ch))
                {
                    result.Append(ch);
                }
            }
            else
            {
                result.Append(ch);
            }
        }

        return result.ToString();
    }

}
