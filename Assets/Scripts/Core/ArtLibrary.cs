using System.Collections.Generic;
using UnityEngine;

namespace LQFarm
{
    /// <summary>A mutation tier — which is also the mutation's IDENTITY.
    ///
    /// The spec asked for four rarity grades (thường / hiếm / cực hiếm / huyền thoại) and the
    /// build already had three named elements with painted artwork. Two designers independently
    /// proposed keeping those axes separate; that would give 4 x 3 = 12 variants per crop and a
    /// collection book of 364 cells — a wall nobody fills.
    ///
    /// So they are one axis. The grade is the LEVEL, the element name is the IDENTITY, and the
    /// reason to keep the names is the sentence that settled it:
    ///
    ///     "Băng Giá Khoai Tây" is something a player tells a friend about.
    ///     "Khoai Tây hiếm" is a statistic.
    ///
    /// Throwing away four named, illustrated, collectable identities to gain an abstract
    /// adjective is a loss. Only one new tier had to be invented — Ngọc Bích, the common one —
    /// and it costs nothing because it tints, like the twenty-seven crops that have no painted
    /// elemental art anyway.</summary>
    public class Element
    {
        public int id; public string key, name, shortName;
        /// <summary>Share of mutations that land on this tier. Conditional on mutating at all.</summary>
        public float chance;
        public float sell, xp, energy;
        /// <summary>Multiplier on grow time. A better mutation takes longer, which is what makes
        /// the glow on a freshly planted plot mean something.</summary>
        public float grow;
        /// <summary>Extra fruits at harvest. These do NOT multiply the coin value — see
        /// <see cref="PlayerState.HarvestValue"/> — they feed tribute and the collection book.</summary>
        public int yieldAdd;
        public Color glow; public bool hasGlow;

        public Element(int id, string key, string name, string shortName, float chance,
                       float sell, float xp, float energy, float grow, int yieldAdd, string glowHex)
        {
            this.id = id; this.key = key; this.name = name; this.shortName = shortName;
            this.chance = chance; this.sell = sell; this.xp = xp; this.energy = energy;
            this.grow = grow; this.yieldAdd = yieldAdd;
            hasGlow = !string.IsNullOrEmpty(glowHex);
            if (hasGlow) ColorUtility.TryParseHtmlString(glowHex, out glow);
        }

