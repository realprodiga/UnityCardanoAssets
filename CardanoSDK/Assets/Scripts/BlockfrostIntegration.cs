using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// BlockfrostIntegration handles the interaction between Unity and the Cardano blockchain
/// using the Blockfrost API. It currently fetches and displays information about a specific
/// stake account, associated addresses, assets, specific addresses, assets, and transactions.
/// Future extensions can include additional endpoints and functionalities.
/// Get a Blockfrost Project ID from https://blockfrost.io/ 
/// Free plans allow 50,000 requests per day. Paid plans are also available.
/// </summary>
public class BlockfrostIntegration : MonoBehaviour
{
    [Header("Blockfrost Settings")]
    [Tooltip("Your Blockfrost Project ID (API Token)")] 
    public string ProjectId;

    [Tooltip("Cardano Stake Address to Query")]
    public string StakeAddress = "stake1u8xgz6ed4vsqwnvpe3qv57mel575h0yhmkl70sts3fqysgsz95350";

    [Tooltip("Specific Address to Query")]
    public string SpecificAddress = "addr1qxqs59lphg8g6qndelq8xwqn60ag3aeyfcp33c2kdp46a09re5df3pzwwmyq946axfcejy5n4x0y99wqpgtp2gd0k09qsgy6pz";

    [Tooltip("Specific Asset ID to Query")]
    public string SpecificAssetId = "b0d07d45fe9514f80213f4020e5a61241458be626841cde717cb38a76e7574636f696e";

    [Tooltip("Specific Transaction Hash to Query")]
    public string SpecificTransactionHash = "6e5f825c42c1c6d6b77f2a14092f3b78c8f1b66db6f4cf8caec1555b6f967b3b";

    // Base URLs for different Blockfrost networks
    private readonly string MainnetUrl = "https://cardano-mainnet.blockfrost.io/api/v0";
    private readonly string PreprodUrl = "https://cardano-preprod.blockfrost.io/api/v0";
    private readonly string PreviewUrl = "https://cardano-preview.blockfrost.io/api/v0";
    private readonly string IPFSUrl = "https://ipfs.blockfrost.io/api/v0";
    private readonly string MilkomedaMainnetUrl = "https://milkomeda-mainnet.blockfrost.io/api/v0";
    private readonly string MilkomedaTestnetUrl = "https://milkomeda-testnet.blockfrost.io/api/v0";

    /// <summary>
    /// Unity's Start method is called on the frame when a script is enabled just before
    /// any of the Update methods are called the first time.
    /// </summary>
    private void Start()
    {
        if (string.IsNullOrEmpty(ProjectId))
        {
            Debug.LogError("ProjectId is not set. Please set it in the inspector.");
            return;
        }

        if (string.IsNullOrEmpty(StakeAddress))
        {
            Debug.LogError("StakeAddress is not set. Please set it in the inspector.");
            return;
        }

        if (string.IsNullOrEmpty(SpecificAddress))
        {
            Debug.LogError("SpecificAddress is not set. Please set it in the inspector.");
            return;
        }

        if (string.IsNullOrEmpty(SpecificAssetId))
        {
            Debug.LogError("SpecificAssetId is not set. Please set it in the inspector.");
            return;
        }

        if (string.IsNullOrEmpty(SpecificTransactionHash))
        {
            Debug.LogError("SpecificTransactionHash is not set. Please set it in the inspector.");
            return;
        }

        // Start the coroutine to fetch stake account information
        StartCoroutine(GetStakeAccountInfo());

        // Start additional coroutines for other endpoints
        StartCoroutine(GetAccountAddresses());
        StartCoroutine(GetAccountAddressesAssets());
        StartCoroutine(GetAddressInfo());
        StartCoroutine(GetAssetsList());
        StartCoroutine(GetAssetInfo());
        StartCoroutine(GetTransactionInfo());
    }

