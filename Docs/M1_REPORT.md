# Milestone 1 Raporu — Core Board Control

Tarih: 2026-09-29 · Tag: `m1-done`

## 0. Preflight sonucu (bağlam)

- **Unity Editor MCP:** bu oturumda yok, çağrılamadı. Container'da Unity kurulu değil, `download.unity3d.com` ağ politikası tarafından engelli.
- **Blender MCP:** bu oturumda yok. `download.blender.org` da engelli. M4'te primitive fallback kullanılacak.
- Karar: çekirdek simülasyon Unity'den bağımsız saf C# olarak yazıldı ve headless test edildi. Unity katmanı yazıldı ve derleme kontrolünden geçti ama **Unity içinde hiç çalıştırılmadı**.

## 1. Ne yapıldı

- Proje iskeleti: `Packages/manifest.json` (Input System, URP, Test Framework), `ProjectVersion.txt`, `.gitignore`, `Game.Runtime` / `Game.Editor` / `Game.Tests` asmdef'leri, README.
- **Simülasyon (UnityEngine bağımlılığı yok):**
  - `BoardWeightSystem`: board-local pozisyonlar, ağırlıklı ortalama ile lateral/longitudinal, ardından `sign·|x|^1.6`.
  - `BoardPhysicsSim`: steering smoothing → hıza bağlı hedef yaw rate → torque benzeri 1. derece yaw tepkisi → heading entegrasyonu. Traction ve grip kaybı ile kayma, hız modeli (cruise rampası, ön ağırlık hızlandırır, arka ağırlık frenler, soft cap), danger/wobble/tip/crash, dönüşe yatma (roll), zemin takibi, rampadan kalkış ve iniş.
  - `PlayerSim`: board-local kinematic karakter. Yürüme, zıplama, stagger, wobble'da düşük tarafa kayma, frende öne savrulma, kenardan düşme.
  - `PlayerCrowdSolver`: iç içe geçme yok, çarpışınca itme ve stagger.
  - `BoardSimulation`: tek otoriter adım. Güvenli respawn, crash'te herkes fırlar, restart.
  - 6 bot davranışı: cooperative, stubborn-left, stubborn-right, random wanderer, greedy-front, scared-rear.
- **Unity katmanı:** `BoardController` (FixedUpdate 60 Hz, kinematic Rigidbody, pose sadece simülasyondan gelir), `BoardView`, `PlayerView` (büyük kafa, iri sneaker, lean/crouch/panik kolları, düşünce takla atarak uçma, respawn'da pop animasyonu), `PlayerInputRouter` (klavye + her gamepad bir oyuncu + botlar), `TestRoad` (yaklaşık 2.3 km, %6 eğim, sol viraj, sağ viraj, 11 koni, 1 rampa, çim banket = offroad drag), `UnityGroundProvider` (raycast), `ConeObstacle`, `CameraController` (7 m arkada, 3.5 m yukarıda, yaklaşık 15° bakış, FOV 65→80, hafif banking, sadece yüksek hızda veya çarpmada shake, crash kamerası), `RunManager` (crash sonrası yolda respawn, R ile restart, yol sonunda başa dönüş), `DebugOverlay` (F1), `TuningPanel` (F2, bütün `BoardTuning` alanları reflection ile slider olarak), `GameHotkeys`, `ProjectSetup` (sahne ve tuning asset'i ilk açılışta otomatik oluşturulur).

## 2. Test edilenler ve nasıl

Headless: `Tools/Headless/Tests` → `dotnet test`, **28/28 geçti**. Aynı test dosyaları Unity Test Runner'da da derleniyor. Simülasyon 60 Hz sabit adımla ve %5 eğimli düzlem üzerinde koşuyor. "pinned" oyuncu sabit tutuluyor, "held" oyuncu gerçek input ile hedef noktaya yürüyüp orada duruyor.

