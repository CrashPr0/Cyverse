// Browser clipboard bridge for CyVerse.
// Under WebGL, GUIUtility.systemCopyBuffer is an engine-internal buffer with no
// connection to the OS clipboard, and the browser handles Ctrl/Cmd+V itself
// rather than forwarding it as key input — so pasting into a Unity field is a
// no-op unless we listen for the page's own paste event. Only compiled into
// WebGL builds; the C# side uses systemCopyBuffer everywhere else.
mergeInto(LibraryManager.library, {

  // Route the page's paste events to a Unity GameObject method.
  Cyverse_ClipboardInit: function (objectPtr, methodPtr) {
    if (typeof document === "undefined") return;
    var target = UTF8ToString(objectPtr);
    var method = UTF8ToString(methodPtr);

    // Re-registering (scene reload) must not stack listeners.
    if (window.cyverseClipboardHook)
      document.removeEventListener("paste", window.cyverseClipboardHook);

    window.cyverseClipboardHook = function (e) {
      try {
        var data = e.clipboardData || window.clipboardData;
        if (!data) return;
        var text = data.getData("text");
        if (!text) return;
        e.preventDefault();
        if (typeof window.unityInstance !== "undefined" && window.unityInstance !== null)
          window.unityInstance.SendMessage(target, method, text);
      } catch (err) { /* game object gone (scene reload) — ignore */ }
    };
    document.addEventListener("paste", window.cyverseClipboardHook);
  },

  // Explicit read, for the case where the key press reached Unity but the
  // browser fired no paste event. Async and permission-gated, so the result
  // comes back through the same callback; failure is silent by design.
  Cyverse_ClipboardRequestRead: function (objectPtr, methodPtr) {
    var target = UTF8ToString(objectPtr);
    var method = UTF8ToString(methodPtr);
    try {
      if (!navigator.clipboard || !navigator.clipboard.readText) return;
      navigator.clipboard.readText().then(function (text) {
        if (!text) return;
        if (typeof window.unityInstance !== "undefined" && window.unityInstance !== null)
          window.unityInstance.SendMessage(target, method, text);
      })["catch"](function () { /* denied or unavailable — ignore */ });
    } catch (err) { /* ignore */ }
  },

  Cyverse_ClipboardWrite: function (textPtr) {
    var text = UTF8ToString(textPtr);
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text)["catch"](function () {});
        return;
      }
    } catch (err) { /* fall through to the legacy path */ }
    try {
      var scratch = document.createElement("textarea");
      scratch.value = text;
      scratch.style.position = "fixed";
      scratch.style.opacity = "0";
      document.body.appendChild(scratch);
      scratch.select();
      document.execCommand("copy");
      document.body.removeChild(scratch);
    } catch (err) { /* ignore */ }
  }
});
