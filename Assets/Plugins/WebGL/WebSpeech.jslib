// Browser text-to-speech bridge for CyVerse (Web Speech API).
// Free, offline-capable (uses the OS's local voices), no assets or keys.
// Only compiled into WebGL builds; the C# side no-ops elsewhere.
mergeInto(LibraryManager.library, {

  Cyverse_SpeechAvailable: function () {
    return (typeof window !== "undefined" && "speechSynthesis" in window) ? 1 : 0;
  },

  Cyverse_Speak: function (textPtr, rate, pitch, volume) {
    if (typeof window === "undefined" || !("speechSynthesis" in window)) return;
    var text = UTF8ToString(textPtr);
    try {
      window.speechSynthesis.cancel(); // one line speaks at a time
      var u = new SpeechSynthesisUtterance(text);
      u.rate = rate;
      u.pitch = pitch;
      u.volume = volume;
      u.onend = function () {
        try {
          if (typeof window.unityInstance !== "undefined" && window.unityInstance !== null)
            window.unityInstance.SendMessage("GameSystems", "OnTtsEnd", "");
        } catch (e) { /* game object gone (scene reload) — ignore */ }
      };
      window.speechSynthesis.speak(u);
    } catch (e) { /* never let speech errors reach gameplay */ }
  },

  Cyverse_CancelSpeech: function () {
    if (typeof window !== "undefined" && "speechSynthesis" in window) {
      try { window.speechSynthesis.cancel(); } catch (e) {}
    }
  }
});
