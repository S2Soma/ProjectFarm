# LQ Farm — Plan Re-design lớn

> Trạng thái: **đã chốt hướng, chưa viết code**. Cập nhật lần cuối 2026-09-12.
> Nguồn: 4 agent chuyên môn (thời tiết/tag, kinh tế/đảo, kiến trúc dữ liệu, UX) + khảo sát code hiện tại.
> Baseline: 6.286 dòng C#, biên dịch sạch qua Roslyn.

---

## 0. Những gì đã chốt

| # | Vấn đề | Chốt | Ở mục |
|---|---|---|---|
| 1 | Tag "Thời Vụ" — khoá hay thưởng? | ✅ **Thưởng**, không khoá | §3.3 |
| 2 | Chỗ tiêu xu | ✅ **Shop vật phẩm quay lại** | §4.4 |
| 3 | Quy mô ship | ✅ **Đủ 6 đảo** | §7 |
| 4 | Bản đồ | ✅ **Tất cả đảo trên một canvas** (chốt trước đó) | §2 |
| 5 | Save cũ | ✅ **Reset sạch** (chốt trước đó) | §5 |
| 6 | Rương + bạn bè | ✅ **Giữ**, dựng sẵn cho online | §9 |

Các quyết định kỹ thuật trong §1–§6 tôi tự quyết và nêu lý do; bạn phản đối chỗ nào thì tôi sửa chỗ đó.

---

## 1. Kiến trúc — trả lời câu bạn hỏi hôm trước

Bạn nói đúng hai điểm: thư mục lộn xộn, và tôi đang gen toàn bộ component lên scene. Nhưng hai
điểm đó cần hai câu trả lời khác nhau.

### 1.1 Gen UI bằng code — giữ, có điều kiện

Tôi giữ cách dựng UI bằng code cho **panel và HUD**, vì lý do thật chứ không phải vì lười đổi:

- Sửa được không cần chiếm Editor lock. Suốt dự án này cầu MCP đã treo 3 lần, mỗi lần 30 phút.
- Diff được trong git. Prefab/scene YAML merge rất tệ khi hai người cùng sửa.
- Không vỡ GUID.

Nhưng cách này đang **trả giá thật** ở đúng ba chỗ, và redesign làm ba chỗ đó tệ hơn:

| Chỗ | Vì sao code-gen sai ở đây | Đổi thành |
|---|---|---|
| Ô đất | 16 ô → **96 ô**. Vật thể lặp nhiều nhất, phức tạp nhất, cần bản LOD | **Prefab + object pool** |
| Đảo | Lặp 6 lần, cần nạp/nhả theo khoảng cách | **Prefab + pool** |
| Hiệu ứng (spark, burst, droplet) | `Sparkle(i,18)` cấp phát 36 GameObject rồi huỷ. Thu hoạch 96 ô = hàng nghìn GO bị GC trong 1 frame | **FxPool cố định** |

Panel giữ nguyên code-gen. Ô đất, đảo, FX chuyển sang prefab + pool. Đây là chỗ prefab thật sự
đáng tiền (instancing, pooling, LOD), không phải chỗ nó chỉ là "cách làm đúng chuẩn Unity".

### 1.2 Dữ liệu nội dung — **giữ bảng C#, KHÔNG chuyển ScriptableObject**

Đây là chỗ tôi đi ngược "chuẩn Unity" và muốn nói rõ lý do. SO có một lợi ích: designer chỉnh
trong Inspector không cần compile. Nhưng ở dự án này designer là bạn và tôi, và luồng làm việc
thật là *bạn nói số → tôi sửa file*. Với luồng đó bảng C# **tốt hơn** SO: diff được, review được,
grep được, không cần mở Editor. Chuyển sang SO sẽ làm tôi phải sinh file .asset qua editor script
rồi sửa YAML — chậm hơn và dễ sai hơn.

Việc cần làm là **tách theo hệ thống**, không phải đổi định dạng:

```
Assets/Scripts/
  Core/        GameState, PlayerState, FarmContext, Clock, SaveIO
  Content/     Crops.cs  Islands.cs  Weather.cs  Tags.cs  Missions.cs  Rarity.cs  Chests.cs
  Systems/     WeatherSys  TagSys  WaterSys  MutationSys  MissionSys  OfflineSys
  World/       ArchipelagoView  IslandView  PlotView  MapCamera  FieldAnimator  FxPool
  UI/          (như hiện tại)
```

`GameData.cs` 258 dòng hiện đang chứa cây + rương + chương + shop + bạn bè + sưu tập. Sau redesign
nó sẽ chứa gấp ba. Tách ngay bây giờ, trước khi viết thêm.

### 1.3 Dọn thư mục — số liệu cụ thể

| Việc | Dung lượng | Ghi chú |
|---|---|---|
| Xoá `Resources/Art/ui/` | **2,4 MB** | `Art.Ui()` có **0 call site** — đã kiểm |
| Xoá `Resources/Art/tiles/` | **544 KB** | Bộ tile procedural đã revert |
| Xoá `Assets/Hyper_Casual_UI/` | **10 MB** | Đã loại (chữ Anh nướng sẵn) |
| Dọn thư mục gốc | **~26 MB** | zip web game, 3 PNG ChatGPT, 1 PNG Gemini |

Resources **luôn** bị đóng gói toàn bộ vào build kể cả không ai tham chiếu — nên 2,9 MB đầu là
cân nặng thật trên máy người chơi, không phải rác trên đĩa.

### 1.4 Một rủi ro phụ thuộc vừa phát hiện

Save mới cần Newtonsoft JSON (§5). Package **đã có sẵn** (3.2.2) — nhưng chỉ là phụ thuộc *gián
tiếp* của `com.unity.ai.assistant` và `com.unity.ai.inference`. `ai.assistant` chính là package MCP,
tức là **công cụ dev, không nên nằm trong build mobile**. Gỡ nó ra là mất Newtonsoft và vỡ hệ thống
save.

→ Phải khai báo `com.unity.nuget.newtonsoft-json` **trực tiếp** trong `manifest.json`.

---

## 2. Bản đồ — nhiều đảo trên một canvas

Bạn chọn "tất cả đảo trên một canvas". Đây là phương án tôi từng cảnh báo là khó nhất; dưới đây là
cách tôi làm cho nó chạy.

### 2.1 Ba mức zoom rời rạc, không phải một dải liên tục

Pinch chạy liên tục *trong lúc* vuốt, nhưng **bắt về mức gần nhất khi nhả tay** (ease 0,18 s).

Lý do không dùng dải liên tục: UI dựng bằng code có ~40 rect overlay chỉnh tay. Một dải liên tục
nghĩa là ngưỡng LOD ở mọi giá trị — không kiểm thử nổi. Ba mức nghĩa là **ba nhánh code, ba bộ số
đã kiểm**. Nó cũng làm câu "tôi đang ở đâu" trả lời được.

| | **Z2 — Ruộng** | **Z1 — Đảo** | **Z0 — Bản đồ** |
|---|---|---|---|
| `localScale` | **1,05** (đúng giá trị `FitFarm` hiện tại) | **0,62** | **0,28** |
| Ô trên màn | 176×88 | 104×52 | 47×24 |
| Trong khung | 1 đảo, đủ 16 ô | 1 đảo + mép đảo bên | 3–4 đảo |
| Overlay | đầy đủ | tile + cây + **badge thành ghim cỡ cố định**; chip ẩn | **không overlay ô nào**; mỗi đảo 1 thẻ tên |
| Pan | **kẹp trong ranh giới đảo đó** | tự do dọc hàng đảo | tự do dọc hàng đảo |
| Chạm | **trực tiếp** — một chạm là hành động | **chọn rồi xác nhận** — hai chạm | **chỉ đảo** — di chuyển camera, không bao giờ là hành động |

**Z2 kẹp trong một đảo.** Không pan sang đảo bên ở Z2 được; phải zoom ra. Đây là luật giữ nguyên
**mọi con số chỉnh tay trong `FarmView`** — `GapX 1,60`/`GapY 1,44` tính theo trụ cột 32,3px và
váy đất 22,54px; hai đường cảnh vật `y = ±(317,8 − 0,5225|x|)`; `StageHeight` chặn ở 78 vì luống
sau bắt đầu ở +78,96; dải váy 25px của `DiamondHit`; offset +96 của popup ô. **Z2 giống hệt hôm
nay từng pixel.**

### 2.2 "Chạm trúng ô 20px thì sao" — không bao giờ xảy ra

Ngưỡng chia theo kích thước thật trên màn:

- **≥ 88 px → thao tác trực tiếp (Z2).** Vùng chạm `DiamondHit` là 12.600 px² ≈ ô vuông 112px,
  trên chuẩn 48dp (= 80px trong hệ quy chiếu này).
- **47–88 px → chọn rồi xác nhận (Z1).** `DiamondHit` cho 112×56 ở mức này — chiều cao trượt
  chuẩn, và giữa các hình thoi có khe hở. Nên ở Z1 **đổi sang bắt ô gần tâm nhất** (Voronoi trên
  16 tâm ô, bán kính 64px): không khe hở, không nhập nhằng, mọi chạm trong đảo đều ra đúng một ô.
  Kiểu hỏng đổi từ "không có gì xảy ra" thành "trúng ô bên cạnh" — dễ chịu hơn nhiều.
  **Trả giá cho kiểu hỏng đó bằng cách khiến chạm ở Z1 không phá gì**: chạm là *chọn* (viền kem
  3px), và một **thanh hành động** trồi lên ở đáy `Bottom(0,100), 320×88` mang đúng một động từ hợp
  lệ của ô đó — `Thu hoạch` / `Tưới` / `Gieo` / `Mở · 12.000`. Chạm thanh mới thực thi.
- **< 47 px → ô đất không phải mục tiêu (Z0).** Đơn vị là cả hòn đảo.

Lưu ý thật: khoảng cách dọc giữa hai ô là 60,48px ≈ **5,75 mm**, trong khi sai số đầu ngón tay
σ ≈ 2 mm — tức **ngay hôm nay** đã có tỉ lệ chạm nhầm sang hàng bên. Thứ cứu nó là popup xác nhận.
Nên **mọi hành động tốn tài nguyên phải nằm sau popup ở mọi mức zoom**: gieo, tưới, mua ô, thúc
chín. Thu hoạch được phép chạm-một-lần vì chạm nhầm ô chín không mất gì.

### 2.3 Đảo xếp **một hàng ngang**, không phải lưới 2D

Game là landscape. Một hàng ngang nghĩa là pan chỉ một trục, Z0 là một dải vuốt, thanh chuyển đảo
là `‹ 2/6 ›` tuyến tính, và **không bao giờ cần minimap**. Lưới 2D không mua được gì thêm mà phải
trả bằng một minimap cộng một lớp bug "cây của tôi ở đảo nào không tìm ra".

