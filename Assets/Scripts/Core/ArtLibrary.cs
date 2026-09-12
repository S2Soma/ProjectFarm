using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    /// <summary>One of the four elements a crop can come up in.</summary>
    public class Element
    {
        public int id; public string key, name, shortName;
        public float chance, sell, xp, energy;
        public Color glow; public bool hasGlow;

        public Element(int id, string key, string name, string shortName, float chance,
                       float sell, float xp, float energy, string glowHex)
        {
            this.id = id; this.key = key; this.name = name; this.shortName = shortName;
            this.chance = chance; this.sell = sell; this.xp = xp; this.energy = energy;
            hasGlow = !string.IsNullOrEmpty(glowHex);
            if (hasGlow) ColorUtility.TryParseHtmlString(glowHex, out glow);
        }
    }

    /// <summary>Sprite registry. Everything lives under Resources/Art/.</summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Load(string path)
        {
            Sprite s;
            if (Cache.TryGetValue(path, out s)) return s;
            s = Resources.Load<Sprite>(path);
            Cache[path] = s;
            return s;
        }

        public static Sprite Farm(string id) { return Load("Art/farm/" + id); }
        public static Sprite Crop(string id) { return Load("Art/crop/" + id); }
        public static Sprite Ui(string id)   { return Load("Art/ui/" + id); }

        // ---- plot tiles ----
        // Reverted to the original sheet. Tools/gen_tiles.py draws a geometrically exact
        // replacement, but the painted tiles carry stone texture, moss, scattered flowers
        // and — critically — the padlock baked into tile_locked. The drawn version lost all
        // of that and read as flat stickers, so it was a downgrade, not an upgrade.
        public static Sprite TileEmpty   => Farm("tile_empty");
        public static Sprite TileLocked  => Farm("tile_locked");
        public static Sprite TileWatered => Farm("tile_watered");
        public static Sprite TileReady   => Farm("tile_ready");

        public static Sprite Tile(string key)
        {
            switch (key)
            {
                case "locked":  return TileLocked;
                case "ready":   return TileReady;
                case "watered": return TileWatered;
                default:        return TileEmpty;
            }
        }

        // ---- elements ----
        public static readonly Element[] Elements =
        {
            new Element(0, "",      "Thường",   "",     0f,    1f,   1f,   1f,   null),
            new Element(1, "_ice",  "Băng Giá", "Băng", 0.55f, 2.2f, 1.5f, 1.5f, "#6ec6ff"),
            new Element(2, "_fire", "Viêm Hoả", "Hoả",  0.30f, 3.5f, 2.0f, 2.0f, "#ff7a3a"),
            new Element(3, "_bolt", "Lôi Điện", "Lôi",  0.15f, 5.0f, 3.0f, 3.0f, "#ffd84a"),
        };

        public static Element Elem(int v)
        {
            return (v >= 0 && v < Elements.Length) ? Elements[v] : Elements[0];
        }

        /// <summary>Crops drawn for this project, under Resources/Art/crops_gen. Three stages
        /// each, no per-element artwork — mutations tint them, the same as every crop outside
        /// the original painted set. Owned art, so these carry no provenance question.</summary>
        static readonly HashSet<string> Generated = new HashSet<string>
        { "wheat", "tomato", "corn", "watermelon", "strawberry", "peach" };

        public static bool IsGenerated(string art) { return Generated.Contains(art); }

        /// <summary>Crops whose sheet carries all four elements (3 painted stages).</summary>
        static readonly HashSet<string> Elemental = new HashSet<string> { "potato" };
        /// <summary>Crops with 4 painted stages but no elemental art.</summary>
        static readonly HashSet<string> Staged = new HashSet<string> { "pumpkin", "carrot" };

        public static bool IsElemental(string art) { return Elemental.Contains(art); }
        public static bool IsStaged(string art)    { return Staged.Contains(art); }
        public static bool IsPainted(string art)   { return IsElemental(art) || IsStaged(art) || IsGenerated(art); }

        /// <summary>On-screen height per growth stage, in reference pixels.
        ///
        /// Capped at 78: the bed of the plot directly behind starts at +78.96 (two vertical
        /// steps up, less its own half-height), so anything taller grows straight through the
        /// neighbour's soil. The old 96/116 overhung it by 17 and 37px, which is what made
        /// full-grown bushes look like they were sprawling across other plots.</summary>
        public static readonly float[] StageHeight = { 44f, 60f, 72f, 78f };

        /// <summary>Plant art for a crop at growth stage 0..3 in an element.</summary>
        public static Sprite Plant(string art, int stage, int v)
        {
            if (IsGenerated(art)) return Load("Art/crops_gen/" + art + "_" + Mathf.Min(stage + 1, 3));
            if (IsElemental(art)) return Farm(art + Elem(v).key + "_" + Mathf.Min(stage + 1, 3));
            if (IsStaged(art))    return Farm(art + "_" + (stage + 1));
            // every other crop borrows the generic bush
            return Load("Art/crops_gen/tomato_" + Mathf.Min(stage + 1, 3));
        }

        /// <summary>Produce laid on the generic bush when ripe; null for painted crops.</summary>
        public static Sprite Fruit(string art) { return IsPainted(art) ? null : Crop(art); }

        /// <summary>Inventory / shop icon.</summary>
        public static Sprite Icon(string art, int v)
        {
            if (IsGenerated(art)) return Load("Art/crops_gen/" + art + "_3");
            if (IsElemental(art)) return Farm(art + Elem(v).key + "_3");
            if (IsStaged(art))    return Farm(art + "_4");
            return Crop(art);
        }

        /// <summary>Crops without painted elemental art get tinted instead.</summary>
        static readonly Color[] Tint =
        {
            Color.white,
            new Color(0.63f, 0.86f, 1.00f),   // băng
            new Color(1.00f, 0.62f, 0.40f),   // hoả
            new Color(1.00f, 0.90f, 0.42f),   // lôi
        };

        public static Color VariantTint(string art, int v)
        {
            if (v <= 0 || IsElemental(art)) return Color.white;
            return Tint[Mathf.Clamp(v, 0, 3)];
        }
    }
}
