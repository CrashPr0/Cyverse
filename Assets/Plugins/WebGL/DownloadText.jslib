// Hands the player a text file to save (CyVerse playtest data export).
// The data is built in the game and never leaves the browser: it becomes a
// Blob and a download link, nothing is uploaded. Only compiled into WebGL
// builds; the C# side writes a local file elsewhere.
mergeInto(LibraryManager.library, {

  Cyverse_DownloadText: function (namePtr, mimePtr, textPtr) {
    if (typeof document === "undefined") return 0;
    try {
      var name = UTF8ToString(namePtr);
      var mime = UTF8ToString(mimePtr);
      var text = UTF8ToString(textPtr);
      var blob = new Blob([text], { type: mime });
      var url = URL.createObjectURL(blob);
      var link = document.createElement("a");
      link.href = url;
      link.download = name;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      setTimeout(function () { URL.revokeObjectURL(url); }, 10000);
      return 1;
    } catch (e) {
      return 0; // never let a failed save reach gameplay
    }
  }
});