    /// <summary>
    /// Coroutine to fetch stake account information from Blockfrost API.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator GetStakeAccountInfo()
    {
        string url = $"{MainnetUrl}/accounts/{StakeAddress}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);

        // Send the request and wait for a response
        yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
        {
            // Attempt to parse the error response
            ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.message))
            {
                Debug.LogError($"Error {errorResponse.status_code}: {errorResponse.error} - {errorResponse.message}");
            }
            else
            {
                Debug.LogError($"Error fetching stake address info: {request.error}");
            }
        }
        else
        {
            // Parse the successful response into an Account object
            Account account = JsonUtility.FromJson<Account>(request.downloadHandler.text);
            if (account != null)
            {
                DisplayAccountInfo(account);
            }
            else
            {
                Debug.LogError("Failed to parse account information.");
            }
        }
    }

    /// <summary>
    /// Coroutine to fetch addresses associated with a stake account from Blockfrost API.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator GetAccountAddresses()
    {
        string url = $"{MainnetUrl}/accounts/{StakeAddress}/addresses?count=10&page=1&order=asc";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);

        // Send the request and wait for a response
        yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
        {
            // Attempt to parse the error response
            ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.message))
            {
                Debug.LogError($"Error {errorResponse.status_code}: {errorResponse.error} - {errorResponse.message}");
            }
            else
            {
                Debug.LogError($"Error fetching account addresses: {request.error}");
            }
        }
        else
        {
            // Parse the successful response into a list of Address objects
            AddressArrayWrapper wrapper = JsonUtility.FromJson<AddressArrayWrapper>("{\"items\":" + request.downloadHandler.text + "}");
            Address[] addresses = wrapper.items;
            if (addresses != null && addresses.Length > 0)
            {
                DisplayAccountAddresses(addresses);
            }
            else
            {
                Debug.LogError("Failed to parse account addresses.");
            }
        }
    }

    /// <summary>
    /// Coroutine to fetch assets associated with the account's addresses from Blockfrost API.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator GetAccountAddressesAssets()
    {
        string url = $"{MainnetUrl}/accounts/{StakeAddress}/addresses/assets?count=10&page=1&order=asc";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);

        // Send the request and wait for a response
        yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
        {
            // Attempt to parse the error response
            ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.message))
            {
                Debug.LogError($"Error {errorResponse.status_code}: {errorResponse.error} - {errorResponse.message}");
            }
            else
            {
                Debug.LogError($"Error fetching account addresses assets: {request.error}");
            }
        }
        else
        {
            // Parse the successful response into a list of AccountAsset objects
            AccountAssetArrayWrapper wrapper = JsonUtility.FromJson<AccountAssetArrayWrapper>("{\"items\":" + request.downloadHandler.text + "}");
            AccountAsset[] assets = wrapper.items;
            if (assets != null && assets.Length > 0)
            {
                DisplayAccountAddressesAssets(assets);
            }
            else
            {
                Debug.LogError("Failed to parse account addresses assets.");
            }
        }
    }

    /// <summary>
    /// Coroutine to fetch information about a specific address from Blockfrost API.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator GetAddressInfo()
    {
        string url = $"{MainnetUrl}/addresses/{SpecificAddress}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);

        // Send the request and wait for a response
        yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
        {
            // Attempt to parse the error response
            ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.message))
            {
                Debug.LogError($"Error {errorResponse.status_code}: {errorResponse.error} - {errorResponse.message}");
            }
            else
            {
                Debug.LogError($"Error fetching address info: {request.error}");
            }
        }
        else
        {
            // Parse the successful response into an AddressInfo object
            AddressInfo addressInfo = JsonUtility.FromJson<AddressInfo>(request.downloadHandler.text);
            if (addressInfo != null)
            {
                DisplayAddressInfo(addressInfo);
            }
            else
            {
                Debug.LogError("Failed to parse address information.");
            }
        }
    }

    /// <summary>
    /// Coroutine to fetch the list of assets from Blockfrost API.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator GetAssetsList()
    {
        string url = $"{MainnetUrl}/assets?count=10&page=1&order=asc";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);

        // Send the request and wait for a response
        yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
        {
            // Attempt to parse the error response
            ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.message))
            {
                Debug.LogError($"Error {errorResponse.status_code}: {errorResponse.error} - {errorResponse.message}");
            }
            else
            {
                Debug.LogError($"Error fetching assets list: {request.error}");
            }
        }
        else
        {
            // Parse the successful response into a list of AssetSummary objects
            AssetSummaryArrayWrapper wrapper = JsonUtility.FromJson<AssetSummaryArrayWrapper>("{\"items\":" + request.downloadHandler.text + "}");
            AssetSummary[] assets = wrapper.items;
            if (assets != null && assets.Length > 0)
            {
                DisplayAssetsList(assets);
            }
            else
            {
                Debug.LogError("Failed to parse assets list.");
            }
        }
    }

    /// <summary>
    /// Coroutine to fetch information about a specific asset from Blockfrost API.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator GetAssetInfo()
    {
        string url = $"{MainnetUrl}/assets/{SpecificAssetId}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);

        // Send the request and wait for a response
        yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
        {
            // Attempt to parse the error response
            ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.message))
            {
                Debug.LogError($"Error {errorResponse.status_code}: {errorResponse.error} - {errorResponse.message}");
            }
            else
            {
                Debug.LogError($"Error fetching asset info: {request.error}");
            }
        }
        else
        {
            // Parse the successful response into an AssetInfo object
            AssetInfo assetInfo = JsonUtility.FromJson<AssetInfo>(request.downloadHandler.text);
            if (assetInfo != null)
            {
                DisplayAssetInfo(assetInfo);
            }
            else
            {
                Debug.LogError("Failed to parse asset information.");
            }
        }
    }

    /// <summary>
    /// Coroutine to fetch information about a specific transaction from Blockfrost API.
    /// </summary>
    /// <returns>IEnumerator for coroutine.</returns>
    private IEnumerator GetTransactionInfo()
    {
        string url = $"{MainnetUrl}/txs/{SpecificTransactionHash}";
        UnityWebRequest request = UnityWebRequest.Get(url);
        request.SetRequestHeader("project_id", ProjectId);

        // Send the request and wait for a response
        yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
#else
        if (request.isNetworkError || request.isHttpError)
#endif
        {
            // Attempt to parse the error response
            ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.message))
            {
                Debug.LogError($"Error {errorResponse.status_code}: {errorResponse.error} - {errorResponse.message}");
            }
            else
            {
                Debug.LogError($"Error fetching transaction info: {request.error}");
            }
        }
        else
        {
            // Parse the successful response into a TransactionInfo object
            TransactionInfo transactionInfo = JsonUtility.FromJson<TransactionInfo>(request.downloadHandler.text);
            if (transactionInfo != null)
            {
                DisplayTransactionInfo(transactionInfo);
            }
            else
            {
                Debug.LogError("Failed to parse transaction information.");
            }
        }
    }

    /// <summary>
    /// Displays the fetched stake account information in the console.
    /// </summary>
    /// <param name="account">The Account object containing stake account details.</param>
    private void DisplayAccountInfo(Account account)
    {
        Debug.Log("=== Stake Account Information ===");
        Debug.Log($"Stake Address: {account.stake_address}");
        Debug.Log($"Active: {account.active}");
        Debug.Log($"Active Epoch: {account.active_epoch}");
        Debug.Log($"Controlled Amount: {account.controlled_amount} Lovelaces");
        Debug.Log($"Rewards Sum: {account.rewards_sum} Lovelaces");
        Debug.Log($"Withdrawals Sum: {account.withdrawals_sum} Lovelaces");
        Debug.Log($"Reserves Sum: {account.reserves_sum} Lovelaces");
        Debug.Log($"Treasury Sum: {account.treasury_sum} Lovelaces");
        Debug.Log($"Withdrawable Amount: {account.withdrawable_amount} Lovelaces");
        Debug.Log($"Pool ID: {account.pool_id}");
        Debug.Log($"DRep ID: {account.drep_id}");
    }

    /// <summary>
    /// Displays the fetched account addresses in the console.
    /// </summary>
    /// <param name="addresses">Array of Address objects associated with the stake account.</param>
    private void DisplayAccountAddresses(Address[] addresses)
    {
        Debug.Log("=== Account Associated Addresses ===");
        foreach (var addr in addresses)
        {
            Debug.Log($"Address: {addr.address}");
        }
    }

    /// <summary>
    /// Displays the fetched assets associated with the account's addresses in the console.
    /// </summary>
    /// <param name="assets">Array of AccountAsset objects associated with the account's addresses.</param>
    private void DisplayAccountAddressesAssets(AccountAsset[] assets)
    {
        Debug.Log("=== Assets Associated with Account Addresses ===");
        foreach (var asset in assets)
        {
            Debug.Log($"Unit: {asset.unit}, Quantity: {asset.quantity}");
        }
    }

    /// <summary>
    /// Displays the fetched address information in the console.
    /// </summary>
    /// <param name="addressInfo">The AddressInfo object containing address details.</param>
    private void DisplayAddressInfo(AddressInfo addressInfo)
    {
        Debug.Log("=== Specific Address Information ===");
        Debug.Log($"Address: {addressInfo.address}");
        Debug.Log("Amounts:");
        foreach (var amt in addressInfo.amount)
        {
            Debug.Log($"  Unit: {amt.unit}, Quantity: {amt.quantity}");
        }
        Debug.Log($"Stake Address: {addressInfo.stake_address}");
        Debug.Log($"Type: {addressInfo.type}");
        Debug.Log($"Script: {addressInfo.script}");
    }

    /// <summary>
    /// Displays the fetched assets list in the console.
    /// </summary>
    /// <param name="assets">Array of AssetSummary objects.</param>
    private void DisplayAssetsList(AssetSummary[] assets)
    {
        Debug.Log("=== Assets List ===");
        foreach (var asset in assets)
        {
            Debug.Log($"Asset: {asset.asset}, Quantity: {asset.quantity}");
        }
    }

    /// <summary>
    /// Displays the fetched asset information in the console, including metadata.
    /// </summary>
    /// <param name="assetInfo">The AssetInfo object containing asset details.</param>
    private void DisplayAssetInfo(AssetInfo assetInfo)
    {
        Debug.Log("=== Specific Asset Information ===");
        Debug.Log($"Asset: {assetInfo.asset}");
        Debug.Log($"Policy ID: {assetInfo.policy_id}");
        Debug.Log($"Asset Name: {assetInfo.asset_name}");
        Debug.Log($"Fingerprint: {assetInfo.fingerprint}");
        Debug.Log($"Quantity: {assetInfo.quantity}");
        Debug.Log($"Initial Mint Tx Hash: {assetInfo.initial_mint_tx_hash}");
        Debug.Log($"Mint or Burn Count: {assetInfo.mint_or_burn_count}");
        Debug.Log($"Onchain Metadata Standard: {assetInfo.onchain_metadata_standard}");
        if (assetInfo.onchain_metadata != null)
        {
            Debug.Log($"Metadata Name: {assetInfo.onchain_metadata.name}");
            Debug.Log($"Metadata Image: {assetInfo.onchain_metadata.image}");
            Debug.Log($"Metadata Arweave ID: {assetInfo.onchain_metadata.arweaveId}");
            Debug.Log($"Metadata Media Type: {assetInfo.onchain_metadata.mediaType}");
            Debug.Log($"Metadata Collection: {assetInfo.onchain_metadata.collection}");

            if (assetInfo.onchain_metadata.attributes != null)
            {
                Debug.Log("Metadata Attributes:");
                foreach (var attr in assetInfo.onchain_metadata.attributes.GetAllAttributes())
                {
                    Debug.Log($"  {attr.Key}: {attr.Value}");
                }
            }
            else
            {
                Debug.Log("Metadata Attributes: null");
            }
        }
        else
        {
            Debug.Log("Onchain Metadata: null");
        }

        if (assetInfo.metadata != null)
        {
            Debug.Log($"Offchain Metadata Name: {assetInfo.metadata.name}");
            Debug.Log($"Offchain Metadata Description: {assetInfo.metadata.description}");
            Debug.Log($"Offchain Metadata Ticker: {assetInfo.metadata.ticker}");
            Debug.Log($"Offchain Metadata URL: {assetInfo.metadata.url}");
            Debug.Log($"Offchain Metadata Decimals: {assetInfo.metadata.decimals}");
        }
        else
        {
            Debug.Log("Offchain Metadata: null");
        }

        // Additional fields can be displayed as needed
    }

    /// <summary>
    /// Displays the fetched transaction information in the console.
    /// </summary>
    /// <param name="transactionInfo">The TransactionInfo object containing transaction details.</param>
    private void DisplayTransactionInfo(TransactionInfo transactionInfo)
    {
        Debug.Log("=== Specific Transaction Information ===");
        Debug.Log($"Hash: {transactionInfo.hash}");
        Debug.Log($"Block: {transactionInfo.block}");
        Debug.Log($"Block Height: {transactionInfo.block_height}");
        Debug.Log($"Block Time: {transactionInfo.block_time}");
        Debug.Log($"Slot: {transactionInfo.slot}");
        Debug.Log($"Index: {transactionInfo.index}");
        Debug.Log("Output Amounts:");
        foreach (var amt in transactionInfo.output_amount)
        {
            Debug.Log($"  Unit: {amt.unit}, Quantity: {amt.quantity}");
        }
        Debug.Log($"Fees: {transactionInfo.fees} Lovelaces");
        Debug.Log($"Deposit: {transactionInfo.deposit} Lovelaces");
        Debug.Log($"Size: {transactionInfo.size} Bytes");
        Debug.Log($"Invalid Before: {transactionInfo.invalid_before}");
        Debug.Log($"Invalid Hereafter: {transactionInfo.invalid_hereafter}");
        Debug.Log($"UTXO Count: {transactionInfo.utxo_count}");
        Debug.Log($"Withdrawal Count: {transactionInfo.withdrawal_count}");
        Debug.Log($"MIR Cert Count: {transactionInfo.mir_cert_count}");
        Debug.Log($"Delegation Count: {transactionInfo.delegation_count}");
        Debug.Log($"Stake Cert Count: {transactionInfo.stake_cert_count}");
        Debug.Log($"Pool Update Count: {transactionInfo.pool_update_count}");
        Debug.Log($"Pool Retire Count: {transactionInfo.pool_retire_count}");
        Debug.Log($"Asset Mint or Burn Count: {transactionInfo.asset_mint_or_burn_count}");
        Debug.Log($"Redeemer Count: {transactionInfo.redeemer_count}");
        Debug.Log($"Valid Contract: {transactionInfo.valid_contract}");
    }

    #region Future Implementations

    // TODO: Implement methods for other Blockfrost Accounts endpoints
    // Examples:
    // - GetAccountRewards
    // - GetAccountHistory
    // - GetAccountDelegations
    // - GetAccountRegistrations
    // - GetAccountWithdrawals
    // - GetAccountMIRs
    // - GetAccountAddressesTotal

    // TODO: Implement methods for Addresses endpoints
    // Examples:
    // - GetAddressExtended
    // - GetAddressTotal
    // - GetAddressUTXOs
    // - GetAddressUTXOsAsset
    // - GetAddressTransactions

    // TODO: Implement methods for Assets endpoints
    // Examples:
    // - GetAssetHistory
    // - GetAssetTransactions
    // - GetAssetAddresses
    // - GetAssetsPolicy

    // TODO: Implement methods for Transactions endpoints
    // Examples:
    // - GetTransactionUTXOs
    // - GetTransactionStakes
    // - GetTransactionDelegations
    // - GetTransactionWithdrawals
    // - GetTransactionMIRs
    // - GetTransactionPoolUpdates
    // - GetTransactionPoolRetires
    // - GetTransactionMetadata
    // - GetTransactionMetadataCBOR
    // - GetTransactionRedeemers
    // - GetTransactionRequiredSigners
    // - GetTransactionCBOR

    // TODO: Implement POST /tx/submit endpoint for submitting transactions

    // TODO: Add support for multiple networks by allowing selection of network endpoints

    // TODO: Implement rate limiting and retry logic based on Blockfrost's rate limiting policies

    // TODO: Extend data models to include additional endpoints and nested data structures

    // TODO: Provide methods for interacting with other parts of the Cardano blockchain (e.g., transactions, assets)

    // TODO: Implement comprehensive error handling and logging mechanisms

    // TODO: Add unit tests to validate the functionality of each method

    // TODO: Develop a user-friendly interface within Unity for configuring and using the Blockfrost SDK

    #endregion

    /// <summary>
    /// Data model representing a stake account as returned by Blockfrost API.
    /// </summary>
    [System.Serializable]
    public class Account
    {
        public string stake_address;
        public bool active;
        public int active_epoch;
        public string controlled_amount;
        public string rewards_sum;
        public string withdrawals_sum;
        public string reserves_sum;
        public string treasury_sum;
        public string withdrawable_amount;
        public string pool_id;
        public string drep_id;
    }

    /// <summary>
    /// Data model representing an error response from Blockfrost API.
    /// </summary>
    [System.Serializable]
    public class ErrorResponse
    {
        public int status_code;
        public string error;
        public string message;
    }

    /// <summary>
    /// Data model representing an address associated with a stake account.
    /// </summary>
    [System.Serializable]
    public class Address
    {
        public string address;
    }

    /// <summary>
    /// Wrapper class to deserialize an array of addresses.
    /// </summary>
    [System.Serializable]
    public class AddressArrayWrapper
    {
        public Address[] items;
    }

    /// <summary>
    /// Data model representing an asset associated with account addresses.
    /// </summary>
    [System.Serializable]
    public class AccountAsset
    {
        public string unit;
        public string quantity;
    }

    /// <summary>
    /// Wrapper class to deserialize an array of account assets.
    /// </summary>
    [System.Serializable]
    public class AccountAssetArrayWrapper
    {
        public AccountAsset[] items;
    }

    /// <summary>
    /// Data model representing a summary of an asset from the assets list.
    /// </summary>
    [System.Serializable]
    public class AssetSummary
    {
        public string asset;
        public string quantity;
    }

    /// <summary>
    /// Wrapper class to deserialize an array of asset summaries.
    /// </summary>
    [System.Serializable]
    public class AssetSummaryArrayWrapper
    {
        public AssetSummary[] items;
    }

    /// <summary>
    /// Data model representing detailed information about a specific address.
    /// </summary>
    [System.Serializable]
    public class AddressInfo
    {
        public string address;
        public Amount[] amount;
        public string stake_address;
        public string type;
        public bool script;
    }

    /// <summary>
    /// Data model representing an amount in an address.
    /// </summary>
    [System.Serializable]
    public class Amount
    {
        public string unit;
        public string quantity;
    }

    /// <summary>
    /// Data model representing detailed information about a specific asset.
    /// </summary>
    [System.Serializable]
    public class AssetInfo
    {
        public string asset;
        public string policy_id;
        public string asset_name;
        public string fingerprint;
        public string quantity;
        public string initial_mint_tx_hash;
        public int mint_or_burn_count;
        public OnchainMetadata onchain_metadata;
        public Metadata metadata;
        public string onchain_metadata_standard;
        public string onchain_metadata_extra;
    }

    /// <summary>
    /// Data model representing on-chain metadata of an asset.
    /// </summary>
    [System.Serializable]
    public class OnchainMetadata
    {
        public string name;
        public string image;
        public string arweaveId;
        public string mediaType;
        public Attributes attributes;
        public string collection;
    }

    /// <summary>
    /// Data model representing attributes of an asset.
    /// </summary>
    [System.Serializable]
    public class Attributes
    {
        public string Hat;
        public string Body;
        public string Cane;
        public string Eyes;
        public string Mouth;
        public string Clothes;
        public string Background;
        public string Accessories;

        /// <summary>
        /// Retrieves all attributes as a dictionary.
        /// </summary>
        /// <returns>Dictionary of attribute key-value pairs.</returns>
        public Dictionary<string, string> GetAllAttributes()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(Hat)) dict.Add("Hat", Hat);
            if (!string.IsNullOrEmpty(Body)) dict.Add("Body", Body);
            if (!string.IsNullOrEmpty(Cane)) dict.Add("Cane", Cane);
            if (!string.IsNullOrEmpty(Eyes)) dict.Add("Eyes", Eyes);
            if (!string.IsNullOrEmpty(Mouth)) dict.Add("Mouth", Mouth);
            if (!string.IsNullOrEmpty(Clothes)) dict.Add("Clothes", Clothes);
            if (!string.IsNullOrEmpty(Background)) dict.Add("Background", Background);
            if (!string.IsNullOrEmpty(Accessories)) dict.Add("Accessories", Accessories);
            return dict;
        }
    }

    /// <summary>
    /// Data model representing off-chain metadata of an asset.
    /// </summary>
    [System.Serializable]
    public class Metadata
    {
        public string name;
        public string description;
        public string ticker;
        public string url;
        public string logo;
        public int decimals;
    }

    /// <summary>
    /// Data model representing detailed information about a specific transaction.
    /// </summary>
    [System.Serializable]
    public class TransactionInfo
    {
        public string hash;
        public string block;
        public int block_height;
        public int block_time;
        public int slot;
        public int index;
        public Amount[] output_amount;
        public string fees;
        public string deposit;
        public int size;
        public string invalid_before;
        public string invalid_hereafter;
        public int utxo_count;
        public int withdrawal_count;
        public int mir_cert_count;
        public int delegation_count;
        public int stake_cert_count;
        public int pool_update_count;
        public int pool_retire_count;
        public int asset_mint_or_burn_count;
        public int redeemer_count;
        public bool valid_contract;
    }
}
