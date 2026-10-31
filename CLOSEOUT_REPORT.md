# Project Close-out Report: Cardano Game Development SDK for Unity

## Project details
- **Project name:** Cardano Game Development SDK for Unity
- **Project ID:** 1200055 (Project Catalyst Fund 12, Cardano Open: Developers)
- **Project manager / main proposer:** John Moreland (prodiga)
- **Funding:** 84,000 ADA
- **Completion date:** 2026-10-31
- **GitHub:** https://github.com/realprodiga/UnityCardanoAssets
- **Close-out video:** https://youtu.be/hTamqiFrlSA
- **All project videos (YouTube playlist):** https://www.youtube.com/playlist?list=PLOo6w61IkGqs

## Problem and goal
Resources for building Cardano games are scarce. The project set out to give Unity developers an SDK of drop-in assets (WebGL templates, C# scripts for Cardano APIs, and sample dApps for common use cases) so they can prototype and ship Cardano games in less time. All outputs were to be open source under MIT and published free on the Unity Asset Store.

## Deliverables by milestone

| Milestone | Delivered | Evidence |
|---|---|---|
| M1: Cardano Integration Scripts | `BlockfrostIntegration.cs` and `KoiosIntegration.cs` (accounts, addresses, assets, transactions) plus a framework for adding endpoints | Approved. Video: https://www.youtube.com/watch?v=xjHhKLWU6Zo |
| M2: Unity WebGL Templates | CardanoBasic and CardanoResponsive (mobile-first), CIP-30 wallet connect and balance display | Approved. Live demos: https://realprodiga.github.io/UnityCardanoAssets/CardanoBasic/ and https://realprodiga.github.io/UnityCardanoAssets/CardanoResponsive/ |
| M3: Unity Sample dApps | Gated Access, Awarding Tokens (treasury wallet) and Minting Tokens (NFT or fungible token, CIP-25 metadata), each with a scene, prefabs and tutorial; optional fee sponsorship | POA submitted 2026-10-31. Demo + tutorial video (real mainnet transactions): https://youtu.be/HfDrD3IEzA0 |
| M4: Asset Store publication | v1.0.0 package with documentation, submitted free to the Unity Asset Store on 2026-10-27 (validator passed, in Unity's curation queue); GitHub release as the contingency; video shared on the Cardano Forum and X | POA submitted 2026-10-31. Release: https://github.com/realprodiga/UnityCardanoAssets/releases/tag/v1.0.0 · Video: https://youtu.be/p1_6gx1SeKo · Forum: https://forum.cardano.org/t/cardano-game-development-sdk-for-unity/157648 |
| Final: Close-out | This report and the close-out video | Video: https://youtu.be/hTamqiFrlSA |

Criterion-by-criterion evidence for every milestone is in the trackable spreadsheet: https://github.com/realprodiga/UnityCardanoAssets/blob/main/MILESTONE_TRACKING.md

## Challenge KPIs and how the project addressed them
- **Open-source developer tooling for Cardano:** every output is MIT licensed on GitHub with documentation, and the package is free.
- **Lowering the barrier for new developers:** a single .unitypackage, a setup checker (Tools > Cardano SDK > Check Project Settings), a wallet setup window, one-click demo scene builders, and written plus video tutorials for each component.
- **Practical, reusable use cases:** the three sample dApps (token-gated access, minting, awarding) cover common game patterns and work as templates.

## Project KPIs and how the project addressed them
- **All proposed outputs delivered with no change in scope:** 2 API integrations, 2 WebGL templates, 3 sample dApps, Asset Store submission. The only change request (December 2025) moved the timeline, not the scope.
- **Multiple API providers to reduce dependency risk:** Blockfrost and Koios are both supported.
- **Developer adoption:** v1.0.0 was released on GitHub on 2026-10-27 and announced on the Cardano Forum and X on 2026-10-31. Usage (GitHub stars, release downloads, Asset Store downloads once live) will be tracked from here.

## Key achievements
- A complete, documented v1.0.0 that goes beyond the proposal: NFT-or-token minting with CIP-25 metadata, an AND/OR/NOT access rule engine with quantity and metadata conditions, optional fee sponsorship, a mobile-first template, and editor tooling.
- Guideline-compliant packaging that passed Unity's Asset Store validator, with secrets kept out of the package by design.
- Tutorials, demo videos and live demos for every milestone, with the M3 dApps shown working on mainnet.

## Key learnings
- **Client-side signing has to be explained clearly.** A wallet key inside a WebGL build is public. The SDK documents this prominently and isolates signing in one bridge; a server-relay option is planned so production games keep keys on a server.
- **Indexers lag the chain.** Back-to-back transactions can fail with stale UTXOs; waiting or retrying is needed (a retry is planned for v1.0.1).
- **Asset Store curation takes time.** Packaging rules and a 10+ business-day queue need to be planned for well before a deadline. Having the GitHub contingency in the acceptance criteria was valuable.
- **Plan for personal disruptions.** Family emergencies delayed the project; an early, transparent change request kept it on track.

## Next steps
- Playable preprod builds of the three M3 dApps on the GitHub Pages links (by 2026-11-02).
- Mark the Asset Store listing live once Unity approves it, and add the demo video and screenshots to the listing.
- v1.0.1: server-relay signing option, URP support, auto-assigning wallet secrets from Wallet Setup, retry on stale UTXOs, testnet-friendly demo defaults, documentation fixes.
- Ongoing support through GitHub issues.

## Final thoughts
Thank you to the Project Catalyst team, the reviewers and the voters who funded this. The goal was to make building on Cardano feel natural for Unity developers, and v1.0.0 gives them working, documented building blocks to start from.

## Links
- GitHub: https://github.com/realprodiga/UnityCardanoAssets
- Release v1.0.0: https://github.com/realprodiga/UnityCardanoAssets/releases/tag/v1.0.0
- Documentation: https://github.com/realprodiga/UnityCardanoAssets/blob/main/CardanoSDK/Assets/CardanoSDK/Documentation/CardanoSDK_Documentation.md
- Milestone tracking: https://github.com/realprodiga/UnityCardanoAssets/blob/main/MILESTONE_TRACKING.md
- Unity Asset Store (live after Unity's review): https://assetstore.unity.com/packages/slug/422888
- YouTube playlist: https://www.youtube.com/playlist?list=PLOo6w61IkGqs
- Cardano Forum: https://forum.cardano.org/t/cardano-game-development-sdk-for-unity/157648
