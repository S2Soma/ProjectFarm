# Farm3 — MATU Farm (Unity 6, mobile landscape)

Tên game là **MATU Farm** (viết đúng kiểu chữ này; đổi từ "MiT FArM" ngày 2026-09-15), mã gói Android vẫn
`com.mitfarm.game` — **cố ý giữ**: đổi mã gói là máy coi như app khác, người test mất ván. "LQ Farm"/`LQFarm` và "MiT"
là tên cũ, chỉ còn trong code (namespace, tên file lưu `lqfarm.save.json`, menu `Tools ▸ LQ Farm`, tên shader `MiT/UI …`,
font "Baloo 2 MiT", key PlayerPrefs `mitfarm.*`) — đừng đổi những chỗ đó: đổi tên file lưu là mất ván, đổi tên shader là
`Shader.Find` trả null. Pet "MiT" là tên nhân vật, không phải tên game. Chữ hiển thị tên game lấy từ `Application.productName`.

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

- Ưu tiên **CC0 / public domain** (Kenney, OpenGameArt). Hiện `Assets/Resources/Art/ui2/` là Kenney CC0 (chỉ còn
  icon; nền/nút đã thay bằng hệ vật liệu), `Assets/Resources/Audio/` là âm thanh **tự tổng hợp** (`Tools/gen_sfx.py`,
  `cine_*` bằng `Tools/gen_cine_audio.py`; xem `UI/Sfx.cs`). Nhạc nền `Audio/Music/` do chủ dự án đưa, sinh bằng AI của
  Google (C2PA trong file) — **quyền dùng khi phát hành chưa xác nhận**.
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

Chạy cả mười lăm sau mỗi thay đổi. Đều là editor script thuần, không cần vào Play.

| Menu | Kiểm gì |
|---|---|
| **Kiểm tra kiến trúc** | `GS` chỉ có 7 static; `Assets/Scripts/Farm/` không đọc `GS.Local` |
| **Kiểm tra file lưu** | round-trip đủ trường (cả `waterSec`, `econV`), giữ khoá lạ, từ chối v1 / bản tương lai / file hỏng, **save kinh tế cũ (cấp 12, 286.400 xu) quy đổi đúng một lần**: xu & XP giữ vị trí trên đường cấp, ô đang trồng giữ thời gian, nạp lại không quy đổi thêm, file đã có `waterSec` không bị đụng, xu mã quà không tràn số |
| **Kiểm tra tưới nước** | 29 cây × 4 cấp × 6 thời tiết × 5 bậc đột biến: **số cữ = số lần tưới của cây**, tưới đủ sớm **đúng N × thời gian của cây**, cữ liền nhau / không chồng, **lượt cuối kịp mở** (kể cả hạn hán, cấp cao), **cữ đã mở thì giữ tới khi cữ sau mở**, trần (N × thời gian × hạn, ≤ nửa thời gian), **thời gian thật ≤ 24 giờ** (cả nhà kính), offline không tích luỹ, bình tưới vàng không vượt sàn, **ô gieo theo luật cũ (`waterSec` = 0) vẫn chín đúng 20%**, **mưa/bão tự tưới đúng cữ mở trong giờ mưa** |
| **Kiểm tra chạm vs kéo** | kéo trên ô đất KHÔNG được tính là chạm (kể cả khi nền tảng không gửi sự kiện drag) |
| **Kiểm tra thời tiết & tag** | 24 seed × 20.000 giờ: không lặp liền, Bão/Hạn không nối nhau, 24 giờ đầu yên ả, phân bố hợp lý, tag 2/2/2 theo bậc |
| **Kiểm tra đột biến & số quả** | bậc tăng đều, **luật không-nhân-hai** (huyền thoại đúng ×10 chứ không ×25), đột biến cộng thêm ≤ 1 giờ và **không vượt trần 24 giờ**, giá theo quả, bảo hiểm xui |
| **Kiểm tra nhiệm vụ** | hạng tăng đều, hết hạn phá chuỗi, offline **không trả bù**, trần ngày, đơn chỉ định cây ×1,8, **đơn chỉ gọi tên cây ≤ 3 giờ và sống đủ lâu để trồng nó** |
| **Kiểm tra đảo & ô đất** | **Đảo Nước là đảo 2, Khổng Lồ là đảo 3**, ô 2 bờ sông khít nhau và không lấn xuống sông, 4 ô lớn phủ khít cánh đồng, **cây lớn chỉ ô lớn / cây nhỏ chỉ ô nhỏ** (không mất hạt), **file lưu cũ không đổi tên đảo**, **ô đất khít nhau, thẳng hàng, không chồng**, vật trang trí nằm trong rào và không đứng trên ô, cổng đủ rộng cho cầu mây (đừng "sửa" art bị chồng bằng cách nới khe lại), tên ≤8 ký tự (vừa ô phân trang), cây cống nạp **nằm trong 6 cấp gần nhất**, cống nạp **16 ô cày nổi trong 1 ngày chơi theo phiên** (`EconomyModel`), thang giá ô đất tăng đều và **dưới 1 ngày thu nhập của cả nông trại lúc đó**, nộp tiêu **quả thường trước**, mở đảo đúng thứ tự, đặc quyền nhân dồn |
| **Kiểm tra bản đồ & gieo trồng** | nút nhanh mở theo thứ tự Thu hoạch → Gieo → Tưới, sheet hạt giống xếp hạt đang có trước và không hiện hạt chưa mở, cầu mây nối đúng hai đảo, mỗi kiểu thời tiết có diện mạo đảo riêng |
| **Kiểm tra ngày đêm** | mỗi phút × 6 thời tiết: đất luma ≥ 0,50, cây ≥ 0,62; bảng màu không nhảy quá 0,02/phút; ban ngày không sao, không đèn; mặt trời/trăng chỉ hiện đúng giờ |
| **Kiểm tra hướng dẫn & cấp độ** | ván mới vào Welcome; file lưu cũ đã chơi (kể cả ván dựng bằng Dev, thống kê = 0) bỏ qua hướng dẫn và không bị báo lại mẹo; tên bước lạ = xong; cấp 1 trả được bằng xu khởi đầu; không cấp nào nhảy quá ×4,2 |
| **Kiểm tra hành trình chơi** | chơi thử từ ván mới **theo phiên** (4 phiên/ngày, 85 phút, ngủ 10,5 giờ; 30 lần chạm/phút; logic thật, đồng hồ tua nhanh; lượt đầu 60 ngày, 2 lượt 14 ngày): lên cấp 2 < 20 phút chơi, **thú cưng (cấp 5) trong ngày 1**, Đảo Nước < 2 ngày, **Đảo Gió < 4 ngày**, Đảo Băng < 10 ngày, **không cấp nào (tới 30) quá 4 ngày**, trứng thú cưng thứ 2 < 2 ngày, không bước chương nào đứng quá 3 ngày, không phiên nào rời game còn ô trống, không phiên nào ≥ 5 phút không có gì làm; **không mã quà**; thêm 1 lượt từ **save cũ cấp 12 đã quy đổi** (7 ngày phải lên ≥ 2 cấp). In nhật ký ngày đầu, **bảng từng cấp** (ngày, giờ chơi, XP/xu cần, thu/ngày, đợi XP hay đợi xu, giá trứng/shop, mở khoá), giá & ngày mở đảo, trứng #1/#2/#5/#10, thu nhập & UNIT từng ngày (kèm UNIT 4 giờ / 24 giờ), nguồn thu, và bảng cây |
| **Kiểm tra cửa hàng** | mọi giá bám theo UNIT (3–40 UNIT ở **mọi cấp 1–30**), **"Chín ngay" tính theo thời gian còn lại** (cà rốt ≤ 0,2 UNIT, dưa hấu vừa gieo 3–10 UNIT, còn nhiều không rẻ hơn còn ít), không còn bán ô đất, nhà kính **chỉ trừ lượt khi thời tiết xấu**, dự báo bật/tắt đúng |
| **Kiểm tra tài khoản & đồng bộ** | bảng quyết định bản lưu nào thắng (15 ca), **ván của tài khoản khác không bao giờ bị đẩy lên tài khoản đang đăng nhập** (72 tổ hợp), fingerprint bỏ qua đồng hồ/meta, đọc phiên GoTrue (kể cả đăng ký chờ xác nhận email = chưa đăng nhập), lỗi GoTrue/PostgREST ra câu tiếng Việt, **không có secret key trong `supabase.json`**, schema bật RLS và không cấp gì cho anon |
| **Kiểm tra thú cưng** | đủ ảnh 6 pet, tỉ lệ nở đúng bảng (±1%), **bảo hiểm 40 trứng ra Sử thi**, trùng thì lên cấp (trần 5), trứng đầu miễn phí và giá 15–40 UNIT ở mọi cấp (đang là 35), thiếu cấp/thiếu xu không trừ gì, đi tuần **tưới trước thu hoạch sau**, ăn vụng ưu tiên nông sản hiếm |

