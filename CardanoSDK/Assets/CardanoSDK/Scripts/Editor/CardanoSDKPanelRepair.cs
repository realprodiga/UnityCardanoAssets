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
    /// Tools > Cardano SDK > Repair > ...
    ///
    /// Targeted rebuild for ONE UI panel at a time (WalletStatusPanel / MintStatusPanel /
    /// AwardStatusPanel), without touching CardanoManager/Minter/Treasury/GatedGate or anything else
    /// already in the scene. Exists specifically for recovering a scene where one of these panels
    /// ended up as an orphaned "Missing Prefab" reference -- e.g. after the prefab asset it was
    /// connected to got deleted before the panel itself was unpacked in that particular scene.
    /// Symptom: build errors mentioning "Missing Prefab Asset" for one of these three names,
    /// sometimes alongside a RectTransform/TextMeshProUGUI.Awake() crash caused by Unity fabricating
    /// a placeholder for the orphaned instance.
    ///
    /// Each command destroys whatever currently has that name under the scene's Canvas (broken or
    /// not -- safe either way, since this rebuilds it from scratch), then reconstructs it fresh and
    /// re-wires it to whatever CardanoManager/Minter/Treasury already exists in the scene. Does NOT
    /// create or touch CardanoManager/Minter/Treasury/GatedGate themselves -- if the object a panel
    /// needs to wire to isn't present, the relevant command logs a warning and stops rather than
    /// creating a duplicate of something that isn't actually broken.
    ///
    /// After running one of these, re-run Tools > Cardano SDK > Extract Demo Prefabs in that scene
    /// if you want the rebuilt panel captured as a prefab again (it starts as a plain GameObject,
    /// same as right after any of the three *DemoSceneBuilder tools first create one).
    /// </summary>
    public static class CardanoSDKPanelRepair
    {
        [MenuItem("Tools/Cardano SDK/Repair/Rebuild Wallet Status Panel")]
        public static void RebuildWalletStatusPanel()
        {
            GameObject cardanoManagerObj = GameObject.Find("CardanoManager");
            if (cardanoManagerObj == null)
            {
                Debug.LogWarning("[Cardano SDK] No CardanoManager found in the open scene -- nothing to wire the panel to. Aborting.");
                return;
            }
            CardanoWalletManager walletManager = cardanoManagerObj.GetComponent<CardanoWalletManager>();
            WalletDisplay walletDisplay = cardanoManagerObj.GetComponent<WalletDisplay>();
            BlockfrostIntegration blockfrost = walletDisplay != null ? walletDisplay.BlockfrostManager : null;
            if (blockfrost == null) blockfrost = cardanoManagerObj.GetComponentInChildren<BlockfrostIntegration>();

            if (walletManager == null || walletDisplay == null || blockfrost == null)
            {
                Debug.LogWarning("[Cardano SDK] CardanoManager is missing CardanoWalletManager/WalletDisplay/a BlockfrostIntegration -- can't wire the panel. Aborting.");
                return;
            }

            Canvas canvas = FindOrCreateCanvas();
            DestroyExisting(canvas, "WalletStatusPanel");

            GameObject panelObj = new GameObject("WalletStatusPanel", typeof(RectTransform));
            panelObj.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-200f, -75f);
            panelRect.sizeDelta = new Vector2(400f, 150f);
            panelObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            Undo.RegisterCreatedObjectUndo(panelObj, "Rebuild WalletStatusPanel");

            TextMeshProUGUI connectionStatusText = CreateAnchoredText(panelObj.transform, "Text_ConnectionStatus", "Waiting for Wallet...",
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(360f, 60f), 32);
            TextMeshProUGUI balanceText = CreateAnchoredText(panelObj.transform, "Text_WalletBalance", "",
                new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(360f, 40f), 32);

            walletManager.statusText = connectionStatusText;
            walletDisplay.BlockfrostManager = blockfrost;
            walletDisplay.BalanceText = balanceText;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Cardano SDK] WalletStatusPanel rebuilt and re-wired.");
        }

        [MenuItem("Tools/Cardano SDK/Repair/Rebuild Mint Status Panel")]
        public static void RebuildMintStatusPanel()
        {
            GameObject minterObj = GameObject.Find("Minter");
            if (minterObj == null)
            {
                Debug.LogWarning("[Cardano SDK] No Minter found in the open scene -- nothing to wire the panel to. Aborting.");
                return;
            }
            MintingController minter = minterObj.GetComponent<MintingController>();
            if (minter == null)
            {
                Debug.LogWarning("[Cardano SDK] \"Minter\" has no MintingController component. Aborting.");
                return;
            }

            Canvas canvas = FindOrCreateCanvas();
            DestroyExisting(canvas, "MintStatusPanel");

            BuildActionPanel(canvas, "MintStatusPanel", "MintButton", "Mint", new Color(0.15f, 0.55f, 0.2f),
                out TextMeshProUGUI statusText, out TMP_InputField txHashField, out GameObject txHashRow, out Button actionButton);

            minter.StatusText = statusText;
            minter.TxHashField = txHashField;
            minter.TxHashRow = txHashRow;
            UnityEventTools.AddPersistentListener(actionButton.onClick, minter.TriggerMint);

            Button linkButton = txHashRow.transform.Find("ExplorerLink").GetComponent<Button>();
            UnityEventTools.AddPersistentListener(linkButton.onClick, minter.OpenTransactionOnExplorer);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Cardano SDK] MintStatusPanel rebuilt and re-wired.");
        }

        [MenuItem("Tools/Cardano SDK/Repair/Rebuild Award Status Panel")]
        public static void RebuildAwardStatusPanel()
        {
            GameObject treasuryObj = GameObject.Find("Treasury");
            if (treasuryObj == null)
            {
                Debug.LogWarning("[Cardano SDK] No Treasury found in the open scene -- nothing to wire the panel to. Aborting.");
                return;
            }
            AwardingController treasury = treasuryObj.GetComponent<AwardingController>();
            if (treasury == null)
            {
                Debug.LogWarning("[Cardano SDK] \"Treasury\" has no AwardingController component. Aborting.");
                return;
            }

            Canvas canvas = FindOrCreateCanvas();
            DestroyExisting(canvas, "AwardStatusPanel");

            BuildActionPanel(canvas, "AwardStatusPanel", "AwardButton", "Award", new Color(0.6f, 0.45f, 0.1f),
                out TextMeshProUGUI statusText, out TMP_InputField txHashField, out GameObject txHashRow, out Button actionButton);

            treasury.StatusText = statusText;
            treasury.TxHashField = txHashField;
            treasury.TxHashRow = txHashRow;
            UnityEventTools.AddPersistentListener(actionButton.onClick, treasury.TriggerAward);

            Button linkButton = txHashRow.transform.Find("ExplorerLink").GetComponent<Button>();
            UnityEventTools.AddPersistentListener(linkButton.onClick, treasury.OpenTransactionOnExplorer);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Cardano SDK] AwardStatusPanel rebuilt and re-wired.");
        }

        // ---------------------------------------------------------------- Shared building blocks
        // (Same layout as MintingDemoSceneBuilder.CreateMintUI / AwardingDemoSceneBuilder.CreateAwardUI
        // -- factored into one parameterized method here since this tool rebuilds either on demand.)

        private static void DestroyExisting(Canvas canvas, string name)
        {
            Transform existing = canvas.transform.Find(name);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }
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

        private static void BuildActionPanel(Canvas canvas, string panelName, string buttonName, string buttonLabelText, Color buttonColor,
            out TextMeshProUGUI statusText, out TMP_InputField txHashField, out GameObject txHashRow, out Button actionButton)
        {
            GameObject panelObj = new GameObject(panelName, typeof(RectTransform));
            panelObj.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panelObj.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(600f, 300f);
            panelObj.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            Undo.RegisterCreatedObjectUndo(panelObj, "Rebuild " + panelName);

            GameObject buttonObj = new GameObject(buttonName, typeof(RectTransform));
            buttonObj.transform.SetParent(panelObj.transform, false);
            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.anchoredPosition = new Vector2(0f, -30f);
            buttonRect.sizeDelta = new Vector2(200f, 60f);
            buttonObj.AddComponent<Image>().color = buttonColor;
            actionButton = buttonObj.AddComponent<Button>();

            GameObject buttonLabelObj = new GameObject("Label", typeof(RectTransform));
            buttonLabelObj.transform.SetParent(buttonObj.transform, false);
            StretchFull(buttonLabelObj.GetComponent<RectTransform>());
            TextMeshProUGUI buttonLabel = buttonLabelObj.AddComponent<TextMeshProUGUI>();
            buttonLabel.text = buttonLabelText;
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.color = Color.white;
            buttonLabel.fontSize = 24;

            statusText = CreateAnchoredText(panelObj.transform, panelName + "Text", "",
                new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(560f, 30f), 16);

            txHashRow = new GameObject("TxHashRow", typeof(RectTransform));
            txHashRow.transform.SetParent(panelObj.transform, false);
            RectTransform rowRect = txHashRow.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchoredPosition = new Vector2(0f, -160f);
            rowRect.sizeDelta = new Vector2(560f, 40f);

            GameObject labelObj = new GameObject("TxHashLabel", typeof(RectTransform));
            labelObj.transform.SetParent(txHashRow.transform, false);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 1f);
            labelRect.anchorMax = new Vector2(0.5f, 1f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            labelRect.sizeDelta = new Vector2(560f, 20f);
            TextMeshProUGUI labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = "Transaction Hash:";
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.fontSize = 14;
            labelText.color = new Color(1f, 1f, 1f, 0.75f);

            txHashField = CreateReadOnlyInputField(txHashRow.transform);
            CreateCopyButton(txHashRow.transform, txHashField);

            GameObject linkObj = new GameObject("ExplorerLink", typeof(RectTransform));
            linkObj.transform.SetParent(txHashRow.transform, false);
            RectTransform linkRect = linkObj.GetComponent<RectTransform>();
            linkRect.anchorMin = new Vector2(0.5f, 0f);
            linkRect.anchorMax = new Vector2(0.5f, 0f);
            linkRect.pivot = new Vector2(0.5f, 1f);
            linkRect.anchoredPosition = new Vector2(0f, -4f);
            linkRect.sizeDelta = new Vector2(560f, 25f);
            TextMeshProUGUI linkText = linkObj.AddComponent<TextMeshProUGUI>();
            linkText.text = "<u>View on Cardanoscan \u2197</u>";
            linkText.alignment = TextAlignmentOptions.Center;
            linkText.fontSize = 15;
            linkText.color = new Color(0.45f, 0.7f, 1f);
            Button linkButton = linkObj.AddComponent<Button>();
            linkButton.targetGraphic = linkText;

            txHashRow.SetActive(false);
        }

        private static TMP_InputField CreateReadOnlyInputField(Transform parent)
        {
            GameObject fieldObj = new GameObject("TxHashField", typeof(RectTransform));
            fieldObj.transform.SetParent(parent, false);
            RectTransform fieldRect = fieldObj.GetComponent<RectTransform>();
            fieldRect.anchorMin = new Vector2(0f, 0f);
            fieldRect.anchorMax = new Vector2(1f, 1f);
            fieldRect.offsetMin = new Vector2(0f, 0f);
            fieldRect.offsetMax = new Vector2(-44f, 0f);
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

            Sprite copyIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CardanoSDK/Icons/copy-icon.png");
            Sprite checkmarkIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/CardanoSDK/Icons/checkmark-icon.png");
            copyButton.CopyIcon = copyIcon;
            copyButton.CheckmarkIcon = checkmarkIcon;

            if (copyIcon == null || checkmarkIcon == null)
            {
                Debug.LogWarning("[Cardano SDK] Copy/checkmark icon not found -- assign CopyButton's CopyToClipboardButton icons manually.");
            }
        }

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
