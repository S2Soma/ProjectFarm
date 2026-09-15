using System.Collections.Generic;

namespace LQFarm
{
    /// <summary>Where a cosmetic shows. Appended only: the value's NAME is the key in the save
    /// (<c>social.equipped</c>), so reordering would not lose anything, but the shop's filter row
    /// follows this order.</summary>
    public enum CosmeticSlot { Frame, Badge, Harvest, Plant, Decor, Plot, Water, Toast, Tap, Swipe }

    public class Cosmetic
    {
        public string id, name, desc, art;
        public CosmeticSlot slot;
        public int price;
        public Cosmetic(string id, string name, string desc, CosmeticSlot slot, string art, int price)
        { this.id = id; this.name = name; this.desc = desc; this.slot = slot; this.art = art; this.price = price; }
    }

    /// <summary>The Trang trí shelf: things bought once and worn, and every one of them visible.
    ///
    /// The shelf used to sell nine placeholders ("Khung ảnh Mùa Vàng" under a cheese icon) that did
    /// nothing once bought. Each item now has a slot and changes something the player sees every
    /// session: the frame round their portrait, a badge by their name, what flies off a plot when
    /// they harvest or plant, or a planting in the yard of the home island.
    ///
    /// One item per slot is worn at a time. Buying wears it; tapping an owned item in the shop
    /// wears it or takes it off. Wearing is pure presentation — no cosmetic touches a price, a
    /// grow time or a chance — so the shop's UNIT pricing rules do not apply to this shelf.</summary>
    public static class Cosmetics
    {
        public static readonly Cosmetic[] All =
        {
            new Cosmetic("fr_silver", "Khung Bạc",        "Khung ảnh đại diện", CosmeticSlot.Frame,   "frame_silver", 8800),
            new Cosmetic("fr_gold",   "Khung Mùa Vàng",   "Khung ảnh đại diện", CosmeticSlot.Frame,   "frame_gold",   19999),
            new Cosmetic("fr_jade",   "Khung Ngọc Bích",  "Khung ảnh đại diện", CosmeticSlot.Frame,   "frame_jade",   29999),
            new Cosmetic("bd_farmer", "Huy hiệu Nhà Nông","Cạnh tên của bạn",   CosmeticSlot.Badge,   "badge_farmer", 9900),
            new Cosmetic("bd_star",   "Huy hiệu Ngôi Sao","Cạnh tên của bạn",   CosmeticSlot.Badge,   "badge_star",   14900),
            new Cosmetic("fx_leaves", "Lá Bay",           "Khi thu hoạch",      CosmeticSlot.Harvest, "fx_leaves",    19999),
            new Cosmetic("fx_petals", "Cánh Hoa",         "Khi gieo hạt",       CosmeticSlot.Plant,   "fx_petals",    12000),
            new Cosmetic("dc_roses",  "Bụi Hồng",         "Trang trí Vườn Nhà", CosmeticSlot.Decor,   "decor_roses",  3500),
            new Cosmetic("dc_sunflowers", "Hướng Dương",  "Trang trí Vườn Nhà", CosmeticSlot.Decor,   "decor_sunflowers", 6600),

            // ô đất: a border on every bed of the farm (Art/beds/skin_*, Tools/gen_cosmetics.py)
            new Cosmetic("pk_wood",    "Viền Gỗ Mộc",     "Viền mọi ô đất",     CosmeticSlot.Plot,    "cos_pk_wood",    9900),
            new Cosmetic("pk_stone",   "Viền Đá Cuội",    "Viền mọi ô đất",     CosmeticSlot.Plot,    "cos_pk_stone",   14900),
            new Cosmetic("pk_flower",  "Viền Hoa Nhí",    "Viền mọi ô đất",     CosmeticSlot.Plot,    "cos_pk_flower",  24900),
            new Cosmetic("pk_crystal", "Viền Pha Lê",     "Viền mọi ô đất",     CosmeticSlot.Plot,    "cos_pk_crystal", 39900),
            new Cosmetic("pk_gold",    "Viền Hoàng Kim",  "Viền mọi ô đất",     CosmeticSlot.Plot,    "cos_pk_gold",    59900),

            new Cosmetic("pl_stars",    "Sao Lấp Lánh",   "Khi gieo hạt",       CosmeticSlot.Plant,   "cos_pl_stars",    9900),
            new Cosmetic("pl_sprout",   "Chồi Non",       "Khi gieo hạt",       CosmeticSlot.Plant,   "cos_pl_sprout",   9900),
            new Cosmetic("pl_bubbles",  "Bong Bóng",      "Khi gieo hạt",       CosmeticSlot.Plant,   "cos_pl_bubbles",  14900),
            new Cosmetic("pl_firework", "Pháo Hoa Nhỏ",   "Khi gieo hạt",       CosmeticSlot.Plant,   "cos_pl_firework", 24900),

            new Cosmetic("wt_bubbles",  "Bong Bóng Xà Phòng", "Khi tưới cây",   CosmeticSlot.Water,   "cos_wt_bubbles",  9900),
            new Cosmetic("wt_flower",   "Mưa Hoa",        "Khi tưới cây",       CosmeticSlot.Water,   "cos_wt_flower",   14900),
            new Cosmetic("wt_diamond",  "Giọt Kim Cương", "Khi tưới cây",       CosmeticSlot.Water,   "cos_wt_diamond",  24900),
            new Cosmetic("wt_rainbow",  "Cầu Vồng",       "Khi tưới cây",       CosmeticSlot.Water,   "cos_wt_rainbow",  34900),

            new Cosmetic("hv_confetti", "Pháo Giấy",      "Khi thu hoạch",      CosmeticSlot.Harvest, "cos_hv_confetti", 14900),
            new Cosmetic("hv_stars",    "Sao Băng",       "Khi thu hoạch",      CosmeticSlot.Harvest, "cos_hv_stars",    24900),
            new Cosmetic("hv_coins",    "Mưa Xu",         "Khi thu hoạch",      CosmeticSlot.Harvest, "cos_hv_coins",    34900),

            new Cosmetic("ts_wood",  "Thông Báo Gỗ",      "Khung thông báo",    CosmeticSlot.Toast,   "cos_ts_wood",  6900),
            new Cosmetic("ts_candy", "Thông Báo Kẹo Dâu", "Khung thông báo",    CosmeticSlot.Toast,   "cos_ts_candy", 9900),
            new Cosmetic("ts_night", "Thông Báo Đêm Sao", "Khung thông báo",    CosmeticSlot.Toast,   "cos_ts_night", 14900),
            new Cosmetic("ts_gold",  "Thông Báo Hoàng Kim", "Khung thông báo",  CosmeticSlot.Toast,   "cos_ts_gold",  24900),

            new Cosmetic("tp_ring",  "Vòng Sóng",         "Khi chạm màn hình",  CosmeticSlot.Tap,     "cos_tp_ring",  6900),
            new Cosmetic("tp_leaf",  "Lá Xanh",           "Khi chạm màn hình",  CosmeticSlot.Tap,     "cos_tp_leaf",  9900),
            new Cosmetic("tp_star",  "Ngôi Sao",          "Khi chạm màn hình",  CosmeticSlot.Tap,     "cos_tp_star",  12900),
            new Cosmetic("tp_heart", "Trái Tim",          "Khi chạm màn hình",  CosmeticSlot.Tap,     "cos_tp_heart", 14900),

            new Cosmetic("sw_petals",  "Vệt Cánh Hoa",    "Khi vuốt màn hình",  CosmeticSlot.Swipe,   "cos_sw_petals",  12900),
            new Cosmetic("sw_stars",   "Vệt Sao",         "Khi vuốt màn hình",  CosmeticSlot.Swipe,   "cos_sw_stars",   14900),
            new Cosmetic("sw_rainbow", "Vệt Cầu Vồng",    "Khi vuốt màn hình",  CosmeticSlot.Swipe,   "cos_sw_rainbow", 24900),
            new Cosmetic("sw_fairy",   "Bụi Tiên",        "Khi vuốt màn hình",  CosmeticSlot.Swipe,   "cos_sw_fairy",   34900),
        };