6 đảo × bước 1.560px (đảo rộng 1.144 + 420 biển) = **canvas 7.800px**. Ở Z0 = 0,28 là 2.184px ≈
**1,4 màn hình** — vuốt một cái là hết.

Ba đường đi giữa các đảo, đều đáp xuống cùng một trạng thái camera (dư thừa là đúng với điều hướng):
1. **Nút mũi tên** trên thanh đáy — bay 0,35 s ở mức zoom hiện tại.
2. **Vuốt** ở Z1 hoặc Z0.
3. **Chạm đúp** đổi Z2↔Z1 quanh điểm chạm. Đây là đường nhanh và phần lớn người chơi sẽ không bao
   giờ dùng tới pinch.

**Đảo khoá vẫn hiện trong thanh chuyển**, xám và có ổ khoá — người chơi luôn thấy cái tiếp theo.
Đó là phần tiếp thị, và nó miễn phí.

**Khai thác hệ quả của canvas chung: bộ đếm cống nạp đặt ngay trên đảo khoá** dưới dạng biển hiệu
(`Nho Tím 412 / 660`). Bản đồ trở thành mặt phẳng mục tiêu chính — đáng giá hơn bất kỳ menu nào.

### 2.4 Số GameObject — chỗ này quyết định game có lag không

| | GO | ở 6 đảo |
|---|---|---|
| Mỗi ô đất | 16 | — |
| Mỗi đảo, làm ngây thơ | 280 | 1.680 |
| Mỗi đảo, thường trú (vỏ + ruộng phẳng 16 tile trơn) | 27 | 162 |
| Ruộng đầy đủ, **pool 3 cái** | 275 × 3 = 825 | 825 |
| **Tổng thiết kế** | | **≈ 1.340** |

Ngây thơ thì số ô đất là `256 × N`; thiết kế này là `256 × min(N,3) ≤ 768` — **hằng số theo N**.

Chỗ thật sự giết hiệu năng không phải số GO mà là **`Update()`**: `Swayer` + `Bobber` gắn trên mỗi
ô nghĩa là 128 lệnh gọi `Update` mỗi frame ở 6 đảo, 320 ở 20 đảo. Thay bằng **một `FieldAnimator`**
duyệt danh sách ô LOD0: **1 `Update`, ≤48 phần tử, bất kể N**.

Ngưỡng làm ngây thơ chết: **N ≈ 8**.

### 2.5 Rủi ro tôi coi là lớn nhất: chạm vs kéo

Hiện mỗi ô có `Button`. `Button` bắn `IPointerClickHandler` khi nhấn và thả trên cùng một
GameObject — **bất kể ngón tay đã đi bao xa ở giữa**. Ngay khi có `MapCamera`, **kéo bản đồ sẽ
gieo hạt**. Chuyện này sẽ không tinh tế và không hiếm.

→ Thay `Button` bằng `TapOrDrag`: chỉ bắn `onTap` nếu **đi < 12px VÀ < 0,40 s VÀ chưa vào drag**;
mọi sự kiện drag chuyển thẳng cho `MapCamera`. `DiamondHit` giữ nguyên — nó chưa bao giờ là vấn đề.

Kiểm chứng ở **bước 4 với một đảo duy nhất**, trước khi làm đa đảo. Test hồi quy: kéo 30px bắt đầu
từ một ô phải cho **0** lần `onTap`.

---

## 3. Hệ thống mới

### 3.1 Thời tiết — 6 trạng thái, đổi mỗi giờ

| Thời tiết | Thời gian lớn | Giá bán | XP | Đột biến | Luật riêng |
|---|---|---|---|---|---|
| **Nắng** | ×1,00 | ×1,00 | ×1,00 | ×1,00 | Giá hạt giống −10% |
| **Mưa** | ×0,80 | ×0,95 | ×0,95 | ×1,10 | **Tự tưới** — tặng sẵn 1 lượt khi gieo |
| **Gió Lớn** | ×0,85 | ×0,90 | ×1,20 | ×0,90 | 8% cơ hội tự gieo lại khi thu hoạch |
| **Tuyết** | ×1,25 | ×1,30 | ×0,90 | ×1,35 | Đột biến lệch về bậc thấp — nhiều mà rẻ |
| **Bão** | ×1,15 | ×0,80 | ×1,40 | ×1,60 | **Không bao giờ phá cây.** Lệch về bậc cao |
| **Hạn Hán** | ×1,30 | ×1,15 | ×0,80 | ×0,70 | Tưới cho **+40%** thay vì +20%. Khoá tới cấp 8 |

Tần suất (sau luật không lặp): Nắng 26,9% · Mưa 22,0% · Gió 17,2% · Tuyết 13,5% · Bão 10,2% ·
Hạn 10,2%. Hai ràng buộc: **không lặp liền**, và **Bão/Hạn không nối tiếp nhau**. 24 giờ đầu của
tài khoản mới không có Bão/Hạn.

Tổng EV qua một chu kỳ đầy đủ: **+4,2% ở cấp 15, +5,6% ở cấp 30**. Cố ý dương chứ không phải 0 —
nếu EV bằng 0 thì phiên trung bình y hệt như không có thời tiết, trong khi những giờ xấu vẫn đau.

**Luật quan trọng nhất toàn hệ thống: mọi hệ số chốt lúc gieo, đóng dấu vào ô đất.** Không bao giờ
tính lại. Đây là thứ khiến thời tiết **không thể lấy mất giá trị của người chơi đang offline** —
và khiến `Elapsed()` vẫn là một phép trừ thay vì một tích phân qua từng giờ đã đóng app.

> Hai agent mâu thuẫn ở đúng điểm này: agent kiến trúc muốn tính giá bán theo thời tiết *lúc thu
> hoạch*. Tôi chọn theo agent thời tiết. Tính lúc thu hoạch tạo ra khiếu nại "ngủ dậy cây mất giá",
> và ép người chơi canh giờ thu hoạch — sai hoàn toàn với game phiên ngắn.

**Vì sao thời tiết không bao giờ cắt ngang một vụ:** cây lâu nhất là Bơ Sáp 470s → 329s ở cấp 30 =
**5,5 phút**, so với cửa sổ **60 phút**. Bất khả thi về mặt cấu trúc.

**Thứ người chơi phải thấy, xếp theo độ quan trọng:**
1. **Đồng hồ đếm ngược hết giờ (mm:ss)** — quan trọng nhất. Sáu hệ số là bảng tĩnh, thuộc trong
   một tuần là hết thông tin. Đồng hồ là biến sống duy nhất, và nó tạo ra quyết định duy nhất của
   cả hệ thống: *gieo bây giờ, hay chờ N phút để đổi bài*.
2. Icon + 2 hệ số đầu bảng.
3. Thời tiết kế tiếp, **chỉ lộ ở T−10 phút** (trước đó là "?"). Lộ sớm cả tiếng là người chơi lên
   kế hoạch cả ngày rồi không cần mở app nữa.
4. Badge trên từng ô: vụ này gieo dưới thời tiết nào.
5. **Hoá đơn thu hoạch tách dòng** — bắt buộc, xem §6.

### 3.2 Tag cây trồng — 6/28 cây, xoay mỗi 12 giờ

Xoay lúc **06:00 và 18:00** — hai đỉnh dùng điện thoại ở VN, nên lần đổi rơi vào lúc đăng nhập như
một phần thưởng chứ không phải giữa phiên.

Cơ cấu cố định mỗi lượt: **2 cây bậc 0–1 · 2 cây bậc 2 · 2 cây bậc 3** — để người chơi ở *mọi* cấp
luôn có ít nhất 2 cây được tag mà mình trồng nổi.

| Tag | Hiệu ứng | Giới hạn |
|---|---|---|
| **Kinh Nghiệm** | XP ×2,00 | mọi cây |
| **Giá Cao** | Giá ×1,35 | mọi cây |
| **Đột Biến** | Tỉ lệ đột biến ×1,75 | **chỉ cây bậc 2–3** |
| **Thời Vụ** | Giá **và** XP ×2,20 trong thời tiết chỉ định | tối đa 1 tag/lượt |

**Vì sao ×1,35 mà không cao hơn:** giá hạt không tăng theo tag, nên hệ số giá được khuếch đại rất
mạnh về *biên lợi nhuận*. Ở cấp 30 năm cây đỉnh bảng gần như bằng nhau (4,64 → 3,95 xu/giây, chênh
1,17 lần). Gắn ×1,35 lên Chuối cho 6,54/giây so với Bơ Sáp 4,64 = **1,41 lần** — đủ để đổi thứ mình
trồng hôm nay, nhưng chưa đủ để cây không tag thành vô nghĩa. ×1,60 sẽ cho 2,3 lần biên, và lúc đó
28 cây chỉ còn 6 cây đáng trồng.

Ba tag giá trị cố ý rơi vào cùng dải 1,3–1,45 lần nhưng **đỉnh ở ba giai đoạn khác nhau**: XP mạnh
nhất lúc đầu (xpNeed tăng 1,28^lv, XP là tài nguyên khan), Giá Cao đều, Đột Biến chỉ đáng kể về sau
(tỉ lệ 0,42 → 0,735). Tag đột biến bị khoá ở cây bậc 2–3 chính vì ở cấp thấp nó chỉ cho +15% —
một tag trông như phần thưởng mà chỉ cho +15% còn tệ hơn không có tag.

> ⚠️ Một cảnh báo: dưới tag XP, **Nấm Rừng** (cấp 5, 0,60 XP/giây — cao nhất game, hạt chỉ 137 xu)
> lên 1,20 XP/giây, gấp đôi cây đứng nhì. Đây là tính năng chứ không phải lỗi: một cây cấp 5 rẻ
> tiền thành vua trong 12 giờ và *mọi* người chơi bất kể cấp đều tham gia được. Nhưng nó sẽ là
> sự kiện tag ồn ào nhất mà bạn ship.

### 3.3 ✅ Tag Thời Vụ — thưởng, không khoá (đã chốt)

Spec ban đầu: *"tag thời tiết (cây chỉ trồng được trong thời tiết chỉ định)"*. Bạn đã đồng ý đổi
sang dạng thưởng. Giữ lại phần số học ở đây làm hồ sơ quyết định:

Nếu thời tiết chỉ định là Hạn Hán (10,2% số giờ), nó chiếm ~1,2 trong 12 giờ của lượt tag. Một người
chơi có 5 phiên rải trong cửa sổ đó có **1 − (1 − 0,102)⁵ ≈ 42%** khả năng **không một lần nào** ở
trong game đúng lúc đó. Bốn trên mười người chơi nhìn thấy phần thưởng quảng cáo trên thẻ đăng nhập
rồi **không có cách nào lấy được**. Trong 58% còn lại, phần lớn lúc đó ruộng đã đầy cây khác.