`Dev: …` là công cụ dựng cảnh để chụp ảnh đối chiếu, chỉ chạy trong Play mode.
**Dev: dựng ván giữa game (cấp 12)** tạo lại trạng thái chuẩn cho ảnh audit (cấp 12, 286.400 xu, 3 đảo, ô chín/khát/đang lớn).

**Đừng sửa script khi đang Play.** Unity biên dịch lại giữa phiên sẽ khởi tạo lại mọi static (`GS.Local` thành
ván trống) trong khi game vẫn tự lưu mỗi 5 s — từng ghi đè mất file lưu cấp 12. `SaveIO.Save` giờ từ chối
lưu trạng thái chưa được nạp (`PlayerState.loaded`), nhưng phiên Play đó vẫn hỏng: thoát Play rồi vào lại.
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

**Đừng gọi `string.Replace("", …)`** — nó ném exception; từng làm số xu trên HUD đứng im.

Ruộng dùng `Art/beds/` (sinh bởi `Tools/gen_beds.py`), **không** dùng `Art/farm/tile_*`: tile vẽ tay
có cọc ở 4 góc nên không đặt khít được. Mọi thứ trong một ô vẫn vẽ ở hệ 168×84 và nhân
`IslandView.PlotScale`. **Trạng thái ô thể hiện trên art của ô**, không dùng icon: khát = đất nứt + viền
xanh, chín = viền vàng + lấp lánh, đã tưới = đất ướt sẫm. Thẻ thanh tiến trình nằm trên mặt đất phía trước cây
(lớp `labels`), nhỏ; ngoài nó và hiệu ứng đột biến, không đặt thứ gì đè lên cây.

Mỗi đảo có tranh riêng ở `Art/islands/` (sinh bởi `Tools/gen_islands.py`, 2000×1278, có mipmap) — chủ đề
theo tên đảo. **Số tranh là `IslandDef.style`, không phải thứ tự đảo** (Đảo Nước = tranh 6, Khổng Lồ = tranh 7). **Đảo là hình thoi**: một hình vuông bo góc tính bằng ô lưới (`IslandView.RimCells`,
`RimRound`), nên cạnh đảo song song với luống. Số liệu này nằm ở CẢ `IslandView` lẫn `gen_islands.py`
(`RIM`, `ROUND`, `K`, `SPRITE_Y`) — đổi một bên phải đổi bên kia. Hàng rào chạy quanh 4 cạnh ở
`FenceCells`, ghép từ **một cọc mỗi nút** + thanh ngang (`Art/gen/fence_post|rail|gate`, cắt bởi
`gen_fences.py`) — đừng quay lại ghép nguyên đoạn rào, mỗi đoạn có cọc ở hai đầu nên cọc bị đôi. Cổng có
đèn ở mũi trái/phải, cầu mây cập vào đó (`IslandView.BridgeLandX`). Vật trang trí khai báo trong
`IslandView.Props` bằng toạ độ ô, phải nằm trong rào; vật cao không đặt ở sân trước (sẽ che cây). Ảnh thế giới (đảo, ô, rào, cây, mây) phải bật mipmap + trilinear: bản đồ zoom từ 0,14× đến
1,35×, thiếu mip là vỡ nét khi zoom xa. **Mipmap được quyết định trong
`Assets/Editor/FarmTextureImporter.cs` (`WantsMips`), không phải trong file .meta** — importer chạy lại
mỗi lần import và ghi đè `enableMipMap` trong meta. Thêm thư mục art mới thì thêm vào đó.

## Bầu trời, ngày/đêm, cầu mây

- **Bầu trời** (`UI/SkyView.cs`, art từ `Tools/gen_sky.py` → `Art/sky/`): quần đảo lơ lửng trên **biển mây**,
  không còn mặt biển/bọt sóng. Mọi sprite là **xám + alpha**, tô màu bằng vertex colour (`VGradient`) —
  đừng vẽ màu cố định vào art trời, vì cùng một đám mây phải trắng lúc trưa và tím than lúc đêm.
- **Ngày/đêm** (`UI/DayCycle.cs`): theo **giờ máy** (`DateTime.Now`), không dùng `GS.Now`, không ảnh hưởng
  kinh tế. 5 mốc (đêm 19:45–04:30, bình minh 05:45, ngày 07:15–16:00, nắng vàng 17:15, chạng vạng 18:30).
  `SkyView` là nơi gộp giờ × thời tiết (`WeatherFx.Blended`), rồi đẩy ánh sáng xuống đảo qua
  `DayCycle.FloorAmbient` — **đất không tối hơn luma 0,50, cây không tối hơn 0,62** (có test).
  HUD, viền trạng thái ô, đồng hồ, đèn lồng **không** bị nhuộm tối. `WeatherFx` không còn tự tô nền trời.
- Dev: `Dev: giờ 06:00 / 12:00 / 17:15 / 18:30 / 23:00`, `Dev: giờ thật`. Ảnh audit 40–47.
- **Mây** vẽ theo lối tranh (`gen_sky.py`, `paint_clouds`): từng cụm tròn chồng lên nhau, sáng/bóng 2 bậc
  mềm, màu nền trưa (trắng + bóng tím xanh) được nhân theo giờ. Tổng hợp **premultiplied** rồi mới
  bỏ nhân khi lưu — lưu thẳng sẽ ra viền xám quanh mọi cụm mây. Noise của dải biển mây phải lặp khít
  (`value_noise(wrap=True)`), không thì lộ đường nối giữa hai tấm.
- **Cầu nối đảo là cầu treo gỗ** (không còn cầu mây — nhìn như nhựa): `Farm/BridgeRibbon.cs` +
  `ArchipelagoView.BuildBridge`, art `Tools/gen_bridge.py` → `Art/bridge/` (ván, mép ván, tay vịn dây thừng;
  đèn lồng cắt từ `fence_gate.png`; cọc dùng chính `fence_post`). Dải lưới dùng chung texture lặp theo U
  (importer đặt `wrapModeU = Repeat`); `ROWS` trong script phải khớp `BridgeRibbon.Rows`. Số ván luôn
  nguyên (không có tấm bị cắt ở đầu cầu). Cầu nhận ánh sáng như rào (`LandTint × ambient`). Cọc đầu cầu cắm
  trên bãi cỏ, không đè đèn lồng cổng (test trong "bản đồ & gieo trồng").
- Đèn lồng (cổng rào, cầu) nằm trên canvas lồng riêng `lights` và lập loè mỗi frame — đừng đưa glow về
  lại lớp deco, sẽ bắt dựng lại cả canvas thế giới mỗi frame. Đom đóm ở lớp `WeatherFx.Air`, chỉ khi đêm
  quang/gió/hạn và đang zoom gần. Đảo nhỏ ở xa là chính tranh đảo thu nhỏ, chìm trong dải mây xa.
  Biểu tượng thời tiết: `Art.WeatherIconNow` — trời quang ban đêm hiện trăng (`w_moon`).

## Hệ vật liệu giao diện (`Assets/Scripts/UI/Surface.cs`)

