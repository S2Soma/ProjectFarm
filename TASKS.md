# MATU Farm — việc đang làm

> 2026-09-15: đổi tên game từ MiT FArM thành **MATU Farm**. Mã gói giữ `com.mitfarm.game` (APK mới cài đè bản cũ, giữ ván).
> 2026-09-14: đổi tên game từ LQ Farm thành **MiT FArM**, mã gói `com.mitfarm.game`.

## 🔵 Bản web GitHub Pages (2026-09-15)

- [x] W1 `Editor/BuildWebGL.cs` + template `WebGLTemplates/MATU` (Brotli + fallback, tự lưu IndexedDB, nhắc xoay ngang) → `Builds/WebGL/MATUFarm` (76,9 MB)
- [x] W2 `Tools/deploy_web.sh` đẩy bản build lên nhánh `gh-pages` (chỉ bản build, force-push) — đã đẩy lần đầu 15/9 16:46
- [ ] W3 **Chủ dự án:** public repo + Settings ▸ Pages ▸ gh-pages / root → https://s2soma.github.io/ProjectFarm/
- [ ] W4 Chơi thử bản web trên máy tính và điện thoại: tải trang, lưu sau khi tải lại trang, đăng nhập/đồng bộ, âm thanh sau cú chạm đầu

## 🔵 Đợt góp ý 15/9 chiều: icon, hướng dẫn, ô khoá (2026-09-15)

- [x] G1 Icon trứng méo → vẽ lại trứng đối xứng (`gen_items.py pet_egg`, hình trứng giải tích thay vì ghép elip)
- [x] G2 Hướng dẫn về sau (gieo kín đảo, chờ khát, chờ chín, thu hoạch thêm) và mẹo có nền tối như các bước đầu; bước chờ khoét
  sáng ô/sheet đang nói tới nhưng cho chạm lọt qua (`CoachSpec.passThrough`); tua nhanh hướng dẫn ×12 cho cà rốt 2 phút
- [x] G3 Ô khoá không hiện giá; chỉ ô sắp mở hiện "Cấp N" nhỏ dưới ổ khoá khi còn thiếu cấp
- [x] G4 Re-test: 15 bộ kiểm tra, 89 ảnh audit, chơi thật từ ván mới hết hướng dẫn, nhập code / lên cấp 1→26 / mua ô / ấp 10 trứng
- [x] G5 Sửa lỗi lộ ra khi re-test: `Tween.Stagger` văng MissingReference khi đóng thẻ thưởng nhanh; đốm cam nhoè ở bảng Nâng cấp
  khi cấp tới "Không có nội dung mới"; sổ hướng dẫn còn ghi "ô sắp mở hiện cấp hoặc giá"

## 🔵 Đợt âm thanh êm & nhạc nền (2026-09-15)

- [x] S1 Bộ hiệu ứng mới tự tổng hợp, êm, cùng giọng La giáng (`Tools/gen_sfx.py`, thay Kenney, giữ tên file); nhạc nền
  `Terrace_in_the_Clouds` → vòng lặp liền (`Tools/make_music.py`) + `UI/Music.cs` + `Editor/AudioImporterRules.cs`
- [x] S2a Trong Unity: import đúng (hiệu ứng Decompress On Load, nhạc Streaming không preload; Normalize còn tick nhưng không tác dụng
  vì Force To Mono tắt), nhạc phát lặp ở màn hình bắt đầu (vol 0,4); nút loa + nút nốt nhạc trong đầu Menu bật/tắt được
- [ ] S2b Nghe thật trên điện thoại: cân nhạc/hiệu ứng, chỗ nối vòng lặp 2:31,7, độ nhỏ nhạc khi có jingle
- [ ] S3 **Chủ dự án xác nhận quyền dùng nhạc** (file mang C2PA "Created by Google Generative AI") trước khi phát hành

## 🔵 Đợt cân bằng lần 2: chơi không mã quà & save cũ (2026-09-15)

