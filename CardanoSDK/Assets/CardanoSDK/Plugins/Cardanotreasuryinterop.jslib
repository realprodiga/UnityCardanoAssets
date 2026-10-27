mergeInto(LibraryManager.library, {

  // Call once (AwardingController does this in OnEnable) before GetTreasuryWalletBalance/AwardToken
  // can be used. Requires cardano-mint-sdk.bundle.js (window.CardanoMintSDK) to already be loaded --
  // same bundle the minting bridge (Cardanomintinterop.jslib) uses, no second copy of the library
  // needed.
  //
  // Note: if the treasury wallet's mnemonic happens to be the SAME as the minting wallet's, this
  // still initializes its own independent wallet instance under window._treasuryWallet rather
  // than reusing window._mintingWallet -- functionally identical (same on-chain address, same
  // key), just a second in-memory MeshWallet object and a second init round-trip. Simplification,
  // not a bug: sharing one cached instance across both bridges would need a lookup keyed by
  // mnemonic hash, which wasn't worth the complexity for tonight.
  InitTreasuryWallet: function (mnemonicPtr, networkPtr, blockfrostProjectIdPtr, objectNamePtr, methodNamePtr) {
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

        var treasuryAddress = await wallet.getChangeAddress();

        window._treasuryWallet = wallet;
        window._treasuryProvider = provider;

        window.myGameInstance.SendMessage(objectName, methodName, "OK|" + treasuryAddress);
      } catch (err) {
        console.error("[Cardano SDK] InitTreasuryWallet failed:", err);
        window.myGameInstance.SendMessage(objectName, methodName, "ERROR: " + (err.message || err));
      }
    })();
  },

  // Callback receives "OK|<lovelace>" or "ERROR: ...". Used for the sponsorship balance check
  // before deciding who pays the transfer fee.
  GetTreasuryWalletBalance: function (objectNamePtr, methodNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);

    (async function () {
      try {
        if (!window._treasuryWallet) throw new Error("Treasury wallet not initialized -- call InitTreasuryWallet first.");
        var lovelace = await window._treasuryWallet.getLovelace();
        window.myGameInstance.SendMessage(objectName, methodName, "OK|" + lovelace);
      } catch (err) {
        window.myGameInstance.SendMessage(objectName, methodName, "ERROR: " + (err.message || err));
      }
    })();
  },

  // Sends `quantity` of `unit` (Blockfrost/Mesh-style concatenated policyId+assetNameHex) from
  // the treasury wallet to `playerAddress`.
  //
  // sponsored = "1": treasury wallet alone funds, signs, and submits. No player interaction --
  // the reliable, well-trodden path: single signer, and Mesh handles change automatically since
  // every input in the tx belongs to the treasury wallet.
  //
  // sponsored = "0": the treasury wallet still has to sign (it's spending its own UTXO to
  // release the token), but the PLAYER pays the fee via their own connected wallet.
  //
  // WARNING: the player-pays path below is, like MintToken's equivalent, the least-verified part
  // of this bridge -- written against Mesh's documented Transaction.setTxInputs() pattern (see
  // MeshJS's own multisig-minting demo, which uses the same "explicit foreign UTXO + two
  // signers" shape) but not exercised against a real testnet transaction. The specific risk worth
  // testing FIRST, in isolation: the treasury's token UTXO is a bundle -- it likely carries some
  // ADA and possibly OTHER assets alongside the awarded token, not just the token in isolation.
  // This code explicitly computes everything from that UTXO other than the awarded quantity and
  // routes it back to the treasury's own address (see treasuryChangeAssets below), specifically
  // so the treasury doesn't inadvertently leak its own ADA/other assets into the player's change
  // output. If Mesh's automatic change handling doesn't behave the way this assumes, that's the
  // first thing to check against a real preprod transaction. Also worth noting: this spends the
  // treasury's UTXO whole, so if it holds far more of `unit` than one award's worth, consider
  // periodically consolidating treasury UTXOs so each award doesn't needlessly touch a huge
  // bundle.
  AwardToken: function (playerAddressPtr, unitPtr, quantityPtr, sponsoredPtr, objectNamePtr, methodNamePtr) {
    var playerAddress = UTF8ToString(playerAddressPtr);
    var unit = UTF8ToString(unitPtr);
    var quantity = UTF8ToString(quantityPtr);
    var sponsored = UTF8ToString(sponsoredPtr) === "1";
    var objectName = UTF8ToString(objectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);

    (async function () {
      try {
        if (!window._treasuryWallet) throw new Error("Treasury wallet not initialized -- call InitTreasuryWallet first.");
        var treasuryWallet = window._treasuryWallet;

        if (sponsored) {
          // Treasury is both the fee payer and the sender -- single signer, no player popup.
          var tx = new window.CardanoMintSDK.Transaction({ initiator: treasuryWallet });
          tx.sendAssets(playerAddress, [{ unit: unit, quantity: quantity }]);
          var unsignedTx = await tx.build();
          var signedTx = await treasuryWallet.signTx(unsignedTx);
          var txHash = await treasuryWallet.submitTx(signedTx);
          window.myGameInstance.SendMessage(objectName, methodName, "OK|" + txHash);
          return;
        }

        // Player-pays path -- see the WARNING above.
        if (!window._selectedCardanoWalletKey) throw new Error("No connected player wallet to sign with.");

        var playerWallet = await window.CardanoMintSDK.BrowserWallet.enable(window._selectedCardanoWalletKey);
        var treasuryAddress = await treasuryWallet.getChangeAddress();

        // Find the specific treasury UTXO holding the token being awarded. Whatever else is
        // bundled into that same UTXO (lovelace, other assets) gets explicitly routed back to
        // the treasury below -- see treasuryChangeAssets.
        var treasuryUtxos = await treasuryWallet.getUtxos();
        var tokenUtxo = treasuryUtxos.find(function (u) {
          return u.output.amount.some(function (a) { return a.unit === unit; });
        });
        if (!tokenUtxo) throw new Error("Treasury wallet does not currently hold unit " + unit);

        var awardedAmount = tokenUtxo.output.amount.find(function (a) { return a.unit === unit; });
        if (BigInt(awardedAmount.quantity) < BigInt(quantity)) {
          throw new Error("Treasury UTXO holds less than the requested award quantity for unit " + unit);
        }

        // Everything else that UTXO carries (lovelace, other bundled assets, and any of `unit`
        // beyond what's being awarded) has to come back to the treasury explicitly -- otherwise
        // it would fall into the tx's single changeAddress (set to the player below) and the
        // treasury would lose it.
        var treasuryChangeAssets = tokenUtxo.output.amount
          .map(function (a) {
            if (a.unit !== unit) return { unit: a.unit, quantity: a.quantity };
            var remaining = (BigInt(a.quantity) - BigInt(quantity)).toString();
            return remaining !== "0" ? { unit: a.unit, quantity: remaining } : null;
          })
          .filter(function (a) { return a !== null; });

        var tx = new window.CardanoMintSDK.Transaction({ initiator: playerWallet });
        tx.setTxInputs([tokenUtxo]); // the treasury's contributed input, on top of whatever the player's own wallet auto-selects for the fee
        tx.sendAssets(playerAddress, [{ unit: unit, quantity: quantity }]);
        if (treasuryChangeAssets.length > 0) {
          tx.sendAssets(treasuryAddress, treasuryChangeAssets);
        }
        tx.setChangeAddress(playerAddress); // the player's own leftover fee-paying ADA
        var unsignedTx = await tx.build();

        // Treasury signs first to authorize spending its UTXO, then the player signs to
        // authorize their own fee-paying inputs -- same signer ordering MintToken uses for its
        // policy-signer-then-fee-payer pattern.
        var withTreasuryWitness = await treasuryWallet.signTx(unsignedTx, true);
        var fullySignedTx = await playerWallet.signTx(withTreasuryWitness, true);
        var txHash = await playerWallet.submitTx(fullySignedTx);
        window.myGameInstance.SendMessage(objectName, methodName, "OK|" + txHash);

      } catch (err) {
        console.error("[Cardano SDK] AwardToken failed:", err);
        window.myGameInstance.SendMessage(objectName, methodName, "ERROR: " + (err.message || err));
      }
    })();
  },

});
