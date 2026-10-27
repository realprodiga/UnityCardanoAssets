# Cardano Game Development SDK for Unity — Documentation

Version 1.0.0 · Unity 2022.3 LTS or newer · WebGL
Source, issues and releases: https://github.com/realprodiga/UnityCardanoAssets
Funded by Cardano Project Catalyst, Fund 12.

---

## Contents

1. Overview
2. Requirements and external services
3. Installation
4. Project setup
5. Networks, API keys and test wallets
6. Components reference
7. Demo scenes
8. Editor tools
9. Building and testing WebGL
10. Security model
11. Adding your own API endpoints
12. Troubleshooting
13. Third-party components and licensing

---

## 1. Overview

A lightweight, dependency-light toolkit for adding Cardano to Unity WebGL games:

| Feature | What it does |
| --- | --- |
| **Blockfrost and Koios integration** | Read account, address, asset and transaction data from either provider with plain `UnityWebRequest`. Results show in the Inspector under `--- LOADED DATA ---`. |
| **Wallet connection (CIP-30)** | Connect a browser wallet (Eternl, Lace, Typhon, Yoroi, Vespr, ...) from a WebGL build and read its balance and assets. |
| **WebGL templates** | `CardanoBasic` (fixed-size page with a Connect Wallet panel) and `CardanoResponsive` (mobile-first, full-window). |
| **Gated Access** | Open or close in-game barriers depending on NFTs/tokens the player holds, with AND/OR/NOT rules, quantity ranges and metadata matching. |
| **Minting Tokens** | Mint a CIP-25 NFT or fungible token to the player from a game event, with optional fee sponsorship. |
| **Awarding Tokens** | Send existing tokens from a treasury wallet to the player from a game event, with optional fee sponsorship. |
| **Editor tools** | Project-settings checker, wallet-secret setup window, one-click demo scene builders and UI repair tools. |

All code lives in the `Prodiga.CardanoSDK` namespace (demo code in `Prodiga.CardanoSDK.Demo`, editor tools in `Prodiga.CardanoSDK.EditorTools`).

---

## 2. Requirements and external services

**Unity**
- Unity 2022.3 LTS or newer, with the **WebGL Build Support** module.
- Packages (installed automatically as dependencies; otherwise add them in Package Manager):
  - `com.unity.textmeshpro` (TextMeshPro, plus TMP Essential Resources)
  - `com.unity.ugui`
  - `com.unity.nuget.newtonsoft-json` (used by Gated Access metadata rules, Minting and Awarding)