Mọi mặt nền là **một trong các vật liệu ở `Looks`** (Theme.cs): `Glass` (HUD), `Paper` (thẻ, sheet,
popup, khay), `Ribbon`/`RibbonGreen` (dải tiêu đề), `Well` (rãnh lõm, track), `Row`, `Btn*`.
Đừng tạo lại tấm màu phẳng `UIKit.Round(parent, new Color(0,0,0,0.6f))` — đó chính là thứ làm
HUD trông rẻ.

- `SurfaceLook.Add(host, look, radius)` chèn các lớp (bóng, môi, viền, viền sáng, nền) vào **đầu**
  danh sách con của `host`. Thứ đã gắn vào `host` trước đó sẽ bị đẩy LÊN TRÊN lớp nền — muốn nó
  nằm dưới thì `SetAsFirstSibling()` sau khi Add (xem đuôi khay trong Hud).
- `radius`: số ≥ 0 là bán kính cố định; `SurfaceLook.Pill` = nửa chiều cao (tự cập nhật khi đổi
  kích thước); `SurfaceLook.Auto` = luật nút (18 khi mặt ≥ 60, pill khi thấp hơn).
- Bo góc luôn qua `Chrome.Shape(image, r)` — sprite vẽ lớn + `pixelsPerUnitMultiplier`. Sprite
  `round_N` cũ là 1× nên mọi góc bị nhoè trên màn 1,5×. `UIKit.Round` đã đi qua `Chrome.Shape`.
- **Thanh tiến độ không dùng `Image.Type.Filled`** (bỏ qua 9-slice → đầu cắt vuông). `UIKit.Bar`
  trả về ảnh fill; cứ gán `fillAmount`, `BarDriver` đổi nó thành chiều dài. Dưới 3% thì ẩn.
- Nút là `SkinButton`: tắt `interactable` là tự sang bảng màu xám; `Restyle(b, Theme.Cream3)` cũng
  ra mặt xám. Chữ nút luôn trắng viền màu môi — gán `label.color` sẽ bị ghi đè.
- Font: Nunito 3 độ đậm trong `Resources/Fonts`, chọn qua `Theme.FontFor(style, size)`.
  **Không đặt `fontStyle = Bold`** trên nhãn — font động không có mặt bold sẽ bị làm đậm giả, nhoè.
- Chữ trắng trên kính/ảnh dùng `LabelOutlined(..., line)` với viền màu tối của chính mặt nền.

Icon vật phẩm, đồng xu, rương, huân chương: `Tools/gen_items.py` → `Art/items/`, tải bằng `Art.Item(tên)`.
(`Art.Crop` chỉ tìm trong `Art/items` với khoá bắt đầu bằng `item_` — `chest_`/`medal_` qua `Art.Crop` ra null.)
Icon nông sản: `Tools/gen_produce.py` → `Art/crop/`.

## Âm thanh, hoá đơn thu hoạch, trang trí, tai thỏ

- **Âm thanh:** `Sfx.Play(SfxId.X)`. Bảng âm lượng và khoảng cách tối thiểu trong `Sfx.Table`. Toast bắt đầu bằng
  "Không đủ / Chưa / Hết lượt…" tự kêu tiếng lỗi (`Sfx.LooksLikeRefusal`). Bật/tắt ở đầu Menu bằng **hai nút tròn**
  (loa = hiệu ứng, nốt nhạc = nhạc nền; `Hud.ToggleChip`, icon `sound_on/off`, `music_on/off` từ `gen_icons.py`), lưu bằng
  `PlayerPrefs` (thuộc máy, không thuộc file lưu).
  - **Hiệu ứng** sinh bằng `Tools/gen_sfx.py` (marimba/kalimba/chuông/nước, không sample), cùng giọng **La giáng trưởng
    ngũ cung** với nhạc nền. **Thang độ to nằm trong file** (tap nhỏ nhất … jingle to nhất, đỉnh ≤ −6 dBFS) nên
    `Sfx.Table` gần như đều 0,9 — muốn to/nhỏ một tiếng thì sửa bậc trong script rồi chạy lại. Jitter cao độ của tiếng
    có nốt giữ ±1,2 % (lệch hơn là lạc giọng với nhạc). `--preview x.png` vẽ sóng + phổ để soi tiếng click/chói.
  - **Nhạc nền** `UI/Music.cs`: tự khởi động (không cần GameApp), vòng lặp, fade vào 2 s từ màn hình bắt đầu, AudioSource
    riêng (không dùng voice của Sfx). Bật/tắt `Music.Enabled` (PlayerPrefs `mitfarm.music`, mặc định bật, có fade).
    `Music.Duck(amount, s)` — Sfx tự gọi cho LevelUp/IslandUnlock/Legendary/Whoosh/Chime (cột `duck`).
    File `Audio/Music/terrace_in_the_clouds.ogg` do `Tools/make_music.py` cắt từ mp3 gốc ở gốc repo (**đừng xoá mp3**):
    cắt đúng 364 phách (144 BPM) sau hợp âm cuối, phần ngân còn lại trộn lên đầu file → lặp liền; −19 LUFS.
  - **Import** do `Assets/Editor/AudioImporterRules.cs` ép (như `FarmTextureImporter`): Music → Streaming, Vorbis 0,5,
    không preload; hiệu ứng → Decompress On Load, Vorbis 0,7. **Normalize luôn tắt** (nó san phẳng thang độ to).
- **Hoá đơn thu hoạch:** thu 1 ô = số tiền nổi lên + tối đa 2 chip hệ số (khi ×≥1,25) + vòng đột biến theo bậc.
  Thu hàng loạt tắt hết số từng ô, cung bay lệch 0,04 s/ô, rồi **một** thẻ tổng kết 3 dòng (cái gì · bao nhiêu ·
  vì sao). Lôi Điện là modal duy nhất, và xếp hàng sau thẻ tổng kết.
- **Trang trí** (`Core/Cosmetics.cs`): 10 ô — khung ảnh, huy hiệu, vật trang trí Vườn Nhà, **viền ô đất**, hiệu ứng
  **gieo / tưới / thu hoạch**, **khung thông báo**, **hiệu ứng chạm**, **vệt vuốt**. Mỗi ô đeo 1 món, mua là đeo luôn.
  Thuần hình ảnh — không món nào đụng tới giá/thời gian/xác suất. Enum `CosmeticSlot` chỉ được **thêm vào cuối** (tên
  enum là khoá trong `social.equipped`). Kệ Trang trí có hàng lọc theo ô.
  - Viền ô đất: `Art/beds/skin_<tên>.png` (cùng khung 780×640 với luống), vẽ trên luống, dưới cây (`PlotView.skin`).
  - Hiệu ứng: công thức hạt trong `CosmeticFx` (`UI/FxKit.cs`), một pool Image cho mỗi lớp, cập nhật trong một Update.
    Trên nền ban ngày sáng dùng alpha thường; chỉ vòng sáng/đốm mềm dùng cộng sáng (cộng sáng nhìn mất trên nền sáng).
  - Chạm/vuốt: `UI/TouchFx.cs` đọc thẳng Pointer của Input System, vẽ ở lớp `touchfx` (order 9, không nhận chạm).
  - Khung thông báo: `GameApp.BuildToast(skin)` đổi màu/viền/chữ theo `ts_*`.
  - Art: `Tools/gen_cosmetics.py` (viền ô, hạt `Art/fx/p_*`, icon `Art/items/cos_*`). Test "cửa hàng" kiểm mọi món có
    công thức/hình thật. Ảnh audit 60–63: **Tools ▸ LQ Farm ▸ Chụp trang trí**.
  Vị trí vật trang trí ở `IslandView.Decor`. Đồ trang trí đang đeo lưu ở `social.equipped`.
- **Tai thỏ:** Editor luôn báo cả màn hình là vùng an toàn. `SafeAreaFitter.Simulated` giả lập máy có tai thỏ;
  ảnh audit `33_safe_area_notch` phải cho thấy mọi cụm HUD nằm trong vùng an toàn.

## Hướng dẫn chơi & mẹo (`UI/Tutorial.cs`, `UI/Coach.cs`, `UI/PanelGuide.cs`)

