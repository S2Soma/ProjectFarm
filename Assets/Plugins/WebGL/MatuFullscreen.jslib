// Web build ▸ toàn màn hình. The logic lives in the page (Assets/WebGLTemplates/MATU/index.html,
// window.MatuFullscreen) so the page's own "Chơi toàn màn hình" card and the in-game button share it;
// these are thin calls with a safe answer when the page is some other template.
mergeInto(LibraryManager.library, {
  MatuFs_Available: function () {
    var f = window.MatuFullscreen;
    return f && f.available() ? 1 : 0;
  },
  MatuFs_IsOn: function () {
    var f = window.MatuFullscreen;
    return f && f.isOn() ? 1 : 0;
  },
  MatuFs_Standalone: function () {
    var f = window.MatuFullscreen;
    return f && f.standalone() ? 1 : 0;
  },
  MatuFs_Toggle: function () {
    var f = window.MatuFullscreen;
    if (f) f.toggle();
  },
});