        /// <summary>"Hiếm", "Cực hiếm"… — the grade, as opposed to the identity.</summary>
        public string Grade
        {
            get
            {
                switch (id)
                {
                    case 1:  return "Thường";
                    case 2:  return "Hiếm";
                    case 3:  return "Cực hiếm";
                    case 4:  return "Huyền thoại";
                    default: return "";
                }
            }
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
        /// <summary>Produce icons, and shop items: an "item_" key lives in Art/items, drawn by
        /// Tools/gen_items.py. The shop used to borrow fruit here — a lime for the watering can.</summary>
        /// <summary>Anything in Art/items by its file name — chests, medals, the coin.</summary>
        public static Sprite Item(string id) { return Load("Art/items/" + id); }

        public static Sprite Crop(string id)
        {
            if (id != null && id.StartsWith("item_")) return Load("Art/items/" + id);
            return Load("Art/crop/" + id);
        }
        public static Sprite Ui(string id)   { return Load("Art/ui/" + id); }

        /// <summary>Weather glyphs. Drawn for this project (Tools/gen_weather.py) and kept white
        /// so they can be tinted per weather — tint multiplies, so the art has to be bright.</summary>
        /// <summary>The glyph for the weather as it looks right now: a clear night (sunny or dry
        /// weather after dark) shows the moon, not a blazing sun over a starry sky.</summary>
        public static Sprite WeatherIconNow(Weather w, out Color tint)
        {
            tint = Theme.Hex(WeatherSys.Def(w).hex);
            bool clear = w == Weather.Sunny || w == Weather.Drought;
            if (clear && DayCycle.Sample(DayCycle.Hour).night >= 0.5f)
            {
                tint = Theme.Hex("#F4EDC2");
                return Load("Art/weather/w_moon");
            }
            return WeatherIcon(w);
        }

        public static Sprite WeatherIcon(Weather w)
        {
            switch (w)
            {
                case Weather.Rain:    return Load("Art/weather/w_rain");
                case Weather.Wind:    return Load("Art/weather/w_wind");
                case Weather.Snow:    return Load("Art/weather/w_snow");
                case Weather.Storm:   return Load("Art/weather/w_storm");
                case Weather.Drought: return Load("Art/weather/w_drought");
                default:              return Load("Art/weather/w_sunny");
            }
        }

        // ---- plot tiles ----
        // Reverted to the original sheet. Tools/gen_tiles.py draws a geometrically exact
        // replacement, but the painted tiles carry stone texture, moss, scattered flowers
        // and — critically — the padlock baked into tile_locked. The drawn version lost all
        // of that and read as flat stickers, so it was a downgrade, not an upgrade.
        // The field draws Bed(), not these: the painted tiles carry a fence post at every
        // vertex and cannot touch their neighbours. See Tools/gen_beds.py. TileEmpty is kept as
        // an ICON (reward cards, the unlock panel) and now shows the same bed the field does.
        public static Sprite TileEmpty   => Bed("empty", 0);
        public static Sprite TileLocked  => Bed("locked", 0);
        public static Sprite TileWatered => Bed("watered", 0);
        public static Sprite TileReady   => Bed("ready", 0);

        public const int BedVariants = 3;

        /// <summary>A plot bed designed to tile edge to edge. Soil states come in three variants,
        /// picked by plot index, so a full field is not one picture stamped sixteen times.</summary>
        public static Sprite Bed(string key, int plotIndex)
        {
            if (key == "locked") return Load("Art/beds/bed_locked");
            if (key == "unclaimed") return Load("Art/beds/bed_unclaimed");
            int v = ((plotIndex % BedVariants) + BedVariants) % BedVariants;
            return Load("Art/beds/bed_" + key + "_" + v);
        }

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

        // ---- mutation tiers ----
        //
        // Weights are conditional on a mutation happening at all, so the absolute rates come out
        // of these times the level curve: at level 10 (p = 18%) legendary is 1 in 556 plantings;
        // at level 30 (p = 42%) it is 1 in 238. Rare enough to be worth a screenshot, common
        // enough to be met.
        //
        // Sell multipliers are the ONLY thing that scales coin value. The extra fruits are how a
        // tier is presented, not a second multiplier — see the no-double-dip note on
        // PlayerState.HarvestValue.
        public static readonly Element[] Elements =
        {
            //          key      name         short   chance  sell   xp    en    grow  +quả  glow
            new Element(0, "",      "Thường",    "",     0f,    1f,   1f,   1f,   1.0f,  0, null),
            new Element(1, "_jade", "Ngọc Bích", "Ngọc", 0.70f, 1.5f, 1.5f, 1.5f, 1.20f, 0, "#6fe0a8"),
            new Element(2, "_ice",  "Băng Giá",  "Băng", 0.22f, 2.5f, 2.2f, 2.0f, 1.40f, 1, "#6ec6ff"),
            new Element(3, "_fire", "Viêm Hoả",  "Hoả",  0.07f, 4.5f, 3.5f, 2.5f, 1.70f, 2, "#ff7a3a"),
            new Element(4, "_bolt", "Lôi Điện",  "Lôi",  0.01f, 10f,  6.0f, 3.0f, 2.20f, 3, "#ffd84a"),
        };

        /// <summary>Grow time is stretched by tier, but never by more than this in absolute terms.
        /// The tiers are ×1,2 to ×2,2, which is felt on a short crop (a legendary tomato: 10 → 22
        /// minutes) and would be a day and a half on a 24-hour one — so the extra is at most an hour,
        /// and the whole duration is still clamped to <see cref="GameData.MaxGrowSeconds"/>: a good
        /// roll is never a punishment for sleeping.</summary>
        public const float MaxMutationGrowAddSeconds = 60f * 60f;

        public static Element Elem(int v)
        {
            return (v >= 0 && v < Elements.Length) ? Elements[v] : Elements[0];
        }

        /// <summary>Crops drawn for this project, under Resources/Art/crops_gen. Three stages
        /// each, no per-element artwork — mutations tint them, the same as every crop outside
        /// the original painted set. Owned art, so these carry no provenance question.</summary>
        static readonly HashSet<string> Generated = new HashSet<string>
        { "wheat", "tomato", "corn", "watermelon", "strawberry", "peach", "grape", "mushroom", "eggplant", "pineapple",
          "pumpkin", "banana", "coconut", "orange", "apple" };

        public static bool IsGenerated(string art) { return Generated.Contains(art); }

        /// <summary>Crops whose sheet carries all four elements (3 painted stages).</summary>
        static readonly HashSet<string> Elemental = new HashSet<string> { "potato" };
        /// <summary>Crops with 4 painted stages but no elemental art.</summary>
        static readonly HashSet<string> Staged = new HashSet<string> { "carrot" };

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
            if (IsElemental(art) && v != 1) return Farm(art + Elem(v).key + "_" + Mathf.Min(stage + 1, 3));
            if (IsElemental(art)) return Farm(art + "_" + Mathf.Min(stage + 1, 3));
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
            if (IsElemental(art) && v != 1) return Farm(art + Elem(v).key + "_3");
            if (IsElemental(art)) return Farm(art + "_3");
            if (IsStaged(art))    return Farm(art + "_4");
            return Crop(art);
        }

        /// <summary>Crops without painted elemental art get tinted instead.</summary>
        static readonly Color[] Tint =
        {
            Color.white,
            // deeper than they were (0.62/0.63/0.40/0.42 floors): a light wash read as "a bit
            // pale", not as a different crop
            new Color(0.50f, 1.00f, 0.70f),   // ngọc bích
            new Color(0.52f, 0.78f, 1.00f),   // băng
            new Color(1.00f, 0.50f, 0.30f),   // hoả
            new Color(1.00f, 0.84f, 0.28f),   // lôi
        };

        public static Color VariantTint(string art, int v)
        {
            // Jade has no painted sheet anywhere, so even the one elemental crop tints for it.
            if (v <= 0) return Color.white;
            if (v == 1) return Tint[1];
            if (IsElemental(art)) return Color.white;
            return Tint[Mathf.Clamp(v, 0, Tint.Length - 1)];
        }
    }
}
