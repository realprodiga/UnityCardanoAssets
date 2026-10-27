using System.Collections;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Prodiga.CardanoSDK
{
    /// <summary>
    /// Generic "click to copy" button -- not specific to minting, reusable for anything with text to
    /// copy (a tx hash, an address, whatever). Copies TextSource's current text to the real browser
    /// clipboard via the CardanoUIInterop.jslib bridge in WebGL builds -- NOT via
    /// GUIUtility.systemCopyBuffer, which isn't reliably bridged to the actual browser clipboard in
    /// WebGL and can silently no-op. Falls back to systemCopyBuffer outside WebGL (Editor,
    /// Standalone), where it works fine.
    ///
    /// Briefly swaps its own icon to CheckmarkIcon on click, then reverts to CopyIcon after
    /// ConfirmationSeconds -- the same "flash a checkmark" confirmation most LLM chat apps use for
    /// their copy buttons.
    ///
    /// Setup: attach to a Button, assign TextSource (works with a plain TMP_Text, or a read-only
    /// TMP_InputField's textComponent), assign IconImage + CopyIcon + CheckmarkIcon.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class CopyToClipboardButton : MonoBehaviour
    {
        [Tooltip("Text to copy when clicked. Works with a plain TMP_Text or a TMP_InputField's textComponent field.")]
        public TMP_Text TextSource;

        [Header("Icon")]
        public Image IconImage;
        public Sprite CopyIcon;
        public Sprite CheckmarkIcon;
        [Tooltip("How long the checkmark stays before reverting to the copy icon.")]
        public float ConfirmationSeconds = 1.2f;

        [DllImport("__Internal")] private static extern void CopyTextToClipboard(string text);

        private Button _button;
        private Coroutine _revertRoutine;

        void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(CopyNow);
            if (IconImage != null && CopyIcon != null) IconImage.sprite = CopyIcon;
        }

        public void CopyNow()
        {
            if (TextSource == null || string.IsNullOrEmpty(TextSource.text)) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            CopyTextToClipboard(TextSource.text);
#else
            GUIUtility.systemCopyBuffer = TextSource.text;
#endif

            if (IconImage != null && CheckmarkIcon != null)
            {
                if (_revertRoutine != null) StopCoroutine(_revertRoutine);
                IconImage.sprite = CheckmarkIcon;
                _revertRoutine = StartCoroutine(RevertIconAfterDelay());
            }
        }

        private IEnumerator RevertIconAfterDelay()
        {
            yield return new WaitForSeconds(ConfirmationSeconds);
            if (IconImage != null && CopyIcon != null) IconImage.sprite = CopyIcon;
            _revertRoutine = null;
        }
    }
}
