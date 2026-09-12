# Spec gen art — Hướng A (thống nhất theo phong cách vẽ tay)

> Chốt ngày 2026-09-12. Mục tiêu: xoá cảm giác "ghép từ bảy nguồn".
> Chuẩn phong cách là **tile ruộng hiện có** (`Assets/Resources/Art/farm/tile_*.png`).

## Quy tắc bắt buộc — đọc trước khi gen

**1. Gen cả bộ trong MỘT lượt hội thoại.** Đây là bài học từ bộ cây trồng: gen một lượt
thì đồng bộ, gen lẻ từng cái thì mỗi cái một kiểu. Đây chính là nguyên nhân gốc của
"trông như AI ghép".

**2. Yêu cầu rõ "nền trong suốt thật" (true transparent PNG, alpha channel).**
Ba sheet cây trồng lần trước đều là RGB với **nền ca-rô vẽ vào pixel** — nhìn y hệt nền
trong suốt nhưng không phải. Tôi phải tự tách nền, và luôn có rủi ro ở viền mềm.

**3. KHÔNG nướng chữ vào ảnh.** Game là tiếng Việt, mọi chữ do code vẽ.
(Bộ `Hyper_Casual_UI` hỏng vì đúng lỗi này: 76/76 nút đều có chữ tiếng Anh in sẵn.)

**4. Với khung co giãn (9-slice): viền đều, GIỮA PHẲNG.**
Unity kéo giãn phần giữa. Nếu giữa có hoa văn/gradient/hoạ tiết thì kéo ra sẽ méo.
Bốn cạnh phải dày đều nhau. Đây là chỗ art gen hay hỏng nhất.

**5. Art phải là thiết kế gốc** — không mô phỏng nhân vật, logo hay giao diện của game nào.

## Mô tả phong cách (dán vào prompt)

> hand-painted 2D game UI, warm earthy palette (browns, cream, muted green),
> soft airbrush shading with visible brush texture, thick soft outline,
> slightly worn wooden and stone materials, cozy farm game, top-down/isometric friendly,
> transparent background, no text, no letters

Tránh: bóng loáng kiểu hyper-casual, gradient nhựa, viền đen cứng, màu neon.

---

## Đợt 1 — Khung giao diện (tác động lớn nhất)

Đây là phần bao quanh **mọi màn hình**, sửa được là đổi hẳn cảm giác.

| # | Asset | Kích thước | Yêu cầu |
|---|---|---|---|
| 1 | Khung panel | 512×512 | Gỗ/giấy da, **viền đều 96px**, giữa phẳng để kéo giãn |
| 2 | Khung lõm (well) | 512×512 | Như trên, tông tối hơn — vùng chứa danh sách |
| 3 | Thanh tiêu đề | 512×160 | Gỗ đậm, viền trái/phải 96px, giữa phẳng |
| 4 | Nút bấm ×5 màu | 384×128 | Có **gờ dày ở cạnh dưới**. Màu: xanh lá / vàng đất / xanh dương / đỏ / xám. Viền trái-phải 64px, trên 40px, dưới 56px, giữa phẳng |

## Đợt 2 — Bộ icon (gen chung MỘT ảnh)

Gen thành **một sheet duy nhất**, lưới đều, nền trong suốt. Như vậy chắc chắn đồng bộ.

Cần 14 icon: cửa hàng, rương, nhiệm vụ, hạt giống, kho, bạn bè, bộ sưu tập,
đồng xu, ngôi sao, ổ khoá, dấu tích, giọt nước, dấu chấm than, vương miện.

Mỗi ô ~256×256, vẽ đơn giản đủ đọc ở cỡ **40px** trên màn hình.

## Đợt 3 — Nút tròn + thanh tiến độ

| Asset | Kích thước | Yêu cầu |
|---|---|---|
| Nút tròn ×5 màu | 256×256 | Đĩa tròn có gờ nổi, cùng 5 màu với nút chữ nhật |
| Rãnh thanh bar | 128×48 | Viền trái/phải 24px, giữa phẳng |
| Ruột thanh bar ×3 | 128×48 | Xanh lá / xanh dương / vàng. Cùng quy cách viền |

## Đợt 4 — Nền (làm sau cùng)

Mây và đồi xa hiện dùng Kenney phẳng pastel, chỏi với art vẽ tay.
Cần: 4–6 đám mây, 1 mặt trời, 1–2 dải đồi xa. Nền trong suốt.

---

## Cách bàn giao

Bỏ file vào `Assets/Resources/Art/handpaint/`, báo tên. Tôi lo phần còn lại:
cắt sheet, sinh `.meta`, đặt border 9-slice, trỏ `Theme.Skin` sang bộ mới, chụp đối chiếu.

Thay được từng đợt — không cần đủ cả bộ mới ghép được.

## Cái gì KHÔNG cần gen

- **Cây trồng**: đã xong 6 loại, giữ nguyên
- **Hòn đảo**: code vẽ, hợp tông, giữ
- **Bóng đổ / quầng sáng / vignette / gradient trời**: code vẽ tốt hơn ảnh
  (không vỡ khi phóng to, tự co giãn theo tỉ lệ màn hình)
- **Tile ruộng + vật trang trí**: đang là chuẩn phong cách, giữ nguyên.
  Tôi đã thử vẽ lại bằng code và **thất bại** — xem `TASKS.md` Task 4.
