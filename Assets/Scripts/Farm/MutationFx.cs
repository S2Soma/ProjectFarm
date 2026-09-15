using UnityEngine;
using UnityEngine.UI;

namespace LQFarm
{
    /// <summary>The dressing on a mutated crop, from the moment it is planted.
    ///
    /// The tier is rolled at plant, and it used to show only at harvest: three stages of a plain
    /// green sprout and then, at the last second, a tint. A player who bought the mutation charm
    /// had nothing to look at for the whole wait. Now the plot says it the whole time, louder
    /// with each tier:
    ///
    ///   Ngọc Bích   a glowing ring on the soil and a soft aura
    ///   Băng Giá    + slow light rays behind the plant, two motes rising
    ///   Viêm Hoả    + faster rays, four motes
    ///   Lôi Điện    + six motes and the aura flickering like a charge
    ///
    /// Everything is grey art (Tools/gen_fx.py) tinted with the element's colour. The plot
    /// canvas already redraws every frame for the sway and the rim lights, so animating here adds
    /// no canvas rebuild of its own; a plot with no mutation disables the component.</summary>
    public class MutationFx : MonoBehaviour
    {
        Image _halo, _rays, _aura;
        Image[] _motes;
        float[] _moteAge;
        Vector2[] _moteFrom;
        int _tier;
        float _height, _phase;
        Color _col;

        const int MaxMotes = 6;

        /// <summary>Lay the layers into a plot body. <paramref name="aura"/> is the plot's existing
        /// glow image (behind the crop); the halo goes on the soil, the rays behind the aura, the
        /// motes in front of the plant.</summary>
        public static MutationFx Build(RectTransform body, Image aura, Image crop, Vector2 soilCentre, Vector2 soilSize, float phase)
        {
            var fx = body.gameObject.AddComponent<MutationFx>();
            fx._phase = phase;
            fx._aura = aura;

            // every layer is LIGHT (additive): an alpha glow laid a pale film over the soil and the crop
            var add = MutationTint.AdditiveMaterial;
            aura.material = add;
            fx._halo = UIKit.Img(body, Art.Load("Art/fx/mut_halo"), Color.clear, "mutHalo");
            fx._halo.material = add;
            fx._halo.raycastTarget = false;
            fx._halo.rectTransform.Anchor(UIKit.Center, soilCentre, soilSize);
            fx._halo.transform.SetSiblingIndex(aura.transform.GetSiblingIndex());

            fx._rays = UIKit.Img(body, Art.Load("Art/fx/mut_rays"), Color.clear, "mutRays");
            fx._rays.material = add;
            fx._rays.raycastTarget = false;
            fx._rays.transform.SetSiblingIndex(aura.transform.GetSiblingIndex());

            var spark = Art.Load("Art/fx/mut_spark");
            fx._motes = new Image[MaxMotes];
            fx._moteAge = new float[MaxMotes];
            fx._moteFrom = new Vector2[MaxMotes];
            int after = crop.transform.GetSiblingIndex() + 1;
            for (int i = 0; i < MaxMotes; i++)
            {
                var m = UIKit.Img(body, spark, Color.clear, "mote");
                m.material = add;
                m.raycastTarget = false;
                m.rectTransform.anchorMin = m.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                m.rectTransform.sizeDelta = new Vector2(22, 22);
                m.transform.SetSiblingIndex(after + i + 1);
                m.gameObject.SetActive(false);
                fx._motes[i] = m;
                fx._moteAge[i] = -((i * 0.37f + phase) % 1.6f);      // staggered start
            }
            fx.Set(0, 0f, Color.white);
            return fx;
        }

        /// <summary>Show tier <paramref name="tier"/> on a crop <paramref name="height"/> tall.</summary>
        public void Set(int tier, float height, Color glow)
        {
            _tier = tier; _height = height; _col = glow;
            bool on = tier > 0;
            enabled = on;
            _halo.gameObject.SetActive(on);
            _rays.gameObject.SetActive(tier >= 2);
            if (!on)
            {
                _aura.color = new Color(1, 1, 1, 0);
                foreach (var m in _motes) m.gameObject.SetActive(false);
                return;
            }

            float aura = (40f + height * 0.8f) * (1f + 0.10f * tier);
            _aura.rectTransform.anchoredPosition = new Vector2(0, height * 0.42f);
            _aura.rectTransform.sizeDelta = new Vector2(aura, aura);
            _rays.rectTransform.anchorMin = _rays.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rays.rectTransform.anchoredPosition = _aura.rectTransform.anchoredPosition;
            _rays.rectTransform.sizeDelta = Vector2.one * aura * 1.55f;

            int motes = tier >= 4 ? 6 : tier == 3 ? 4 : tier == 2 ? 2 : 0;
            for (int i = 0; i < MaxMotes; i++) _motes[i].gameObject.SetActive(i < motes);
            Tick(0f);
        }

        void Update() { Tick(Time.unscaledDeltaTime); }

        void Tick(float dt)
        {
            if (_tier <= 0) return;
            float t = Time.unscaledTime + _phase;
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);

            // Lôi Điện flickers: a quick bright blink every second or so on top of the breathing
            float flick = 0f;
            if (_tier >= 4)
            {
                float u = Mathf.Repeat(t * 0.9f, 1f);
                flick = u < 0.06f ? 1f - u / 0.06f : 0f;
            }

            // additive now: these alphas are amounts of light, about half what an alpha glow needed
            _halo.color = _col.Alpha((0.22f + 0.07f * _tier) * (0.65f + 0.35f * breathe) + 0.2f * flick);
            _aura.color = _col.Alpha(Mathf.Clamp01((0.14f + 0.05f * _tier) * (0.7f + 0.3f * breathe) + 0.25f * flick));

            if (_tier >= 2)
            {
                _rays.rectTransform.localRotation = Quaternion.Euler(0, 0, -t * (8f + 10f * (_tier - 2)));
                _rays.color = _col.Alpha((0.10f + 0.05f * _tier) * (0.75f + 0.25f * breathe) + 0.12f * flick);
            }

            // motes: rise from the foliage to above the plant over 1.6 s, fading in and out
            const float life = 1.6f;
            for (int i = 0; i < MaxMotes; i++)
            {
                var m = _motes[i];
                if (!m.gameObject.activeSelf) continue;
                _moteAge[i] += dt;
                if (_moteAge[i] < 0f) { m.color = Color.clear; continue; }
                if (_moteAge[i] >= life || _moteFrom[i] == Vector2.zero)
                {
                    _moteAge[i] = _moteAge[i] >= life ? 0f : _moteAge[i];
                    float w = 26f + _height * 0.35f;
                    _moteFrom[i] = new Vector2(Random.Range(-w, w), Random.Range(_height * 0.1f, _height * 0.6f) + 0.01f);
                }
                float k = _moteAge[i] / life;
                var rt = m.rectTransform;
                rt.anchoredPosition = _moteFrom[i] + new Vector2(Mathf.Sin(k * 6.2f + i) * 5f, k * (40f + _height * 0.4f));
                float size = (14f + 4f * _tier) * (0.6f + 0.4f * Mathf.Sin(k * Mathf.PI));
                rt.sizeDelta = new Vector2(size, size);
                m.color = Color.Lerp(Color.white, _col, 0.35f).Alpha(Mathf.Sin(k * Mathf.PI));
            }
        }
    }
}