- **Mỗi bước là một điều kiện trên trạng thái game, không phải kịch bản chạm.** Bước đọc game (sheet có mở?
  số thu hoạch đã tăng? có ô khát?) rồi đi tiếp, lùi lại hoặc tự bỏ qua. Nhờ vậy thoát game giữa chừng, trời mưa
  tưới sẵn, người chơi làm trước, hay "Chơi lại hướng dẫn" trên ván cấp 20 đều không kẹt. Thêm bước mới phải
  giữ nguyên tắc này.
- Tiến độ lưu ở `progress.tutorial` (tên bước, không phải số thứ tự) và `progress.tips`. File lưu cũ không có khoá:
  đã chơi → `Done` và đánh dấu sẵn các mẹo đã đúng (`Tutorial.Resume`).
- Lớp `coach` (order 7) nằm **trên** modal (6) vì phải chỉ vào nút trong bảng (Bán sỉ); toast lên order 8.
  Bước chỉ một chạm thì làm tối + chỉ cho chạm lọt qua lỗ (`Spotlight.IsRaycastLocationValid`). Bước chờ / làm tiếp
  (gieo kín đảo, chờ khát, chờ chín, thu hoạch thêm) **cũng làm tối** và khoét sáng đúng ô/sheet đang nói tới, nhưng
  `CoachSpec.passThrough` cho mọi chạm lọt qua — người chơi vẫn chơi được. Mẹo cũng có nền tối, đóng bằng "Đã hiểu".
- Thẻ lời dẫn tự tránh mục tiêu, bàn tay và vùng `avoid` (sheet hạt, Menu). Target là hàm gọi mỗi frame — phải
  chịu được đối tượng đã bị huỷ (panel đóng giữa hai lần nghĩ).
- Mẹo (`Tutorial.Tips`) hiện một lần, chỉ khi không có bảng/sheet/Menu nào mở, cách nhau ≥ 25 s.
- Lúc chờ tưới / chờ chín, ô đảo nhà chạy nhanh ×6 (`Tutorial.FastForward`, lùi `plantedAt`).
- Sổ **Menu ▸ Hướng dẫn chơi**: mọi con số đọc từ bảng thật (`WeatherSys.All`, `TagSys.Defs`, `Art.Elements`,
  `IslandSys.Defs`) — đổi cân bằng không cần sửa sổ.
- Ảnh audit: **Tools ▸ LQ Farm ▸ Chụp hướng dẫn chơi** (70–87). `PreviewForAudit` chỉ xem trước trong bộ nhớ,
  không ghi bước vào file lưu.

## Luật đã đổi sau bản APK thử đầu (2026-09-13)

- **Tưới:** chạm ô đang khát là tưới luôn (như chạm ô chín là thu hoạch). Cữ đã mở giữ tới khi cữ sau mở — cữ cũ
  tự tắt sau 8 s làm người chơi tưởng mất lượt. Vẫn chỉ một cữ mở mỗi lúc. (Luật "tổng 20%" đã thay bằng số lần tưới
  và thời gian cố định theo cây ngày 2026-09-15, xem mục bên dưới.)
- **Cấp độ:** thay công thức cũ (cấp 1 cần 3.600 XP và 15.000 xu — đầy XP mà kẹt xu) bằng bảng REDESIGN §4.3
  (`GameData.LevelAnchors`, nội suy log, ×1,08 sau cấp 30). Bảng Nâng cấp ghi rõ "Thiếu … XP/xu"; chạm thẻ người
  chơi mở bảng Nâng cấp; ô Nâng cấp trong Menu có chấm "!" khi đủ điều kiện.
- **Ô đất treo "1s":** `IslandView.RefreshStale` vẽ lại mọi ô có trạng thái khác lần vẽ trước, mỗi giây.
- **Danh sách nhiệm vụ:** việc có thưởng lên đầu, khe trống/đã nhận xuống cuối; hàng cũ tách khỏi layout ngay
  (không đợi Destroy cuối frame).

## Đợt góp ý 2026-09-14 tối: giao diện, hiệu ứng, font, thú cưng

- **Toast:** một viên duy nhất nền giấy sáng (`GameApp.Toast`); cùng nội dung thì đếm "×2", khác nội dung thì thay
  chữ tại chỗ. Đã bỏ toast "Đã gieo kín", "Đã tưới n cây", "Đã gieo n", đột biến bậc 2 — hiệu ứng trên ô đã nói rồi.
- **Ô đất:** đồng hồ số thay bằng **thẻ nhỏ 2 thanh** (lớn · tới cữ tưới) ở lớp `labels` phía trên mọi ô, nên cây
  hàng trước không che được. Popup chạm vào cây: 2 hàng thanh + chữ ngắn, cập nhật mỗi giây (`RefreshPlotPop`).
  "Chín ngay" tính theo thời gian còn lại (`ShopSys.RushPrice`, từ 2026-09-15), không còn 800 cố định.
- **Danh sách:** `PanelBase.ClearList` tách hàng cũ khỏi layout ngay (Destroy trễ 1 frame làm list tụt xuống);
  `ScrollTop` khi đổi tab. Áp dụng cho mọi bảng có danh sách.
- **Tai thỏ:** nền tối của bảng/thẻ thưởng và giấy của sheet hạt giống dùng `FullBleed` để tràn ra mép màn hình
  thật; thẻ và nút vẫn nằm trong vùng an toàn. Ảnh audit `34_notch_panel`.
- **Cây đột biến:** hiện từ lúc gieo (`Farm/MutationFx.cs`): vòng sáng trên đất + hào quang; Băng Giá trở lên thêm
  tia sáng xoay và đốm sáng bay lên; Lôi Điện chớp như tích điện. Art xám tô màu ở `Tools/gen_fx.py` → `Art/fx/`.
  **Màu cây đột biến đi qua shader `Resources/Shaders/UIMutation.shader`**, không nhân tint: nhân chỉ làm tối được,
  cây trông như bị hư. Shader đẩy màu bậc bão hoà hơn rồi đổ sáng tối của tranh lên đó (bóng đậm, giữa rực, sáng gần
  trắng), có vệt sáng quét qua và phần phát sáng không bị đêm làm tối. Màu + cường độ đi theo từng vertex (TEXCOORD1,
  `Farm/MutationTint.cs`) nên mọi cây dùng chung một material. Dùng `MutationTint.Apply(image, art, variant)` cho mọi
  hình nông sản đột biến (ô đất, kho, sổ sưu tập, hướng dẫn, icon bay). Hào quang/tia/đốm dùng `UIAdditive.shader`
  (cộng sáng) thay vì alpha — alpha phủ một lớp mờ lên đất và cây. Thử công thức màu trên ảnh bằng Python trước khi sửa
  shader, rẻ hơn nhiều vòng Unity.
- **Hạt thời tiết có chiều sâu** (`WeatherFx`): mỗi hạt có `depth` 0–1 quyết định cỡ, tốc độ, độ đậm, độ lắc; hạt xa
  nhiều, hạt gần ít. Mưa là vệt (`p_rain`) + gợn nước trên mặt đất; tuyết là chấm xa + bông tuyết lớn xoay ở gần; bão
  thêm tia sét (`p_bolt_*`) và lá bay; gió theo cơn (lá, cánh hoa, vệt gió); hạn có bụi nóng + hơi nóng; nắng có phấn
  hoa lấp lánh và tia nắng từ góc trên. Art ở `Tools/gen_fx.py`.
- **Thời tiết không phủ màn hình:** màu "không khí" của mưa/bão/tuyết/hạn chỉ nằm ở **mép màn hình**
  (`Art/fx/edge_wash`); giữa màn hình, nơi có nông trại, luôn trong. Lớp tuyết trên đảo 62%, đất ngả trắng 22%.
- **Biển mây:** mỗi dải là các **cụm** to nhỏ khác nhau, lớp xa mờ và nhoè hơn (`band(haze, soften)`), thêm vệt
  mây tầng giữa lớp xa và lớp giữa (`sea_wisps`) và mây ti trên cao (`cirrus`).
