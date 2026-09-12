# LQ Farm — bản Unity

Port của bản web `Clone LQ Farm` (HTML/CSS/JS) sang Unity 6 (uGUI, mobile landscape).
Toàn bộ giao diện được **thiết kế lại** và dựng bằng code — không cần kéo thả gì trong Editor.

## Chạy thử

Mở `Assets/Scenes/SampleScene.unity` rồi bấm Play. Scene không cần GameObject nào:
`GameApp.Boot()` gắn `[RuntimeInitializeOnLoadMethod]` nên tự dựng Canvas, nông trại,
HUD và EventSystem khi game khởi động.

## Cấu trúc

| File | Vai trò |
|---|---|
| `Core/GameData.cs` | Toàn bộ nội dung tĩnh: 28 hạt giống, rương, chương nhiệm vụ, nhiệm vụ ngày, cửa hàng, bạn bè, bộ sưu tập, công thức cấp độ |
| `Core/GameState.cs` | Trạng thái game, kinh tế, nhiệm vụ, lưu/đọc JSON vào `Application.persistentDataPath` |
| `Core/ArtLibrary.cs` | Nạp sprite từ `Resources/Art/`, bảng 4 nguyên tố và art theo giai đoạn sinh trưởng |
| `Farm/FarmView.cs` | Ruộng isometric 4×4, đảo cỏ, vật trang trí, gieo/tưới/thu hoạch và hiệu ứng |
| `UI/Theme.cs` | Bảng màu + chrome sinh tự động (bo góc, đổ bóng, vòng tròn, gradient, glow) |
| `UI/UIKit.cs` | Bộ widget: nút, chip, thanh tiến độ, tab, scroll list/grid |
| `UI/Tween.cs` | Hoạt ảnh chạy bằng coroutine (pop, đếm số, bay theo cung, rung, lấp lánh) |
| `UI/Hud.cs` | Thẻ người chơi, ví, chip nhiệm vụ, hai rãnh nút, thanh hành động |
| `UI/PanelBase.cs` | Khung panel dùng chung + ô vật phẩm, hàng danh sách, chip thưởng |
| `UI/PanelsFarm.cs` | Nâng cấp, Rương, Nhiệm vụ, Kho |
| `UI/PanelsShop.cs` | Cửa hàng hạt giống, Bạn bè, Cửa hàng, Bộ sưu tập |
| `GameApp.cs` | Khởi động, nền trời/mây/nước, khung modal, popup ô đất, thưởng, toast, vòng lặp game |

## Khác biệt so với bản web

- Giao diện thiết kế mới hoàn toàn: thẻ bo góc kem, header màu theo từng panel,
  nút có gờ dưới và hiệu ứng nhún, HUD dạng chip, hai rãnh nút trái/phải,
  thanh hành động chính ở đáy. Chrome vẽ bằng code nên đổi màu chỉ cần sửa `Theme.cs`.
- Chữ dùng font động của hệ điều hành để tiếng Việt có dấu luôn hiển thị đúng.
- Cây trồng tiếp tục lớn khi tắt game (mốc thời gian lưu theo Unix time).
- Đã khoá màn hình ngang, `CanvasScaler` chuẩn 1280×720.

## Art

`Assets/Resources/Art/` lấy từ thư mục `assets/` của bản web. Xem `Assets/Art_CREDITS.txt`:
thư mục `crop/` là Kenney Food Kit (CC0); `farm/` và `ui/` cắt ra từ hai sprite sheet do
chủ dự án cung cấp — nguồn gốc hai sheet đó cần tự xác nhận trước khi phát hành.