Một phần thưởng hiện ra rồi bị từ chối là hình dạng tệ nhất trong live-ops. Người chơi đọc nó là lỗi.

**"Cây Thời Vụ" — thưởng, không khoá:**

| Điều kiện | Hiệu ứng |
|---|---|
| Gieo đúng thời tiết chỉ định | **Giá ×2,20 và XP ×2,20** |
| Gieo thời tiết khác | ×1,00 — bình thường, **không phạt** |
| Có trồng được không | **Luôn luôn** |

Giữ nguyên cảm giác bạn muốn — *"bỏ hết đi, trời đang mưa, gieo nho ngay"* — nhưng bỏ mọi sự từ
chối. Nó đổi một cái khoá thành một cuộc hẹn, và cuộc hẹn đúng là thứ bạn muốn từ một đồng hồ chạy
theo giờ.

×2,20 an toàn vì EV ngang với các tag luôn-bật: nếu thời tiết chỉ định là Mưa (22%),
EV = 0,22 × 2,20 + 0,78 × 1,00 = **1,26** — còn *thấp hơn* tag Giá Cao 1,35 — trong khi khoảnh khắc
đỉnh thì lớn hơn nhiều (Lê cấp 20 nhảy 2,29 → 7,64 xu/giây, **3,3 lần**). Đỉnh cao, EV bằng. Đó
đúng là hình dạng của một cái móc câu.


### 3.4 Đột biến — 4 bậc hiếm, gộp với nguyên tố, **giữ tên riêng**

Hai agent kinh tế/kiến trúc đều đề nghị giữ nguyên tố (Băng/Hoả/Lôi) **độc lập** với bậc hiếm. Tôi
**không theo**: để độc lập thì mỗi cây có 4 bậc × 3 nguyên tố = 12 biến thể, sổ sưu tập thành
**364 ô** — một bức tường không ai lấp nổi — và nó thêm một trục mà spec của bạn không có.

Nhưng agent UX phản đối phương án ngược lại, và câu của họ làm tôi đổi cách đặt tên:

> *"Băng Giá Khoai Tây" là thứ người chơi kể cho bạn bè. "Khoai Tây hiếm" là một con số thống kê.*

Họ đúng. Vứt bỏ bốn danh tính **có tên, có tranh vẽ, sưu tầm được** để đổi lấy một tính từ trừu
tượng là lỗ. Nên: **gộp hai trục, nhưng bậc hiếm mang tên nguyên tố chứ không phải mang tính từ.**
Bậc hiếm là *cấp độ*, tên nguyên tố là *danh tính* — người chơi gọi nó bằng tên, giao diện xếp nó
bằng bậc.

| Bậc | Tên hiển thị | Trọng số* | Giá | XP | Mọc lâu thêm | Quả thêm | Art |
|---|---|---|---|---|---|---|---|
| — | Không đột biến | — | ×1,0 | ×1,0 | — | +0 | gốc |
| 1 · thường | **Ngọc Bích** 🟢 | 70% | ×1,5 | ×1,5 | +20% | +0 | *cần thêm* — tô xanh ngọc + lấp lánh |
| 2 · hiếm | **Băng Giá** 🔵 | 22% | ×2,5 | ×2,2 | +40% | +1 | ✓ có sẵn |
| 3 · cực hiếm | **Viêm Hoả** 🟣 | 7% | ×4,5 | ×3,5 | +70% | +2 | ✓ có sẵn |
| 4 · huyền thoại | **Lôi Điện** 🟠 | 1% | ×10 | ×6 | +120% | +3 | ✓ có sẵn |

Chỉ phải thêm **một** danh tính mới (Ngọc Bích, bậc thấp nhất) — rẻ vì nó dùng tô màu như 27 cây
không có art nguyên tố riêng vẫn đang dùng. `Theme.Rarity[]` đã sẵn đúng 4 mã màu theo đúng thứ tự
xanh lá → xanh dương → tím → cam, khớp luôn.

\* trọng số *với điều kiện đã đột biến*. Tỉ lệ đột biến tổng giữ nguyên công thức hiện có
`min(0,45; 0,012·lv + 0,06)` — 7,2% ở cấp 1, 24% ở cấp 15, 42% ở cấp 30. Công thức này tốt, không
động vào.

Tỉ lệ tuyệt đối: Huyền thoại **1/556 lần gieo ở cấp 10**, **1/238 ở cấp 30**. Bảo hiểm xui: chắc
chắn ra Huyền thoại nếu 400 lần gieo chưa có lần nào.

Sổ sưu tập thành **28 cây × 5 trạng thái = 140 ô** — sát con số 135 đang có. 5 bộ sưu tập hiện tại
giữ nguyên *cả tên lẫn hình dạng*: "Tứ Nguyên Lúa Mì" vẫn là 4 ô Lúa Mì, chỉ khác là 4 nguyên tố
giờ xếp theo bậc hiếm. **Sổ sưu tập gần như không phải làm lại** — agent UX chỉ ra rằng
`08_collection.png` đã có sẵn ô xám "Chưa có", thẻ bộ, mốc thưởng và thưởng trọn bộ. Phần duy nhất
thật sự mới trong spec sổ sưu tập của bạn — "bôi đen cho tới khi thu hoạch được" — chính là thứ
panel đó đang làm. **Ước lượng lại: một ngày, không phải một sprint.**

Giới hạn mọc lâu thêm: **tối đa +15 phút tuyệt đối**, để Bơ Sáp huyền thoại (470s → 1.034s) vẫn
nằm gọn trong một đêm.

**Luật không-nhân-hai, bắt buộc:** giá trị vụ thu = `giá gốc × hệ số bậc`. **Hết.** Số quả thêm là
*cách trình bày* bậc hiếm, không nhân lên giá. Không có luật này, Bơ Sáp huyền thoại trả
845 × 5 × 1,66 × 10 = **70.135 xu** (gấp 25 lần vụ thường). Có luật: **28.050** (đúng 10 lần).

Quả thêm vẫn có giá trị thật — chúng tính vào **cống nạp và sổ sưu tập**. Một vụ Anh Đào huyền
thoại cho 9 quả thay vì 6 để nộp cống. Đó là lý do sạch nhất để người chơi reo lên khi thấy đột
biến giữa lúc đang cày cống nạp.

### 3.5 Tưới nước theo lượt

Ví dụ của bạn: cây 300s, mỗi 90s mở một lượt, mỗi lượt giảm 20s.

Các con số đó là **tỉ lệ chứ không phải tuyệt đối** — khoảng 90s cố định sẽ cho cà rốt (40s, còn
22s ở cấp cao) **không lượt nào**, và cà rốt chiếm phần lớn giai đoạn đầu.

```
W (số lượt)   = 1 nếu dur < 60 giây, ngược lại 3
P (chu kỳ)    = dur / (W + 1)
D (mở trong)  = max(8 giây, 0,15 · dur) × hệ số thời tiết
C (giảm/lượt) = 0,20 · dur / W
Tổng giảm     = 0,20 · dur   ← luôn luôn 20%
```

Ở `dur = 300`: lượt mở tại **75 / 150 / 225 giây**, mỗi lượt mở **45 giây**, mỗi lượt giảm
**20 giây**, tổng giảm **60 giây**. Khớp chính xác ví dụ của bạn.

**Một điều chỉnh so với ví dụ.** Bạn nói chu kỳ 90s (= 0,30·dur). Ở đó lượt 3 mở tại 0,90·dur —
nhưng người chơi đã tưới lượt 1 và 2 thì cây chín tại 0,867·dur, tức **trước khi lượt 3 kịp mở**.
Lượt 3 sẽ là code chết với bất kỳ ai chơi tốt. Chuyển chu kỳ về `dur/4` sửa đúng chỗ đó và giữ
nguyên mọi con số khác trong ví dụ.

**Chống tích luỹ khi offline — bằng cấu trúc, không phải bằng phòng thủ.** Lượt `k` mở tại
`k·P` tính theo **đồng hồ thực từ lúc gieo**, mở trong `D`. Đóng app 6 tiếng → `tn` vượt xa mọi
`k·P + D` → mọi lượt chưa dùng **vĩnh viễn lỡ**. Không có pass bù, không có tích luỹ. Chuyện bạn
lo là *bất khả thi*, không phải *được chặn*.

Vì giờ mở lượt là hàm thuần của `plantedAt`, **lên lịch cả 3 thông báo ngay lúc gieo** và không
bao giờ phải đụng lại.

**Trần chung cho bạn bè (quan trọng khi lên online):** lượt tự tưới và lượt bạn bè tưới **dùng
chung một hạn mức 20%/ô**. Một ô không bao giờ xuống dưới 0,80 × thời gian gốc dù có bao nhiêu bạn.
Không có luật này, người chơi 50 bạn có ruộng chín tức thì và cả xương sống sập.

`friendMask` ghi lượt nào do khách tưới — chặn tính công hai lần và cấp dữ liệu cho "Hà đã tưới 3
cây của bạn" mà không cần cấu trúc thứ hai.

---

## 4. Tiến trình — đảo, ô đất, nhiệm vụ

### 4.1 Sáu đảo

Hình học ở 1920×1080: một đảo 16 ô ≈ **800×800 px** ở zoom 100%. Sáu đảo xếp 3×2 với rãnh 250px =
canvas 2900×1850 = **1,5 × 1,7 màn hình** — đọc hết ở zoom 55%, không cần minimap.
**Trần thực tế của một canvas là 9 đảo.** Dựng lưới cho 9, ship 6, giữ 3 dự phòng.

| # | Tên | Cấp | Xu | Cống nạp | Đặc quyền toàn trang trại | Ngày mở (nhàn) |
|---|---|---|---|---|---|---|
| 1 | Vườn Nhà | 1 | — | — | — | d0 |
| 2 | Đảo Gió | 5 | 60.000 | 160 Ngô + 200 Cà Chua | +5% giá bán | **d4** |
| 3 | Đảo Băng | 10 | 420.000 | 260 Cà Tím + 300 Ớt Chuông + 320 Nấm Rừng | +3% đột biến | **d14** |
| 4 | Đảo Hoả | 16 | 1.500.000 | 330 Bắp Cải + 660 Nho Tím + 300 Củ Dền | −8% thời gian mọc | **d27** |
| 5 | Đảo Lôi | 22 | 4.000.000 | 450 Táo Đỏ + 390 Lê + 560 Cam | +10% XP | **d46** |
| 6 | Đảo Vàng | 28 | 9.500.000 | 360 Dứa + 360 Dưa Hấu + 900 Chuối + 1080 Anh Đào | +15% giá bán | **d72** |

Người chơi chăm đạt các mốc này ở **0,56×** số ngày trên: d2 / d8 / d15 / d26 / d40.