- **Font:** Baloo 2 (OFL) — `Tools/make_fonts.py` cắt 3 độ đậm, bỏ chữ Devanagari, **phóng 12% và đặt ascent/descent
  bằng tỉ lệ của Nunito** để mọi nhãn cũ vẫn vừa khung. Nunito chỉ còn trong `Tools/fonts/` cho `gen_items.py`.
- **Cửa hàng:** thêm Bùa kinh nghiệm (×2 XP thu hoạch 10 phút, `buffXpUntil`), Thuốc lớn nhanh (cả đảo -50% thời gian
  còn lại, cộng vào `cut` nên không lệch cữ tưới), Rương quý, Túi hạt quý. Năng lượng thần kỳ = đầy thanh.
- **Thời tiết đổi mỗi 15 phút** (`WeatherSys.SlotMs`). Nhiệm vụ ngày trả theo UNIT (`MissionSys.DailyXp/DailyCoin`).

## 8 đảo, ô lớn / ô nhỏ, trời mưa tự tưới (2026-09-15)

- **Thứ tự đảo:** Vườn Nhà · **Đảo Nước** (cấp 3) · **Khổng Lồ** (cấp 6) · Đảo Gió (9) · Băng (12) · Hoả (16) · Lôi (22) ·
  Vàng (28). File lưu cũ (thiếu `islandsV` hoặc < 2) được chèn 2 đảo mới vào vị trí 1, 2 (`SaveIO.MigrateIslands`);
  ai đã có Đảo Gió được mở sẵn cả hai. **Đừng đổi thứ tự `IslandSys.Defs` nữa mà không tăng `SaveIO.IslandsVersion`.**
- **Bố cục đảo** (`IslandLayout`): `Grid` 4×4; `River` 16 ô nhỏ, hàng 0–1 và 2–3 lùi ra 2 bên nửa ô (`RiverHalf`) để sông
  chạy giữa và đổ thác ở mép trước-phải; `Giant` 4 ô lớn neo ở ô (hàng chẵn, cột chẵn), phủ 2×2. Vị trí ô luôn đi qua
  `IslandSys.SlotCell` / `IslandView.SlotPos`, **đừng dùng `CellPos` cho đảo** (chỉ còn là lưới gốc cho test).
  Ô không phải đất có `Plot.none` (luôn khoá, không vẽ, không mua được); ô lớn có `Plot.big`. Hai cờ này đóng dấu lại
  mỗi lần `SyncPlots`, không phải dữ liệu người chơi.
- **Cây lớn** (`Seed.big`): Táo 6 · Cam 8 · Chuối 11 · Dừa 15. Chỉ trồng ô lớn (`PlotLogic.Fits`), cây nhỏ chỉ ô nhỏ.
  Cây lớn **không tính** vào UNIT, XpUnit, mục tiêu năng lượng, đơn hàng, túi hạt/rương/thăm bạn — một ô lớn bằng 4 ô
  đất, tính vào thì mọi giá nhảy ×4. Sheet hạt giống lọc theo cỡ ô (`SeedSheet.Big`); cửa hàng hạt ghi "CÂY LỚN".
  Giá ô lớn = công thức ×4. Ảnh cây: `Tools/slice_crops_sep14.py` (tờ D, ngày 15/9), bí đỏ cũng thay bằng ảnh mới.
- **Mưa / Bão tự tưới:** cữ nào **mở trong khung giờ Mưa hoặc Bão** thì trời tưới luôn (`WaterSys.RainWater`), không cần
  chạm, không cộng nhiệm vụ/năng lượng. Mỗi giây game kiểm cữ đang mở trên mọi đảo; lúc nạp game kiểm bù mọi cữ đã
  mở khi tắt máy (giờ mưa là hàm thuần của world seed nên kết quả như nhau).
- **Ô sắp mở:** chỉ **một** ô (ô khoá đầu tiên theo `FreeOrder`) hiện chip nhỏ "Cấp N" **ngay dưới ổ khoá**, và chỉ khi
  còn thiếu cấp (`RenderNextPlotHint`). **Không hiện giá tiền trên ruộng** (chủ dự án, 15/9) — giá nằm trong popup khi chạm.
- **Nút Thu hoạch / Tưới nước** chỉ hiện khi có ô đủ điều kiện.
- **Lướt giữa đảo:** thả tay chỉ snap **một lần** (PointerUp; EndDrag bỏ qua nếu đã snap — snap lần hai từng kéo map về
  đảo cũ). Vuốt nhanh (>700/s) theo hướng vuốt; kéo chậm quá 7% khoảng cách 2 đảo là sang đảo kế.

## Thời gian trồng kiểu Hay Day, tưới theo lần, UNIT 8 giờ (2026-09-15)

- **Thời gian gốc** (chủ dự án duyệt, `GameData.Seeds`, giây): Cà Rốt 2p · Lúa Mì 5p · Cà Chua 10p · Khoai Tây 15p · Ngô 20p ·
  Nấm 30p · Tỏi Tây 45p · Hành 1g · Súp Lơ Xanh 1g30 · Ớt 2g · Cà Tím 2g30 · Súp Lơ Trắng 3g · Bí Ngô 4g · Củ Cải 5g · Củ Dền 6g ·
  Nho 7g · Bắp Cải 8g · Chanh 10g · Lê 12g · Đào 14g · Dâu 16g · Anh Đào 18g · Dưa Hấu 20g · Dứa 22g · Bơ 24g; cây lớn Táo 6g · Cam 10g ·
  Chuối 16g · Dừa 24g. **Trần cứng 24 giờ** (`GameData.MaxGrowSeconds`) sau thời tiết, đột biến, đặc quyền — kẹp **một chỗ duy nhất**
  là `PlayerState.GrowTimeIn`. Đột biến cộng thêm tối đa 1 giờ (`Art.MaxMutationGrowAddSeconds`). Thời gian làm tròn cho dễ đọc
  (`GameData.NiceSeconds`): 5 s dưới 10 phút, phút dưới 2 giờ, 5 phút từ 2 giờ. `Fmt.Time` bỏ đuôi 0: "24g", "2g 30p", "20p".
- **Luật giá cây** (đừng sửa lẻ từng dòng, sửa theo luật rồi chạy "hành trình chơi"): **lãi mỗi vụ** tăng theo thời gian^0,55
  (× 1,5%/cấp mở khoá) → cây dài luôn lãi hơn mỗi vụ, là cây để gieo trước khi rời game/đi ngủ; **lãi mỗi giờ** giảm theo thời gian
  tới 10 giờ rồi đi ngang → cà rốt được chăm liên tục lãi/giờ ~10× bơ, là cây của lúc đang chơi; cây dài sau cấp 17 nhỉnh hơn cây
  trước cả mỗi giờ lẫn mỗi vụ. XP nghiêng về cây ngắn mạnh hơn (thời gian^0,45). Hạt = 40/45/50/55% giá bán theo độ hiếm. Năng
  lượng ∝ √thời gian. Cây lớn = 4 ô của một cây nhỏ cùng thời gian ở cấp của nó, năng lượng ×2.
- **Tưới theo lần** (`Seed.waters`, `Seed.waterCut`, `WaterSys`): mỗi cây khát N lần (1 tới 5 phút, 2 tới 45 phút, 3 tới 8 giờ,
  4 từ 10 giờ), mỗi lần tưới **chín sớm một thời gian cố định** của cây (cà rốt 30s, bí ngô 16p, bơ 1g) — tổng ~25% cây rất ngắn,
  20% tới 8 giờ, ~16% cây dài. **Người chơi chỉ thấy thời gian, không bao giờ thấy %.** Không tưới vẫn lớn, chỉ chậm hơn. Giữ nguyên:
  cữ mở ở dur/(N+1), mở tới khi cữ sau mở, không tích luỹ offline, mưa/bão tự tưới (cả lúc tắt game), hạn hán ×2, bình tưới vàng
  (`PlotLogic.WaterAhead`, đã rời khỏi view), pet tưới cùng hàm, `friendMask`. **Sàn:** một lần tưới không bao giờ làm cây chín trước khi
  cữ kế tiếp kịp mở (và mở thêm 5% chu kỳ), tổng ≤ N × thời gian × hạn và ≤ nửa thời gian — cưỡng chế trong `WaterSys.Consume`.
