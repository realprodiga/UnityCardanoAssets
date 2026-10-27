# 🎲 Cardano Game Development SDK for Unity

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Unity](https://img.shields.io/badge/Unity-2022.3%2B-blue.svg)](https://unity.com/)
[![Project Catalyst](https://img.shields.io/badge/Proposal-Fund%2012-red)](https://projectcatalyst.io/)

**A comprehensive, open-source SDK to integrate the Cardano Blockchain into Unity games.**

Developed with support from **Project Catalyst Fund 12**, this SDK provides lightweight, data-oriented scripts to interact with Cardano via **Blockfrost** or **Koios** APIs. It is designed to be a "drop-in" solution—no complex dependencies or heavy libraries required.

---

## ✨ Features
*   **🔌 Dual Provider Support:** Seamless integration for both **Blockfrost** (API Key required) and **Koios** (Free/Public).
*   **🛠️ Data-Oriented Inspector:** View Blockchain data (ADA Balance, Assets, Transactions) directly in the Unity Inspector for easy debugging.
*   **📝 Extensible Framework:** Both integration scripts include a commented, 6-step framework allowing you to easily add new API endpoints.
*   **📱 Lightweight:** No external DLLs or heavy plugins. Just pure C# `UnityWebRequest`.
*   **🌐 WebGL Templates:** `CardanoBasic` and mobile-first `CardanoResponsive` templates with CIP-30 wallet connection and balance display.
*   **🚪 Gated Access:** Open in-game barriers only for wallets holding specific NFTs/tokens (AND/OR/NOT rules, quantity ranges, metadata matching).
*   **🪙 Minting Tokens:** Mint CIP-25 NFTs or fungible tokens to players from game events, with optional fee sponsorship.
*   **🎁 Awarding Tokens:** Send existing tokens from a treasury wallet to players from game events.
*   **🧰 Editor Tools:** Project-settings checker, wallet setup window and one-click demo scene builders under `Tools > Cardano SDK`.

📘 **Full documentation:** [CardanoSDK_Documentation.md](CardanoSDK/Assets/CardanoSDK/Documentation/CardanoSDK_Documentation.md)

---

## 📥 Installation

*   **Unity Asset Store:** search for **Cardano Game Development SDK** (free), then import it from *Package Manager > My Assets*.
*   **GitHub release:** download `CardanoSDK_v1.0.0.unitypackage` from [Releases](https://github.com/realprodiga/UnityCardanoAssets/releases) and use *Assets > Import Package > Custom Package...*.
*   **From source:** clone this repository and copy `CardanoSDK/Assets/CardanoSDK` into your project's `Assets` folder.

Then run **Tools > Cardano SDK > Check Project Settings** to install the WebGL templates and apply the required WebGL settings. Everything lives under `Assets/CardanoSDK` in the `Prodiga.CardanoSDK` namespace.

> 🔐 **Never commit secrets.** Wallet secrets created with *Tools > Cardano SDK > Wallet Setup* are saved to `Assets/_CardanoSDK_LocalSecrets/` (git-ignored), and the shipped prefabs and scenes contain no Blockfrost project ID. Use the **preprod** testnet and a dedicated, low-value wallet for minting and awarding. See the documentation's security section.

---

## 🚀 Quick Start

### Option A: Using Blockfrost
*Best for production reliability and higher rate limits.*

1.  Get a Project ID from [Blockfrost.io](https://blockfrost.io/).
2.  In Unity, create a new GameObject (e.g., `CardanoManager`).
3.  Add the `BlockfrostIntegration` component.
4.  **Configuration:**
    *   Paste your **Project ID**.
    *   Enter a **Stake Address** (e.g., `stake1...`) to fetch account info.
    *   Enter an **Address** (e.g., `addr1...`) to fetch address info.
    *   Enter a **Transaction Hash** to fetch specific transaction details.
5.  **Press Play.**
    *   Expand the `--- LOADED DATA ---` foldout in the Inspector to see real-time data from the blockchain.

### Option B: Using Koios
*Best for free access and decentralization.*

1.  In Unity, create a new GameObject.
2.  Add the `KoiosIntegration` component.
3.  **Configuration:**
    *   The `Base Url` is pre-set to Mainnet (`https://api.koios.rest/api/v1`).
    *   Enter your target **Stake Address**, **Address**, or **Transaction Hash**.
4.  **Press Play.**
    *   Expand the `--- LOADED DATA ---` foldout in the Inspector to view the results.

---

## 🛠️ How to Extend (The Framework)

We know every game is unique. You might need the current Epoch number, specific pool details, or network parameters. We built these scripts with a **6-Step Framework** that makes adding new API endpoints easy, even if you are new to working with APIs.

### 🎓 Tutorial: Adding "Latest Epoch"

Let's say your game needs to know the current **Cardano Epoch number**. Here is how you would add that feature using the framework.

#### 1. Find the Data
First, visit the [Blockfrost Documentation](https://docs.blockfrost.io/).
1.  In the left sidebar, look for the **Cardano > Epochs** group.
2.  Click on `Latest Epoch` (or `GET /epochs/latest`).
3.  Look at the **Response** JSON to find the data you want:

    {
      "epoch": 225,              <-- WE WANT THIS!
      "start_time": 1603403091,
      "end_time": 1603835086,
      ...
    }

Now, open `BlockfrostIntegration.cs` and follow the numbered steps in the code:

#### [Step 1] Create the Data Class
This is the "clean" container for your game data. We use **PascalCase** (e.g., `EpochNumber`) because this is what you will see in the Unity Inspector. It should be easy to read.

    // [FRAMEWORK STEP 1: Add public Data Class here]
    [System.Serializable] 
    public class BlockfrostEpochData { 
        public int EpochNumber; 
    }

#### [Step 2] Create the JSON Mapper
This is the "raw" container. Unity's `JsonUtility` is strict—the variable name here **must match the JSON key exactly**.
*   In the docs, the key is `"epoch"`.
*   Therefore, our variable must be `public int epoch;`.

    // [FRAMEWORK STEP 2: Add JSON Mapper here]
    [System.Serializable] 
    class BlockfrostEpochRaw { 
        public int epoch; 
    }

#### [Step 3] Add the Variable
Create a slot for your data class so it appears in the Unity Inspector. We'll name it `CurrentEpoch` so it's easy to find.

    // [FRAMEWORK STEP 3: Add new public variables here]
    public BlockfrostEpochData CurrentEpoch; 

#### [Step 4] Write the Fetch Function
This is the worker function. It goes to the URL, gets the raw data, and moves it into your clean data class.
*   **The URL:** We see in the docs the endpoint is `/epochs/latest`. We append that to our `BaseUrl`.

    // [FRAMEWORK STEP 4: Implement new Coroutines here]
    private IEnumerator FetchEpoch()
    {
        // Build the specific URL based on the documentation
        string url = $"{BaseUrl}/epochs/latest";         
        
        // Create the authorized request
        UnityWebRequest request = CreateRequest(url); 
        yield return request.SendWebRequest();
    
        if (request.result == UnityWebRequest.Result.Success)
        {
            // 1. Convert text to Raw Data (using the Step 2 class)
            var info = JsonUtility.FromJson<BlockfrostEpochRaw>(request.downloadHandler.text);
            
            // 2. Move data to Public Data (using the Step 1 class)
            CurrentEpoch.EpochNumber = info.epoch;
        }
    }

#### [Steps 5 & 6] Initialize and Run
Finally, scroll down to the `OnEnable()` function. We need to make sure the class exists before we use it (Step 5) and then tell Unity to run the fetch function (Step 6).

    private void OnEnable()
    {
        // ... existing initialization ...
    
        // [FRAMEWORK STEP 5: Initialize new data classes here]
        CurrentEpoch = new BlockfrostEpochData();
    
        // ... existing coroutines ...
    
        // [FRAMEWORK STEP 6: Start new Coroutines here]
        StartCoroutine(FetchEpoch());
    }

**🎉 Done!** When you press Play in Unity, your script will now fetch the Latest Epoch, and you will see `EpochNumber: 225` (or current) appear in the Inspector.

---

## 🗺️ Roadmap (Fund 12)

*   ✅ **Milestone 1:** Core Integration Scripts (Blockfrost & Koios)
*   ✅ **Milestone 2:** Unity WebGL Templates (Wallet Connection & Responsive Design)
*   ✅ **Milestone 3:** Sample dApps (Gated Access, Minting, Token Awards)
*   🟡 **Milestone 4:** Unity Asset Store Publication

Detailed status and evidence: [MILESTONE_TRACKING.md](MILESTONE_TRACKING.md)

## 📦 Third-Party Components

The Minting and Awarding features use `cardano-mint-sdk.bundle.js`, an unmodified build of [MeshJS](https://github.com/MeshJS/mesh) and its dependencies (Apache-2.0, MIT, ISC, BSD and MPL-2.0 licensed). The full license list and texts are in [`cardano-mint-sdk.bundle.LICENSES.txt`](CardanoSDK/Assets/CardanoSDK/WebGLTemplates/CardanoBasic/cardano-mint-sdk.bundle.LICENSES.txt).

---

## 🤝 Contributing

Contributions are welcome! Please fork the repository and submit a pull request. 

## 📄 License

This project is licensed under the [MIT License](LICENSE) - see the LICENSE file for details.

---

*Built by [John Moreland](https://github.com/realprodiga) for the Cardano Community.*