- [x] B1 `SaveDto.econV` + `SaveIO.MigrateEconomy`: save kinh tế cũ quy đổi xu/XP theo vị trí trên đường cấp, một lần; test trong "Kiểm tra file lưu"
- [x] B2 "Kiểm tra hành trình chơi" chạy thêm save cũ cấp 12 đã quy đổi (7 ngày: cấp 12 → 17)
- [x] B3 Đi hết đường cấp 1–30 không mã quà: bảng từng cấp, nhật ký ngày đầu, trứng #2/#5/#10, nguồn thu; trần mới: không cấp nào quá 4 ngày, trứng #2 trong 2 ngày
- [x] B4 Sửa tường: bảng cấp 13–30 (cấp 16/22/28 trùng lúc mua đảo, cấp 25+ đứng đợi XP), giá Đảo Băng/Hoả/Lôi/Vàng, hệ số giá ô Băng/Hoả/Vàng
- [x] B5 Trứng 25 → 35 UNIT; Bùa đột biến & Bùa kinh nghiệm 18/16 → 30 UNIT
- [ ] B6 **Chủ dự án quyết:** cấp 18, 19, 20 không mở cây nào (Chanh 17 → Lê 21) — dời Lê xuống 19 hoặc thêm chương nhiệm vụ 5–6 (hết chương ở cấp 12, ngày 6)
- [ ] B7 Thử trên máy: tài khoản test1@gmail.com (save mây cũ) — mở game, kiểm tra xu/XP sau quy đổi và bản đẩy lên có `econV`

## 🔵 Đợt thời gian trồng kiểu Hay Day & tưới theo lần (2026-09-15)

- [x] E1 Thời gian trồng 2 phút → 24 giờ theo bảng chủ dự án duyệt; trần cứng 24 giờ sau thời tiết/đột biến/đặc quyền (`PlayerState.GrowTimeIn`)
- [x] E2 Tưới theo lần: mỗi cây N lần (1–4), mỗi lần chín sớm một thời gian cố định; không bao giờ hiện "%"; sàn chống cữ chết
- [x] E3 File lưu cũ: ô đang trồng giữ `dur`/số cữ, chín nốt theo luật 20% cũ (`Plot.waterSec` = 0)
- [x] E4 Cân bằng lại giá/XP/năng lượng/hạt theo một luật (lãi mỗi vụ tăng theo thời gian, lãi mỗi giờ giảm), bảng cấp, giá đảo, thang ô đất, cống nạp
- [x] E5 UNIT = lãi một ô trong 8 giờ; "Chín ngay" theo thời gian còn lại; xu thăm bạn và xu rương theo UNIT; đơn hàng sống lâu hơn
- [x] E6 Đặc quyền đảo (giá bán/XP/đột biến/thời gian) được áp dụng thật
- [x] E7 "Kiểm tra hành trình chơi" mô phỏng theo phiên (4 phiên/ngày, ngủ đêm), mốc mới; viết lại "Kiểm tra tưới nước"; cập nhật đảo/đột biến/cửa hàng/nhiệm vụ/file lưu
- [x] E8 Chữ trên popup ô đất, nhãn nổi sau khi tưới, thẻ hạt giống, cửa hàng hạt giống, sổ hướng dẫn trang Tưới nước
- [x] E9 Tutorial.cs: `FastForward` 6 → 12 (cà rốt giờ 2 phút) và mẹo tưới không còn "20%" (điều phối viên đã sửa)
- [ ] E10 Thử trên máy: một ngày chơi thật theo phiên, xem cây dài trước khi ngủ có "đáng" không, giá Bùa kinh nghiệm/Bùa đột biến khi gom vụ cây dài
- [x] E11 Người test đang có ván cấp cao theo bảng cấp cũ: đã quy đổi khi nạp (xem B1)

## 🔵 Đợt chuyển cảnh vào nông trại (2026-09-15)

- [x] C1 Bấm vào game → cảnh "lặn qua biển mây" ~2,75 s thay cho fade 0,35 s (không phải màn hình tải, không dùng video)
- [x] C2 Farm chỉ dựng khi màn mây đã phủ kín: frame đứng hình lúc `BuildGame` là frame mây, không lộ farm dựng dở
- [x] C3 Thẻ nở thành mây, logo bay lên, camera lao vào đảo; mây tan từ giữa theo viền cụm mây (shader `UICloudVeil`),
  camera hạ xuống Vườn Nhà, HUD vào từ mép