- **File lưu cũ:** `Plot.waterSec` (giây/lần, chốt lúc gieo) là trường mới; ô gieo trước đó có `waterSec` = 0 và **chín nốt theo luật cũ**
  (20% thời gian chia đều số cữ đã lưu) — `WaterSys.BaseCut`. `dur`/`windowCount` đã lưu không bị đụng.
- **UNIT = lãi một ô kiếm được trong 8 giờ vắng mặt** với cây tốt nhất (cả lãi nếu cây ≤ 8 giờ, phần 8 giờ nếu dài hơn;
  `MissionSys.Unit`, `UnitHours`). Lãi mỗi vụ (định nghĩa cũ) tăng gần ×3 từ Bắp Cải (8 giờ) tới Bơ (24 giờ) trong khi thu nhập mỗi giờ
  của một ô chỉ tăng ~20%; lãi mỗi giờ thì luôn là cà rốt nên không bao giờ lớn. "Hành trình chơi" in cả UNIT 4 giờ/24 giờ để so độ trôi so với thu nhập ngày.
  Đổi theo UNIT: phần thưởng đơn hàng (hệ số gốc 2), nhiệm vụ, xu thăm bạn (0,8–2 UNIT), xu rương (3–7 UNIT × bậc), trứng pet, giá shop.
- **"Chín ngay"** = 2,5 UNIT cho mỗi 8 giờ còn phải lớn (tối thiểu 0,1 UNIT). Phân bón thần kỳ 6 UNIT (chín ngay ô chờ lâu nhất).
- **Đơn hàng:** sống 2 giờ (đột biến 6 giờ) × hệ số hạng, đơn chỉ định cây chỉ gọi cây ≤ 3 giờ và cộng 1,5 × thời gian của cây.
- **Đặc quyền đảo** (+5% giá bán, +5% XP, +3% đột biến, −8% thời gian…) trước đây chỉ ghi trên đảo mà **không áp dụng ở đâu**; nay đọc trong
  `HarvestValue`, `XpFor`, `MutateChance`, `GrowTime`.
- **Người chơi tham chiếu** (`Assets/Editor/EconomyModel.cs`): 07:00 (20p) · 12:00 (15p) · 17:30 (20p) · 20:30 (30p), ngày đầu dài hơn,
  **không bao giờ dùng mã quà** (người chơi thật không có). Bảng cấp (`GameData.LevelAnchors`), giá đảo, thang ô đất, cống nạp, giá
  trứng đều chỉnh theo mô phỏng này (đợt 2, cùng ngày): ngày 1 lên cấp 7 (cấp 2 sau 1 phút, cấp 5 + trứng miễn phí sau 35 phút chơi),
  cấp 9 ngày 2, Đảo Gió ngày 4, cấp 12 ngày 6, cấp 16 ngày 11, cấp 22 ngày 22, **cấp 30 ngày 41 (~58 giờ chơi)**; một cấp mất vài phút ở
  ngày 1, ~1 ngày quanh cấp 10, ~2 ngày quanh 20, ≤ 3 ngày tới cấp 30. XP đi ngang từ 17 tới 22 (nông trại ngừng lớn giữa Đảo Hoả và Đảo
  Lôi, cấp 18–20 không mở cây nào); xu gánh nhiều hơn ở cuối để không ứ xu. Giá đảo: 15k · 60k · 250k · 700k · 1,3 tr · 3,5 tr · 5 tr;
  hệ số giá ô: 2,5 · 5 · 15 · 30 · 50 · 90 · 100. Bùa đột biến và Bùa kinh nghiệm 30 UNIT (10 phút phủ được cả nông trại cây dài).
  Trứng #2 ngày 2, #5 cuối ngày 2, #10 ngày 5 với người chơi chỉ ấp khi đã để dành đủ xu cấp kế tiếp.
- **Save cũ** (`SaveDto.econV` = 0, `SaveIO.MigrateEconomy`, chạy một lần khi nạp, cùng tinh thần `MigrateIslands`): xu × (giá cấp kế
  tiếp bảng mới ÷ bảng cũ `GameData.LegacyLevelAnchors`), XP × (XP cấp kế tiếp mới ÷ cũ), năng lượng giữ nhưng dưới 1 rương. Không có
  số nào khác theo thang cũ được lưu: thưởng đơn/nhiệm vụ/rương tính từ UNIT lúc nhận, hạt/nông sản/cống nạp là số đếm, ô đang trồng giữ
  thời gian cũ. File ghi lại mang `econV` = 1 nên không quy đổi lần hai; file thiếu khoá mà đã có ô `waterSec` > 0 là kinh tế mới, bỏ qua.
  Ví dụ save dev cấp 12: 286.400 xu → 4.686.545, 1.200/26.000 XP → 3.692/80.000. Không đặc cách xu từ mã quà.

## Thú cưng (`Core/PetSys.cs`, `UI/PetDirector.cs`, `UI/PanelPets.cs`)

- Mở ở **cấp 5**. 6 pet, ảnh do chủ dự án đưa ở `Pets/<tên>.png`, cắt bằng `Tools/slice_pets.py` → `Art/pets/<id>/`
  (portrait, idle, walk_N hướng phải, sleep, happy). Đổi tư thế thì sửa bảng `PETS` (chỉ số vùng ảnh) trong script.
- **Ấp trứng:** Thường 62% · Hiếm 27% · Sử thi 9% · Huyền thoại 2%; 40 trứng không ra Sử thi thì trứng sau chắc chắn
  Sử thi. Trùng thì pet đó lên cấp (trần 5, +1 ô mỗi lượt). Trứng đầu **miễn phí**, sau đó 35 UNIT (`PetSys.EggUnits`), 10 trứng giá 9.
- **Đi tuần mỗi 3 phút** (lượt đầu sau 20 s): tìm ô khát rồi ô chín trên mọi đảo đã mở (đảo đang xem trước), đi tới
  từng ô (khác đảo thì "poof" sang), làm việc qua `GameApp.PetWork` → cùng hàm với chạm tay. **Không bù offline.**
- **Ăn vụng:** 35% sau mỗi lượt, lấy 1 nông sản trong kho, trọng số theo bậc đột biến (Lôi Điện ×200) × độ hiếm hạt
  × 2 nếu là món khoái khẩu. Bong bóng hiện món trên đầu pet + toast.
- Pet đi trên canvas riêng `ArchipelagoView.PetLayer` (trên đảo và cầu) để khỏi dựng lại canvas thế giới mỗi frame.
  Nút pet ở HUD cạnh nút Menu (mặt pet + đồng hồ tới lượt tuần). Ảnh audit 90–97: **Tools ▸ LQ Farm ▸ Chụp thú cưng**.

## Tài khoản & lưu lên mây (Supabase) — `Online/Supa.cs`, `Online/CloudSync.cs`, `UI/StartScreen.cs`, `UI/PanelAccount.cs`

- **Cấu hình:** `Assets/Resources/Config/supabase.json` (`url`, `anonKey`). **Chỉ publishable/anon key** — file nằm trong
  APK. `SupaConfig.Ok` từ chối `sb_secret_…`/`service_role`, test cũng chặn. Không có file/khoá hợp lệ → game bỏ màn
  hình bắt đầu, chơi offline như cũ. Bảng tạo bằng `Tools/supabase/schema.sql` (chạy trong SQL Editor, chạy lại được).
- **Không dùng SDK:** REST thuần qua `UnityWebRequest` (GoTrue `/auth/v1/*`, PostgREST `/rest/v1/saves|profiles`).
  `apikey` = publishable key; `Authorization: Bearer` = access token của người chơi, **không bao giờ** là publishable key.
  Coroutine mạng chạy trên host riêng `~Supabase` (`Supa.Run`) để sống qua lúc game bị dựng lại.
