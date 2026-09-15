using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>A mesh of free quads for the island ambience shaders (Resources/Shaders/UI*.shader).
    ///
    /// An <see cref="Image"/> can only be a rectangle with 0..1 texcoords. The ambience needs quads that
    /// lie along the grid (the river is a parallelogram whose texcoords ARE grid cells), quads that
    /// carry per-quad data in TEXCOORD1 (a plant's sway phase, a plume's seed), and many small quads in
    /// one draw call (every grass tuft on an island). Built once; after that the shaders animate it
    /// from _Time and the mesh is never touched again, which is the whole point — the world canvas
    /// does not re-batch for anything that moves here.
    ///
    /// Positions are in the parent's local space (IslandView puts the node at the field centre, so
    /// they are field coordinates). Vertex colour = this graphic's colour, so the day's light and the
    /// weather tint reach it exactly as they reach an Image.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class LifeQuads : MaskableGraphic
    {
        struct Quad
        {
            public Vector2 p0, p1, p2, p3;       // bottom-left, top-left, top-right, bottom-right
            public Vector2 t0, t1, t2, t3;
            public Vector4 d0, d1, d2, d3;
        }

        readonly List<Quad> _quads = new List<Quad>();
        Texture _texture;

        public Texture Texture
        {
            get { return _texture; }
            set { if (_texture == value) return; _texture = value; SetMaterialDirty(); }
        }

        public override Texture mainTexture => _texture != null ? _texture : s_WhiteTexture;

        public int QuadCount => _quads.Count;

        /// <summary>Create a node at the parent's centre holding an empty mesh.</summary>
        public static LifeQuads Create(Transform parent, string name, Material mat, Texture tex)
        {
            var rt = UIKit.Node(name, parent);
            rt.Anchor(UIKit.Center, Vector2.zero, Vector2.one);
            var q = rt.gameObject.AddComponent<LifeQuads>();
            q.raycastTarget = false;
            q.material = mat;
            q.Texture = tex;
            return q;
        }

        public void Clear() { _quads.Clear(); SetVerticesDirty(); }

        /// <summary>A quad from four corners (bottom-left, top-left, top-right, bottom-right) with
        /// matching texcoords and the same TEXCOORD1 at every corner.</summary>
        public void Add(Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br,
                        Vector2 tbl, Vector2 ttl, Vector2 ttr, Vector2 tbr, Vector4 data)
        {
            _quads.Add(new Quad { p0 = bl, p1 = tl, p2 = tr, p3 = br, t0 = tbl, t1 = ttl, t2 = ttr, t3 = tbr, d0 = data, d1 = data, d2 = data, d3 = data });
            SetVerticesDirty();
        }

        /// <summary>A quad with its own TEXCOORD1 per corner (a sway weight that is 0 at the root).</summary>
        public void Add(Vector2 bl, Vector2 tl, Vector2 tr, Vector2 br,
                        Vector2 tbl, Vector2 ttl, Vector2 ttr, Vector2 tbr,
                        Vector4 dbl, Vector4 dtl, Vector4 dtr, Vector4 dbr)
        {
            _quads.Add(new Quad { p0 = bl, p1 = tl, p2 = tr, p3 = br, t0 = tbl, t1 = ttl, t2 = ttr, t3 = tbr, d0 = dbl, d1 = dtl, d2 = dtr, d3 = dbr });
            SetVerticesDirty();
        }

        /// <summary>An axis-aligned rectangle standing on <paramref name="foot"/> (bottom-centre),
        /// showing <paramref name="uvRect"/> of the texture, mirrored if asked.</summary>
        public void AddStanding(Vector2 foot, Vector2 size, Rect uvRect, Vector4 dataBottom, Vector4 dataTop, bool mirror = false)
        {
            float hw = size.x * 0.5f;
            var bl = foot + new Vector2(-hw, 0f);
            var tl = foot + new Vector2(-hw, size.y);
            var tr = foot + new Vector2(hw, size.y);
            var br = foot + new Vector2(hw, 0f);
            float u0 = mirror ? uvRect.xMax : uvRect.xMin, u1 = mirror ? uvRect.xMin : uvRect.xMax;
            Add(bl, tl, tr, br,
                new Vector2(u0, uvRect.yMin), new Vector2(u0, uvRect.yMax), new Vector2(u1, uvRect.yMax), new Vector2(u1, uvRect.yMin),
                dataBottom, dataTop, dataTop, dataBottom);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Color32 c = color;
            var v = new UIVertex { color = c, normal = Vector3.back, tangent = new Vector4(1, 0, 0, -1) };
            for (int i = 0; i < _quads.Count; i++)
            {
                var q = _quads[i];
                int start = vh.currentVertCount;
                v.position = q.p0; v.uv0 = q.t0; v.uv1 = q.d0; vh.AddVert(v);
                v.position = q.p1; v.uv0 = q.t1; v.uv1 = q.d1; vh.AddVert(v);
                v.position = q.p2; v.uv0 = q.t2; v.uv1 = q.d2; vh.AddVert(v);
                v.position = q.p3; v.uv0 = q.t3; v.uv1 = q.d3; vh.AddVert(v);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start + 2, start + 3, start);
            }
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            MutationTint.EnableChannel(this);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            MutationTint.EnableChannel(this);
        }
    }
}
