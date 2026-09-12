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

Khi Unity Editor đang mở, batchmode bị khoá. Compile kiểm tra bằng Roslyn của Unity:

    U=/Applications/Unity/Hub/Editor/6000.4.10f1/Unity.app/Contents
    $U/Resources/Scripting/NetCoreRuntime/dotnet \
      $U/Resources/Scripting/DotNetSdkRoslyn/csc.dll @<response-file>

## Node.js

Cài user-local ở `~/.local/node` (không sudo, không đụng hệ thống). Không có trong PATH mặc định —
gọi bằng đường dẫn tuyệt đối.
