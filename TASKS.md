# LQ Farm — việc đang làm

> ## ⚠️ 2026-09-12 — RE-DESIGN LỚN. Đọc `REDESIGN.md` TRƯỚC.
> Toàn bộ file này nói về bản game cũ (1 đảo, 16 ô, không thời tiết/tag).
> Plan mới đã chốt hướng: 6 đảo trên một canvas pan/zoom, thời tiết 6 trạng thái đổi mỗi giờ,
> tag cây xoay 12 giờ, 4 bậc đột biến, tưới theo lượt, nhiệm vụ có hạng, shop vật phẩm giữ lại.
> Lộ trình 11 bước ở `REDESIGN.md` §7. Bước 0 (tách `GS` thành `PlayerState`) **đã xong**. Bước tiếp theo: **bước 1 — đổi serialiser sang Newtonsoft**.
> Các task bên dưới vẫn đúng ở phần art và dọn thư mục; phần gameplay đã bị plan mới thay thế.

> **Đã chốt hướng A (2026-09-12):** thống nhất toàn bộ art theo phong cách **vẽ tay**,
> lấy tile ruộng làm chuẩn. Spec đầy đủ ở **`ART_SPEC.md`**.
> Lý do: đang có **7 nguồn art** hiển thị cùng lúc trên một màn hình — đó mới là thứ
> làm game "trông như AI ghép", chứ không phải bản thân ảnh AI.
> **Tôi không gen được ảnh** — phần gen do người dùng làm, tôi lo spec + ghép.

> File này để giữ mạch việc khi mất context. Cập nhật trạng thái ngay khi xong từng mục.

## Bối cảnh quyết định (2026-09-12)

Người dùng đưa 4 ảnh AI gen + bộ `Assets/Hyper_Casual_UI` (197 file) và để tôi tự quyết.

**Đã quyết:**
- ✅ LÀM: cắt 3 sheet cây trồng ChatGPT vào game
- ✅ LÀM: đổi icon "Rương" (đang là bình thí nghiệm, không ra rương)
- ❌ BỎ: đổi toàn bộ UI sang Hyper_Casual_UI

**Vì sao bỏ bộ UI:** cả 76 nút đều nướng sẵn chữ tiếng Anh vào ảnh → game tiếng Việt
không dùng được cái nào. Phong cách hyper casual (bóng, gradient đậm, viền vàng kim)
cũng chỏi với art vẽ tay ấm hiện tại. Trộn hai phong cách sẽ xấu hơn cả hai.
Phần dùng được: thư mục `Icons` (140 file, trung tính ngôn ngữ) và ~30 khung panel trống.

**Ảnh Gemini (nền mây kẹo):** không dùng — sai tông, lại bo góc sẵn nên không phải nền tràn viền.

---

## Task 1 — Thay art cây trồng bằng sheet tự gen  🟢 XONG

Mục tiêu kép: đẹp hơn **và** gỡ rủi ro bản quyền của `Art/farm`.

Nguồn: 3 file ở thư mục gốc dự án
- `ChatGPT Image Sep 12, 2026, 03_04_21 PM.png` — mầm, dây leo, dưa hấu, kiwi, thanh long, dâu tây, ngô, táo
- `ChatGPT Image Sep 12, 2026, 03_04_37 PM.png` — dưa chuột, xương rồng, sen đá
- `ChatGPT Image Sep 12, 2026, 03_04_45 PM.png` — lúa mì, huệ, cà chua, hướng dương

- [x] 1.1 ~~Dò bounding box bằng alpha~~ → **3 sheet là RGB, KHÔNG có kênh alpha.**
      Nền ca-rô là hình *vẽ* vào pixel (màu 238–254, xám trung tính), không phải trong suốt.
      Phải tách nền trước. Xem mục "Bài học" bên dưới.
- [x] 1.2 Tách nền bằng **tô loang từ viền ảnh vào trong**, rồi tìm vùng liên thông
- [x] 1.3 Cắt được **44 sprite** → `/tmp/slices/` (tạm)
- [x] 1.4 Xuất lúa mì + cà chua (3 giai đoạn mỗi cây) vào `Assets/Resources/Art/crops_gen/` + meta
- [x] 1.5 `ArtLibrary`: thêm tập `Generated`, lúa mì + cà chua + **toàn bộ cây generic** dùng bộ mới
- [x] 1.6 Đã chụp đối chiếu — lúa mì mới hiển thị đúng, nằm gọn trong luống