Luật cống nạp để nó ép cày thay vì ép giữ xu:
1. **Nộp vào và bị tiêu mất.** Không mua được bằng xu.
2. **Nhiều loại cây** — 2 loại cho đảo 2–3, 3 loại cho đảo 4–5, 4 loại cho đảo 6.
3. Mọi cây cống nạp đều thuộc **6 cấp gần nhất**, tức cây người chơi đang trồng tốt.
4. **Nộp dần được**, nên trên bản đồ nó là một thanh tiến trình.

**Khai thác hệ quả của canvas chung: đảo khoá luôn nhìn thấy được.** Đặt bộ đếm cống nạp ngay trên
đảo khoá dưới dạng biển hiệu (`Nho Tím 412 / 660`). Bản đồ trở thành mặt phẳng mục tiêu chính —
đáng giá hơn bất kỳ menu nào.

### 4.2 Ô đất

**Đảo 1 — cố ý hào phóng.** 6 ô miễn phí, thang giá mỗi ô dưới nửa ngày thu nhập:

| Ô | 1–6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Cấp | 1 | 2 | 3 | 4 | 5 | 6 | 8 | 10 | 12 | 14 | 16 |
| Xu | 0 | 1.200 | 2.200 | 3.600 | 5.800 | 8.800 | 13.000 | 19.000 | 29.000 | 43.000 | 63.000 |

**Đảo 2–6 — 4 ô tặng kèm khi mở đảo**, 12 ô mua. Đặt chân lên một đảo trống 16 ô là cảm giác tệ;
với 4 ô tặng thì mọi đảo đều là một nông trại đang chạy ngay khi mở.

Giá = `500 × 1,32^(k−1) × hệ số đảo`, hệ số **×7 / ×20 / ×40 / ×70 / ×115**. Ô cuối mỗi đảo luôn
đắt gấp ~21 lần ô đầu. Ô cuối đảo 6 là 1,22 triệu ≈ 0,7 ngày thu nhập ở cấp 28 — một món mua kết
thúc thật sự. Mọi ô ở mọi đảo đều nằm trong khoảng 0,13–0,7 ngày, nên "mua ô tiếp theo" luôn là
việc hiển nhiên nên làm với xu lẻ. **Đó là lý do để tiếp tục lên cấp**: lên cấp làm tăng biên lợi
nhuận, và biên lợi nhuận là thứ khiến ô tiếp theo mua nổi.

Tổng chỗ tiêu xu từ ô đất: **10,79 triệu**.

### 4.3 Đường cong cấp độ — phải thay, công thức hiện tại hỏng ở cả hai đầu

| | Hiện tại | Vấn đề |
|---|---|---|
| Cấp 1 cần 3.600 XP | so với 811 XP/ngày | **4,4 ngày cho lần lên cấp đầu** |
| Cấp 1 tốn 15.000 xu | so với 4.651 xu/ngày, khởi đầu 5.000 | **3,2 ngày.** Con số tệ nhất trong build — nó làm nghẽn ngay phần hướng dẫn |
| Cấp 27 cần 2,21 triệu XP | so với 203k XP/ngày | **10,9 ngày cho một cấp** |
| Cấp 29 tốn 295.000 xu | so với 1,95 triệu xu/ngày | **0,15 ngày — vô nghĩa** |

Tỉ lệ đang phẳng 1,28 mãi mãi; nó cần giảm dần từ ~4,1 xuống ~1,10. Thay bằng bảng 40 dòng, mục
tiêu `số ngày = 0,30 + 0,15·(lv−1)`:

| Cấp | XP cần | Xu | Ngày | Cộng dồn nhàn/chăm |
|---|---|---|---|---|
| 1 | 240 | 2.300 | 0,30 | 0,3 / 0,2 |
| 5 | 6.200 | 14.000 | 0,90 | 3,0 / 1,7 |
| 10 | 45.000 | 48.000 | 1,65 | 9,8 / 5,5 |
| 15 | 160.000 | 120.000 | 2,40 | 20,2 / 11,3 |
| 20 | 324.000 | 260.000 | 3,15 | 34,5 / 19,4 |
| 25 | 793.152 | 620.000 | 3,90 | 53,5 / 30,1 |
| 30 | 1.360.000 | 1.150.000 | 4,65 | **75,8 / 42,6** |

(bảng đầy đủ 40 dòng khi code). Không cấp nào vượt 4,65 ngày. Từ cấp 31 tiếp tục ×1,08 tới cấp 40.

`Level().plots = min(16, 9 + (lv−1)/2)` — **xoá**. Ô đất giờ mua theo đảo.
`growCut`, `mutate`, `priceUp`, `energyUp` — **giữ nguyên cả bốn**, đều có hình dạng tốt.

### 4.4 ✅ Shop vật phẩm quay lại — và đây là lý do nó cần thiết

Vấn đề nếu bỏ shop là số học: các chỗ tiêu cấu trúc (lên cấp 7,24tr + ô đất 10,79tr + đảo 15,48tr
= 33,51tr) chỉ hút **51%** tổng thu nhập tới cấp 30. 49% còn lại đọng thành lạm phát xu, và khi xu
mất giá thì **mọi** phần thưởng trong game mất ý nghĩa — kể cả phần thưởng nhiệm vụ Kim Cương mà
ta vừa bỏ công cân.

Bạn đã chốt giữ shop. Tốt — nó là chỗ tiêu **co giãn** duy nhất trong thiết kế: hút đúng phần xu
người chơi không tiêu vào đảo, và tự động hút nhiều hơn khi người chơi giàu hơn.

**`ShopGoods` hiện có 6 món và cấu trúc đúng rồi** — giữ nguyên panel, đổi giá và bổ sung cho hệ
thống mới. Giá tính theo `UNIT` (biên lợi nhuận cây tốt nhất ở cấp đó) nên **không bao giờ phải
cân bằng lại**:

| Món | Giá | Hiệu ứng | Trạng thái |
|---|---|---|---|
| Bình tưới vàng | 2 × UNIT | mở ngay lượt tưới kế tiếp trên 1 ô | **sửa** — cơ chế tưới đã đổi |
| Phân bón thần kỳ | 8 × UNIT | chín ngay 1 ô | giữ (`instant`) |
| Bùa đột biến | 12 × UNIT | +30% đột biến trong 5 phút | giữ (`mutate`) |
| Túi hạt ngẫu nhiên | 3 × UNIT | ×5 hạt giống | giữ (`seedbag`) |
| Năng lượng thần kỳ | 6 × UNIT | +300 năng lượng | giữ (`energy`) |
| Mở rộng luống đất | — | — | **bỏ** — ô đất giờ mua thẳng trên bản đồ |
| **Dự Báo Thời Tiết** | 5 × UNIT | lộ 3 khung giờ thời tiết kế tiếp, 24 giờ | **mới** |
| **Đổi nhiệm vụ** | 15 × UNIT | đổi 1 nhiệm vụ ngắn (đổi cả hạng) | **mới** |
| **Nhà Kính** | 9k / 24k / 60k | miễn nhiễm thời tiết cho 2 / 4 / **6 ô (trần cứng)** | **mới, vĩnh viễn** |

Ba món mới đáng nói:

- **Dự Báo Thời Tiết bán *thông tin*, không bán *quyền điều khiển*.** Đây là khác biệt quan trọng
  nhất trong cả mục này. Nếu bán được "làm cho trời có tuyết" thì thời tiết thôi là lý do mở app —
  mà đó là công dụng duy nhất của nó. Bán dự báo thì ngược lại: nó làm việc lập kế hoạch sâu hơn.
- **Nhà Kính không đổi thời tiết, nó *miễn trừ* ô đất khỏi thời tiết** — những ô đó luôn chạy ở
  mức chuẩn ×1,00. Theo cấu trúc thì không thể dùng để cày Tuyết hay Bão. Trần **6/16 ô (37,5%)**
  để thời tiết luôn chi phối phần đa số.
- **Bình tưới vàng là món người chơi bực bội mua thay vì đóng app.** Giữ nó rẻ.

**Cảnh báo về Nhà Kính:** nó là thứ duy nhất trong plan này cho phép người chơi *né* một hệ thống
thay vì *chơi* nó. Tôi để trần 6 ô và không cho mua thêm. Nếu sau này thấy người chơi mua Nhà Kính
rồi ngừng quan tâm tới thanh thời tiết, đó là dấu hiệu phải hạ trần xuống 4 chứ không phải nâng lên.

### 4.5 Nhiệm vụ

**Loại A — ngắn hạn, ngẫu nhiên, hết hạn.** 3 khe (cấp 1–14) → 4 (15–24) → 5 (25+). Khe trống nạp
lại sau **8 phút**, hoặc **ngay lập tức khi mở app**. Trần 24 nhiệm vụ/ngày.

**Hạng hiện ngay lúc sinh ra** — người chơi phải quyết định được có đuổi theo hay không.

> ⚠️ **Cảnh báo của agent UX, tôi nhận:** nhiệm vụ hết hạn đem vào **đồng hồ đếm ngược thứ ba**,
> cạnh tranh chú ý với đồng hồ thời tiết (60 phút) và đồng hồ tag (12 giờ). *"Ba đồng hồ là thừa
> một"* — người chơi bắt đầu phớt lờ cả ba, mà thứ bị phá đầu tiên chính là đồng hồ thời tiết, thứ
> mạnh nhất trong toàn bộ plan này.
>
> **Giải pháp: hạn của nhiệm vụ KHÔNG được lên HUD.** Nó là một thanh mảnh trên chính dòng nhiệm
> vụ trong panel, không phải một chip đếm ngược trên màn chính. Chỉ khi còn **dưới 10 phút** thì
> dải nhiệm vụ mới hiện lên HUD (§10). Hai đồng hồ thường trực, cái thứ ba chỉ xuất hiện khi thật
> sự gấp.

| Chuỗi | Đồng | Bạc | Vàng | Kim Cương |
|---|---|---|---|---|
| 0 (nền) | 50 | 32 | 15 | 3 |
| 10+ (trần) | 30 | 40 | 24 | 6 |

**Chuỗi = số nhiệm vụ ngắn hoàn thành liên tiếp không để cái nào hết hạn. Để hết hạn một cái là về 0.**
Đây là toàn bộ cơ chế gắn kết: người dọn sạch bảng đều đặn thấy hạng Vàng gấp đôi và Kim Cương gấp
đôi so với người chơi tạt ngang, và nó đọc được ("chuỗi 7"). Bảo hiểm xui: chắc chắn Kim Cương nếu
40 nhiệm vụ chưa ra cái nào.

Thưởng = `hệ số hạng × 3 × UNIT`, hệ số **×1 / ×2,2 / ×5 / ×12**.

**Nhiệm vụ chỉ định cây cụ thể trả thêm ×1,8**, và phải chiếm **40%** số nhiệm vụ ngắn. Con số 1,8
này chịu lực — xem §4.6.

