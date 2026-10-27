mergeInto(LibraryManager.library, {

  // Call once (MintingController does this in OnEnable) before InitMintingWallet/MintToken can
  // be used. Requires cardano-mint-sdk.bundle.js (window.CardanoMintSDK) to already be loaded --
  // add a <script src="cardano-mint-sdk.bundle.js"> tag to the WebGL template, same as loader.js.
  InitMintingWallet: function (mnemonicPtr, networkPtr, blockfrostProjectIdPtr, objectNamePtr, methodNamePtr) {
    var mnemonic = UTF8ToString(mnemonicPtr);
    var network = UTF8ToString(networkPtr); // "mainnet" | "preprod" | "preview"
    var blockfrostProjectId = UTF8ToString(blockfrostProjectIdPtr);
    var objectName = UTF8ToString(objectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);

    (async function () {
      try {
        if (!window.CardanoMintSDK) {
          throw new Error("CardanoMintSDK bundle not loaded -- check the WebGL template includes cardano-mint-sdk.bundle.js before this runs.");
        }

        var networkId = (network === "mainnet") ? 1 : 0;
        var provider = new window.CardanoMintSDK.BlockfrostProvider(blockfrostProjectId);
        var words = mnemonic.trim().split(/\s+/);

        var wallet = new window.CardanoMintSDK.MeshWallet({
          networkId: networkId,
          fetcher: provider,
          submitter: provider,
          key: { type: "mnemonic", words: words },
        });
        await wallet.init();

        var mintingAddress = await wallet.getChangeAddress();

        // Single-signature policy tied to the minting wallet's own address -- every token
        // minted through this wallet shares one Policy ID.
        var forgeScript = window.CardanoMintSDK.ForgeScript.withOneSignature(mintingAddress);

        window._mintingWallet = wallet;
        window._mintingProvider = provider;
        window._mintingForgeScript = forgeScript;

        window.myGameInstance.SendMessage(objectName, methodName, "OK|" + mintingAddress);
      } catch (err) {
        console.error("[Cardano SDK] InitMintingWallet failed:", err);
        window.myGameInstance.SendMessage(objectName, methodName, "ERROR: " + (err.message || err));
      }
    })();
  },

  // Callback receives "OK|<lovelace>" or "ERROR: ...". Used for the sponsorship balance check
  // before deciding who pays for a given mint.
  GetMintingWalletBalance: function (objectNamePtr, methodNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);

    (async function () {
      try {
        if (!window._mintingWallet) throw new Error("Minting wallet not initialized -- call InitMintingWallet first.");
        var lovelace = await window._mintingWallet.getLovelace();
        window.myGameInstance.SendMessage(objectName, methodName, "OK|" + lovelace);
      } catch (err) {
        window.myGameInstance.SendMessage(objectName, methodName, "ERROR: " + (err.message || err));
      }
    })();
  },

  // Mints `quantity` of `assetName` to `playerAddress`, with `metadataJson` as the CIP-25 payload.
  // `assetName` should already be fully resolved by the caller (C# owns any uniqueness-suffix
  // numbering -- this function just executes what it's told).
  //
  // sponsored = "1": minting wallet alone funds, signs, and submits. No player interaction.
  // sponsored = "0": the minting wallet signs to authorize the mint under its policy, AND the
  // currently-connected player wallet signs to authorize spending its own ADA for the fee.
  //
  // WARNING: the sponsored path is straightforward and used exactly as Mesh's own docs show it.
  // The player-pays path below is the least-verified part of this whole bridge -- it's written
  // against Mesh's documented signTx(unsignedTx, partialSign, returnFullTx) semantics (chaining
  // the growing signed CBOR through each signer in turn), but hasn't been exercised against a
  // real testnet transaction. Test THIS path first, in isolation, before trusting it. If it
  // doesn't work as written, Mesh's docs/Discord are the place to confirm the exact multi-signer
  // sequence for whatever version ends up installed -- BrowserWallet.addBrowserWitnesses (a
  // static helper for combining a separately-obtained witness set onto a tx) is the likely
  // alternate route if simple chaining doesn't hold up.
  MintToken: function (playerAddressPtr, assetNamePtr, metadataJsonPtr, quantityPtr, sponsoredPtr, objectNamePtr, methodNamePtr) {
    var playerAddress = UTF8ToString(playerAddressPtr);
    var assetName = UTF8ToString(assetNamePtr);
    var metadataJson = UTF8ToString(metadataJsonPtr);
    var quantity = UTF8ToString(quantityPtr);
    var sponsored = UTF8ToString(sponsoredPtr) === "1";
    var objectName = UTF8ToString(objectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);

    (async function () {
      try {
        if (!window._mintingWallet) throw new Error("Minting wallet not initialized -- call InitMintingWallet first.");

        var mintingWallet = window._mintingWallet;
        var forgeScript = window._mintingForgeScript;
        var metadata = JSON.parse(metadataJson);

        var mint = {
          assetName: assetName,
          assetQuantity: quantity,
          recipient: playerAddress,
          metadata: metadata,
          label: "721",
        };

        if (sponsored) {
          // Minting wallet is both the fee payer and the mint's signer -- single signer, no
          // player popup.
          var tx = new window.CardanoMintSDK.Transaction({ initiator: mintingWallet });
          tx.mintAsset(forgeScript, mint);
          var unsignedTx = await tx.build();
          var signedTx = await mintingWallet.signTx(unsignedTx);
          var txHash = await mintingWallet.submitTx(signedTx);
          window.myGameInstance.SendMessage(objectName, methodName, "OK|" + txHash);
          return;
        }

        // Player-pays path -- see the WARNING above.
        if (!window._selectedCardanoWalletKey) throw new Error("No connected player wallet to sign with.");

        var playerWallet = await window.CardanoMintSDK.BrowserWallet.enable(window._selectedCardanoWalletKey);

        var tx = new window.CardanoMintSDK.Transaction({ initiator: playerWallet });
        tx.mintAsset(forgeScript, mint);
        var unsignedTx = await tx.build();

        // Minting wallet adds its witness (authorizing the mint) first, then the player adds
        // theirs (authorizing their own inputs for the fee). partialSign=true on both calls;
        // returnFullTx defaults to true, so each call returns the growing tx with witnesses
        // accumulating, ready to hand to the next signer.
        var withMintingWitness = await mintingWallet.signTx(unsignedTx, true);
        var fullySignedTx = await playerWallet.signTx(withMintingWitness, true);

        var txHash = await playerWallet.submitTx(fullySignedTx);
        window.myGameInstance.SendMessage(objectName, methodName, "OK|" + txHash);

      } catch (err) {
        console.error("[Cardano SDK] MintToken failed:", err);
        window.myGameInstance.SendMessage(objectName, methodName, "ERROR: " + (err.message || err));
      }
    })();
  },

});
