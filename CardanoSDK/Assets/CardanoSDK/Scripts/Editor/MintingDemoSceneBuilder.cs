using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Prodiga.CardanoSDK.EditorTools
{
    /// <summary>
    /// Editor-only tool that builds the Minting Tokens demo scene end to end: the Blockfrost/Koios/
    /// CardanoWalletManager wallet-connect setup (reused if already present in the scene, same as
    /// GatedAccessDemoSceneBuilder does), a WalletStatusPanel showing connection status + balance, a
    /// MintStatusPanel containing the Mint button, status text, and a selectable/copyable tx-hash
    /// row, and a Minter GameObject with MintingController pre-filled with example token/policy
    /// values.
    ///
    /// Unlike the Gated Access demo, there's no 3D world here -- minting doesn't need a player
    /// standing anywhere in particular, so this is a UI-only scene. Safe to run on either an empty
    /// scene (builds everything from scratch) or one that already has a CardanoManager (reuses it
    /// and skips straight to the minting-specific pieces). Run it from
    /// Tools > Cardano SDK > Build Minting Demo Scene.
    ///
    /// If a MintingWalletSecret asset already exists at Assets/_CardanoSDK_LocalSecrets/MintingWalletSecret.asset
    /// it gets wired in automatically; otherwise the generated MintingController is left without one
    /// and a console warning points at Tools > Cardano SDK > Minting Wallet Setup. Same story for the
    /// copy-button icons -- wired in automatically if found at CopyIconPath/CheckmarkIconPath below,
    /// otherwise a warning tells you to assign them by hand.
    /// </summary>
    public static class MintingDemoSceneBuilder
    {
        private const string SecretAssetPath = "Assets/_CardanoSDK_LocalSecrets/MintingWalletSecret.asset";
        private const string CopyIconPath = "Assets/CardanoSDK/Icons/copy-icon.png";
        private const string CheckmarkIconPath = "Assets/CardanoSDK/Icons/checkmark-icon.png";

        [MenuItem("Tools/Cardano SDK/Build Minting Demo Scene")]
        public static void BuildScene()
        {
            if (GameObject.Find("Minter") != null)
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Minting Demo Scene Builder",
                    "This scene already has a \"Minter\" object. Running this again will create a duplicate. Continue anyway?",
                    "Continue",
                    "Cancel");
                if (!proceed) return;
            }

            Undo.SetCurrentGroupName("Build Minting Demo Scene");
            int undoGroup = Undo.GetCurrentGroup();

            BlockfrostIntegration blockfrost;
            KoiosIntegration koios;
            CardanoWalletManager walletManager;
            WalletDisplay walletDisplay;
            CreateCardanoManagers(out blockfrost, out koios, out walletManager, out walletDisplay);

            Canvas canvas = FindOrCreateCanvas();

            // Wallet connection happens via the HTML page's own Connect Wallet button (see the
            // WebGL templates), never an in-Canvas Unity button -- so there's nothing to create
            // here beyond the status display.
            EnsureWalletStatusPanel(canvas, walletManager, walletDisplay, blockfrost);

            MintingController minter = CreateMinter(blockfrost, koios);
            CreateMintUI(canvas, minter);

            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = minter.gameObject;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[Cardano SDK] Minting demo scene built. Remaining manual steps: set BlockfrostManager's " +
                "Project ID, and run Tools > Cardano SDK > Minting Wallet Setup if MintingController's Wallet " +
                "Secret is empty.");
        }

        // ---------------------------------------------------------------- Cardano wallet setup

        private static void CreateCardanoManagers(out BlockfrostIntegration blockfrost, out KoiosIntegration koios, out CardanoWalletManager walletManager, out WalletDisplay walletDisplay)
        {
            bool cardanoManagerExisted = GameObject.Find("CardanoManager") != null;

            GameObject blockfrostObj = GameObject.Find("BlockfrostManager");
            if (blockfrostObj != null)
            {
                blockfrost = blockfrostObj.GetComponent<BlockfrostIntegration>();
            }
            else
            {
                blockfrostObj = new GameObject("BlockfrostManager");
                blockfrost = blockfrostObj.AddComponent<BlockfrostIntegration>();
                Undo.RegisterCreatedObjectUndo(blockfrostObj, "Create BlockfrostManager");
            }

            GameObject koiosObj = GameObject.Find("KoiosManager");
            if (koiosObj != null)
            {
                koios = koiosObj.GetComponent<KoiosIntegration>();
            }
            else
            {
                koiosObj = new GameObject("KoiosManager");
                koios = koiosObj.AddComponent<KoiosIntegration>();
                Undo.RegisterCreatedObjectUndo(koiosObj, "Create KoiosManager");
            }

            GameObject cardanoManagerObj = GameObject.Find("CardanoManager");
            if (cardanoManagerObj != null)
            {
                walletManager = cardanoManagerObj.GetComponent<CardanoWalletManager>();
            }
            else
            {
                cardanoManagerObj = new GameObject("CardanoManager");
                walletManager = cardanoManagerObj.AddComponent<CardanoWalletManager>();
                Undo.RegisterCreatedObjectUndo(cardanoManagerObj, "Create CardanoManager");
            }

            if (walletManager.blockfrostProvider == null) walletManager.blockfrostProvider = blockfrost;
            if (walletManager.koiosProvider == null) walletManager.koiosProvider = koios;

            walletDisplay = cardanoManagerObj.GetComponent<WalletDisplay>();
            if (walletDisplay == null) walletDisplay = cardanoManagerObj.AddComponent<WalletDisplay>();
            if (walletDisplay.BlockfrostManager == null) walletDisplay.BlockfrostManager = blockfrost;
            if (walletDisplay.KoiosManager == null) walletDisplay.KoiosManager = koios;

            Debug.Log(cardanoManagerExisted
                ? "[Cardano SDK] Found an existing CardanoManager -- reused it (and its Blockfrost/Koios providers) instead of creating new ones."
                : "[Cardano SDK] No existing CardanoManager found -- created BlockfrostManager/KoiosManager/CardanoManager from scratch.");
        }

        private static Canvas FindOrCreateCanvas()
        {
            Canvas existing = Object.FindObjectOfType<Canvas>();
            if (existing != null) return existing;

            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Canvas");

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(eventSystemObj, "Create EventSystem");
            }

            return canvas;
        }

        // ---------------------------------------------------------------- Wallet status UI

        // Finds an existing WalletStatusPanel by name and re-wires it if present, or creates one
        // from scratch -- either way, this guarantees a status panel showing connection state and
        // balance exists and is correctly wired to walletManager/walletDisplay after this runs, no
        // matter what state the scene was in beforehand.
        private static void EnsureWalletStatusPanel(Canvas canvas, CardanoWalletManager walletManager, WalletDisplay walletDisplay, BlockfrostIntegration blockfrost)
        {
            GameObject panelObj = GameObject.Find("WalletStatusPanel");
            TextMeshProUGUI connectionStatusText;
            TextMeshProUGUI balanceText;

            if (panelObj != null)
            {
                connectionStatusText = panelObj.transform.Find("Text_ConnectionStatus")?.GetComponent<TextMeshProUGUI>();
                balanceText = panelObj.transform.Find("Text_WalletBalance")?.GetComponent<TextMeshProUGUI>();

                if (connectionStatusText == null || balanceText == null)
                {
                    Debug.LogWarning("[Cardano SDK] Found an existing WalletStatusPanel but it's missing Text_ConnectionStatus and/or Text_WalletBalance -- leaving it as-is rather than guessing. Delete it and re-run if you want it rebuilt.");
                    return;
                }
            }
            else
            {
                panelObj = new GameObject("WalletStatusPanel", typeof(RectTransform));
                panelObj.transform.SetParent(canvas.transform, false);
                RectTransform panelRect = panelObj.GetComponent<RectTransform>();
                panelRect.anchorMin = new Vector2(1f, 1f);
                panelRect.anchorMax = new Vector2(1f, 1f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = new Vector2(-200f, -75f);
                panelRect.sizeDelta = new Vector2(400f, 150f);
                panelObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
                Undo.RegisterCreatedObjectUndo(panelObj, "Create WalletStatusPanel");

                connectionStatusText = CreateAnchoredText(panelObj.transform, "Text_ConnectionStatus", "Waiting for Wallet...",
                    new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(360f, 60f), 32);

                balanceText = CreateAnchoredText(panelObj.transform, "Text_WalletBalance", "",
                    new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(360f, 40f), 32);
            }

            walletManager.statusText = connectionStatusText;
            walletDisplay.BlockfrostManager = blockfrost;
            walletDisplay.BalanceText = balanceText;

            if (string.IsNullOrEmpty(blockfrost.ProjectId))
            {
                Debug.LogWarning("[Cardano SDK] BlockfrostManager's Project ID is empty -- set it in the Inspector before testing.");
            }
        }

        // ---------------------------------------------------------------- Minter

        private static MintingController CreateMinter(BlockfrostIntegration blockfrost, KoiosIntegration koios)
        {
            GameObject minterObj = new GameObject("Minter");
            MintingController minter = minterObj.AddComponent<MintingController>();
            minter.BlockfrostManager = blockfrost;
            minter.KoiosManager = koios;

            minter.Token = new MintableTokenConfig
            {
                TokenType = MintableTokenType.NFT,
                AssetName = "AchievementBadge",
                EnforceUniqueness = false,
                Quantity = 1,
                DisplayName = "Achievement Badge",
                ImageUri = "ipfs://REPLACE_WITH_YOUR_IMAGE_CID",
                ImageMediaType = "image/png",
                Description = "Awarded for completing the tutorial quest.",
                Attributes = new[] { new NativeAssetMetadataAttribute { Key = "Rarity", Value = "Common" } },
            };

            minter.Policy = new MintPolicyConfig { ExpirySlot = 0 };

            minter.Sponsorship = new MintSponsorshipConfig
            {
                AllowSponsorship = false,
                MinimumRemainingLovelace = 5_000_000,
                Conditions = new[]
                {
                    new ConditionGroup
                    {
                        Conditions = new[]
                        {
                            new AssetCondition
                            {
                                PolicyId = "",
                                AssetNameHex = "",
                                ConditionType = AssetConditionType.AssetPresent,
                                Invert = true, // example: "player does NOT already own the asset"
                            }
                        }
                    }
                }
            };

            var secret = AssetDatabase.LoadAssetAtPath<MintingWalletSecret>(SecretAssetPath);
            if (secret != null)
            {
                minter.WalletSecret = secret;
            }
            else
            {
                Debug.LogWarning("[Cardano SDK] No MintingWalletSecret found at " + SecretAssetPath +
                    " -- run Tools > Cardano SDK > Minting Wallet Setup, then assign it on the Minter's MintingController.");
            }

            Undo.RegisterCreatedObjectUndo(minterObj, "Create Minter");
            return minter;
        }

        // ---------------------------------------------------------------- Mint UI

        private static void CreateMintUI(Canvas canvas, MintingController minter)
        {
            // Background panel -- everything else in this section is parented under it, which both
            // gives it the "appears behind the button/text" layering for free (children always
            // render above their own parent) and keeps the whole mint UI as one movable group.
            GameObject panelObj = new GameObject("MintStatusPanel", typeof(RectTransform));
            panelObj.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(600f, 300f);
            panelObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            // Mint button
            GameObject buttonObj = new GameObject("MintButton", typeof(RectTransform));
            buttonObj.transform.SetParent(panelObj.transform, false);
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -30f);
            buttonRect.sizeDelta = new Vector2(200f, 60f);
            buttonObj.AddComponent<Image>().color = new Color(0.15f, 0.55f, 0.2f);
            Button button = buttonObj.AddComponent<Button>();

            GameObject buttonLabelObj = new GameObject("Label", typeof(RectTransform));
            buttonLabelObj.transform.SetParent(buttonObj.transform, false);
            StretchFull(buttonLabelObj.GetComponent<RectTransform>());
            TextMeshProUGUI buttonLabel = buttonLabelObj.AddComponent<TextMeshProUGUI>();
            buttonLabel.text = "Mint";
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.color = Color.white;
            buttonLabel.fontSize = 24;

            // Status text
            TextMeshProUGUI statusText = CreateAnchoredText(panelObj.transform, "MintStatusText", "",
                new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(560f, 30f), 16);

            // Tx hash row -- label + read-only selectable field + copy button + explorer link,
            // hidden until a mint succeeds. The label and link are children of rowObj positioned
            // just outside its own bounds (above/below) rather than inside them -- Unity doesn't
            // clip children to a parent's sizeDelta unless there's a Mask on it, so this is fine,
            // and it means rowObj.SetActive() alone still shows/hides all four pieces together.
            GameObject rowObj = new GameObject("TxHashRow", typeof(RectTransform));
            rowObj.transform.SetParent(panelObj.transform, false);
            RectTransform rowRect = rowObj.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -160f);
            rowRect.sizeDelta = new Vector2(560f, 40f);

            GameObject labelObj = new GameObject("TxHashLabel", typeof(RectTransform));
            labelObj.transform.SetParent(rowObj.transform, false);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f); // grows upward from the row's top edge
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            labelRect.sizeDelta = new Vector2(560f, 20f);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "Transaction Hash:";
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 14;
            labelText.color = new Color(1f, 1f, 1f, 0.75f);

            TMP_InputField txHashField = CreateReadOnlyInputField(rowObj.transform);
            CreateCopyButton(rowObj.transform, txHashField);

            GameObject linkObj = new GameObject("ExplorerLink", typeof(RectTransform));
            linkObj.transform.SetParent(rowObj.transform, false);
            RectTransform linkRect = linkObj.GetComponent<RectTransform>();
            linkRect.anchorMin = new Vector2(0.5f, 0f);
            linkRect.anchorMax = new Vector2(0.5f, 0f);
            linkRect.pivot = new Vector2(0.5f, 1f); // grows downward from the row's bottom edge
            linkRect.anchoredPosition = new Vector2(0f, -4f);
            linkRect.sizeDelta = new Vector2(560f, 25f);
            TextMeshProUGUI linkText = linkObj.AddComponent<TextMeshProUGUI>();
            linkText.text = "<u>View on Cardanoscan \u2197</u>";
            linkText.alignment = TextAlignmentOptions.Center;
            linkText.fontSize = 15;
            linkText.color = new Color(0.45f, 0.7f, 1f);
            Button linkButton = linkObj.AddComponent<Button>();
            linkButton.targetGraphic = linkText;
            UnityEventTools.AddPersistentListener(linkButton.onClick, minter.OpenTransactionOnExplorer);

            rowObj.SetActive(false);

            minter.StatusText = statusText;
            minter.TxHashField = txHashField;
            minter.TxHashRow = rowObj;
            UnityEventTools.AddPersistentListener(button.onClick, minter.TriggerMint);
        }

        private static TMP_InputField CreateReadOnlyInputField(Transform parent)
        {
            GameObject fieldObj = new GameObject("TxHashField", typeof(RectTransform));
            fieldObj.transform.SetParent(parent, false);
            RectTransform fieldRect = fieldObj.GetComponent<RectTransform>();
            fieldRect.anchorMin = new Vector2(0f, 0f);
            fieldRect.anchorMax = new Vector2(1f, 1f);
            fieldRect.offsetMin = new Vector2(0f, 0f);
            fieldRect.offsetMax = new Vector2(-44f, 0f); // leaves room for the copy button on the right
            fieldObj.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            GameObject viewportObj = new GameObject("Text Area", typeof(RectTransform));
            viewportObj.transform.SetParent(fieldObj.transform, false);
            RectTransform viewportRect = viewportObj.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(10f, 6f);
            viewportRect.offsetMax = new Vector2(-10f, -6f);
            viewportObj.AddComponent<RectMask2D>();

            GameObject textObj = new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(viewportObj.transform, false);
            StretchFull(textObj.GetComponent<RectTransform>());
            TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 16;
            textComp.alignment = TextAlignmentOptions.MidlineLeft;
            textComp.color = Color.white;
            textComp.enableWordWrapping = false;
            textComp.overflowMode = TextOverflowModes.Truncate;

            GameObject placeholderObj = new GameObject("Placeholder", typeof(RectTransform));
            placeholderObj.transform.SetParent(viewportObj.transform, false);
            StretchFull(placeholderObj.GetComponent<RectTransform>());
            TextMeshProUGUI placeholderComp = placeholderObj.AddComponent<TextMeshProUGUI>();
            placeholderComp.text = "Tx hash will appear here";
            placeholderComp.fontSize = 16;
            placeholderComp.fontStyle = FontStyles.Italic;
            placeholderComp.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderComp.color = new Color(1f, 1f, 1f, 0.4f);

            TMP_InputField inputField = fieldObj.AddComponent<TMP_InputField>();
            inputField.textViewport = viewportRect;
            inputField.textComponent = textComp;
            inputField.placeholder = placeholderComp;
            inputField.readOnly = true;
            inputField.lineType = TMP_InputField.LineType.SingleLine;

            return inputField;
        }

        private static void CreateCopyButton(Transform parent, TMP_InputField sourceField)
        {
            GameObject buttonObj = new GameObject("CopyButton", typeof(RectTransform));
            buttonObj.transform.SetParent(parent, false);
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.pivot = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(36f, 36f);

            Image iconImage = buttonObj.AddComponent<Image>();
            iconImage.preserveAspect = true;
            buttonObj.AddComponent<Button>();
            CopyToClipboardButton copyButton = buttonObj.AddComponent<CopyToClipboardButton>();
            copyButton.TextSource = sourceField.textComponent;
            copyButton.IconImage = iconImage;

            Sprite copyIcon = AssetDatabase.LoadAssetAtPath<Sprite>(CopyIconPath);
            Sprite checkmarkIcon = AssetDatabase.LoadAssetAtPath<Sprite>(CheckmarkIconPath);
            copyButton.CopyIcon = copyIcon;
            copyButton.CheckmarkIcon = checkmarkIcon;

            if (copyIcon == null || checkmarkIcon == null)
            {
                Debug.LogWarning("[Cardano SDK] Copy/checkmark icon not found at " + CopyIconPath + " / " + CheckmarkIconPath +
                    " -- assign CopyButton's CopyToClipboardButton icons manually, and make sure both are imported as Sprite (2D and UI).");
            }
        }

        // ---------------------------------------------------------------- Small shared helpers

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateAnchoredText(Transform parent, string name, string text, Vector2 anchorTopOrBottomCenter, Vector2 anchoredPosition, Vector2 sizeDelta, int fontSize)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorTopOrBottomCenter;
            rect.anchorMax = anchorTopOrBottomCenter;
            rect.pivot = anchorTopOrBottomCenter;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = fontSize;
            return tmp;
        }
    }
}
