# Farm3 — LQ Farm (Unity 6, mobile landscape)

Game nông trại port từ bản web. Toàn bộ giao diện dựng bằng code, xem `Assets/Scripts/README.md`.

## Quy tắc dùng MCP

Project này có hai MCP server (`.mcp.json`):

### `unity` — Unity MCP chính thức
Package `com.unity.ai.assistant`, relay chạy local. Dùng để đọc scene, console, chụp Game view.
Cần Unity Editor đang mở project thì mới kết nối được.

### `browser` — Playwright MCP
**Chỉ dùng cho việc tìm và tải asset cho game này.** Không dùng vào việc khác.

Ranh giới đã thiết lập:

| Lớp | Biện pháp |
|---|---|
| Hồ sơ trình duyệt | `--user-data-dir ~/.claude-browser` — **tách hoàn toàn** khỏi Chrome cá nhân. Hồ sơ này chỉ nên đăng nhập ChatGPT và Gemini, không đăng nhập gì khác |
| Tải file lên web | `mcp__browser__browser_file_upload` bị **deny** trong `.claude/settings.json` — không đẩy được file trong máy lên bất kỳ trang nào |
| Chạy JS tuỳ ý | `mcp__browser__browser_evaluate` đặt **ask** — phải hỏi trước |
| Domain nhạy cảm | `--blocked-origins` chặn mail/pay của Google |

Lưu ý thật: `--allowed-origins`/`--blocked-origins` của Playwright MCP **không phải ranh giới bảo mật**
(tài liệu của chính nó ghi vậy, và có bug đang mở). Ranh giới thật là **hồ sơ trình duyệt tách biệt**.
Vì vậy đừng đăng nhập tài khoản quan trọng vào `~/.claude-browser`.

## Quy tắc về asset

- Ưu tiên **CC0 / public domain** (Kenney, OpenGameArt). Hiện `Assets/Resources/Art/ui2/` là Kenney CC0.
- Art sinh bằng AI phải là **thiết kế gốc** — không mô phỏng nhân vật, logo hay giao diện của game thương mại nào.
- `Assets/Art_CREDITS.txt` ghi rõ: hai sprite sheet gốc (`Art/farm`, `Art/ui`) do chủ dự án cung cấp và
  **nguồn gốc chưa được xác nhận** — cần làm rõ trước khi phát hành.

## Kiểm tra biên dịch không cần chiếm Editor

Khi Unity Editor đang mở, batchmode bị khoá. Dùng script (Roslyn của chính Unity):

    Tools/compile_check.sh              # chỉ Assets/Scripts — nhanh, chạy trước mỗi commit
    Tools/compile_check.sh --editor     # thêm Assets/Editor

Thoát khác 0 nếu có lỗi. Hai cái bẫy đã gặp rồi, script đã xử lý sẵn — **đừng tự dựng lại
response file bằng tay**:

- **Không được tham chiếu `Library/ScriptAssemblies/Assembly-CSharp*.dll`.** Đó là bản Editor đã
  biên dịch chính những file đang compile → mọi type bị định nghĩa hai lần, hàng trăm cảnh báo
  CS0436 chôn mất lỗi thật.
- **`UnityEditor.CoreModule.dll` nằm trong `Managed/UnityEngine/`**, không nằm cạnh
  `Managed/UnityEditor.dll`. Thêm cả hai là trùng type (CS0433). Chế độ không `--editor` **cố ý**
  giấu mọi assembly `UnityEditor*` để bắt được lỗi game code lỡ gọi API Editor — thứ compile được
  trong Editor nhưng vỡ khi build player.

## Bộ kiểm tra (chạy từ menu Unity, `Tools ▸ LQ Farm`)

Chạy cả mười sau mỗi bước của `REDESIGN.md`. Đều là editor script thuần, không cần vào Play.

| Menu | Kiểm gì |
|---|---|
| **Kiểm tra kiến trúc** | `GS` chỉ có 7 static; `Assets/Scripts/Farm/` không đọc `GS.Local` |
| **Kiểm tra file lưu** | round-trip đủ trường, giữ khoá lạ, từ chối v1 / bản tương lai / file hỏng |
| **Kiểm tra tưới nước** | 24 thời gian trồng: tổng giảm đúng 20%, lượt không chồng, **lượt cuối kịp mở**, offline không tích luỹ |
| **Kiểm tra chạm vs kéo** | kéo trên ô đất KHÔNG được tính là chạm (kể cả khi nền tảng không gửi sự kiện drag) |
| **Kiểm tra thời tiết & tag** | 24 seed × 20.000 giờ: không lặp liền, Bão/Hạn không nối nhau, 24 giờ đầu yên ả, phân bố hợp lý, tag 2/2/2 theo bậc |
| **Kiểm tra đột biến & số quả** | bậc tăng đều, **luật không-nhân-hai** (huyền thoại đúng ×10 chứ không ×25), trần thời gian, giá theo quả, bảo hiểm xui |
| **Kiểm tra nhiệm vụ** | hạng tăng đều, hết hạn phá chuỗi, offline **không trả bù**, trần ngày, đơn chỉ định cây ×1,8 |
| **Kiểm tra đảo & ô đất** | **ô đất khít nhau, thẳng hàng, không chồng** (đừng "sửa" art bị chồng bằng cách nới khe lại), tên ≤8 ký tự (vừa ô phân trang), cây cống nạp **nằm trong 6 cấp gần nhất**, cống nạp cày nổi trong 1 ngày, thang giá ô đất tăng đều và **dưới 1 ngày thu nhập**, nộp tiêu **quả thường trước**, mở đảo đúng thứ tự, đặc quyền nhân dồn |
| **Kiểm tra bản đồ & gieo trồng** | nút nhanh mở theo thứ tự Thu hoạch → Gieo → Tưới, sheet hạt giống xếp hạt đang có trước và không hiện hạt chưa mở, cầu mây nối đúng hai đảo, mỗi kiểu thời tiết có diện mạo đảo riêng |
| **Kiểm tra cửa hàng** | mọi giá bám theo UNIT (3–40 lần thu hoạch ở **mọi cấp 1–30**), không còn bán ô đất, nhà kính **chỉ trừ lượt khi thời tiết xấu**, dự báo bật/tắt đúng |