- [x] C4 Chạm để tua nhanh (≤ 0,4 s); hướng dẫn/mẹo/toast đợi cảnh xong; lỗi trong cảnh → vào farm ngay
- [x] C5 Màu mây lúc mở theo giờ + thời tiết thật (đêm ra mây chàm, mưa ra mây xám)
- [x] C6 Art `Tools/gen_cinematic.py`, âm thanh tổng hợp `Tools/gen_cine_audio.py`; menu **Chụp chuyển cảnh** (ảnh T00–T29)
- [ ] C7 Thử trên điện thoại tầm trung: độ mượt lúc mây bay, thời gian đứng hình khi dựng, âm lượng whoosh/chime
- [ ] C8 (tuỳ chọn) Bản ngắn của cảnh mở cho `Restart(false)` (kéo save từ mây giữa game)

## 🔵 Đợt tài khoản & lưu lên mây — Supabase (2026-09-15)

- [x] A1 `Tools/supabase/schema.sql`: bảng `saves` + `profiles`, RLS mỗi người chỉ đọc/ghi dòng của mình, anon không có quyền
- [x] A2 Màn hình bắt đầu: Chơi ngay (khách) · Đăng nhập · Tạo tài khoản bằng email; nhớ phiên, lần sau vào thẳng "Vào nông trại"
- [x] A3 Lần đầu đăng nhập: tài khoản trống → đẩy save trên máy lên; tài khoản đã có save → hỏi giữ bản nào (bản còn lại cất .bak)
- [x] A4 Đồng bộ mỗi 45 s khi tiến trình đổi + khi ẩn app; mất mạng vẫn chơi bằng file trên máy
- [x] A5 Chống ghi đè giữa 2 máy (PATCH theo `saved_at`); ván của tài khoản A không bao giờ bị đẩy sang tài khoản B
- [x] A6 Menu ▸ Tài khoản: trạng thái đồng bộ, liên kết email cho khách, Đồng bộ ngay, Đăng xuất (chạm 2 lần)
- [x] A7 Bộ kiểm tra "Kiểm tra tài khoản & đồng bộ" + ảnh audit A0–AC; thử mạng thật: lỗi khách bị tắt, sai mật khẩu
- [ ] A8 **Chủ dự án làm trên Supabase Dashboard:** bật Anonymous sign-ins · tắt Confirm email (khi test) · chạy schema.sql
- [ ] A9 Sau A8: thử thật trọn luồng (khách → đẩy save → máy thứ 2 đăng nhập → chọn bản) rồi build APK

## 🔵 Đợt đảo mới & cây lớn (2026-09-15)

- [x] I1 Cây mới từ ảnh (chỉ lấy cây): chuối, dừa, bí đỏ, táo (hạt mới), cam
- [x] I2 Mưa / bão: tới cữ tưới là trời tự tưới, không cần chạm (kể cả lúc tắt game)
- [x] I3 Đảo Nước (sau Vườn Nhà, cấp 3): sông cắt ngang đảo, đổ thành thác xuống mép đảo; 16 ô ở 2 bờ sông
- [x] I4 Khổng Lồ (cấp 6, trước Đảo Gió): 4 ô đất lớn, tặng sẵn 2
- [x] I5 Luật ô nhỏ / ô lớn: táo 6, cam 8, chuối 11, dừa 15 chỉ ô lớn; cây nhỏ chỉ ô nhỏ
- [x] I6 File lưu cũ: chèn 2 đảo mới, đảo cũ giữ nguyên; ai có Đảo Gió được mở sẵn 2 đảo mới
- [x] I7 Ô đất khoá: chỉ ô sắp mở hiện "Cấp N" (hoặc giá khi đã đủ cấp)
- [x] I8 Nút Thu hoạch / Tưới nước chỉ hiện khi có ô đủ điều kiện
- [x] I9 Lướt map: bỏ snap lần hai (kéo map về đảo cũ); vuốt ngắn cũng sang đảo

