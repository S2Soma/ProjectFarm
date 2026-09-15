using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>One layer of a rope bridge — the planks, their end grain, or a handrail — as a mesh
    /// laid along the bridge's path.
    ///
    /// A strip of quads follows the centreline; each column's top and bottom are VERTICAL offsets
    /// from it, which keeps the layer an isometric ground strip even where the path slopes (offsets
    /// along the curve's normal would tilt the planks like a ramp seen side-on). U runs with arc
    /// length and wraps (the strip texture repeats horizontally), V picks this layer's row out of
    /// Art/bridge/bridge_strip.png. Every layer of every bridge shares that one texture, so they
    /// batch.
    ///
    /// <see cref="fill"/> draws only the first part of the path, which is how the bridge grows
    /// across the gap when an island unlocks.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class BridgeRibbon : MaskableGraphic
    {
        /// <summary>Rows of bridge_strip.png: top pixel and content height, out of 744. Must match
        /// Tools/gen_bridge.py ROWS.</summary>
        public enum Row { Planks, Edge, Rail }
        static readonly (float top, float h)[] Rows = { (24, 144), (216, 24), (288, 112) };
        const float TexH = 448f;
        /// <summary>World units of bridge per repeat of the 1024 px texture (3.2 px per unit).</summary>
        public const float DefaultUnitsPerTile = 320f;
        /// <summary>Stretched a little per bridge so a whole number of planks spans it.</summary>
        public float unitsPerTile = DefaultUnitsPerTile;

        static Texture2D _tex;
        public override Texture mainTexture
        {
            get
            {
                if (_tex == null) _tex = Resources.Load<Texture2D>("Art/bridge/bridge_strip");
                return _tex != null ? _tex : s_WhiteTexture;
            }
        }

        public Row row;
        public Color topColor = Color.white, bottomColor = Color.white;
        public float uOffset;
        [Range(0, 1)] public float fill = 1f;

        Vector2[] _centre;
        float[] _top, _bottom, _arc;

        /// <summary>Set the geometry: sampled centreline points, and for each the top and bottom
        /// offset (in world units, relative to the centreline's y).</summary>
        public void SetPath(Vector2[] centre, float[] top, float[] bottom)
        {
            _centre = centre; _top = top; _bottom = bottom;
            _arc = new float[centre.Length];
            for (int i = 1; i < centre.Length; i++) _arc[i] = _arc[i - 1] + Vector2.Distance(centre[i - 1], centre[i]);
            SetVerticesDirty();
        }

        public void SetColors(Color top, Color bottom)
        {
            if (top == topColor && bottom == bottomColor) return;
            topColor = top; bottomColor = bottom;
            SetVerticesDirty();
        }

        public void SetFill(float f)
        {
            f = Mathf.Clamp01(f);
            if (Mathf.Approximately(f, fill)) return;
            fill = f;
            SetVerticesDirty();
        }

        /// <summary>The point the growing end has reached, for the head puff to ride.</summary>
        public Vector2 HeadPoint()
        {
            if (_centre == null || _centre.Length == 0) return Vector2.zero;
            float want = fill * _arc[_arc.Length - 1];
            for (int i = 1; i < _centre.Length; i++)
                if (_arc[i] >= want)
                {
                    float t = Mathf.InverseLerp(_arc[i - 1], _arc[i], want);
                    return Vector2.Lerp(_centre[i - 1], _centre[i], t);
                }
            return _centre[_centre.Length - 1];
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_centre == null || _centre.Length < 2 || fill <= 0.0001f) return;

            var r = Rows[(int)row];
            float vTop = 1f - r.top / TexH;
            float vBot = 1f - (r.top + r.h) / TexH;
            float total = _arc[_arc.Length - 1];
            float stop = fill * total;
            var ct = (Color32)(topColor * color);
            var cb = (Color32)(bottomColor * color);

            int n = 0;
            for (int i = 0; i < _centre.Length; i++)
            {
                Vector2 c; float tp, bt, s;
                if (_arc[i] <= stop)
                {
                    c = _centre[i]; tp = _top[i]; bt = _bottom[i]; s = _arc[i];
                }
                else
                {
                    // cut the last column exactly at the growing end
                    float t = Mathf.InverseLerp(_arc[i - 1], _arc[i], stop);
                    c = Vector2.Lerp(_centre[i - 1], _centre[i], t);
                    tp = Mathf.Lerp(_top[i - 1], _top[i], t);
                    bt = Mathf.Lerp(_bottom[i - 1], _bottom[i], t);
                    s = stop;
                }
                float u = s / unitsPerTile + uOffset;
                vh.AddVert(new Vector3(c.x, c.y + tp), ct, new Vector2(u, vTop));
                vh.AddVert(new Vector3(c.x, c.y + bt), cb, new Vector2(u, vBot));
                if (n > 0)
                {
                    int a = (n - 1) * 2;
                    vh.AddTriangle(a, a + 2, a + 1);
                    vh.AddTriangle(a + 1, a + 2, a + 3);
                }
                n++;
                if (_arc[i] > stop) break;
            }
        }
    }
}
