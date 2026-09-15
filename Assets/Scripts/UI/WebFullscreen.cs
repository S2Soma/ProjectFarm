using System.Runtime.InteropServices;
using UnityEngine;

namespace LQFarm
{
    /// <summary>Full screen on the web build (the HUD button beside the wallet).
    ///
    /// The browser owns full screen, so the work is in the page: window.MatuFullscreen in
    /// Assets/WebGLTemplates/MATU/index.html, reached through Assets/Plugins/WebGL/MatuFullscreen.jslib.
    /// A request needs a recent tap; Unity handles the tap a frame later, which is inside the
    /// browser's user-activation window, and the page retries on the next tap if it was refused.
    ///
    /// iPhone Safari has no full screen for a page at all — the button is still offered there and
    /// explains "Thêm vào MH chính", which opens the game without browser bars (the template carries
    /// a web manifest and apple-mobile-web-app-capable for that). Launched that way the button hides.</summary>
    public static class WebFullscreen
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int MatuFs_Available();
        [DllImport("__Internal")] static extern int MatuFs_IsOn();
        [DllImport("__Internal")] static extern int MatuFs_Standalone();
        [DllImport("__Internal")] static extern void MatuFs_Toggle();

        /// <summary>Whether the HUD shows the button: every web page not already running as an app.</summary>
        public static bool Offered => MatuFs_Standalone() == 0;
        /// <summary>Whether this browser can put the page in full screen (false on iPhone).</summary>
        public static bool Available => MatuFs_Available() != 0;
        public static bool IsOn => MatuFs_IsOn() != 0;
        public static void Toggle() { MatuFs_Toggle(); }
#else
        /// <summary>Editor and native builds have no browser around them. The screenshot pass sets this
        /// to see the button; Toggle then only flips the icon.</summary>
        public static bool PreviewInEditor;
        static bool s_previewOn;
        public static bool Offered => PreviewInEditor;
        public static bool Available => PreviewInEditor;
        public static bool IsOn => s_previewOn;
        public static void Toggle() { s_previewOn = !s_previewOn; }
#endif
    }
}