## 🔵 Đợt trang trí & thời tiết (2026-09-15)

**Cửa hàng trang trí — ô mới và món mới**
- [x] T1 Ô "Ô đất": Viền Gỗ Mộc, Đá Cuội, Hoa Nhí, Pha Lê, Hoàng Kim
- [x] T2 Hiệu ứng gieo: thêm Sao Lấp Lánh, Chồi Non, Bong Bóng, Pháo Hoa Nhỏ
- [x] T3 Ô mới "Tưới cây": Bong Bóng Xà Phòng, Mưa Hoa, Giọt Kim Cương, Cầu Vồng
- [x] T4 Hiệu ứng thu hoạch: thêm Pháo Giấy, Sao Băng, Mưa Xu
- [x] T5 Ô mới "Thông báo": Gỗ, Kẹo Dâu, Đêm Sao, Hoàng Kim
- [x] T6 Ô mới "Chạm": Vòng Sóng, Lá Xanh, Ngôi Sao, Trái Tim
- [x] T7 Ô mới "Vuốt": Vệt Cánh Hoa, Vệt Sao, Vệt Cầu Vồng, Bụi Tiên
- [x] T8 Kệ Trang trí có hàng lọc theo ô

**Thời tiết đẹp hơn, có chiều sâu**
- [x] T9 Mưa/bão/tuyết nhiều lớp xa–gần, gợn nước mưa trên mặt đất, bông tuyết lớn ở gần
- [x] T10 Bão: tia sét; Gió: lá + cánh hoa theo cơn; Hạn: hơi nóng + bụi; Nắng: phấn hoa + tia nắng

## 🔵 Đợt góp ý sau khi chơi thử (2026-09-14 tối)

**Đợt 1 — kinh tế & cân bằng**
- [x] F1 Đổi thời tiết mỗi 15 phút (thay vì 1 giờ); câu chữ "mỗi giờ" đã sửa hết
- [x] F2 Cân bằng lại: thời gian trồng 40 s → 70 phút, bảng cấp đầu thấp hơn, cống nạp giảm;
      nhiệm vụ ngày trả theo UNIT (trước trả cố định 1.500 XP → nhảy 4 cấp ở cấp 1)
- [x] F3 Rương: thanh năng lượng theo số ô × năng lượng cây, bậc rương tung ngẫu nhiên, huyền thoại 1–3%
- [x] F4 Cửa hàng thêm Bùa kinh nghiệm, Thuốc lớn nhanh, Rương quý, Túi hạt quý; Năng lượng thần kỳ = đầy thanh
- [x] F5 Hướng dẫn trồng cà rốt: thời gian chạy ×6 trong lúc chờ tưới/chờ chín

**Đợt 2 — giao diện**
- [x] F6 Cửa hàng và các danh sách khác không đẩy lên đầu (xoá hàng cũ ngay; đổi tab thì cuộn về đầu)
- [x] F7 Bớt thông báo: một toast duy nhất nền sáng, trùng thì đếm ×N; bỏ toast gieo kín/đã tưới/đã gieo
- [x] F8 Đồng hồ số → thẻ 2 thanh (lớn + tới cữ tưới) ở lớp trên mọi ô, không bị che
- [x] F9 Popup cây: 2 hàng thanh + chữ ngắn, cập nhật mỗi giây; "Chín ngay" giá theo UNIT
- [x] F10 Bỏ dấu gạch "—" trong mọi câu chữ hiển thị
- [x] F11 Tab Chương truyện: thanh chọn chương thành hàng riêng (trước đè lên tab "Hằng ngày"), thêm "Xong n/4"
- [x] F12 Tai thỏ: nền tối popup/thẻ thưởng và giấy sheet hạt giống tràn ra mép (`FullBleed`)

**Đợt 3 — art & hiệu ứng**
- [x] F13 Cây mới từ 3 ảnh ChatGPT (chỉ lấy cây, không lấy ô đất): cà chua, dưa hấu, dâu, ngô, lúa mì thay mới;
      thêm nho, nấm, cà tím, dứa (`Tools/slice_crops_sep14.py`)