**Loại B — cố định theo cấp.** 4 nhiệm vụ mỗi cấp, **đã định sẵn hạng**: nhiệm vụ 1 Đồng, 2 Bạc,
3 Vàng, 4 Kim Cương. Mỗi cấp vì vậy **kết thúc bằng một Kim Cương**. Xong cả 4: `+12 × UNIT` +
1 túi hạt + **1 ô đất miễn phí** trên đảo hiện tại. Cấu trúc `Chapters[]` hiện có đã đúng hình
dạng 4-nhiệm-vụ — giữ, chỉ gán hạng và định lại giá.

**Hạng là ngưỡng của cùng một nhiệm vụ**, trả theo hạng cao nhất đạt được trước khi hết hạn. Đó là
thứ làm cho việc hết hạn có ý nghĩa.

**Rương/năng lượng phải cân lại.** Ngưỡng cố định hiện tại (300/600/1200/3000) lệch thảm hại ở cấp
thấp: cấp 1 người chơi tạo **106 năng lượng/ngày**, nên rương đầu tiên rơi vào **ngày thứ 3**.
Đổi sang `ngưỡng(bậc, lv) = round(0,9 × số ô × năng lượng mỗi vụ) × {1, 2, 4, 10}` → giữ **~2
rương/ngày ở mọi cấp** từ 1 đến 40, và rương đầu tiên rơi vào phiên thứ 2 của ngày 1.

### 4.6 Rủi ro lớn nhất: game chán ở cấp 10 — và nó không phải do thiếu nội dung

**Thủ phạm là độc canh. Ở mọi cấp, có đúng một cây đáng trồng, và nó luôn là cây mới nhất.**

Đây là tính chất đo được của bảng hiện tại chứ không phải giả thuyết: biên lợi nhuận tăng gần như
đơn điệu qua cả 28 cây (45 → 1.525), chỉ trũng hai chỗ. Nên ở cấp 10 người chơi có 35 ô trên 3 đảo
và **cả 35 ô đều trồng Cà Tím**. Nông trại một màu, không gian quyết định rỗng, và thứ duy nhất
thay đổi từ cấp 1 là số lần chạm nhiều hơn. Đó chính là thứ giết game nông trại ở cấp 8–12.

Năm biện pháp, xếp theo sức chịu lực:

1. **Nhiệm vụ chỉ định cây, thưởng ×1,8.** 40% nhiệm vụ ngắn gọi tên một cây người chơi chưa trồng
   trong 48 giờ. Hệ số 1,8 được căn để việc đổi cây là *đúng* chứ không chỉ là *cho vui*: nhiệm vụ
   Vàng "24 Cà Rốt" trả 4.698 xu so với chi phí cơ hội 4.176 xu. Ở hệ số 1,0 nó trả 2.610 và mọi
   người chơi đều bỏ qua — đúng đắn. **Đây là biện pháp rẻ nhất và chịu lực nhất.**
2. **Cống nạp nhiều loại cây.** Đảo 4 đòi 330 Bắp Cải + 660 Nho Tím + 300 Củ Dền — ba cây, 320 vụ,
   ~2,5 ngày. Suốt 2,5 ngày đó nông trại ba màu và người chơi đang thật sự phân bổ ô. **Cống nạp
   không phải cổng thu xu; nó là động cơ đa dạng cây trồng.**
3. **Nhiễu thị giác từ đột biến.** Cấp 1: 7,2% trên 6 ô = 0,4 ô phát sáng mỗi vụ — gần như vô hình.
   Cấp 10: 18% trên 35 ô = **6,3 ô phát sáng mỗi vụ**. Độ biến thiên thị giác tăng **15 lần** đúng
   trong cửa sổ nguy hiểm, mà không phải dựng thêm nội dung nào.
4. **Nhịp mở cây (dữ liệu đang có — giữ nguyên).** Cấp 5–11 mở cây mới **mỗi cấp một cây**. Đây là
   dải mở dày nhất game và nó đã nằm đúng chỗ. Đừng làm thưa ra.
5. **Cột mốc.** Đảo 2 ở ngày 4, đảo 3 ở ngày 14 — cửa sổ nguy hiểm cấp 8–12 nằm giữa hai lần mở
   đảo, cả hai đều nhìn thấy thường trực trên canvas chung với bộ đếm cống nạp sống.

---

## 5. Lưu trữ

### 5.1 Bỏ `JsonUtility`, dùng Newtonsoft

Lý do mang tính quyết định: **`JsonUtility` âm thầm xoá field lạ khi đọc-ghi lại.** `FromJson` bỏ
qua khoá nó không có field, `ToJson` chỉ ghi field đã khai báo. Bất kỳ field nào do phiên bản sau
hoặc do server ghi vào sẽ **bị xoá** ngay lần build này lưu tiếp theo. Với save mà bạn định đồng bộ
lên server sau này, đó là lỗi chí mạng. Newtonsoft có `[JsonExtensionData]` giữ nguyên khoá lạ.

Ba lý do phụ: không có Dictionary (hack `seedK`/`seedV` hiện tại là đoạn xấu nhất `GameState.cs`,
và vòng đọc `i < seedK.Count && i < seedV.Count` **âm thầm cắt cụt dữ liệu** khi lệch độ dài thay vì
báo lỗi); không phân biệt `null` với `""`; enum lưu bằng số nên đổi thứ tự `Weather` là diễn giải
sai mọi save cũ.

Package đã có sẵn — nhưng phải khai báo **trực tiếp** (§1.4).

### 5.2 Luật tương thích ngược, bắt buộc khi review

- Mọi DTO mang `[JsonExtensionData]`.
- Mọi collection là **object có khoá theo id**, không phải mảng theo vị trí — thêm một cây không
  được làm lệch nghĩa dữ liệu cũ.
- Mọi mốc thời gian là **int64 unix mili giây UTC**. Không `DateTime`, không `float`.
- Mọi enum lưu thành **chuỗi**.
- Field không bao giờ bị xoá, chỉ bị bỏ qua. Đổi tên thì thêm `[JsonProperty("tênCũ")]`.
- Cặp `v` / `minV`: reader có version `< minV` **từ chối đọc** và đổi tên file thành
  `lqfarm.save.v{n}.bak` thay vì ghi đè.

Kích thước: 96 ô × ~180 byte ≈ 17 KB, cả file ~40 KB ở 6 đảo.

### 5.3 Offline — thiết kế để gần như không phải tính lại gì

| Hệ thống | Tính lại khi load? | Vì sao |
|---|---|---|
| Cây lớn | **Không** | `(now − plantedAt)/1000 + cut`. Hàm thuần của field đã lưu |
| Lượt tưới | **Không** | Lượt đã qua trong lúc đóng app là *lỡ theo cấu trúc* |
| Thời tiết | **Không** | `Weather(seed, giờ)` là hàm thuần |
| Tag | **Không** | `Tag(cây, epoch)` thuần; epoch lúc gieo suy ra từ `plantedAt` |
| **Nhiệm vụ hết hạn** | **Có** | Hệ duy nhất có trạng thái theo thời gian |
| **Reset ngày** | **Có** | Một phép so sánh |

Bảng này là phần thưởng cho luật "chốt mọi thứ lúc gieo". Nếu thời tiết hay tag điều biến *liên
tục*, mỗi lần load sẽ phải tích phân từng giờ đã offline, và mỗi lần chỉnh cân bằng sẽ viết lại
ngược mọi cây đang lớn.

Hai thứ phải quét, và **trần của chúng**: nhiệm vụ hết hạn **tái sinh đúng 1 chu kỳ** dù vắng 30
ngày; reset ngày **reset đúng 1 lần** dù qua bao nhiêu ngày.

### 5.4 Đồng hồ chạy ngược

Không chặn thì: cây như bị reset (`now < plantedAt`), nhiệm vụ hết hạn sống lại, và **thưởng hằng
ngày cày được vô hạn** — `GS.CheckDay()` hiện tại đang dính đúng lỗi này: vặn ngược đồng hồ → reset
lại → nhận → vặn ngược → nhận tiếp.

Chặn bằng **sàn đơn điệu**: `GS.Now` không bao giờ trả về giá trị nhỏ hơn giá trị lớn nhất từng
thấy. Mọi luật trong game đều đọc qua `GS.Now`, nên vặn ngược đồng hồ thành **vô tác dụng** — không
phải phần thưởng, không phải reset.

Nói thẳng về phạm vi: **đây không phải chống gian lận.** Người chơi quyết tâm vẫn sửa được file
save. Nó tồn tại để người chơi ngay thẳng bị sai múi giờ, đổi giờ mùa, hay NTP hiệu chỉnh **không
mất cây**, và để cái exploit tầm thường không còn tầm thường. Cưỡng chế thật đến cùng với server —
đó là lý do `worldSeed` đã nằm sẵn trong schema.

---

## 6. Hoá đơn thu hoạch — hạng mục bắt buộc

Chuỗi giá trị giờ có tới 5 số nhân: `gốc × cấp × thời tiết × tag × bậc hiếm`.

**Không bao giờ hiện một con số cuối không giải thích.** Người chơi học hệ số nhân từ *hoá đơn*,
không phải từ tooltip:

```
Cơ bản  856  →  Thời tiết ×1,30  →  Tag ×1,35  →  Hoả ×4,5  →  6.756
```

Hệ thống nhân mà không tách dòng thì người chơi đọc nó là ngẫu nhiên, và cái gì đọc là ngẫu nhiên
thì không ai tối ưu — tức toàn bộ chiều sâu bạn vừa xây bị vứt đi.

Trần chống vỡ: `thời tiết × tag ≤ 3,00` trên cả trục giá và trục XP; đột biến `≤ 0,75` tuyệt đối.
Trần đột biến không phải để cân bằng — nó ngăn đột biến trở thành *điều chắc chắn*, mà cảm giác
may rủi mới là toàn bộ lý do tồn tại của hệ thống đột biến.

Kiểm tra trần có thật sự chạm: cấp 30 (0,42) × Bão (1,60) × tag Đột Biến (1,75) = **1,176 → kẹp
xuống 0,75**. Có chạm.

Đỉnh bền vững ở cấp 30 (Tuyết + tag Giá Cao) so với thường ngày là **1,80 lần**. Một người chỉ cày
lúc điều kiện đẹp kiếm hơn 80% mỗi phiên so với người chơi bất kỳ lúc nào — thưởng thật cho sự chú
ý, và xa mức 5–10 lần vốn sẽ làm mọi chỗ tiêu xu thành vô nghĩa.

Jackpot tuyệt đối (Tuyết × tag Thời Vụ × Huyền thoại, cấp 30) = **×23,7**, tức 40.117 xu một ô.
Không hỏng, vì **không có đường lặp lại được**: cần Tuyết (13,5%) × tag Thời Vụ rơi đúng Bơ Sáp
(~1/36 lượt) × Huyền thoại (~1%). Khoảng một lần mỗi tháng. Đó là một kỷ niệm, không phải một nền
kinh tế.