| # | Kriter | Sonuç |
|---|---|---|
| 1 | 3L/3R, hepsi ortada, LRLRLR; 8/20/35 m/s → yerleştikten sonra \|yaw rate\| < 1°/s | **0.0000°/s** (pinned ve held). ±5 cm gürültülü 20 denemede de < 1°/s |
| 2 | 4L/2R kademeli sola dönüş | −3.92 / −5.79 / −8.09 °/s (8/20/35 m/s). %63'e ulaşma süresi 0.67–0.68 s |
| 2 | 5L/1R belirgin şekilde daha güçlü | −11.78 / −17.18 / −23.66 °/s (4L/2R'nin yaklaşık 3 katı) |
| 2 | Hepsi en solda → önce wobble, sonra tanımlı sürede crash | wobble 0.43 / 0.35 / 0.28 s, crash **1.65 / 1.20 / 1.00 s** (limit 3 s) |
| 2 | Aynası (sağ) | Değerler birebir simetrik (fark < 1e-3) |
| 3 | Hepsi önde → hız artar, hepsi arkada → hız düşer | 5 s sonunda, 12 m/s başlangıçla: orta 12.00 / ön 19.56 / arka 3.00 m/s. 20 m/s başlangıçla: 20.00 / 27.56 / 8.66 m/s. Cap'teyken hepsi önde: 37.0 m/s (soft cap çalışıyor) |
| 4 | 2 oyuncu, biri kenarda biri ortada → kontrollü dönüş | 7.46 / 10.95 / 15.18 °/s, max danger 0.35–0.45 (wobble 0.7'de başlar), crash yok |
| 4 | 2 oyuncu, ikisi de kenarda → crash olabilir | Her hızda crash (1.65 / 1.20 / 1.00 s) |
| 4 | Oyuncu sayısına göre normalizasyon | 2 kişi kenarda = 6 kişi kenarda (aynı steering). 6'dan 2'ye düşünce lateral 0.283 → 0.850 |
| 5 | NaN yok | 10 × 60 s rastgele input fuzz, NaN yok (sim NaN'da exception atar) |
| 5 | Dururken jitter/salınım yok | 10 s boyunca yaw rate, roll ve oyuncu kayması **tam 0** |
| 5 | Sabit dönüşte overshoot/salınım yok | overshoot ≤ %1.6, std ≤ 0.049°/s, dönüm noktası 1 (tek monoton yükseliş) |
| 6 | İç içe geçme yok, deck altında kalma yok | 120 s boyunca herkes merkeze koşup rastgele zıpladı. Min mesafe 0.659 m (çap 0.66). Yükseklik hiç < 0 olmadı. 4 düşme / 4 respawn, en uzun board dışında kalma 1.52 s (respawnDelay 1.5) |
| + | Rampa | 18 m/s'de havada 0.47 s, yolun 1.12 m üstüne çıktı, 7.6 m/s ile sert iniş, 6 oyuncu stagger, crash yok |
| + | Crash → restart | Board düşükken respawn yok. 2.5 s sonra `RestartDue`, restart'ta herkes board'da ve çakışma yok |
| + | Bot anlaşmazlığı (6 mixed bot, 180 s, hız cap'e kadar artıyor) | steering std 0.033, zamanın %11'inde dönüş, %0 wobble, 0 crash, 21 düşme, 68 stagger |

**Yakalanıp düzeltilen buglar:** (1) Board sabit bir eğimde her reset ve inişten sonra bir kare havaya kalkıyordu (dikey hız eğime uymuyordu). Bu yüzden 35 m/s'de dönüşler zayıf ölçülüyordu. Düzeltildi. (2) Crash'te pinned oyuncular fırlamıyordu, restart onları geri koymuyordu. Düzeltildi. (3) İlk bot versiyonları birbirini tamamen nötrlüyordu (zamanın %2'sinde dönüş). Stubborn botlara push/rest döngüsü eklendi, greedy bot artık board'un yattığı tarafa koşuyor.

**Unity script derleme kontrolü:** `Tools/UnityCompileCheck/build.sh`, Game.Runtime + Game.Editor + Game.Tests, warnings-as-errors, **0 hata 0 uyarı**. Referanslar Unity 2021.3 engine ve 2021.1 editor. Input System ve Test Runner elle yazılmış stub.

## 3. Test EDİLEMEYENLER

- **Unity 6'da derleme.** Sadece 2021.3 referanslarına karşı derlendi. Input System API'si stub üzerinden kontrol edildi, gerçek paket değil.
- **Hiçbir şey Unity içinde çalıştırılmadı:** sahne, raycast zemin takibi, test yolu mesh'i, koni fiziği, kamera, player görselleri, debug overlay, tuning panel, klavye/gamepad input, Tab ile oyuncu değiştirme.
- **`BoardPlayModeTests` (6 test) yazıldı ama hiç koşturulmadı.**
- Oyun hissi (fun), görsel okunabilirlik, 60 fps, kamera çerçevesi: hiçbiri gözle doğrulanmadı.
- Package sürümleri (`inputsystem 1.11.2`, `URP 17.0.4`, `test-framework 1.4.6`) ve `ProjectVersion 6000.0.58f2` kurulu Editor'e karşı doğrulanmadı.
- Unity katmanının çalışma zamanı mantığı (RoadPath projeksiyonu, bot yol ipucu) headless koşturulamadı, çünkü Unity DLL'leri native çağrı içeriyor. Yol ipucunun işaretini elle doğruladım.

## 4. Bilinen sorunlar / riskler

- Mixed botlarla 6 kişide anlaşmazlık **hafif**. Ağırlıklı ortalama yüzünden tek bir bot board'u çok az etkiliyor (spec'teki normalizasyonun doğal sonucu). "Komik" hissettiriyor mu, sizin oynayıp değerlendirmeniz lazım. C tuşu ile All-cooperative moduna geçilebilir.
- 1 insan + 5 mixed bot ile test yolunun virajlarını almak zor olabilir. Botlardan sadece cooperative olan yolu takip ediyor.
- Kinematic board rampanın yan yüzeyiyle fiziksel çarpışmıyor (görsel olarak iç içe geçebilir). Zemin örneklemesi board'un orta hattından yapılıyor.
- Board boyutu runtime'da değiştirilirse görseller ancak restart'ta güncellenir.
- `UnityGroundProvider` collider cache'i M2'deki chunk pooling için temizlenmeli.
- URP asset'i otomatik oluşturulmuyor. URP yoksa Standard shader'a düşülüyor.

## 5. Son tuning değerleri

Başlangıç değerlerinden hiçbiri değiştirilmedi. Spec'teki başlangıç sayıları: exponent 1.6, smoothing 0.4 s, max roll 18°, wobble başlangıcı 0.7, board 6×2.4 m, tekerlek 0.35 m, hız 8 → 35 m/s. Türetilen değerler: yawRateBase 20°/s + 1.2°/s per m/s, yawResponse 0.25 s, stability 1.25 → 1.8, crashTipTime 0.5 s, frontAccel 4, rearBrake 6 m/s², cruiseGain 0.35, playerRadius 0.33, moveSpeed 3.6 m/s, respawn 1.5 s, crash restart 2.5 s. Tam liste README'deki tabloda.

## 6. Önerilen sonraki adımlar

1. Siz Unity 6'da açın ve PlayMode testlerini koşturun. Derleme veya paket hatası çıkarsa hata logunu gönderin, düzelteyim.
2. 5–10 dakika oynayın: steering ağırlığı (0.4 s), 6 kişide tek oyuncunun etkisi, virajlar, crash sıklığı. Beğenmediğiniz değerleri F2 panelinden ayarlayıp bana bildirin.
3. Onay gelirse Milestone 2: prosedürel yol, chunk kütüphanesi, DifficultyManager.
