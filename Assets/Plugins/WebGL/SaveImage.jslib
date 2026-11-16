// Saves what the game canvas is showing as a PNG download (CyVerse).
// Call it at the end of a frame (WaitForEndOfFrame): a WebGL canvas only
// holds its picture until the browser presents it, so later it reads back
// blank. Only compiled into WebGL builds; the C# side no-ops elsewhere.
mergeInto(LibraryManager.library, {

  Cyverse_SaveCanvasImage: function (namePtr) {
    if (typeof document === "undefined") return 0;
    var name = UTF8ToString(namePtr);
    try {
      var canvas = (typeof Module !== "undefined" && Module.canvas) || document.querySelector("#unity-canvas");
      if (!canvas) return 0;
      var url = canvas.toDataURL("image/png");
      var link = document.createElement("a");
      link.href = url;
      link.download = name;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      return 1;
    } catch (e) {
      return 0; // never let a failed save reach gameplay
    }
  }
});
