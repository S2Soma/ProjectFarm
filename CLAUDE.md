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
