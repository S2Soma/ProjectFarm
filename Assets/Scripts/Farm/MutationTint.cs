using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>Feeds a mutation's colour and strength to <c>MiT/UI Mutation</c> through each vertex's
    /// TEXCOORD1, so every mutated crop shares one material (and one batch) whatever its tier.
    /// The canvas has to pass that channel: <see cref="EnableChannel"/>.</summary>
    [RequireComponent(typeof(Graphic))]
    public class MutationTint : BaseMeshEffect
    {
        Vector4 _data;

        static Material _mutation, _additive;

        /// <summary>The recolour-and-glow material for mutated crops; null if the shader is missing,
        /// in which case the crop simply keeps the default look.</summary>
        public static Material MutationMaterial
        {
            get
            {
                if (_mutation == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/UIMutation");
                    if (sh != null) _mutation = new Material(sh) { name = "UIMutation (shared)" };
                }
                return _mutation;
            }
        }

        /// <summary>Additive light for glows, rays and motes.</summary>
        public static Material AdditiveMaterial
        {
            get
            {
                if (_additive == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/UIAdditive");
                    if (sh != null) _additive = new Material(sh) { name = "UIAdditive (shared)" };
                }
                return _additive;
            }
        }

        /// <summary>How strongly a tier recolours a crop: stronger for rarer tiers, and gentler on
        /// the one crop whose sheet already paints its elements.</summary>
        public static float StrengthFor(string art, int variant)
        {
            var el = Art.Elem(variant);
            if (!el.hasGlow) return 0f;
            bool painted = Art.IsElemental(art) && variant != 1;
            return painted ? 0.45f : 0.78f + 0.055f * variant;
        }

        /// <summary>Give any crop picture — a plot, a warehouse cell, an icon flying to the store —
        /// the same mutation look. Variant 0 leaves it plain.</summary>
        public static void Apply(Graphic g, string art, int variant)
        {
            if (g == null) return;
            var t = g.GetComponent<MutationTint>();
            if (variant <= 0 && t == null) return;
            if (t == null) t = g.gameObject.AddComponent<MutationTint>();
            t.Set(Art.Elem(variant).glow, StrengthFor(art, variant));
        }

        public static void EnableChannel(Graphic g)
        {
            var c = g != null ? g.canvas : null;
            if (c == null) return;
            c.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
            var root = c.rootCanvas;
            if (root != null) root.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        }

        /// <summary>Colour the graphic as tier <paramref name="strength"/> &gt; 0, or back to the plain
        /// material at 0.</summary>
        public void Set(Color tint, float strength)
        {
            var g = graphic;
            var v = new Vector4(tint.r, tint.g, tint.b, strength);
            bool on = strength > 0f && MutationMaterial != null;
            var want = on ? MutationMaterial : null;
            if (g.material != (want ?? g.defaultMaterial)) g.material = want;
            if (on) EnableChannel(g);
            if (v == _data) return;
            _data = v;
            g.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            var vert = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                vert.uv1 = _data;
                vh.SetUIVertex(vert, i);
            }
        }
    }
}
