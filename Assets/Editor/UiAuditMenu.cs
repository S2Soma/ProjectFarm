using UnityEditor;
using UnityEngine;

namespace LQFarm.EditorTools
{
    public static class UiAuditMenu
    {
        [MenuItem("Tools/LQ Farm/Chụp toàn bộ giao diện")]
        static void Capture()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("Cần đang chạy",
                    "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            var app = GameApp.I;
            if (app == null)
            {
                EditorUtility.DisplayDialog("Chưa sẵn sàng",
                    "Không tìm thấy GameApp. Đợi game khởi động xong rồi thử lại.", "OK");
                return;
            }
            app.StartCoroutine(UiAudit.Run(app));
        }

        [MenuItem("Tools/LQ Farm/Mở thư mục ảnh")]
        static void OpenFolder()
        {
            string dir = System.IO.Path.GetFullPath(UiAudit.OutputDir);
            System.IO.Directory.CreateDirectory(dir);
            EditorUtility.RevealInFinder(dir);
        }
    }
}