        public static readonly string[] SlotNames =
            { "Khung ảnh", "Huy hiệu", "Thu hoạch", "Gieo hạt", "Vườn Nhà", "Ô đất", "Tưới cây", "Thông báo", "Chạm", "Vuốt" };

        public static Cosmetic Get(string id)
        {
            foreach (var c in All) if (c.id == id) return c;
            return null;
        }

        public static bool Owns(PlayerState s, string id) { return s.shopBought.Contains(id); }

        static readonly Dictionary<CosmeticSlot, string> _preview = new Dictionary<CosmeticSlot, string>();

        /// <summary>For the screenshot pass: show an item as worn without owning it and without
        /// touching the save. Cleared by <see cref="ClearPreview"/>.</summary>
        public static void Preview(string id) { var c = Get(id); if (c != null) _preview[c.slot] = id; }
        public static void ClearPreview() { _preview.Clear(); }

        public static string Worn(PlayerState s, CosmeticSlot slot)
        {
            if (_preview.TryGetValue(slot, out var p)) return p;
            return s.equipped.TryGetValue(slot.ToString(), out var id) && Owns(s, id) ? id : null;
        }

        public static bool IsWorn(PlayerState s, string id)
        {
            var c = Get(id);
            return c != null && Worn(s, c.slot) == id;
        }

        /// <summary>Wear an owned item, replacing whatever was in its slot. False if not owned.</summary>
        public static bool Wear(PlayerState s, string id)
        {
            var c = Get(id);
            if (c == null || !Owns(s, id)) return false;
            s.equipped[c.slot.ToString()] = id;
            return true;
        }

        public static void TakeOff(PlayerState s, CosmeticSlot slot) { s.equipped.Remove(slot.ToString()); }

        /// <summary>Buy and wear. False if already owned or not affordable (nothing changes).</summary>
        public static bool Buy(PlayerState s, string id)
        {
            var c = Get(id);
            if (c == null || Owns(s, id) || s.coin < c.price) return false;
            s.AddCoin(-c.price);
            s.shopBought.Add(id);
            Wear(s, id);
            return true;
        }

        /// <summary>The placeholder shelf sold s1-s9. The five that became real items carry over to
        /// whoever bought them; the rest (a mystery gem, a mystery chest, an emote, a portrait)
        /// had no item to become and are simply no longer sold.</summary>
        static readonly Dictionary<string, string> Legacy = new Dictionary<string, string>
        {
            { "s1", "fr_gold" }, { "s5", "fr_silver" }, { "s8", "bd_farmer" }, { "s2", "fx_leaves" }, { "s9", "dc_roses" },
        };

        public static void MigrateLegacy(PlayerState s)
        {
            foreach (var kv in Legacy)
                if (s.shopBought.Contains(kv.Key) && !s.shopBought.Contains(kv.Value))
                    s.shopBought.Add(kv.Value);
        }
    }
}
