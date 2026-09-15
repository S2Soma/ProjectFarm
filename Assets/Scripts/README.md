# MATU Farm — bản Unity

Port của bản web `Clone LQ Farm` sang Unity 6 (uGUI, mobile landscape), thiết kế lại toàn bộ.
Mọi thứ trên màn hình dựng bằng code — không cần kéo thả gì trong Editor.

## Chạy thử

Mở `Assets/Scenes/SampleScene.unity` rồi bấm Play. Scene không cần GameObject nào:
`GameApp.Boot()` gắn `[RuntimeInitializeOnLoadMethod]` nên tự dựng Canvas, quần đảo, HUD và
EventSystem khi game khởi động. Ván mới bắt đầu bằng hướng dẫn chơi.

Build thử lên điện thoại: **Tools ▸ LQ Farm ▸ Build Android (APK thử)** → `Builds/Android/`.

## Cấu trúc

| Thư mục / file | Vai trò |
|---|---|
| `Core/GameData.cs` | Nội dung tĩnh: 28 hạt giống, rương, chương nhiệm vụ, nhiệm vụ ngày, cửa hàng, bạn bè, bộ sưu tập, **đường cong cấp độ** |
| `Core/PlayerState.cs` | Toàn bộ trạng thái một nông trại và mọi luật đọc/ghi nó (kinh tế, nhiệm vụ, tiến độ hướng dẫn) |
| `Core/GameState.cs` | `GS` (7 static), `Plot`, `Stats`, đồng hồ chống quay ngược |
| `Core/SaveIO.cs` | Lưu/đọc JSON bằng Newtonsoft, giữ khoá lạ, cách ly file hỏng |
| `Core/WeatherSys.cs`, `TagSys.cs`, `WaterSys.cs`, `MissionSys.cs`, `IslandSys.cs`, `ShopSys.cs`, `QuickActions.cs`, `Cosmetics.cs` | Mỗi hệ thống một file: thời tiết, cây bonus, cữ tưới, đơn hàng, đảo & ô đất, cửa hàng, nút nhanh, trang trí |
| `Core/ArtLibrary.cs` | Nạp sprite từ `Resources/Art/`, bảng 4 bậc đột biến |
| `Farm/ArchipelagoView.cs` | Quần đảo trên một canvas, camera, cầu treo nối đảo |
| `Farm/IslandView.cs` | Một đảo: ô đất, rào, vật trang trí, gieo/tưới/thu hoạch và hiệu ứng |
| `Farm/MapCamera.cs`, `FieldComponents.cs`, `FieldAnimator.cs`, `BridgeRibbon.cs` | Kéo/zoom, phân biệt chạm vs kéo, hoạt ảnh ô, lưới cầu |
| `UI/Surface.cs`, `Theme.cs`, `UIKit.cs`, `Tween.cs` | Hệ vật liệu giao diện, bảng màu & font, bộ widget, hoạt ảnh |
| `UI/Hud.cs` | Thẻ người chơi + nhiệm vụ + Thu hoạch/Tưới (trên trái), đĩa thời tiết, ví, đổi đảo (dưới trái), Menu (dưới phải) |
| `UI/SkyView.cs`, `DayCycle.cs`, `WeatherFx.cs` | Bầu trời, ngày/đêm theo giờ máy, hiệu ứng thời tiết |
| `UI/Coach.cs`, `Tutorial.cs` | Hướng dẫn chơi lần đầu (màn tối có lỗ sáng, bàn tay chỉ, thẻ lời dẫn) và mẹo theo ngữ cảnh |
| `UI/PanelGuide.cs` | Sổ "Hướng dẫn chơi" trong Menu, số liệu đọc thẳng từ bảng của game |
| `UI/PanelBase.cs`, `PanelsFarm.cs`, `PanelsShop.cs`, `PanelSeason.cs`, `PanelIsland.cs`, `SeedSheet.cs` | Các bảng: nâng cấp, rương, nhiệm vụ, kho, hạt giống, bạn bè, cửa hàng, sưu tập, mùa vụ, đảo; sheet chọn hạt |
| `UI/Sfx.cs` | Âm thanh (Kenney CC0) |
| `GameApp.cs` | Khởi động, các lớp canvas, khung modal, popup ô đất, thưởng, toast, vòng lặp game, nút Back |
| `UiAudit.cs` | Chụp toàn bộ giao diện ra `Screenshots/` để đối chiếu |

Quy tắc làm việc, bộ kiểm tra và các bẫy đã gặp: xem `CLAUDE.md` ở thư mục gốc.

## Art

`Assets/Resources/Art/` — xem `Assets/Art_CREDITS.txt`. Phần lớn art được vẽ bằng script trong
`Tools/` (đảo, luống, rào, bầu trời, cầu, icon vật phẩm, icon ứng dụng). Hai sprite sheet gốc
`farm/` và `ui/` do chủ dự án cung cấp — **nguồn gốc cần xác nhận trước khi phát hành**.