- [x] **Đợt 2 xong:** ngô, dưa hấu, dâu tây, đào từ sheet `03_04_21`.
      Sheet đó xếp **2 cây mỗi hàng, mỗi cây 3 giai đoạn** (cột 1-3 và cột 4-6) —
      *không* phải mỗi cột một cây như sheet `03_04_45`. Phải dựng lại bảng theo
      đúng toạ độ lưới mới đọc ra, xếp theo tên file sẽ ghép nhầm giai đoạn.

**Hiện có 6 cây art riêng:** lúa mì, cà chua, ngô, dưa hấu, dâu tây, đào (18 sprite).
Ngô đã chuyển khỏi `STAGED` (4 giai đoạn vẽ tay) sang bộ mới 3 giai đoạn.
Còn `STAGED`: bí ngô, cà rốt. Còn `ELEMENTAL`: khoai tây.
Script cắt: `scratchpad/slice.py` (chạy lại được).

**Đánh đổi đã chấp nhận:** lúa mì + cà chua bị bỏ khỏi tập `ELEMENTAL`, nên biến thể
băng/hoả/lôi giờ dùng **tô màu** thay vì art vẽ riêng. Mất art biến thể vẽ tay, đổi lại
art thuộc sở hữu và đồng bộ với 22 cây khác vốn đã dùng tô màu. Khoai tây vẫn `ELEMENTAL`.

**Đã biết trước:**
- Game cần 4 giai đoạn painted cho: bí ngô, ngô, cà rốt (`STAGED`)
- và 3 giai đoạn × 4 nguyên tố cho: lúa mì, khoai tây, cà chua (`ELEMENTAL`)
- Sheet có: lúa mì, cà chua, ngô, dâu tây, dưa hấu → khớp 5 cây
- **Thiếu: cà rốt, khoai tây, bí ngô** → cần gen thêm hoặc giữ art cũ
- Sheet không có biến thể băng/hoả/lôi. Nếu chuyển lúa mì + cà chua khỏi `ELEMENTAL`
  sang `STAGED` thì biến thể sẽ dùng tô màu (`Art.VariantTint`) thay vì art vẽ riêng.
  Chấp nhận được, và đồng bộ với các cây khác.

## Task 2 — Đổi icon Rương  🟢 XONG

- [x] Lấy `Gold Treasure box.png` + `Opened Gold Treasure box.png` → `Art/ui2/chest.png`, `chest_open.png`
- [x] Dùng ở **thanh nhanh "Rương"** và **ảnh rương trong panel**
- [x] **Giữ bình năng lượng** (`nav_magic`) cho chip năng lượng — đó là "năng lượng kỳ diệu",
      không phải rương, nên dùng bình đúng nghĩa hơn
- `chest_open` đã nhập nhưng **chưa dùng** — để dành cho hiệu ứng mở rương

## Task 4 — Tile ruộng + vật trang trí  🔴 ĐÃ THỬ, ĐÃ HOÀN TÁC

**Vẽ tile bằng code: thất bại. Đã trả lại art gốc.**

`Tools/gen_tiles.py` sinh ra 4 tile đúng hình học tuyệt đối (thoi 2:1, viền nổi, chân đất,
vân luống cày). Trên ảnh preview nhìn ổn. Nhưng trong game thì **tệ hơn art gốc**:

1. **Mất ổ khoá** — `tile_locked.png` gốc **vẽ sẵn ổ khoá vào ảnh**. Thay tile là mất
   luôn dấu hiệu ô bị khoá. Đây là lỗi *chức năng*, không chỉ thẩm mỹ.
2. Viền dày trên preview nhưng ở cỡ thật (tile rộng 168px) thì mỏng tang, luống mất khối.
3. Thành các hình thoi nhợt nhạt dán trên cỏ.

**Vì sao khác với các lần vẽ code trước:** đảo, avatar, icon đều là **hình khối trơn** —
code vẽ tốt. Tile ruộng thì mang **chất liệu vẽ tay**: vân đá, rêu, hoa nhỏ rải rác, ổ khoá.
Code không đuổi kịp ở mức chất lượng này.

