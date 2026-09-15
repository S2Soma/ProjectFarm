using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm.EditorTools
{
    /// <summary>Tools ▸ LQ Farm ▸ Chụp thú cưng đi lại — screenshots P00–P6x of the pet moving on the map:
    /// the walkable grid of four islands (debug overlay, editor only), a walk along the fence, work at a
    /// plot, the middle of a rope bridge at two zooms, the hop over Đảo Nước's river, a whole trip from
    /// Vườn Nhà to Khổng Lồ with the camera following, and the pet panel's "Tìm …" button taking the camera to it. Needs Play mode; everything it opens or grants for
    /// the shots is put back afterwards.</summary>
    public static class AuditPets
    {
        [MenuItem("Tools/LQ Farm/Chụp thú cưng đi lại")]
        static void Capture()
        {
            if (!Application.isPlaying || GameApp.I == null)
            {
                EditorUtility.DisplayDialog("Cần đang chạy", "Bấm Play trước, rồi chạy lại lệnh này.", "OK");
                return;
            }
            GameApp.I.StartCoroutine(Run(GameApp.I));
        }

        static string _dir;

        static IEnumerator Shot(string name)
        {
            string path = System.IO.Path.Combine(_dir, name + ".png");
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
        }

        public static IEnumerator Run(GameApp app)
        {
            app.EnsureBuilt();
            yield return null;
            _dir = System.IO.Path.GetFullPath(UiAudit.OutputDir);
            System.IO.Directory.CreateDirectory(_dir);
            var s = GS.Local;
            var farm = app.Farm;
            var pets = app.Pets;
            bool quiet = Tutorial.SuppressTips;
            Tutorial.SuppressTips = true;
            app.CloseAll(); app.CloseSeedSheet(); app.Hud.CloseMenu();

            // what the shots borrow: a pet, level 5, islands 1-2 open (bridges built for them)
            var petsBefore = new Dictionary<string, int>(s.pets);
            string activeBefore = s.petActive;
            int eggsBefore = s.petEggs;
            s.petEggs = Mathf.Max(1, s.petEggs);          // no "Thú cưng đã mở!" tip over the shots
            int lvBefore = s.lv;
            var openBefore = new bool[s.islands.Count];
            for (int i = 0; i < s.islands.Count; i++) openBefore[i] = s.islands[i].unlocked;
            if (s.lv < PetSys.UnlockLevel) s.lv = PetSys.UnlockLevel;
            if (PetSys.Active(s) == null) { s.pets["mit"] = 3; s.petActive = "mit"; }
            for (int i = 1; i <= 2 && i < s.islands.Count; i++) s.islands[i].unlocked = true;
            app.SyncIslands();
            farm.RenderAll();
            // a tip already on screen ("Thú cưng đã mở!" on a save without eggs) closes when a panel opens
            app.Open(new PetPanel(app));
            yield return new WaitForSecondsRealtime(0.3f);
            app.CloseAll();
            yield return new WaitForSecondsRealtime(0.4f);
            if (pets.Actor == null) { Debug.LogError("[AuditPets] Không có thú cưng trên bản đồ."); yield break; }

            var cam = farm.Camera;
            float zFar = cam.ZBase * cam.Levels[0];
            float zMid = cam.ZBase * cam.Levels[Mathf.Clamp(cam.Levels.Count / 2 - 1, 0, cam.Levels.Count - 1)];
            float zFarm = cam.ZBase;

            // ---- P00-P03: the walkable grid, debug overlay ----
            int shot = 0;
            foreach (int island in new[] { 0, 1, 2, 3 })
            {
                PetPaths.ClearCache();
                var overlay = PetGridOverlay.Add(farm.PetLayer, island);
                pets.StageAt(island, PetGrid.CellOf(PetPaths.For(island).Snap(new Vector2(-2.25f, -1.6f))), PetActor.Pose.Idle, 1f);
                farm.GoToIsland(island, animate: false);
                cam.Snap(ArchipelagoView.FieldOrigin(island), zFarm * 0.92f);
                yield return new WaitForSecondsRealtime(0.5f);
                yield return Shot("P0" + shot++ + "_grid_island" + island);
                Object.Destroy(overlay.gameObject);
            }

            // ---- P10-P13: along the back-left fence of Vườn Nhà ----
            farm.GoToIsland(0, animate: false);
            cam.Snap(ArchipelagoView.FieldOrigin(0), zFarm);
            var g0 = PetPaths.For(0);
            pets.StageAt(0, PetGrid.CellOf(g0.Snap(new Vector2(-2.3f, 1.95f))), PetActor.Pose.Idle, -1f);
            yield return new WaitForSecondsRealtime(0.3f);
            pets.StageTrip(0, PetGrid.CellOf(g0.Snap(new Vector2(-2.3f, -2.3f))));
            for (int k = 0; k < 4 && pets.Walking; k++)
            {
                yield return new WaitForSecondsRealtime(0.9f);
                yield return Shot("P1" + k + "_fence_walk");
            }
            while (pets.Walking) yield return null;

            // ---- P20: working a plot ----
            pets.StageForAudit(PetActor.Pose.Happy, 5);
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Shot("P20_work_plot");
            pets.StageForAudit(PetActor.Pose.Idle, 15);
            yield return new WaitForSecondsRealtime(0.2f);
            yield return Shot("P21_work_front_plot");

            // ---- P30-P32: mid-bridge ----
            pets.StageOnBridge(1, 0.5f, true);
            var mid = PetPaths.Bridge(1).At(PetPaths.Bridge(1).Length * 0.5f);
            cam.Snap(mid, zFar);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("P30_bridge_far");
            cam.Snap(mid, zMid);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("P31_bridge_mid");
            cam.Snap(mid, zFarm * 1.2f);
            pets.StageOnBridge(2, 0.35f, false);
            cam.Snap(PetPaths.Bridge(2).At(PetPaths.Bridge(2).Length * 0.35f), zFarm * 1.2f);
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("P32_bridge_close_back");

            // ---- P40-P4x: the hop over Đảo Nước's river ----
            farm.GoToIsland(1, animate: false);
            var g1 = PetPaths.For(1);
            var bank = PetGrid.CellOf(g1.Snap(new Vector2(PetPaths.CrossingU, 1.5f)));
            var other = PetGrid.CellOf(g1.Snap(new Vector2(PetPaths.CrossingU, -1.5f)));
            cam.Snap(ArchipelagoView.PetPosOfCell(1, new Vector2(PetPaths.CrossingU, 0f)) + new Vector2(0f, 40f), zFarm * 1.25f);
            pets.StageAt(1, bank, PetActor.Pose.Idle, 1f);
            yield return new WaitForSecondsRealtime(0.3f);
            pets.StageTrip(1, other);
            int f = 0;
            float t0 = Time.unscaledTime;
            while (pets.Walking && f < 30 && Time.unscaledTime - t0 < 12f)
            {
                yield return new WaitForSecondsRealtime(0.12f);
                yield return Shot("P4" + (f < 10 ? "0" : "") + f + "_river");
                f++;
            }
            while (pets.Walking) yield return null;

            // ---- P60+: a whole trip, Vườn Nhà → Đảo Nước → Khổng Lồ, the camera following ----
            farm.GoToIsland(0, animate: false);
            pets.StageAt(0, PetGrid.CellOf(g0.Snap(new Vector2(-2.3f, -1.6f))), PetActor.Pose.Idle, 1f);
            var goal = PetPaths.For(2).WorkSpot(10, PetPaths.LeftLanding) ?? Vector2.zero;
            float zTrip = zFarm * 0.62f;
            cam.Snap(pets.Actor.Root.anchoredPosition + new Vector2(0, 60), zTrip);
            yield return new WaitForSecondsRealtime(0.3f);
            if (!pets.StageTrip(2, goal)) Debug.LogError("[AuditPets] Không có đường từ Vườn Nhà sang Khổng Lồ.");
            int n = 0;
            float next = Time.unscaledTime;
            t0 = Time.unscaledTime;
            while (pets.Walking && Time.unscaledTime - t0 < 60f)
            {
                cam.Snap(pets.Actor.Root.anchoredPosition + new Vector2(0, 60), zTrip);
                if (Time.unscaledTime >= next)
                {
                    next += 0.5f;
                    yield return Shot("P6" + n.ToString("00") + "_trip" + (pets.OnBridge ? "_bridge" : "_island" + pets.Island));
                    n++;
                }
                else yield return null;
            }
            yield return Shot("P6" + n.ToString("00") + "_trip_end");

            // ---- P70-P71: the pet panel's "Tìm …" button, pressed for real, from another island ----
            farm.GoToIsland(0, animate: false);
            cam.Snap(ArchipelagoView.FieldOrigin(0), zFarm);
            pets.EndAudit();                                   // the pet stands where the trip left it
            PetPanel.OpenOnTab(0);
            app.Open(new PetPanel(app));
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shot("P70_panel_find_button");
            Button find = null;
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                var t = b.GetComponentInChildren<Text>();
                if (t != null && t.text.StartsWith("Tìm ")) find = b;
            }
            if (find == null) Debug.LogError("[AuditPets] Không thấy nút Tìm thú cưng.");
            else find.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot("P71_found_on_island" + farm.CurrentIsland);
            pets.StageAt(pets.NearestIsland, pets.Cell, PetActor.Pose.Idle, 1f);

            // ---- put everything back ----
            pets.EndAudit();
            for (int i = 0; i < s.islands.Count && i < openBefore.Length; i++) s.islands[i].unlocked = openBefore[i];
            s.lv = lvBefore;
            s.pets.Clear(); foreach (var kv in petsBefore) s.pets[kv.Key] = kv.Value;
            s.petActive = activeBefore;
            s.petEggs = eggsBefore;
            farm.SyncBridges(false);
            farm.RenderAll();
            farm.GoToIsland(0, animate: false);
            app.Hud.Render();
            Tutorial.SuppressTips = quiet;
            Debug.Log("[AuditPets] Xong: " + _dir);
        }
    }

    /// <summary>Debug only: an island's walkable lattice drawn over the map, one mesh on the pet layer.
    /// Green yard, orange furrow, yellow gate corner, red bed; blue the river hop, magenta the gates, white
    /// the path from the left gate to the right one, red rings the obstacles' footprints.</summary>
    public class PetGridOverlay : MaskableGraphic
    {
        public int island;

        public static PetGridOverlay Add(RectTransform layer, int island)
        {
            var go = new GameObject("petGridOverlay" + island, typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(layer, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.SetAsFirstSibling();
            var o = go.AddComponent<PetGridOverlay>();
            o.raycastTarget = false;
            o.island = island;
            o.SetVerticesDirty();
            return o;
        }

        Vector2 P(Vector2 cell) { return ArchipelagoView.PetPosOfCell(island, cell); }

        void Quad(VertexHelper vh, Vector2 c, float r, Color col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(P(c + new Vector2(-r, 0)), col, Vector2.zero);
            vh.AddVert(P(c + new Vector2(0, -r)), col, Vector2.zero);
            vh.AddVert(P(c + new Vector2(r, 0)), col, Vector2.zero);
            vh.AddVert(P(c + new Vector2(0, r)), col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        void Line(VertexHelper vh, Vector2 a, Vector2 b, float w, Color col)
        {
            var pa = P(a); var pb = P(b);
            var n = (pb - pa).normalized;
            n = new Vector2(-n.y, n.x) * w * 0.5f;
            int i = vh.currentVertCount;
            vh.AddVert(pa - n, col, Vector2.zero);
            vh.AddVert(pa + n, col, Vector2.zero);
            vh.AddVert(pb + n, col, Vector2.zero);
            vh.AddVert(pb - n, col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var g = PetPaths.For(island);
            for (int n = 0; n < PetGrid.N * PetGrid.N; n++)
            {
                if (!g.Walkable(n)) continue;
                float c = g.cost[n];
                var col = c <= PetGrid.Yard ? new Color(0.2f, 0.95f, 0.3f, 0.85f)
                        : c <= PetGrid.Furrow ? new Color(1f, 0.6f, 0.1f, 0.85f)
                        : c <= PetGrid.GateYard ? new Color(1f, 0.95f, 0.2f, 0.85f)
                        : new Color(0.9f, 0.2f, 0.15f, 0.5f);
                Quad(vh, PetGrid.CellOf(n), PetGrid.H * 0.32f, col);
            }
            foreach (var o in g.obstacles)
                for (int k = 0; k < 24; k++)
                {
                    float a0 = k / 24f * Mathf.PI * 2f, a1 = (k + 1) / 24f * Mathf.PI * 2f;
                    var d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * o.radius;
                    var d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * o.radius;
                    Line(vh, o.a + d0, o.a + d1, 3f, new Color(1f, 0.1f, 0.1f, 0.95f));
                    if (o.b != o.a) Line(vh, o.a, o.b, 3f, new Color(1f, 0.1f, 0.1f, 0.95f));
                }
            foreach (var h in g.hops) Line(vh, PetGrid.CellOf(h.a), PetGrid.CellOf(h.b), 8f, new Color(0.2f, 0.5f, 1f, 1f));
            if (g.LeftGateNode >= 0) Quad(vh, PetGrid.CellOf(g.LeftGateNode), 0.12f, Color.magenta);
            if (g.RightGateNode >= 0) Quad(vh, PetGrid.CellOf(g.RightGateNode), 0.12f, Color.magenta);
            var path = g.FindPath(PetPaths.LeftLanding, PetPaths.RightLanding);
            if (path != null)
                for (int i = 1; i < path.Count; i++)
                    Line(vh, path[i - 1].cell, path[i].cell, path[i].hop ? 6f : 4f, path[i].hop ? new Color(0.3f, 0.7f, 1f) : Color.white);
        }
    }
}