- [x] F14 Cây đột biến: tint đậm hơn, vòng sáng + hào quang + tia sáng + đốm bay lên, hiện ngay từ lúc gieo
- [x] F15 Biển mây: cụm to nhỏ khác nhau, lớp xa mờ/nhoè, thêm mây tầng và mây ti

**Đợt 4 — yêu cầu thêm**
- [x] F16 Font Baloo 2 (OFL), chỉnh kích thước/độ cao dòng khớp bố cục cũ
- [x] F17 Thú cưng (ảnh ở `Pets/`, tên file = tên pet: MiT, TiM, Pinkteriii, Shushi, Yummy, bé gà)
  - [x] Mở ở cấp 5; ấp trứng 62/27/9/2%, bảo hiểm 40 trứng, trùng thì lên cấp, trứng đầu miễn phí
  - [x] Mỗi 3 phút pet kiểm tra vườn: ô khát / ô chín → đi tới đúng đảo, đúng ô để tưới / thu hoạch
  - [x] Thỉnh thoảng (35%) pet ăn vụng 1 nông sản trong kho, nông sản đột biến bị chọn nhiều hơn hẳn

## 🔵 Đợt chuẩn bị đưa đi test (2026-09-14)

- [x] Rà chỗ kẹt bằng mô phỏng chơi thật (**Kiểm tra hành trình chơi**, 3 lượt × tới 40 giờ chơi): tìm ra
      "Đạt cấp 16" (6–10 giờ), "Thăm nom bạn bè ×10" (13–15 giờ, chỉ có 6 bạn), "Đạt cấp 25" (10–13 giờ).
      Đã chỉnh mốc 4/7/10/13 và thăm bạn ×6 → không bước nào quá 3 giờ chơi liên tục.
- [x] Tách kinh tế gieo/tưới/thu hoạch/bán/mở rương ra `PlotLogic`/`PlayerState` để mô phỏng chạy đúng code game
- [x] Mã quà tặng: Menu ▸ Nhập code, mã `TONGDAIYUMMY` = 22 triệu XP + 22 tỷ xu, một lần mỗi nông trại
- [x] Xu đổi sang `long` (22 tỷ vượt `int`), HUD hiện "22 tỷ"
- [x] Icon mới: trời toả nắng, đảo với ngô · cà chua · dâu, đồng xu, viền sticker
- [x] Build Android `Builds/Android/MiTFArM-1.0-20260914-0816.apk` + iOS `Builds/iOS/MiTFArM-Xcode/` (project Xcode — cần Xcode + Apple Developer để ký)

## 🔵 Đợt hoàn thiện 2026-09-13 (sau bản APK thử đầu tiên)

**A. Hướng dẫn chơi** (ưu tiên số 1 — game chưa có gì ngoài một toast "Chạm vào ô đất")
- [x] A1 Hướng dẫn tương tác lần đầu: làm tối màn hình chừa lỗ sáng + ngón tay chỉ + lời dẫn ngắn.
      Chào mừng → gieo cà rốt → gieo thêm → đợi cữ tưới → tưới → thu hoạch → bán ở Kho → nhiệm vụ.
      Lưu bước vào file lưu (thoát giữa chừng vào lại tiếp đúng bước), có nút Bỏ qua, người chơi cũ không bị hiện
- [x] A2 Mẹo theo ngữ cảnh, mỗi mẹo hiện một lần: đủ XP lên cấp, mở Thu hoạch nhanh (cấp 3) / Tưới nhanh (cấp 7),
      rương đầu tiên, đột biến đầu tiên, thời tiết xấu đầu tiên, đủ điều kiện mở đảo mới
- [x] A3 Sổ "Hướng dẫn chơi" trong Menu: các trang có hình (cơ bản, tưới, thời tiết, cây bonus, đột biến,
      nhiệm vụ, năng lượng & rương, đảo & cống nạp) + nút "Chơi lại hướng dẫn"