- **Phiên** lưu ở PlayerPrefs (`mitfarm.supa.session`), **không** trong file lưu (file lưu được đẩy lên mây). Refresh token
  xoay vòng: token mới ghi xuống ngay. Refresh bị server từ chối (4xx) = hết phiên; lỗi mạng thì giữ phiên.
- **Thứ tự khởi động:** `GameApp.Awake` dựng canvas → nếu có cấu hình thì hiện `StartScreen` (layer 30) → đăng nhập →
  `CloudSync.Reconcile` quyết định bản lưu **trước khi** `GS.Load` → `EnterFromStart()` mới `BuildGame()`. Nhờ vậy lấy
  save từ mây chỉ là ghi đè file rồi mở, không phải dựng lại farm đang chạy. Tool Editor (audit, Dev) gọi
  `app.EnsureBuilt()` để bỏ qua màn hình này.
- **Ai thắng** (`CloudPlan.Decide`, thuần, có test): tài khoản trống → đẩy file máy lên (trừ khi file thuộc tài khoản
  khác → cất .bak, ván mới); máy chưa chơi → lấy mây; file chưa gắn tài khoản mà đã chơi → **hỏi**; cùng tài khoản và
  mây vẫn là bản máy này ghi (hoặc `device` trùng) → đẩy lên; máy khác đã ghi: máy này không chơi thêm (hash = lần đồng
  bộ trước) → lấy mây, có chơi thêm → hỏi; file của tài khoản khác → lấy mây. `PlayerState.ownerId` = user id.
- **Đẩy lên:** mỗi 45 s nếu fingerprint (file bỏ `clock`, `meta`) đổi, khi app vào nền, khi thoát. Chưa có dòng → `POST`;
  có rồi → `PATCH …&saved_at=eq.<lần trước>` — **0 dòng = máy khác vừa ghi** → đọc lại, hỏi người chơi. Mọi bản bị thay
  đều cất `lqfarm.save.json.<tag>.bak` (`truoc-dong-bo`, `tai-khoan-khac`, `tai-khoan-cu`).
- **Đổi tài khoản giữa game** đi qua `GameApp.Restart(showStart)`: đặt `GS.Local.loaded = false` trước (autosave của farm
  cũ không được ghi đè file vừa tải), huỷ object, frame sau tạo `GameApp` mới với `PlayerState` mới.
- **Menu ▸ Tài khoản** là pill ở đầu khung Menu (cạnh hai nút âm thanh / nhạc nền); chấm "!" chỉ khi đồng bộ đang lỗi/chờ chọn.
- **Dashboard cần:** Authentication ▸ Sign In / Providers: bật *Anonymous sign-ins* (nút Chơi ngay) và *Email*;
  tắt *Confirm email* khi test (bật lại thì cần SMTP riêng — SMTP có sẵn của Supabase giới hạn vài thư/giờ).
- Ảnh audit: **Tools ▸ LQ Farm ▸ Chụp tài khoản & màn hình bắt đầu** (A0–AC, phiên giả trong bộ nhớ, không gọi mạng).

## Chuyển cảnh vào nông trại (`UI/EnterCinematic.cs`, `Resources/Shaders/UICloudVeil.shader`)

- **Không phải màn hình tải, không phải video** (video phình APK, không cho thấy farm thật, không nối liền được). Một cảnh
  quay dựng bằng code, ~2,75 s, "lặn qua biển mây". Mọi đường vào (Vào nông trại, Chơi ngay/đăng nhập xong, chọn bản
  lưu, Chơi không cần mạng) đều qua `StartScreen.Enter()` → `GameApp.EnterFromStart()` → `EnterCinematic.Play`.
- **Mốc** (hằng số đầu file): thẻ nở thành đám mây trôi đi, logo bay khỏi khung, mặt trời loé, camera lùi nhẹ rồi lao vào
  đảo (0–0,45) · mây bay vụt ra từ đảo (từ 0,26) · màn mây khép từ mép (`CloseFrom` 0,58), cụm mây đặc lại (`WallFrom` 0,90)
  · **phủ kín `CoverAt` 1,10** · thở `Hold` 0,10 · mở `RevealLength` 1,55: mây tan từ giữa (1,0 s), cụm mây trôi ra ngoài,
  camera hạ từ `CamStartRatio` 0,56 × ZBase xuống Vườn Nhà (1,48 s), HUD vào từ mép (từ 0,70 s sau khi mở).
- **Luật dựng khuất — đừng phá:** `BuildGame()` chỉ chạy ở frame **sau** frame đã vẽ phủ kín, nên 1–2 s điện thoại đứng
  hình là một khung mây, không bao giờ là farm dựng dở. Start screen bị huỷ ngay trước khi dựng. Dựng xong vẫn phủ thêm
  `SettleFrames` = 3 frame rồi mới mở. Đồng hồ cảnh kẹp dt ≤ 1/30: frame dựng lâu không làm cảnh nhảy cóc.
- Mọi thứ là **hàm thuần của một đồng hồ** (`Pose(v)`) — không coroutine, không cấp phát mỗi frame. Chạm bất kỳ đâu = tua
  nhanh (phần còn lại chạy trong 0,35 s); ảnh audit dừng cảnh đúng từng mốc.
- **Màn mây** (`Art/cine/cine_veil`): RGB là ánh sáng vẽ, **alpha là mật độ** (không phải độ trong); vertex alpha là độ phủ.
  Shader đốt chỗ gần tâm + chỗ mỏng trước, nên lỗ mở theo viền cụm mây chứ không tròn. Mép giữ khá sắc (`_Soft` 0,04):
  mép mềm để lại mây bán trong suốt đè lên farm, nhìn như ảnh chụp chồng.
- **Ấm là cộng sáng, không nhân màu:** glow, tia sáng, viền sáng mép lỗ dùng `UIAdditive`/cộng trong shader. Nhân vàng lên
  bóng tím của mây ra màu be bẩn (đã thử).
- **Mây mờ dần đè lên đảo là bóng ma.** Mây bay qua nằm SAU đảo, trong chính chồng lớp của start screen (4 lớp sâu
  `depth_sky/far/island/near`); mây phía trước chỉ đi ở mép khung. Cụm mây của bức tường chỉ hiện khi màn mây đã dày.
- Lúc mở, màu mây lấy **giờ + thời tiết** của farm (`DayCycle.Sample`, `WeatherFx.Blended`, cùng công thức SkyView) và bắt
  đầu đổi ngay trong nhịp thở: đêm thì mây đã chàm khi lỗ mở, không phải mây trắng quanh một lỗ đen.
- HUD vào theo `Hud.EntranceParts()` (thẻ người chơi từ trái, ví từ phải, đổi đảo/pet/Menu từ dưới, đĩa thời tiết pop tại chỗ).
  Lớp trời (`GameApp.BackgroundLayer`) hạ 46 px theo camera.
- Trong lúc chiếu: `Tutorial.Tick` dừng (tới 0,7 s sau khi xong), `GameApp.Toast` giữ tin cuối rồi hiện sau. Tool Editor gọi
  `EnsureBuilt()` giữa chừng → `Skip()`, vẫn tức thì. `try/catch` + watchdog 30 s: lỗi trong cảnh → vào farm ngay, không bao
  giờ để người chơi kẹt sau màn mây. **Không có** cảnh khi `Restart(false)` (kéo save từ mây giữa game) — vào thẳng như cũ.
- Hiệu năng: canvas lồng riêng order 31 trên cả toast; lúc phủ kín ≤ 3 lớp full-screen (màn mây, glow, cụm mây ở góc), tia
  sáng tắt khi phủ kín. Xong thì huỷ layer + material và `Resources.UnloadAsset` các texture `Art/cine`.
- Art: `Tools/gen_cinematic.py` → `Art/cine/` (mass/puff có mipmap — khai trong `FarmTextureImporter.WantsMips`; veil và
  shafts không). Âm thanh: `Tools/gen_cine_audio.py` → `Audio/cine_whoosh.ogg`, `cine_chime.ogg` (`SfxId.Whoosh/Chime`).
