mergeInto(LibraryManager.library, {

  IsCardanoAvailable: function () {
    if (window.cardano) {
      return 1;
    }
    return 0;
  },

  SetWalletUI: function(btnTextPtr, statusTextPtr) {
      var btnText = UTF8ToString(btnTextPtr);
      var statusText = UTF8ToString(statusTextPtr);

      // Stop account polling when the wallet is disconnected
      if (btnText === "Connect Wallet" && window._cardanoAccountPollInterval) {
          clearInterval(window._cardanoAccountPollInterval);
          window._cardanoAccountPollInterval = null;
      }

      var btn = document.getElementById("connect-btn") || document.getElementById("btn-connect");
      if (btn) btn.innerText = btnText;

      var stat = document.getElementById("wallet-status");
      if (stat) stat.innerText = "Status: " + statusText;
  },

  ConnectWallet: async function (objectNamePtr, methodNamePtr) {
    var objectName = UTF8ToString(objectNamePtr);
    var methodName = UTF8ToString(methodNamePtr);

    function updateUI(btnText, statusMsg) {
        var btn = document.getElementById("connect-btn") || document.getElementById("btn-connect");
        if (btn) btn.innerText = btnText;
        var stat = document.getElementById("wallet-status");
        if (stat) stat.innerText = "Status: " + statusMsg;
    }

    // Clear any existing poll before starting a new connection
    if (window._cardanoAccountPollInterval) {
        clearInterval(window._cardanoAccountPollInterval);
        window._cardanoAccountPollInterval = null;
    }

    if (!window.cardano) {
      updateUI("Install Wallet", "No Wallet Installed");
      window.myGameInstance.SendMessage(objectName, methodName, "ERROR: No Wallet Installed");
      return;
    }

    var supportedWallets = ['nami', 'eternl', 'flint', 'typhoncip30', 'yoroi', 'gerowallet'];
    var selectedWallet = null;

    for (var i = 0; i < supportedWallets.length; i++) {
        var key = supportedWallets[i];
        if (window.cardano[key]) {
            selectedWallet = key;
            break;
        }
    }

    if (!selectedWallet) {
        updateUI("No Wallet Found", "No Supported Wallet Found");
        window.myGameInstance.SendMessage(objectName, methodName, "ERROR: No Supported Wallet Found");
        return;
    }

    // Shared helper: fetches the current account data string from an enabled API
    async function getWalletData(api) {
        var networkId = await api.getNetworkId();
        var rawAddresses = await api.getUsedAddresses();
        if (rawAddresses.length === 0) {
            rawAddresses = await api.getUnusedAddresses();
        }
        var rewardAddresses = await api.getRewardAddresses();
        var rewardHex = rewardAddresses.length > 0 ? rewardAddresses[0] : "";
        return rawAddresses[0] + "|" + networkId + "|" + rewardHex;
    }

    async function tryConnect(walletKey) {
        var api = await window.cardano[walletKey].enable();
        var data = await getWalletData(api);
        return { api: api, data: data };
    }

    // Polls every 2 seconds.
    // On any error, tries re-enabling the wallet before concluding it's a disconnect.
    // If re-enable succeeds → account just changed, send new data silently.
    // If re-enable also fails → true disconnect, send ERROR and stop polling.
    function startPolling(initialApi) {
        var currentApi = initialApi;

        window._cardanoAccountPollInterval = setInterval(async function() {
            try {
                var newData = await getWalletData(currentApi);
                if (newData !== window._cardanoCurrentWalletData) {
                    window._cardanoCurrentWalletData = newData;
                    updateUI("Disconnect", "Account Updated");
                    window.myGameInstance.SendMessage(objectName, methodName, newData);
                }
            } catch (err) {
                // Poll failed — re-enable the wallet to get a fresh session.
                try {
                    var reconnected = await tryConnect(selectedWallet);
                    currentApi = reconnected.api;
                    window._cardanoCurrentWalletData = reconnected.data;
                    updateUI("Disconnect", "Account Updated");
                    window.myGameInstance.SendMessage(objectName, methodName, reconnected.data);
                } catch (err2) {
                    // Re-enable also failed: wallet is locked or truly disconnected.
                    clearInterval(window._cardanoAccountPollInterval);
                    window._cardanoAccountPollInterval = null;
                    updateUI("Connect Wallet", "Wallet Disconnected");
                    window.myGameInstance.SendMessage(objectName, methodName, "ERROR: Wallet Disconnected");
                }
            }
        }, 2000);
    }

    try {
        var connected = await tryConnect(selectedWallet);
        window._cardanoCurrentWalletData = connected.data;

        updateUI("Disconnect", "Connected");
        window.myGameInstance.SendMessage(objectName, methodName, connected.data);
        startPolling(connected.api);

    } catch (err) {
        updateUI("Connection Failed", "Connection Failed");
        window.myGameInstance.SendMessage(objectName, methodName, "ERROR: " + (err.message || err));
    }
  },
});