- [x] A4 Bộ test "Kiểm tra hướng dẫn" + ảnh audit từng bước

**B. Bản Android**
- [x] B1 Icon ứng dụng (đang là icon Unity mặc định) — vẽ icon gốc
- [x] B2 Màn hình khởi động theo màu game
- [x] B3 Nút Back: đóng lần lượt thẻ thưởng → bảng → sheet hạt → menu → popup ô đất
- [x] B4 Đổi productName làm Editor đổi thư mục lưu (`DefaultCompany/Farm3` → `DefaultCompany/LQ Farm`) — chép ván cấp 12 sang

**C. Giao diện**
- [x] C1 Bảng Nâng cấp nói rõ thiếu gì (XP / xu) thay vì "Chưa đủ điều kiện"
- [x] C2 Sheet hạt giống: nhãn bonus và nhãn độ hiếm cùng một kiểu pill ("Hiếm" và "Kinh Nghiệm" cùng màu xanh) → tách kiểu
- [x] C3 Icon Menu khó hiểu (Kho là chồng gạch, Sưu tập là hộp đỏ) → icon vẽ rõ nghĩa

**E. Lỗi người dùng báo khi chơi thử (2026-09-13)**
- [x] E1 Đầy thanh kinh nghiệm nhưng không lên cấp được
- [x] E2 Cây đã chín nhưng ô vẫn treo đồng hồ "1s"
- [x] E3 Cữ tưới tự ẩn sau vài giây — hết thời gian chờ thì phải luôn hiện cho tới khi tưới
- [x] E4 Danh sách nhiệm vụ canh giữa; nhận thưởng một đơn thì danh sách không dồn lên đầu

**D. Dọn dẹp & kiểm chứng**
- [x] D1 `Assets/Scripts/README.md` lỗi thời (FarmView.cs, thanh đáy, font hệ điều hành…)
- [x] D2 Ảnh cũ trong `Screenshots/` không còn được sinh ra
- [x] D3 Chạy đủ bộ test (12/12) + chụp ảnh + build lại APK `Builds/Android/LQFarm-1.0-20260914-0006.apk`

**Ghi chú khi làm (2026-09-13):**
- E1 thật ra là đường cong cấp độ cũ chưa từng được thay (REDESIGN §4.3): cấp 1 cần 15.000 xu. Đã thay bảng.
- E2: ô chín đúng lúc danh sách đếm giờ bị dựng lại (thu hoạch/gieo ô khác) thì không bao giờ được vẽ lại.
- E3: cữ tưới giờ mở tới khi cữ sau mở; chạm ô khát là tưới luôn.
- Luồng hướng dẫn đã chạy thật từ ván mới tới hết (chạm qua EventSystem, kể cả chạm lọt lỗ sáng).


> ## ⚠️ 2026-09-12 — RE-DESIGN LỚN. Đọc `REDESIGN.md` TRƯỚC.
> Toàn bộ file này nói về bản game cũ (1 đảo, 16 ô, không thời tiết/tag).
> Plan mới đã chốt hướng: 6 đảo trên một canvas pan/zoom, thời tiết 6 trạng thái đổi mỗi giờ,
> tag cây xoay 12 giờ, 4 bậc đột biến, tưới theo lượt, nhiệm vụ có hạng, shop vật phẩm giữ lại.
> Lộ trình 11 bước ở `REDESIGN.md` §7. **Đã xong cả 11 bước (0–11).**
> Việc còn lại **không phải code**: art hướng A theo `ART_SPEC.md` (tôi không gen được ảnh),
> âm thanh, dọn thư mục, và build lên máy thật để kiểm vùng an toàn.
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
- [x] Âm thanh — 23 hiệu ứng Kenney CC0 (`Resources/Audio`, `UI/Sfx.cs`), nút bật/tắt ở đầu Menu
- [~] Safe area — đã giả lập tai thỏ trong Editor (`SafeAreaFitter.Simulated`, ảnh `33_safe_area_notch`):
      mọi cụm HUD vào đúng vùng an toàn. **Vẫn nên build lên máy thật một lần** trước khi phát hành.

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