- Ảnh audit: **Tools ▸ LQ Farm ▸ Chụp chuyển cảnh** → `Screenshots/T00…T29` (khởi động lại game về start screen **không
  đăng nhập**, bấm vào như nút thật, dừng cảnh mỗi 0,1 s; build farm vẫn chạy thật sau màn mây). Muốn xem ban đêm:
  `DayCycle.ForceHour(22.5f)` rồi `UiAudit.RunCinematic(0.1f, "TN")`.

## Bố cục HUD (`Assets/Scripts/UI/Hud.cs`)

- **Trên trái:** thẻ người chơi → dải nhiệm vụ → hai nút **Thu hoạch / Tưới nước** (pill kính, mỗi nút một màu,
  đếm số ô, xám khi không có việc). Không có dải nhiệm vụ thì hai nút dời lên sát thẻ.
- **Đĩa thời tiết:** chỉ icon + vòng thời gian (`WeatherRing`, cập nhật mỗi frame). Chạm → `SeasonPanel`.
- **Dưới trái:** đổi đảo. **Dưới phải:** nút Thú cưng (từ cấp 5) rồi nút Menu; 8 điểm đến trượt lên trong một khung 2×4, đầu khung là
  pill Tài khoản + nút âm thanh + nút nhạc nền, đáy khung là nút **Hướng dẫn chơi**. Huy hiệu của từng ô cộng dồn lên nút Menu.
  Icon: Nâng cấp = mũi tên lên, Kho = nhà kho, Sưu tập = cuốn sổ (`Tools/gen_icons.py`).
- **Không có nút Gieo trên HUD** (theo yêu cầu chủ dự án): ô trống được gieo bằng cách chạm vào nó.
  `GameApp.PlantAll` và `QuickActions.PlantUnlocked` vẫn giữ; thông báo mở khoá dùng
  `QuickActions.AnnouncedAt` để không hứa "Gieo nhanh" không ai tìm thấy. Đừng thêm lại rail bên phải.

## Quy tắc kiến trúc (có cưỡng chế)

`Assets/Editor/ArchitectureGuard.cs` **làm fail build** nếu vi phạm. Chạy tay:
menu **Tools ▸ LQ Farm ▸ Kiểm tra kiến trúc**.

1. `GS` chỉ được có 7 thành viên tĩnh: `Local`, `Viewing`, `Now`, `PlotCount`, `Save`, `Load`,
   `Reset`. Mọi trạng thái người chơi thuộc về `PlayerState`.
2. `Assets/Scripts/Farm/` **không được** đọc `GS.Local` — lớp thế giới đi qua `FarmContext`
   (`Ctx.owner` / `Ctx.actor`).

Cả hai để giữ cho phần bạn bè online sau này rẻ. Xem `REDESIGN.md` §9.

## Mã quà tặng (`Core/GiftCodes.cs`, `UI/PanelCode.cs`)

- Menu ▸ **Nhập code**. Mã so khớp không phân biệt hoa/thường, bỏ khoảng trắng và gạch. Mỗi mã dùng **một lần mỗi
  nông trại** (`social.redeemed` trong file lưu).
- `TONGDAIYUMMY`: +22.000.000 XP, +22.000.000.000 xu và **+300 trứng thú cưng miễn phí** (`PlayerState.petFreeEggs`) — bộ
  quà cho người test. Trứng thêm vào sau: máy đã nhập mã trước đó nhập lại vẫn nhận **chỉ phần trứng**, một lần (đánh dấu
  `TONGDAIYUMMY+eggs` trong `social.redeemed`). Trứng miễn phí chỉ dùng khi đủ cho cả lượt ấp (1 hoặc 10). Bảng mã nằm trong bản build, ai
  giải nén cũng đọc được: chỉ dùng cho test/sự kiện.
- Vì mã này, **xu là `long`** (`PlayerState.coin`, `PlayerDto.coin`). HUD hiển thị số lớn dạng "22 tỷ" (`Fmt.Short`).

## Kinh tế dùng chung cho game và mô phỏng

Gieo / tưới / thu hoạch nằm ở `PlotLogic.Plant/Water/Harvest`, bán sỉ ở `PlayerState.SellAll`, mở rương ở
`PlayerState.OpenChests`. `IslandView`/`GameApp` chỉ gọi các hàm đó rồi vẽ hiệu ứng. **Đừng tính lại kinh tế trong
view** — "Kiểm tra hành trình chơi" chỉ đáng tin khi nó chạy đúng code của game.

Mốc cấp trong chương nhiệm vụ đã chỉnh theo mô phỏng: 4 / 7 / 10 / 12 (trước là 4 / 8 / 16 / 25, rồi 4 / 7 / 10 / 13 —
với cây mọc hàng giờ, 12 → 13 một mình mất 3 ngày), thăm bạn 6 lần
(game chỉ có 6 người bạn, mỗi người một lần/ngày — trước đòi 10 lần là phải chờ sang ngày).

## Build Android

**Tools ▸ LQ Farm ▸ Build Android (APK thử)** → `Builds/Android/MATUFarm-<version>-<ngày-giờ>.apk` (IL2CPP ARM64,
ký bằng khoá debug, chỉ màn ngang). Build gọi `AppIcons.Apply()` trước: icon từ `Tools/gen_appicon.py` →
`Assets/Art/AppIcon/` (bản adaptive cho Android), màn khởi động không logo Unity, nền màu trời.

- **Cài module Android khi Editor đang mở thì phải khởi động lại Editor.** Không thì build báo "Success" nhưng
  không có APK (bước đóng gói ném "Build target 'Android' not supported"). `BuildAndroid` giờ kiểm file APK thật.
- **iOS:** **Tools ▸ LQ Farm ▸ Build iOS (Xcode project)** → `Builds/iOS/MATUFarm-Xcode/`. Unity chỉ xuất project
  Xcode; muốn ra bản cài được phải mở bằng Xcode trên Mac, chọn Team (Apple Developer) ở Signing rồi Archive.
  Máy dev hiện **chưa cài Xcode**.
- Đổi `productName` là Editor đổi thư mục lưu (`~/Library/Application Support/DefaultCompany/<productName>/`).
  Hiện là `.../MATU Farm/`; ván cũ còn ở `.../MiT FArM/`, `.../Farm3/` và `.../LQ Farm/`. PlayerPrefs của Editor cũng theo
  productName (âm thanh, phiên đăng nhập, mã máy bắt đầu lại). Trên Android thư mục lưu theo mã gói.

## Build Web (GitHub Pages)

**Tools ▸ LQ Farm ▸ Build Web (GitHub Pages)** (`Editor/BuildWebGL.cs`) → `Builds/WebGL/MATUFarm/`, rồi `Tools/deploy_web.sh` đẩy
**chỉ bản build** lên nhánh `gh-pages` (một commit, force-push mỗi lần — không để bản build 40 MB chồng vào lịch sử; `main`
không bị đụng). Bật một lần: repo Settings ▸ Pages ▸ Deploy from a branch ▸ `gh-pages` / root.

- **Brotli + decompression fallback:** Pages không gửi `Content-Encoding: br`, loader tự giải nén bằng JS. Tắt fallback là trang lỗi.
- Template `Assets/WebGLTemplates/MATU/` (màn tải kiểu trời xanh + icon app, nhắc xoay ngang trên điện thoại, DPR tối đa 2).
  **`autoSyncPersistentDataPath: true`** trong template — thiếu nó file lưu chỉ nằm trong RAM, tải lại trang là mất nông trại.
- Supabase đã mở CORS cho mọi origin (đã kiểm với origin `s2soma.github.io`), đăng nhập/đồng bộ chạy được trên web.
- Chuyển build target sang WebGL là import lại toàn bộ asset; `BuildAndroid` tự chuyển về Android khi build APK.
- Trình duyệt chặn âm thanh tới lần chạm đầu tiên — nhạc nền bắt đầu sau cú chạm đầu, không phải lỗi.

## Node.js

Cài user-local ở `~/.local/node` (không sudo, không đụng hệ thống). Không có trong PATH mặc định —
gọi bằng đường dẫn tuyệt đối.