File sinh vẫn giữ ở `Assets/Resources/Art/tiles/` và `Tools/gen_tiles.py` để tham khảo,
nhưng **không được dùng**.

**Hướng nên đi:** để bạn gen tile bằng AI như đã làm với cây trồng — AI xử lý chất liệu
vẽ tay tốt. Cần 4 ảnh, **thoi 2:1**, thoi chiếm 380/390 bề ngang, trục ngang ở 47,5% tính
từ đáy, có chân đất đổ xuống. Nhớ yêu cầu **nền trong suốt thật**.
Nếu tile khoá không vẽ ổ khoá thì báo tôi, tôi phủ icon khoá riêng bằng code (thực ra
cách đó đúng hơn — ổ khoá là UI, không nên nướng vào art).

**Vật trang trí (hàng rào, đèn, biển, cờ, đá, đống rơm): chưa đụng tới.**

## Task 3 — Việc còn treo từ trước  🟡 CÒN 3 MỤC

**Chặn:** mục cà rốt/khoai tây/bí ngô cần bạn gen ảnh — tôi không có công cụ sinh ảnh.

- [ ] Gen thêm **cà rốt / khoai tây / bí ngô** — 3 cây còn dùng art gốc.
      Nhớ yêu cầu **nền trong suốt thật** trong prompt (xem "Bài học" bên dưới)
- [x] ~~Thanh nhanh phải đè đống rơm~~ — tự khỏi khi đổi kích thước đảo/lưới,
      giờ hai thứ nằm cạnh nhau chứ không chồng. Đã đối chiếu bằng ảnh.
- [x] **3 icon gốc cuối đã thay xong.** Không còn dòng code nào gọi `Art.Ui(...)`.
      - badge chín → dấu tích, **tự vẽ** (`Tools/gen_icons.py`)
      - nhắc tưới → giọt nước, **tự vẽ** — không bộ CC0 nào có hình này
      - chip nhiệm vụ → dấu chấm than, **tự vẽ**
      Lý do tự vẽ thay vì lấy sẵn: dấu tích của bộ UI là nét mảnh, mất hút ở cỡ badge;
      dấu chấm than của Kenney chỉ có **8×16 pixel**, phóng lên là vỡ.
- [ ] Âm thanh (Kenney có bộ SFX CC0)
- [ ] Build thử lên máy thật để kiểm tra safe area (Editor luôn báo safe area = cả màn hình)

---

## Bài học về ảnh AI gen

**Kiểm tra `im.mode` trước khi tin là có nền trong suốt.** Cả 3 sheet ChatGPT đều là `RGB`,
nền ca-rô được *vẽ* vào pixel. Nhìn bằng mắt thì y hệt nền trong suốt.

**Tách nền phải tô loang từ viền, không được key theo màu.** Sheet `03_04_45` có hoa huệ
trắng — key màu trắng sẽ xoá sạch cánh hoa. Tô loang từ mép ảnh chỉ ăn phần nền nối với
viền, nên màu trắng nằm trong lòng cây vẫn còn nguyên. Đã kiểm chứng bằng ảnh.

Khi gen thêm, **yêu cầu rõ nền trong suốt thật** để khỏi phải tách.

## Quy trình kiểm chứng (đừng bỏ qua)

Mọi thay đổi giao diện phải **nhìn ảnh thật** rồi mới kết luận. Nhiều lỗi chỉ lộ ra khi nhìn:
huy hiệu đè ô bên cạnh, chữ trắng trên nền vàng, icon trắng vô hình trên nền kem.

```
Unity_ManageEditor Stop → ManageMenuItem "Assets/Refresh" → đợi bridge
→ Play → đợi bridge → ManageMenuItem "Tools/LQ Farm/Chụp toàn bộ giao diện"
→ đọc Screenshots/*.png
```

Bridge **rớt sau mỗi lần domain reload** (recompile và vào/ra Play mode) rồi tự lên lại
sau ~15-20 giây. Đợi `~/.unity/mcp/connections/*.json` xuất hiện lại trước khi gọi tiếp.
Gọi lúc bridge đang rớt sẽ **treo đúng 30 phút** rồi mới báo lỗi.

Compile không cần chiếm Editor: xem `CLAUDE.md`.