**External services (please read)**
- **Blockfrost** (https://blockfrost.io) is a third-party API service. It requires a free account and a project ID. The free tier has daily request limits; paid tiers are offered by Blockfrost, not by this asset. Blockfrost's own terms apply.
- **Koios** (https://koios.rest) is a free, community-run public API. No key is needed for basic use. Koios's terms and rate limits apply.
- **Mesh SDK bundle**: the Minting and Awarding features use `cardano-mint-sdk.bundle.js` (an unmodified build of the open-source MeshJS library). The Asset Store package does **not** include this file. At runtime the WebGL templates load a pinned, integrity-checked copy from the jsDelivr CDN (served from this project's GitHub release). For offline or self-hosted builds, download the file from the GitHub release and place it next to `index.html` in your WebGL template folder (see 9.4).
- A **CIP-30 browser wallet** extension (or a mobile wallet's dApp browser) is needed to connect a player wallet.

Read-only features (Blockfrost/Koios integration, wallet display, Gated Access) do not use the Mesh bundle.

---

## 3. Installation

**From the Unity Asset Store:** open *Window > Package Manager > My Assets*, find **Cardano Game Development SDK**, then click *Download* and *Import*.

**From GitHub:** download `CardanoSDK_v1.0.0.unitypackage` from https://github.com/realprodiga/UnityCardanoAssets/releases, then use *Assets > Import Package > Custom Package...*. The GitHub package includes the Mesh SDK bundle and its license file.

Everything is imported under `Assets/CardanoSDK/`:

```
Assets/CardanoSDK/
  Demo/Scenes/        five demo scenes (see section 7)
  Demo/Scripts/       PlayerController (demo only)
  Documentation/      this file
  Icons/              copy / checkmark UI icons
  Plugins/            .jslib browser bridges (wallet, minting, treasury, clipboard)
  Prefabs/            CardanoManager, Minter, Treasury, GatedGate, CheckpointZone, status panels
  Scripts/Core/       Blockfrost/Koios integration, wallet manager, shared types
  Scripts/GatedAccess/
  Scripts/Minting/
  Scripts/Awarding/
  Scripts/Editor/     editor tools (not included in builds)
  WebGLTemplates/     source copies of the two WebGL templates
  Third-Party Notices.txt
```

---

## 4. Project setup

On first import, the SDK checks your project once per Editor session and offers to fix anything missing. You can run the check at any time from **Tools > Cardano SDK > Check Project Settings**. It handles:

1. **TMP Essentials**: imports them if missing.
2. **WebGL compression**: sets *Player Settings > WebGL > Publishing Settings > Compression Format* to **Disabled**. This is required for some mobile wallets and iOS Safari, and for simple local servers.
3. **WebGL templates**: copies `CardanoBasic` and `CardanoResponsive` from `Assets/CardanoSDK/WebGLTemplates` into `Assets/WebGLTemplates`. Unity only finds templates in that folder.
4. **Active template**: selects `CardanoResponsive`. You can change it under *Player Settings > WebGL > Resolution and Presentation*.

Also switch the build target to **WebGL** (*File > Build Settings > WebGL > Switch Platform*). Wallet connection, minting and awarding only run in WebGL builds. In the Editor, the Blockfrost/Koios fetches still work.

---

## 5. Networks, API keys and test wallets

### 5.1 Pick a network
Use the **preprod** testnet while developing. Test ADA is free and nothing on testnet has real value.

| Network | Blockfrost project ID prefix | Koios `BaseUrl` | Explorer |
| --- | --- | --- | --- |
| Mainnet | `mainnet...` | `https://api.koios.rest/api/v1` | cardanoscan.io |
| Preprod | `preprod...` | `https://preprod.koios.rest/api/v1` | preprod.cardanoscan.io |
| Preview | `preview...` | `https://preview.koios.rest/api/v1` | preview.cardanoscan.io |

`BlockfrostIntegration` picks the API host automatically from the project ID's prefix. Your player wallet, Blockfrost project and wallet secrets must all use the **same** network.

### 5.2 Blockfrost project ID
1. Sign up at https://blockfrost.io and create a project for the network you chose.
2. Paste the project ID into **BlockfrostIntegration > Project Id** on the `CardanoManager` prefab (`Assets/CardanoSDK/Prefabs/CardanoManager.prefab`). Every demo scene uses this prefab.
3. Do **not** commit or publish a project that contains your real project ID.

### 5.3 Minting and treasury wallets (only for Minting / Awarding)
Minting and Awarding sign transactions with a game-owned wallet. Its 24-word recovery phrase is stored in a `MintingWalletSecret` asset.

1. Open **Tools > Cardano SDK > Wallet Setup**.
2. Pick the **Minting Wallet** or **Treasury Wallet** tab, enter the 24 words of a wallet you created **only for this purpose** (for example a new wallet in Eternl or Lace set to preprod), and set **Network** (`preprod` by default).
3. Click save. The asset is written to `Assets/_CardanoSDK_LocalSecrets/`, outside the SDK folder, so it is never exported with the SDK. Add that folder to your `.gitignore`.
4. Fund the wallet's address with test ADA from the Cardano faucet: https://docs.cardano.org/cardano-testnets/tools/faucet
5. For Awarding: send the tokens you want to give out to the treasury wallet.

The SDK does **not** ship with any wallet or key. Read section 10 before using a wallet that holds real value.

---

## 6. Components reference

### 6.1 BlockfrostIntegration
Fetches data from Blockfrost when enabled, and again whenever a wallet connects.

| Field | Description |
| --- | --- |
| Project Id | Your Blockfrost project ID. Its prefix selects the network. |
| Stake Address To Fetch | `stake1...` / `stake_test1...` address for account info. Filled in by `CardanoWalletManager` when a wallet connects. |
| Address To Fetch | Payment address. Its ADA and assets appear in *Current Address*. |
| Asset Id To Fetch | Policy ID + asset name hex. |
| Tx Hash To Fetch | Transaction hash. |
| `--- LOADED DATA ---` | Results: `CurrentAccount`, `CurrentAddress` (with asset list), `CurrentAssetDetails`, `CurrentTransaction`. |

Event: `OnDataUpdated` is raised whenever `CurrentAddress` refreshes or clears.

### 6.2 KoiosIntegration
Same purpose and layout as Blockfrost. Set **Base Url** for your network (see 5.1). No key needed.

### 6.3 CardanoWalletManager
Bridges the WebGL page and your wallet. Lives on the `CardanoManager` prefab. **The GameObject must be named `CardanoManager`**, because the template's Connect button calls `SendMessage("CardanoManager", "RequestConnection")`.

| Field / method | Description |
| --- | --- |
| Blockfrost Provider / Koios Provider | Providers that get the connected wallet's address and stake address. |
| Status Text | Optional TMP text for connection status. |
| `RequestConnection()` | Opens the wallet picker / connection prompt, or disconnects if already connected. |
| `Disconnect()` | Clears the connection and the providers' loaded data. |

### 6.4 WalletDisplay
Shows the connected wallet's ADA balance on a TMP text. Uses Blockfrost first and Koios as a fallback.

### 6.5 GatedAccessController and AccessCheckpoint (Gated Access)
Put `GatedAccessController` on a gate object (see `GatedGate.prefab`).

| Field | Description |
| --- | --- |
| Blockfrost Manager / Koios Manager | Data source for the player's holdings. |
| Advanced Mode | Off: check a single NFT/policy. On: use **Groups**. |
| Required Policy Id / Required Asset Name Hex | Simple rule. Leave the asset name empty to accept any asset under the policy. |
| Groups | Advanced rule: conditions inside a group are AND-ed, and the gate opens if ANY group passes (OR). Each condition can be **Invert**ed (NOT). |
| Barrier | Disabled while access is granted (wall, door, collider). |
| Locked / Unlocked Indicator | Optional objects shown in each state. |
| Requirement Description / Requirement Text | Optional text explaining what is needed, shown while locked. |
| Require Checkpoint Trigger | If on, the gate only updates when `PerformCheck()` is called, for example by an `AccessCheckpoint`. |

**Condition types** (`AssetCondition`): `AssetPresent` (leave `AssetNameHex` empty to match any asset under the policy), `QuantityThreshold` (min/max range), `MetadataMatch` (dot-path into the asset's on-chain metadata, e.g. `attributes.Hat`, exact match; needs a specific `AssetNameHex`).

**AccessCheckpoint**: put it on a trigger collider in front of the gate (see `CheckpointZone.prefab`). When the player enters or exits, or the wallet changes while the player is inside, it calls `Gate.PerformCheck()` and invokes `OnCheckPerformed`. The player is any collider tagged **Player** (or carrying the demo `PlayerController`).

### 6.6 MintingController (Minting Tokens)
Put it on a "minter" object (see `Minter.prefab`) and call `TriggerMint()` from a button or any UnityEvent.

| Field | Description |
| --- | --- |
| Blockfrost Manager | Required. Its Project Id is also used by the minting wallet. |
| Wallet Secret | `MintingWalletSecret` asset (section 5.3). |
| Token | `TokenType` (NFT/Fungible), `AssetName` (ASCII, at most 32 bytes), `EnforceUniqueness` (appends a number per mint), `Quantity`, CIP-25 metadata (`DisplayName`, `ImageUri` such as `ipfs://...`, `ImageMediaType`, `Description`, `Attributes`). |
| Policy | Minting policy settings. The policy is a single-signature native script owned by the minting wallet. (`ExpirySlot` is reserved and not used yet.) |
| Sponsorship | `AllowSponsorship`: the minting wallet pays the fee instead of the player if it would keep at least `MinimumRemainingLovelace` and the player's wallet passes `Conditions` (same rule engine as Gated Access). |
| Status Text / Tx Hash Field / Tx Hash Row | Optional UI. |
| On Mint Succeeded / On Mint Failed | Events carrying the tx hash or error message. |
| `OpenTransactionOnExplorer()` | Opens the last tx on Cardanoscan for the configured network. |

**Who pays:** by default the player pays the network fee (two signatures: the player's wallet and the minting wallet). With sponsorship the minting wallet signs and pays alone.

### 6.7 AwardingController (Awarding Tokens)
Sends **existing** tokens from a treasury wallet to the player. Put it on a "treasury" object (see `Treasury.prefab`) and call `TriggerAward()`.

| Field | Description |
| --- | --- |
| Treasury Wallet Secret | `MintingWalletSecret` asset of the treasury wallet. It can be the same asset as the minting wallet. |
| Token | `PolicyId`, `AssetNameHex`, `QuantityPerAward`, optional `DisplayName`. |
| Sponsorship, UI, events, explorer | Same as MintingController (`OnAwardSucceeded`, `OnAwardFailed`). |

### 6.8 CopyToClipboardButton
Copies a TMP text (for example a tx hash) to the real browser clipboard in WebGL, and flashes a checkmark icon.

---

## 7. Demo scenes

Open from `Assets/CardanoSDK/Demo/Scenes/`. Before running any of them, set your Blockfrost project ID on `CardanoManager.prefab` (5.2).

| Scene | Shows | Extra setup |
| --- | --- | --- |
| **IntegrationScriptDemo** | Blockfrost and Koios scripts. Enter addresses/hashes, press Play, and inspect `--- LOADED DATA ---`. Works in the Editor. | None |
| **CardanoWebGL** | Wallet connection and balance display in a WebGL build. | Build for WebGL (section 9) |
| **GatedAccess** | Walk (WASD/arrows, Shift to run, Space to jump, mouse buttons to orbit/steer the camera) up to gates that open only if your wallet holds the configured NFT/policy. | Set each gate's Required Policy Id to an asset you hold on your network |
| **MintingTokens** | Press Mint to mint an NFT to your wallet, then copy or open the tx hash. | Minting wallet (5.3), funded |
| **AwardingTokens** | Press Award to receive tokens from the treasury. | Treasury wallet (5.3), funded and holding the token; set `Token.PolicyId` / `AssetNameHex` |

The Gated Access, Minting and Awarding scenes can be regenerated from scratch with the scene builders (section 8).

---

## 8. Editor tools

All are under **Tools > Cardano SDK**:

| Menu item | Purpose |
| --- | --- |
| Check Project Settings | Run the setup check in section 4. |
| Wallet Setup | Create or edit minting/treasury wallet secrets (5.3). |
| Build Gated Access / Minting / Awarding Demo Scene | Regenerate a demo scene with all objects wired up. |
| Extract Demo Prefabs | Re-create the prefabs in `Prefabs/` from the demo scenes. |
| Repair > Rebuild Wallet / Mint / Award Status Panel | Rebuild a status-panel UI if its layout gets broken. |

---

## 9. Building and testing WebGL

### 9.1 Build
*File > Build Settings > WebGL > Build*, then choose an output folder.

### 9.2 Serve locally
WebGL builds do not run from `file://`. Serve the build folder over HTTP, for example:
```
cd <build folder>
python -m http.server 8000
```
Open `http://localhost:8000` and click **Connect Wallet**.

### 9.3 Mobile wallets (HTTPS)
Mobile dApp browsers need HTTPS. Use a tunnel such as `ngrok http 8000` and open the `https://` URL inside your mobile wallet's dApp browser.

### 9.4 Mesh SDK bundle: CDN vs. local file
The templates first try `cardano-mint-sdk.bundle.js` next to `index.html`. If it is not there, they load a pinned copy from jsDelivr with Subresource Integrity, so the browser refuses any modified file. To host it yourself or work offline, download `cardano-mint-sdk.bundle.js` and `cardano-mint-sdk.bundle.LICENSES.txt` from the GitHub release into `Assets/WebGLTemplates/<template>/`.

---

## 10. Security model (please read)

- **Anything in a WebGL build is public.** The minting/treasury wallet's recovery phrase is compiled into the build and can be extracted by anyone using browser dev tools. Only use **dedicated wallets holding small, expendable amounts**, ideally on testnet. Never use your personal wallet.
- For production games with real value, move signing to a server you control. The signing step is isolated in the `.jslib` bridges (`InitMintingWallet` / `MintToken`, `InitTreasuryWallet` / `AwardToken`), so a server-relay signer can replace them without changing the controllers' public API.
- Keep `Assets/_CardanoSDK_LocalSecrets/` and your Blockfrost project ID out of source control and published packages.
- A Blockfrost project ID in a WebGL build is also visible to players. Use a separate project for each game and watch its usage.
- The player's own wallet is only accessed through the standard CIP-30 API. The player approves every signature in their wallet, and the SDK never sees their keys.

---

## 11. Adding your own API endpoints

`BlockfrostIntegration.cs` and `KoiosIntegration.cs` contain a commented **6-step framework** (`[FRAMEWORK STEP 1]` to `[FRAMEWORK STEP 6]`): data class, JSON mapper, Inspector variable, fetch coroutine, initialization and start call. A full walkthrough that adds "Latest Epoch" is in the GitHub README: https://github.com/realprodiga/UnityCardanoAssets#-how-to-extend-the-framework

---

## 12. Troubleshooting

| Symptom | Fix |
| --- | --- |
| Templates do not appear in Player Settings | Run *Tools > Cardano SDK > Check Project Settings* so they are copied to `Assets/WebGLTemplates`. |
| Connect Wallet does nothing / no wallet prompt | Install a CIP-30 wallet extension, or open the page in a mobile wallet's dApp browser. |
| Blockfrost 403 / "Network mismatch" | The project ID's network does not match the wallet or addresses. Use a matching `preprod...` / `mainnet...` ID. |
| Blockfrost 402 / 429 | Daily or rate limit reached on your Blockfrost plan. Wait, or switch to Koios. |
| Mint/award: "insufficient funds" | Fund the minting/treasury wallet (faucet on testnet), or turn sponsorship off so the player pays. |
| Mint/award: `CardanoMintSDK is undefined` | The Mesh bundle did not load. Check your network/CDN access, or put the bundle next to `index.html` (9.4). |
| Copy button does nothing in the Editor | Expected. It uses the browser clipboard in WebGL and `GUIUtility.systemCopyBuffer` elsewhere. |
| Build fails to load on a server | Make sure WebGL compression is **Disabled**, or configure your server for Brotli/Gzip. |

Support: open an issue at https://github.com/realprodiga/UnityCardanoAssets/issues

---

## 13. Third-party components and licensing

- The SDK's C# code, `.jslib` bridges, templates, prefabs and scenes are original work by the publisher.
- Unity packages used: TextMeshPro, uGUI, Newtonsoft Json (`com.unity.nuget.newtonsoft-json`).
- The Asset Store package **does not contain** third-party code. The optional Mesh SDK bundle loaded at runtime contains open-source packages under Apache-2.0, MIT, ISC, BSD and MPL-2.0 licenses. See `Third-Party Notices.txt` and the full `cardano-mint-sdk.bundle.LICENSES.txt` published with it on GitHub.
- The same SDK is also available as open source (MIT License) on GitHub.