---

## 7. ⚠️ Quy mô — chỗ tôi lo nhất

Cộng hết lại: 6 hệ thống mới, 96 ô đất, canvas pan/zoom, đổi serialiser, đổi đường cong cấp độ,
làm lại nhiệm vụ, làm lại sổ sưu tập, tách `GS` thành `PlayerState`, prefab + pool cho ô/đảo/FX.
Đây **không phải một phiên làm việc**. Ước lượng thật thà: nếu làm tuần tự và kiểm tra hình ảnh sau
mỗi bước như tôi vẫn làm, đây là **vài chục phiên**.

Lộ trình dưới đây được xếp sao cho **game chạy được sau mỗi bước**, và hai bước refactor thuần
(0 và 3) không mang theo tính năng nào để nếu vỡ thì biết ngay vỡ vì đâu.

| # | Bước | Người chơi thấy? | Rủi ro |
|---|---|---|---|
| 0 | ✅ **XONG** — `PlayerState` + `FarmContext`. `GS` còn đúng 7 thành viên tĩnh. 159 call site đã chuyển. Có `ArchitectureGuard` cưỡng chế | Không | Thấp, diện rộng |
| 1 | **Đổi serialiser** sang Newtonsoft, `v:2`. Save cũ xoá luôn (bạn đã đồng ý reset) | Không | Thấp |
| 2 | **Model ô đất + tưới theo lượt.** Vẫn một đảo | **Có — tính năng đầu tiên** | Vừa |
| 3 | **Tách `IslandView` khỏi `FarmView`.** Không đổi một pixel nào | Không | **Cao nhất trong các refactor thuần** |
| 4 | **`MapCamera` + `TapOrDrag`.** Pan/pinch — vẫn một đảo. Toàn bộ mô hình input được kiểm chứng trên nội dung đã chạy | Có | Vừa |
| 5 | **Pool đảo + LOD + đảo #2.** N=2 chạy qua mọi nhánh pool với bán kính vỡ nhỏ nhất. Kèm FxPool, `FieldAnimator` | Có | Vừa |
| 6 | **Thời tiết + tag.** Hàm thuần, không đụng save — ship tối rồi bật | Có | Thấp |
| 7 | **Bậc đột biến + số quả.** Đổi khoá kho → một commit chung với UI kho | Có | Vừa |
| 8 | **Nhiệm vụ v2** — hạng, hết hạn, chuỗi | Có | Vừa |
| 9 | **HUD mới** — thanh Mùa Vụ, gộp rail, thanh đáy 4 ô (§10) | Có | Vừa |
| 10 | **Cống nạp + đảo 3–6.** Chỉ là thêm dữ liệu vì lưới canvas đã dựng cho 9 đảo | Có | Thấp |
| 11 | **Shop vật phẩm v2** — định lại giá theo UNIT, thêm Dự Báo / Đổi nhiệm vụ / Nhà Kính | Có | Thấp |

**Bạn đã chốt làm đủ 6 đảo.** Ghi lại đánh đổi để sau này nhìn lại biết: đảo 4–6 mở ở cấp 16/22/28,
tức ngày 27/46/72 với người chơi nhàn — chúng sẽ không được ai chạm tới trong nhiều tuần nhưng
chiếm chi phí xây dựng và kiểm thử ngay từ bây giờ. Cách giảm đau: **làm đảo 1–3 trước cho chạy
hẳn, rồi thêm 4–6 ở bước 10 như thêm dữ liệu** — vì lưới canvas đã dựng cho 9 đảo ngay từ đầu nên
bước đó rẻ. Bạn vẫn có đủ 6 đảo, chỉ là chúng xuất hiện muộn trong quá trình làm chứ không muộn
trong game.

**Ba việc tôi sẽ cắt nếu phải cắt**, theo thứ tự:
1. Đặc quyền riêng từng đảo (+5% giá, +3% đột biến…) → hoãn. Thêm một tầng nhân vào chuỗi vốn đã
   5 tầng, để đổi lấy rất ít khác biệt cảm nhận được.
2. Bảo hiểm xui đột biến (400 lần gieo) → hoãn. Chỉ có ý nghĩa với người chơi hàng tháng.
3. Nhà Kính → hoãn. Là món shop phức tạp nhất và cũng là món duy nhất cho phép né một hệ thống.

---

## 8. Những thứ giữ nguyên, không đụng vào

- `FarmView.CellPos`, `Depth`, và **mọi hằng số khoảng cách** (`TW/TH/GapX/GapY/TileArtW/TileAxis`).
  Khối comment phía trên `GapX` ghi các phép đo art chịu lực — **giữ nguyên văn**.
- `DiamondHit` — bộ lọc raycast hình thoi đúng hoàn toàn, không cần sửa gì.
- Toàn bộ FX: `Burst`, `Sparkle`, `Puff`, `Droplets`, `FlyToStore`, `Tween.*` (chỉ đổi sang pool).
- **Cache render-diff trong `PlotView`** (`sCrop/sStage/sVariant/sTimer/sWatered`). Đây là code tốt
  và chính nó giữ cho 48 ô đang hoạt động rẻ. Thêm field `sLod`, đừng thay.
- `GS.Progress` / `Track` / `TrackCrop`; `GameData.Level` (đổi số, giữ hình dạng); `Art`.
- `GameApp.Layer` (nested canvas), `SafeAreaFitter`, `ResizeWatcher`.
- Rương + năng lượng (đổi ngưỡng), bạn bè (dựng sẵn cho online).

---

## 9. Chuẩn bị cho online — chi phí trả trước, rẻ

Bạn nói bạn bè sau này sẽ có ghé thăm, tưới cây, trao đổi, mua bán, chôm cây. Ba thứ dưới đây làm
**ngay bây giờ** thì việc lên online sau này là sửa một chỗ; không làm thì là viết lại mọi chỗ.

1. **`PlayerState` thay cho `GS` tĩnh.** "Tưới cà chua của bạn" cần *ô của họ*, *hệ số cấp của họ*,
   *công ghi cho mình*. Với `GS` tĩnh thì phải nhân đôi mọi hàm kinh tế, và code kinh tế nhân đôi
   sẽ lệch nhau trong vòng một sprint.
2. **`PlotRef { owner, island, slot }`** thay cho `int` trần ở mọi nơi.
3. **`outbox` / `inbox` với guid do client sinh làm khoá idempotent** — hôm nay chảy vào một stub
   cục bộ áp dụng ngay lập tức. Khi có server, **chỉ đổi phần chảy vào đâu**; mọi call site, mọi UI,
   mọi khoá idempotent đã đúng sẵn.

Chặn bằng máy, không bằng kỷ luật: **một editor test làm fail build nếu `GS.plots`, `GS.coin`,
`GS.seeds` hay `GS.lv` xuất hiện ngoài `PlayerState.cs`.** Quy tắc kiến trúc không được CI cưỡng
chế thì sẽ mục; quy tắc này chỉ là một dòng grep.

---

## 10. HUD và bố cục màn hình

Tất cả toạ độ dưới đây là pixel quy chiếu 1280×720, viết theo dạng `Anchor(neo, vị trí, kích thước)`
để cắm thẳng vào `UIKit.Anchor()`.

### 10.1 Ngân sách màn hình — HUD mới **nhỏ hơn** HUD cũ

HUD hiện tại chiếm **216.984 px² = 23,5%** màn hình. Chỗ trống duy nhất, kiểm bằng ảnh chụp: một
dải **484 × 110** trời trống ở giữa phía trên (thẻ người chơi kết thúc ở x=360, cụm ví bắt đầu ở
x=844). **Không có dải trống thứ hai.**

Năm thứ bị cắt, và lý do:

| Cắt | Kích thước | Lý do |
|---|---|---|
| Chip bộ sưu tập | 206×44 | Một con số tĩnh `4/135`, không có sức ép thời gian, không gắn quyết định nào — và rail phải đã có nút "Bộ sưu tập" tới đúng panel đó. Hai lối vào cho một con số tĩnh là định nghĩa của một chip không xứng góc màn hình |
| **Toàn bộ 7 caption dưới nút rail** | 20.384 px² | Chúng **đè lên nút bên dưới 7px** (bước rail 104, nút 78, plate caption 26 neo ở −20 → trải −7…−33 — nhìn "Cửa hàng" ngồi trên vành đĩa Rương là thấy). Đọc đúng một lần cả đời người chơi. Thay bằng coach lần đầu + nhấn giữ. Bỏ chúng cho phép hạ bước rail **104 → 92**, và đó là thứ làm vừa ô thứ 5 |
| "Nâng cấp" khỏi thanh đáy | 160×64 | Hai nút kia là *động từ*, nút này là *lối tắt tới panel*, và nó đã có lối vào thứ hai trong popup ô khoá |
| Dải nhiệm vụ | 344×40 | Hiện `"Đã hoàn thành mọi nhiệm vụ"` khi rảnh — 344×40 chrome nói không gì. Đổi thành **có điều kiện**: chỉ gắn khi có thứ nhận được hoặc còn dưới 10 phút hết hạn |
| **Toàn bộ rail trái** | 112×346 | §10.4 |

Kết quả: **209.280 px² = 22,7%**. Thời tiết, tag và điều hướng đảo đều có chỗ, **mà HUD nhỏ đi
7.704 px²** — vì một rail, một chip và bảy caption đã trả tiền cho chúng.

### 10.2 Tai thỏ không phải vấn đề bạn tưởng — nhưng nó gây một vấn đề khác

Tôi đã mặc định phải chừa góc trên trái/phải cho tai thỏ. **Sai.**

| Máy | Canvas sau Expand | Lề mỗi bên ngoài hộp 1280 | Tai thỏ ăn vào (landscape) | Chạm hộp 1280? |
|---|---|---|---|---|
| 16:9 1920×1080 | 1280×720 | 0 | 0 | không |
| 19,5:9 iPhone 14 Pro | 1561×720 | 140 | 108 | **không** |
| 20:9 2400×1080 | 1600×720 | 160 | ~62 | không |
| iPad 4:3 | 1280×890 | 0 | 0 | không |

**Lề do `Expand` sinh ra (70–160px mỗi bên) luôn lớn hơn phần tai thỏ ăn vào (0–110px). Tai thỏ
không bao giờ chạm tới hộp thiết kế.** Chừa chỗ cho nó là lãng phí vĩnh viễn trên mọi máy.

Nhưng nó gây một vấn đề khác, và cái này thật:

1. **Bất đối xứng.** Phần ăn vào chỉ ở một bên, nên tâm của lớp HUD lệch tới **54px** so với tâm
   của lớp thế giới, và lệch bên nào thì tuỳ chiều xoay máy.
   → **Luật: không phần tử HUD nào được căn theo thứ gì trong lớp thế giới.** Đây chính là lý do
   thanh Mùa Vụ neo `TopLeft` dính vào thẻ người chơi, **không** neo `Top` — một thanh neo Top sẽ
   trôi 54px khỏi trục đảo và rơi lệch giữa hai cụm không hề trôi theo nó.