`Dev: …` là công cụ dựng cảnh để chụp ảnh đối chiếu, chỉ chạy trong Play mode.
**Dev: xem thời tiết kế tiếp** đổi hình bản đồ qua 6 kiểu thời tiết (chỉ hình ảnh, không đổi giá trị);
**Dev: thời tiết thật** trả về giờ thật. Ép thời tiết phải dùng `WeatherFx.ForceWeather` (có giữ) — nếu
không, tick mỗi giây của game sẽ kéo bản đồ về thời tiết thật ngay giữa lúc chụp.

## Bẫy layout đã gặp

`UIKit.Anchor(rt, anchor, pos, size)` đặt **`pivot = anchor`**. Nghĩa là:

- `UIKit.Left` → `pos.x` là **mép trái**, không phải tâm
- `UIKit.Right` → `pos.x` là **mép phải** (giá trị âm tính từ cạnh phải)
- `UIKit.TopLeft` → `pos` là góc trên-trái

Đọc nhầm thành tâm đã khiến huy hiệu hạng nằm đè lên thanh tiến trình trong bảng nhiệm vụ.

Và `UIKit.Label` dùng `HorizontalWrapMode.Overflow`: **mọi nhãn tiếng Việt tràn im lặng** — không
xuống dòng, không cắt "…", chỉ chạy đè lên hàng xóm. Nên rect quá hẹp **không** bị cắt, nó đâm vào
thứ bên cạnh. Mỗi hàng có nhiều cột phải ghi rõ ranh giới cột bằng hằng số, đừng đoán từng widget.

Tiếng Việt chồng hai dấu (`ế`, `ộ`, `ữ`) nên hộp dòng cần **≥ 1,45 × cỡ chữ**, không phải 1,2 như
chữ Latin.

`UIKit.Bar` chỉ có sprite vẽ sẵn cho **xanh lá và xanh dương**; màu khác được tô lên thanh trắng.
Và **đừng gọi `string.Replace("", …)`** — nó ném exception; từng làm số xu trên HUD đứng im.

Ruộng dùng `Art/beds/` (sinh bởi `Tools/gen_beds.py`), **không** dùng `Art/farm/tile_*`: tile vẽ tay
có cọc ở 4 góc nên không đặt khít được. Mọi thứ trong một ô vẫn vẽ ở hệ 168×84 và nhân
`IslandView.PlotScale`. **Trạng thái ô thể hiện trên art của ô**, không dùng icon: khát = đất nứt + viền
xanh, chín = viền vàng + lấp lánh, đã tưới = đất ướt sẫm. Đồng hồ nằm dưới lớp cây. Không đặt thứ gì
đè lên cây.

Mỗi đảo có tranh riêng ở `Art/islands/` (sinh bởi `Tools/gen_islands.py`, 1600×1164, có mipmap) — chủ đề
theo tên đảo. Ảnh thế giới (đảo, ô, rào, cây, mây) phải bật mipmap + trilinear: bản đồ zoom từ 0,14× đến
1,35×, thiếu mip là vỡ nét khi zoom xa.

## Quy tắc kiến trúc (có cưỡng chế)

`Assets/Editor/ArchitectureGuard.cs` **làm fail build** nếu vi phạm. Chạy tay:
menu **Tools ▸ LQ Farm ▸ Kiểm tra kiến trúc**.

1. `GS` chỉ được có 7 thành viên tĩnh: `Local`, `Viewing`, `Now`, `PlotCount`, `Save`, `Load`,
   `Reset`. Mọi trạng thái người chơi thuộc về `PlayerState`.
2. `Assets/Scripts/Farm/` **không được** đọc `GS.Local` — lớp thế giới đi qua `FarmContext`
   (`Ctx.owner` / `Ctx.actor`).

Cả hai để giữ cho phần bạn bè online sau này rẻ. Xem `REDESIGN.md` §9.

## Node.js

Cài user-local ở `~/.local/node` (không sudo, không đụng hệ thống). Không có trong PATH mặc định —
gọi bằng đường dẫn tuyệt đối.
