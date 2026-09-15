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

        [MenuItem("Tools/LQ Farm/Chụp hướng dẫn chơi")]
        static void CaptureHelp()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            GameApp.I.StartCoroutine(UiAudit.RunHelp(GameApp.I));
        }

        [MenuItem("Tools/LQ Farm/Chụp trang trí")]
        static void CaptureCosmetics()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            GameApp.I.StartCoroutine(UiAudit.RunCosmetics(GameApp.I));
        }

        [MenuItem("Tools/LQ Farm/Chụp thú cưng")]
        static void CapturePets()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            GameApp.I.StartCoroutine(UiAudit.RunPets(GameApp.I));
        }

        [MenuItem("Tools/LQ Farm/Chụp tài khoản & màn hình bắt đầu")]
        static void CaptureAccount()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            GameApp.I.StartCoroutine(UiAudit.RunAccount(GameApp.I));
        }

        [MenuItem("Tools/LQ Farm/Chụp đảo sống")]
        static void CaptureIslandsAlive()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            GameApp.I.StartCoroutine(UiAudit.RunIslandsAlive(GameApp.I));
        }

        [MenuItem("Tools/LQ Farm/Chụp chuyển cảnh")]
        static void CaptureCinematic()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            // on the Supabase host: the pass restarts the game, and GameApp would take the coroutine with it
            Supa.Run(UiAudit.RunCinematic());
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