2. **Đáy màn, không phải hai cạnh.** ~38px thanh cử chỉ. Thanh đáy mới phải có đáy plate ở y ≥ 6.
3. **`FitFarm` đang đọc `_root.rect.size` chứ không đọc `Screen.safeArea`** ([GameApp.cs:105](Assets/Scripts/GameApp.cs#L105)).
   Hôm nay chiều cao ràng buộc trước: `min((1280−210)/979, (720−190)/504) = 1,052`, nên mép trái
   của tile rơi vào x=125 so với mép trong rail ở 118 — **7px hở trên màn 16:9. Đó là toàn bộ biên
   an toàn.** Thêm bất cứ gì vào rail là va. → `FitFarm` phải tính theo `Screen.safeArea`, và lề
   ngang thành **160** sau khi bỏ rail trái. 118px được giải phóng bên trái trở thành khe biển cho
   đảo bên cạnh ló vào ở Z2 — chính là cảm giác "còn có thứ gì ở đằng kia", và nó miễn phí.

### 10.3 Thanh Mùa Vụ — thời tiết + tag

**Một khung, hai ô, một lần chạm.** Không gộp thành một con số.

Lý do không gộp — và đây là chỗ tôi đổi ý so với ghi chú trước của mình: thời tiết nhân **cả 28
cây**, tag chỉ nhân **6 cây**. Một con số `×2,4` duy nhất trên HUD là đúng với 6 cây và **là lời nói
dối với 22 cây còn lại**. Một số liệu thường trực sai 79% thời gian còn tệ hơn hai ô trung thực.
Thêm nữa: hai đồng hồ khác nhau (60 phút vs 12 giờ) — một con số dưới một đồng hồ khẳng định một
hạn chót. Và **Thời Vụ phụ thuộc vào thời tiết**, đó là một tương tác thật và nó xứng đáng *được
hiện ra như một tương tác* — hai ô nối sáng lên với nhau — chứ không bị dẹp thành một tích số làm
tương tác biến mất.

Còn một lỗi đúng-sai trong ý tưởng "một con số": hệ số trần trụi ở đây **nhập nhằng về hướng**.
`×0,85` lên thời gian chín là **tốt**; `×0,85` lên giá bán là **xấu**. Một scalar gộp giấu mất
điều đó.

**Gốc** `season`: `Anchor(TopLeft, (376, −14), (480, 76))`, nền `Round(#0F3026 α0.60, bán kính 26)`
— cùng màu và cùng bo góc với thẻ người chơi, nên hai cái đọc thành một khối bên trái. Vùng chạm
`Center, (0,0), (480, 96)` — hình 76 cao nhưng vùng chạm 96 để vượt chuẩn 88, phần thừa 10px trên
và dưới rơi vào trời trống.

**Ô A — Thời tiết** (x ∈ [12, 310])

| Nút | Vị trí | Kích thước | Nội dung |
|---|---|---|---|
| `wIcon` | Left(12, −4) | 42×42 | sprite thời tiết |
| `wClock` | Left(60, 15) | 100×38 | **`26 Bold` trắng + Shadow — `"12:44"`** |
| `wProg` | Left(60, −23) | 96×12 | thanh, tỉ lệ đã trôi của khung 60 phút |
| `wName` | Left(168, 15) | 96×28 | `19 viền` — `"Nắng"` |
| `wMulA` | Left(168, −23) | 68×22 | `15` — `chín ↓15%` |
| `wMulB` | Left(240, −23) | 70×22 | `15` — `giá ↑10%` |
| `wNext` | Left(284, 15) | 26×26 | icon thời tiết kế tiếp, hoặc `"?"` |

**Đồng hồ là glyph lớn nhất trong thanh** vì nó là số liệu quan trọng nhất. `wProg` bên dưới trả
lời "tôi đang ở đầu hay cuối khung này" mà không phải đọc số — đó chính là cái cân để quyết định
*gieo ngay hay chờ*.

**Dùng phần trăm, không dùng hệ số.** `chín ↓15%` là 19 ký tự ≈ 134px, vừa ngân sách 138px;
`chín ×0,85 · giá ×1,10` là 22 ký tự ≈ 155px, **không vừa**. Quan trọng hơn: **mũi tên tô màu theo
lợi hại, không theo hướng** — `Theme.Green` khi thay đổi có lợi, `Theme.Red` khi có hại. Đó là cách
giải quyết sự nhập nhằng `×0,85` mà một scalar gộp không làm được.

**Ô B — Cây bonus** (x ∈ [328, 466]): icon tag 34×34, **6 chấm 4px** dưới nó — mỗi chấm một cây
được tag, tô theo loại tag — rồi `"6 cây"` và `"5g 12p"`. Sáu chấm màu cho biết *hình dạng* của bộ
tag hôm nay ("hôm nay chủ yếu là giá") mà không cần một chữ nào.

**Lộ thời tiết kế tiếp ở T−10:00**: `wNext` đổi `"?"` thành icon, thêm vành `Theme.Amber` 1px, và
**cả thanh chớp hổ phách một lần trong 0,4 s**. Cái chớp đó là toàn bộ khoảnh khắc "quyết định
ngay", và nó tốn đúng một tween.

**Thời tiết đổi mỗi 60 phút: không bao giờ hiện thẻ.** Một tấm thẻ mười lần mỗi phiên là một thứ
thuế. Dùng `Toast()` sẵn có: `"Trời chuyển Mưa · chín ↓30% · đột biến ↑25%"` trong 2,1 s.
**Thời tiết là không khí nền. Tag mới là sự kiện** — tag đổi thì dùng `ShowReward` (đã có sẵn, viết
0 dòng UI mới) với 4 cây + một ô `"+2 cây khác"`.

**Chạm vào thanh mở `SeasonSheet`** (920×520): cột trái thời tiết (khối hero + 4 dòng chỉ số + dải
`trước → nay → kế`), cột phải 3×2 lưới 6 cây bonus — và **đây mới là chỗ tích số xuất hiện, theo
từng cây, nơi nó đúng**: `bán ×1,62`. Cây Thời Vụ được viền hổ phách 3px + `"×2,2 · đang hiệu lực"`
khi đúng thời tiết. *Cái viền sáng lên đó chính là toàn bộ lý do thời tiết và tag dùng chung một
khung.*

Một luật kiểu chữ áp cho **toàn bộ** redesign: `UIKit.Label` đặt `HorizontalWrapMode.Overflow`, nên
**mọi nhãn tiếng Việt tràn im lặng** — không xuống dòng, không cắt bằng "…", chỉ chạy đè lên hàng
xóm. Và tiếng Việt chồng hai dấu (`ế`, `ộ`, `ữ`): hộp dòng cần **≥ 1,45 × cỡ chữ**, không phải 1,2
như chữ Latin. Mọi rect trong mục này đã được đo tay theo chuỗi dài nhất của nó.

### 10.4 Điều hướng — vấn đề thật là **trùng lặp**, không phải số lượng

Hôm nay: **11 nút cho 8 đích**. Cửa hàng có 2 lối vào (rail trái + chip xu), Rương 2 (rail trái +
chip năng lượng), Bộ sưu tập 2 (rail phải + chip sưu tập), Nhiệm vụ 2 (rail trái + dải nhiệm vụ).
**Ba đích tự bỏ được trước khi cắt một tính năng nào.**

**Một rail duy nhất, bên phải, 5 ô, bước 92** — `Anchor(Right, (−58, −6), (96, 460))`:

| Ô | Đích | Badge |
|---|---|---|
| 1 | **Hạt giống** — đầu vào của vòng lặp | — |
| 2 | **Kho** — đầu ra của vòng lặp; cũng là mỏ neo `WarehouseWorld` mà code đang phụ thuộc | chấm hổ phách khi ≥90% đầy |
| 3 | **Nhiệm vụ** | chấm đỏ + số nhận được |
| 4 | **Cửa hàng** | chấm hổ phách khi có món chưa mua |
| 5 | **Thêm** → khay 3×2: Rương · Bạn bè · Bộ sưu tập · Nâng cấp · Bản đồ đảo · **Cài đặt** (chưa có, cần làm) | kế thừa chấm bên trong |

**Xoá rail trái**, bốn lý do:
1. Ba đích của nó đều có chỗ: Cửa hàng → ô 4, Nhiệm vụ → ô 3, Rương → khay (nó là sự kiện, không
   phải nơi để duyệt, và chip năng lượng đã mở nó).
2. Nó tốn **112×346 = 38.752 px²** ở mép trái của ruộng — và 118px đó chính là khe biển cho đảo
   bên cạnh ló vào (§10.2).
3. Hai rail đối xứng là thói quen của web di động, không phải của game di động. Nhìn ảnh chụp rồi
   thử nói xem **vì sao Rương ở bên trái còn Kho ở bên phải** — không có câu trả lời.
4. **Badge thay thế lối vào.** Một con số trên nút rail đáng giá hơn hẳn một nút thứ hai tới cùng
   chỗ. Đó là cách 8 đích sống sau 5 nút mà không cái nào bị bỏ sót.

**Không có rail điều hướng ở đáy.** Landscape cao 720px làm dải đáy thành dải đắt nhất màn hình,
và nó thuộc về **động từ bấm 50 lần mỗi phiên**, không thuộc về **đích bấm 5 lần**.
*Động từ lấy đáy, đích lấy cạnh.*

### 10.5 Thanh đáy — 4 ô cố định

`Anchor(Bottom, (0, 14), (864, 72))`, mỗi ô 208×72:

| Ô | Nhãn | Tông |
|---|---|---|
| `Thu hoạch · 6` | Green |
| `Tưới · 3` | Blue |
| `Gieo · 4` | Amber |
| `‹  Đảo Gió  ›` | plate, không phải mặt nút |

- **`Tưới` là động từ hàng loạt mới và nó bắt buộc.** Tưới lặp lại là động từ duy nhất bắn nhiều
  lần *bên trong* một vụ; không có nút hàng loạt thì người chơi chạm 16 ô mỗi cửa sổ và tính năng
  thành việc vặt thay vì phần thưởng.
- **Ô tưới không bao giờ dịch chỗ.** Khi không có cửa sổ nào mở, nó vẽ thành một hốc lõm rỗng chứ
  không phải một nút biến mất. Thanh có nút trượt qua lại là thanh không xây được trí nhớ cơ bắp.
- `"Thu hoạch · 6"` thắng `"Thu hoạch tất cả"`: ngắn hơn **và** mang theo con số — và "tất cả" trở
  nên nhập nhằng ngay khi có nhiều đảo.
- **Tên đảo ≤ 8 ký tự** (`Đảo Gió`, `Đảo Đá`, `Đảo Băng`). `Label 19 Bold` tràn im lặng, nên đây là
  **luật nội dung**, không phải rào chắn lúc chạy. Tên trong §4.1 đã rút theo luật này.

### 10.6 Trạng thái ô đất — **đúng hai khe overlay, không bao giờ ba**

Đây là câu giữ cho một đảo 16 ô còn đọc được:

- **Khe BADGE** `Center, (0, +28), 44×44` — chứa đúng một trong: dấu chín, giọt nước, xu mua, hoặc
  không gì. Một ô không bao giờ vừa chín vừa mở cửa tưới, nên một khe là đủ.
- **Khe CHIP** `Center, (0, −8), ≤112×30` — chứa đồng hồ, hoặc `"Cấp 12"`, hoặc giá. **Không bao
  giờ hai chip.**

`waterMark` ở `(+76, −8)` **bị xoá**: nó nằm ngoài đỉnh phải của hình thoi (nửa chiều rộng là 84),
và dưới cơ chế tưới lặp lại thì nghĩa cũ của nó ("chưa tưới lần nào") không còn tồn tại. Giọt nước
chuyển vào khe BADGE ở cỡ đầy đủ.

| Trạng thái | Tile | Badge | Chip | Chuyển động |
|---|---|---|---|---|
| Trống | `tile_empty` | — | — | — |
| Lớn, chưa tới cữ | `tile_watered` | — | `mm:ss` | chỉ cây đung đưa |
| **Lớn, cửa tưới mở** | `tile_empty` + phủ cyan α0,35, **vành cyan 2px** | giọt nước trên đĩa cyan | `mm:ss` | **vành nảy 1,0→1,18 + mờ dần, lặp 1,2 s** |
| **Chín** | `tile_ready` vàng | dấu ✓ hổ phách | — | `Bobber` nhún dọc 5px (đã có) |
| Khoá (chưa đủ cấp) | `tile_locked` mờ 0,72 | ổ khoá xám | `"Cấp 12"` | — |
| **Mua được** | `tile_locked` sáng, **vành hổ phách 2px** | xu | `"12.000"` | **không có** |
| *sửa đổi:* có tag | — | — | chip rộng 96→**112**, glyph tag 22×22 chèn trái | — |

**Ngân sách chuyển động đúng bằng hai**, và là đúng hai cái nghĩa "hãy hành động":
**vành nảy = gấp, sắp đóng** · **nhún dọc = hãy nhặt, không vội**. Phân biệt được bằng mắt ngoại
vi vì chúng khác *loại* chuyển động (tịnh tiến vs co giãn xuyên tâm), không phải khác tốc độ.
**Ô mua được cố tình không có chuyển động**: nó là lời mời thường trực, không phải hạn chót, và làm
nó động sẽ cạnh tranh với đúng hai trạng thái đang có hạn chót.

**Tag không có icon riêng.** Spec của bạn nói "icon tag trên ô đang lớn" — tôi từ chối làm nó thành
một vật thể riêng: thêm một icon lơ lửng trên mỗi ô chính là thứ biến 16 ô thành nhiễu. Nó nằm
**bên trong chip đồng hồ** như một glyph dẫn đầu. Một chip, một vật thể, và cái tag nằm ngay cạnh
con số mà nó điều chỉnh.

**Nguyên tắc chi phối: trạng thái do *tile* mang, chi tiết do *overlay* mang, và overlay được phép
chết khi zoom ra.** Ở 50% zoom hình thoi vẫn còn 84×42 ≈ 3.500 px² màu đặc và đọc hoàn hảo; một
chip 30px còn 15px và biến mất. **Nên tile tuyệt đối không được uỷ thác trạng thái cho một chip.**

### 10.7 Hoá đơn thu hoạch — không bao giờ mở modal

Chuỗi 5 số nhân, nhưng **bỏ `priceUp` theo cấp khỏi mọi hoá đơn**: nó là thuộc tính vĩnh viễn của
người chơi chứ không phải của vụ thu này, nó không bao giờ đổi giữa hai lần thu liên tiếp, và nó đã
hiện sẵn dưới dạng cấp độ. **Năm số hạng thành ba ngay khi hỏi: cái nào người chơi có thể tác động
được?**

| Mức | Khi nào | Hiện gì |
|---|---|---|
| 1 | mọi vụ thu | `Burst` sẵn có, nhưng đổi nội dung từ `"+12 XP"` thành **tiền**: `"+318"` vàng |
| 2 | khi thời tiết × tag ≥ 1,25 | thêm một dòng 168×22 sống 2,0 s: `+318   ×1,2 ☀   ×1,35 ★` — **tối đa hai chip, mãi mãi** |
| 3 | đột biến | leo thang theo bậc: Ngọc Bích im lặng → Băng Giá vành 2px + 18 tia → Viêm Hoả vành 3px + 30 tia + `Shake(5)` → **Lôi Điện là modal duy nhất trong cả game**, vành 4px + 48 tia + `Shake(9)` |

Mọi hiệu ứng trên **đã tồn tại** (`Sparkle`, `Burst`, `Tween.Shake`, `ShowReward`) và `Theme.Rarity`
đã giữ đúng 4 mã màu theo đúng thứ tự. Leo thang này là một bảng tham số, không phải code mới.

**Thu hoạch 16 ô một lúc**: `HarvestAll` **tắt hẳn mức 1 và mức 2**. Mỗi ô chỉ còn cung bay
`FlyToStore`, **lệch nhau 0,04 s theo thứ tự ô** — 16 × 0,04 = 0,64 s, đọc thành một thác nước chứ
không thành một vệt nhoè. Thay cả 16 hoá đơn bằng **một bản tổng kết** trượt lên từ đáy,
`Bottom(0, 104), 520×132`, sống 3,2 s, **không chặn thao tác**:

```
Thu hoạch 16 cây
+4.820 🪙                     +268 XP
☀ Nắng ×1,2   ★ bonus ×1,35   ✨ 2 đột biến
```

Ba dòng: **cái gì · bao nhiêu · vì sao.** Dòng "vì sao" là nơi chuỗi số nhân cuối cùng xuất hiện,
đã gộp lại — và gộp là cách trung thực duy nhất khi 16 ô là 16 cây khác nhau với tag khác nhau.

**Đột biến tìm thấy trong lúc thu hàng loạt thì xếp hàng, không cắt ngang.** Cung bay xong, tổng
kết hiện, rồi mới tới lượt Lôi Điện bắn modal. Viêm Hoả trở xuống gộp vào `"✨ 2 đột biến"` cộng một
chấm đỏ trên nút Bộ sưu tập.

Chỗ duy nhất xem được đủ cả 5 số hạng: **dòng nông sản trong Kho**, thêm `127 × 1,62` và một nút
`?` 26×26 bung ra 5 dòng giải thích tại chỗ. Một nhãn, một nút bật tắt. Mọi chỗ khác trong game
hiện tối đa ba số hạng.

### 10.8 Danh sách sửa hình học trong code hiện có

| File:dòng | Hôm nay | Phải thành |
|---|---|---|
| [GameApp.cs:105](Assets/Scripts/GameApp.cs#L105) `FitFarm` | đọc `_root.rect.size`, lề 210/190 | đọc `Screen.safeArea`; lề **160**/190 |
| [Hud.cs:108](Assets/Scripts/UI/Hud.cs#L108) cụm ví | 420×108 | **392×108**, chip 206→186, hàng 2 để trống |
| [Hud.cs:149](Assets/Scripts/UI/Hud.cs#L149) chip sưu tập | 206×44 | **xoá** |
| [Hud.cs:80](Assets/Scripts/UI/Hud.cs#L80) dải nhiệm vụ | luôn gắn | **có điều kiện** |
| [Hud.cs:174](Assets/Scripts/UI/Hud.cs#L174) rail trái | 3 ô | **xoá** |
| [Hud.cs:184](Assets/Scripts/UI/Hud.cs#L184) rail phải | 4 ô, bước 104, có caption | **5 ô, bước 92, không caption**, vị trí (−58,−6) |
| [Hud.cs:206](Assets/Scripts/UI/Hud.cs#L206) `Rail()` | `step = 104f` | **92** |
| [UIKit.cs:198](Assets/Scripts/UI/UIKit.cs#L198) `IconBtn` caption | `Bottom(0,−20), 112×26` | **xoá nhánh caption**, thêm tooltip nhấn giữ |
| [Hud.cs:225](Assets/Scripts/UI/Hud.cs#L225) thanh đáy | 664×64, 3 nút | **864×72 tại (0,14)**, 4 ô 208×72 |
| [FarmView.cs:140](Assets/Scripts/Farm/FarmView.cs#L140) `waterMark` | `(+76,−8), 34×34` | **xoá**, giọt nước về khe badge |
| [FarmView.cs:129](Assets/Scripts/Farm/FarmView.cs#L129) `timerBox` | `(0,−8), 96×30` | **96×30 / 112×30** khi có tag |
| [FarmView.cs:113](Assets/Scripts/Farm/FarmView.cs#L113) `badge` | chỉ dấu chín, 40×40 | **44×44**, 1 trong 4 sprite, một khe |
| [GameApp.cs:531](Assets/Scripts/GameApp.cs#L531) `BuildPlantPop` | 420×232, ô 86×96, xếp theo `lv` | **460×252, ô 104×112, cây có tag ghim lên đầu**, dòng `×1,62` |
| [PanelsShop.cs:71](Assets/Scripts/UI/PanelsShop.cs#L71) badge hạt | `TopLeft(4,−4), 46×22` | thêm chip tag ở **`TopRight(−4,−4)`** + nút lọc `"Cây bonus"` |

### 10.9 Art còn thiếu

**6 sprite thời tiết** — nắng (dùng lại `Art/bg/sun`), mưa, gió lớn, tuyết, bão, hạn hán. Mỗi cái
phải **phân biệt được bằng bóng ở cỡ 26px**, vì `wNext` vẽ ở 26. Sinh bằng `Tools/gen_icons.py` —
tác phẩm gốc, không có câu hỏi bản quyền nào (đúng luật asset trong `CLAUDE.md`).

Màu: Nắng `#F5A524` · Mưa `#3E9BD8` · Gió Lớn `#37B5A6` · Tuyết `#8FD8FF` (mã hex mới duy nhất) ·
Bão `#8E64D8` · Hạn Hán `#E2574C`.

**Màu tag — không cần màu mới nào**: XP `Theme.Blue` · Giá Cao `Theme.Amber` · Đột Biến
`Theme.Purple` · Thời Vụ `Theme.Teal` (vừa được giải phóng khi Bạn bè rời rail).

**1 hiệu ứng đột biến bậc Ngọc Bích** — tô xanh ngọc + lấp lánh, dùng đúng đường tô màu mà 27 cây
không có art nguyên tố riêng đang dùng.
