mergeInto(LibraryManager.library, {

  // GUIUtility.systemCopyBuffer isn't reliably bridged to the real browser clipboard in WebGL
  // builds -- it can silently no-op. This calls the browser's Clipboard API directly instead,
  // with a fallback for older/restrictive browsers that don't support it.
  CopyTextToClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);

    function fallbackCopy(value) {
      var textarea = document.createElement("textarea");
      textarea.value = value;
      textarea.style.position = "fixed";
      textarea.style.opacity = "0";
      document.body.appendChild(textarea);
      textarea.focus();
      textarea.select();
      try {
        document.execCommand("copy");
      } catch (e) {
        console.error("[Cardano SDK] Fallback clipboard copy failed:", e);
      }
      document.body.removeChild(textarea);
    }

    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(function (err) {
        console.warn("[Cardano SDK] navigator.clipboard.writeText failed, falling back:", err);
        fallbackCopy(text);
      });
    } else {
      fallbackCopy(text);
    }
  },

});
