using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    /// <summary>Materials for the painted clouds (Shaders/UICloud, "MiT/UI Cloud") and for sky things that
    /// move in the vertex shader (Shaders/UICloudFloat).
    ///
    /// A cloud sprite's grey is light, not colour (Tools/gen_sky.py); the material maps it from a shade
    /// colour to a light colour, breathes its soft edge with slow noise, and scrolls or slides it — all on
    /// the GPU, so a drifting sky never re-batches its canvas. Each moving layer owns one material:
    /// per-layer colours and offsets are material properties, which cost no canvas rebuild when set.
    /// Materials made here are destroyed with the <see cref="CloudMaterialOwner"/> they are registered to.</summary>
    public static class CloudMaterials
    {
        public static readonly int Lit = Shader.PropertyToID("_Lit");
        public static readonly int Shade = Shader.PropertyToID("_Shade");
        public static readonly int Rim = Shader.PropertyToID("_Rim");
        public static readonly int Ramp = Shader.PropertyToID("_Ramp");
        public static readonly int Scroll = Shader.PropertyToID("_Scroll");
        public static readonly int Billow = Shader.PropertyToID("_Billow");
        public static readonly int TexSize = Shader.PropertyToID("_TexSize");
        public static readonly int Drift = Shader.PropertyToID("_Drift");
        public static readonly int Noise = Shader.PropertyToID("_NoiseTex");
        public static readonly int Offset = Shader.PropertyToID("_Offset");
        public static readonly int Spin = Shader.PropertyToID("_Spin");
        static readonly int Clock = Shader.PropertyToID("_MiTCloudTime");

        /// <summary>Texels of a band tile: every band is 2048 wide.</summary>
        public const float TileWidth = 2048f;

        static Shader _cloud, _float;
        static Texture _noise;
        static bool _looked;

        static void Find()
        {
            if (_looked) return;
            _looked = true;
            _cloud = Resources.Load<Shader>("Shaders/UICloud");
            _float = Resources.Load<Shader>("Shaders/UICloudFloat");
            var sp = Art.Load("Art/sky/cloud_noise");
            _noise = sp != null ? sp.texture : null;
            if (_cloud == null || _float == null || _noise == null)
                Debug.LogWarning("[CloudMaterials] cloud shaders or noise missing: clouds fall back to UI/Default");
        }

        /// <summary>Noise tiles per texel for a band: a whole number of noise tiles across the 2048 tile
        /// (and three times as many for the fine octave), so when the scroll wraps by one tile the noise
        /// lands exactly on itself.</summary>
        public static float BandNoise(int tilesAcross) { return tilesAcross / TileWidth; }

        /// <summary>A cloud material, or null when the shader is missing (the Image keeps UI/Default).
        /// <paramref name="wrap"/>: the sprite is a band that repeats horizontally.</summary>
        public static Material Cloud(CloudMaterialOwner owner, Sprite sprite, bool wrap, Vector4 billow, Vector4 ramp)
        {
            Find();
            if (_cloud == null || _noise == null) return null;
            var m = new Material(_cloud) { name = "UICloud " + (sprite != null ? sprite.name : "") };
            m.SetTexture(Noise, _noise);
            var tex = sprite != null ? sprite.texture : null;
            m.SetVector(TexSize, tex != null ? new Vector4(tex.width, tex.height, 0, 0) : new Vector4(1024, 512, 0, 0));
            m.SetVector(Billow, billow);
            m.SetVector(Ramp, ramp);
            m.SetVector(Scroll, new Vector4(0f, 0f, wrap ? 1f : 0f, 1f));
            owner?.Add(m);
            return m;
        }

        /// <summary>UI/Default that can be moved and spun by material properties.</summary>
        public static Material Float(CloudMaterialOwner owner, string name)
        {
            Find();
            if (_float == null) return null;
            var m = new Material(_float) { name = "UICloudFloat " + name };
            owner?.Add(m);
            return m;
        }

        /// <summary>The clouds' shared clock runs with <c>_Time</c>; SkyView nudges it so a storm's clouds
        /// churn faster and a drought's hang still, without a jump when the weather changes.</summary>
        public static void SetClock(float extraSeconds) { Shader.SetGlobalFloat(Clock, extraSeconds); }

        /// <summary>Light colour, shade colour and rim light for one cloud.</summary>
        public static void Paint(Material m, Color lit, Color shade, Color rim)
        {
            if (m == null) return;
            m.SetColor(Lit, lit);
            m.SetColor(Shade, shade);
            m.SetColor(Rim, rim);
        }

    }

    /// <summary>Destroys the cloud materials registered to it when its GameObject goes.</summary>
    public class CloudMaterialOwner : MonoBehaviour
    {
        readonly List<Material> _materials = new List<Material>();
        public void Add(Material m) { if (m != null) _materials.Add(m); }

        void OnDestroy()
        {
            foreach (var m in _materials)
                if (m != null) Destroy(m);
            _materials.Clear();
        }
    }
}
