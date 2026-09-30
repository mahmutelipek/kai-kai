# Milestone 4 Raporu — Stilize görsel pass (+ daha dinamik mekanik, skills denetimi)

Tarih: 2026-09-30 · Tag: `m4-done`

Blender MCP ve Unity Editor MCP yok. Bu yüzden:
- Bütün asset'ler koddan üretilen primitive'ler (box, küre, silindir, kapsül, koni, kama, oktahedron).
- Görsel kontroller, Unity'nin yüklediği **aynı üçgenlerin** headless bir WebGL önizlemede (three.js + Chromium) render edilmesiyle yapıldı.
- Bu önizleme Unity'nin renderer'ı değil. Işık, gölge ve post-processing yalnızca yaklaşık.

Ekran görüntüleri: `Docs/M4/*.jpg`.

## 1. Ne yapıldı

**A. Motordan bağımsız sanat katmanı: `Assets/Scripts/Runtime/Art` (namespace `Game.Art`, sadece System.Numerics)**
- **`ArtModel` / `MeshData` / `MeshSet`:** Primitive'lerden, pivot grupları olan modeller. Unity ve önizleme aynı geometriyi üretir.
- **`ArtLibrary`:**
  - **6 karakter**, referanstaki çocuklar. Hepsi ortak bir rig üzerinde (`Pose > Torso > Head / ArmL / ArmR`): büyük kafa, kalın sneaker, basit yüz (göz, parlama, kaş, yanak, ağız).

    | Oyuncu | Kıyafet | Kafa |
    |---|---|---|
    | P1 | beyaz/mavi raglan | mavi kasket |
    | P2 | kırmızı tişört | dikenli siyah saç |
    | P3 | yeşil hoodie | ters beyaz kasket |
    | P4 | sarı tişört | rasta saç + kırmızı bandana |
    | P5 | mor | mor bere + ponpon, beyaz saç |
    | P6 | turuncu hoodie | ayı kulaklı beyaz şapka |

  - **Longboard:** ahşap kenar, siyah grip, çapraz kırmızı bantlar, metal trucklar, büyük kırmızı tekerlekler.
  - **Engeller:**
    - Kırmızı/beyaz plastik bariyer.
    - 6 renk araba, biri taksi.
    - Uyarı lambalı A-çerçeve inşaat bariyeri.
    - Turuncu sprey halkalı çukur.
    - Sarı-siyah şeritli ayırıcı, sandık, kırık asfalt parçası.
  - **Pickup'lar:** parlayan coin, mor elmas, camgöbeği nitro şişesi.
  - **Dekor:** palmiye, pastel ev (kiremit çatı, pencere, kapı, baca), apartman, sokak lambası, sarı-siyah viraj işareti (chevron), yol işareti, varil, koni grubu, ağaç, çalı.
- **`ChunkGeometry`:** Her yol parçası için yol, şerit çizgileri, bordür, kaldırım, duvar, köprü (kırmızı korkuluk ve ayaklar), tünel (portal + üstünde tepe), ahşap rampa (sarı chevron) ve kenar dekoru.
  - Arazi solda tepe gibi yükseliyor, sağda körfeze doğru iniyor.
  - Bazı parçalarda istinat duvarı var.
  - Körfez tarafında metal bariyer; sert viraj dışında ve inşaat alanlarında kırmızı/beyaz bariyer.
  - Dekor chunk seriali ile deterministik, yol okunabilirliği için bordür dışında kalıyor.
- **`Backdrop`:** Körfez (iki ton su), kırmızı asma köprü, pastel şehir silueti (piramit kule dahil), ada, yelkenliler, uzak tepeler, bulutlar. Board'u takip ediyor ve yol yönüne yavaşça dönüyor, böylece körfez hep önde-sağda.
- **`RiderPose`:** Derin skate çömelmesi.
  - Hızla daha da eğiliyor, virajın içine yatıyor.
  - İçteki kol iniyor, dıştaki kalkıyor.
  - Wobble'da kollar sallanıyor; temiz inişte seviniyor.
- **`Palette`:** Tüm renkler tek bir 32×32 doku üzerinde. Bütün sanat 3 materyali paylaşıyor: mat, parlak, ışıyan.
- **`Atmosphere`:** Güneş, gökyüzü, sis ve ambient renkleri ile kamera kadrajı sabitleri; Unity ve önizleme ortak kullanıyor.

**B. Unity entegrasyonu**
- **Mesh ve materyaller:** `ArtMeshes` palet dokusunu ve 3 ortak materyali yönetiyor, mesh'lerde en fazla 3 submesh var. `ArtBuilder` modeli GameObject hiyerarşisine çeviriyor.
- **View'lar:**
  - `PlayerView`: yeni rig ve `RiderPose`.
  - `BoardView`: yeni board.
  - `ObstacleViews` / `PickupViews`: art modelleri.
  - `ChunkView`: chunk başına 1 mesh ve 1 renderer (önceden 8 renderer vardı).
- **Yeni bileşenler:**
  - `BackdropView`: uzak manzara.
  - `SceneAtmosphere`: güneş ve yumuşak gölge, trilight ambient, lineer sis, prosedürel gökyüzü. URP varsa global Volume: ACES, bloom, renk ayarı, vignette, hafif motion blur. `UrpSetup` eksik PostProcessData'yı atıyor.
  - `BoardFx`: tekerlek tozu (hızla ve virajla artıyor) ve çarpmada kıvılcım.
- **Kamera:** Referans gibi alçak **3/4 arka-sağ açı**. Board'un 5.4 m arkasında, 2.9 m yukarıda, 1.8 m sağında; virajın dışına kayıyor, FOV 70→86. Önceden 7 m arkada, 3.5 m yukarıda, ortadaydı.
- **HUD:** Referans düzeni korundu.
  - 1080p referansla ölçekleniyor ve `Screen.safeArea` içinde kalıyor.
  - Stil ve metinler önbellekte, kareler arası çöp üretmiyor.
  - Debug göstergeleri normal modda kapalı (F1).
- **Tuşlar:**
  - **1–6 (numpad dahil)** tahtada tam o kadar oyuncu bırakıyor ve ekranda "RIDERS: n" gösteriyor. 1 = tek oyuncu.
  - **F3:** hareket azaltma (kamera sarsıntısı, hit-stop ve speed lines kapanıyor).

**C. Daha dinamik mekanik (senin isteğin)**

Sadece `BoardTuning` değerleri değişti, simülasyon kodu değişmedi. Değerler bölüm 5'te. Ölçülen etki (headless, aynı senaryolar):

| tuning | %50 yaw tepkisi | %90 yaw tepkisi | tepe yaw hızı | max roll | ideal sürücü ort. hız (3 seed × 3 km) | crash |
|---|---|---|---|---|---|---|
| M3 | 0.53 s | 1.22 s | 17.4°/s | 6.2° | 81 km/h | 0 |
| M4 | 0.38 s | **0.87 s** | 19.0°/s | **8.7°** | **102 km/h** | 0 |

Senaryo: 4 oyuncu sol kenarda + 2 oyuncu ortada, 20 m/s. Board ~%30 daha hızlı tepki veriyor, daha çok yatıyor ve daha hızlı gidiyor.

Botlar ve test sürücüsü board'un tepki süresini artık tuning'den hesaplıyor (`CrewResponseDelay`). Önceden 0.9 / 1.0 s sabit kodluydu ve yeni tuning ile 1 crash üretiyordu.

**D. Skills (senin isteğin)**

GitHub'dan 4 skill `.claude/skills/` altına lisanslarıyla, değiştirilmeden eklendi:
- `performance-optimization`, `game-feel`, `game-ui-ux`: gamedev-skills/awesome-gamedev-agent-skills, Apache-2.0.
- `tools-unity-profiling`: tjboudreaux/cc-plugin-unity-gamedev, MIT.

İki skill'i (test-framework, mobile-optimization) eklemedim. Bizde olmayan UniTask, NSubstitute ve VContainer'a dayanıyorlar.

## 2. Test edilenler

**Headless: 64/64 geçti** (57 eski + 7 yeni `ArtAcceptanceTests`). Unity scriptleri 2021.3 referanslarına karşı 0 hata ve 0 uyarı ile derleniyor; test derlemesi artık `UNITY_EDITOR` altındaki kodu da kontrol ediyor.

| # | Kabul kriteri | Nasıl | Sonuç |
|---|---|---|---|
| 1 | 6 oyuncu oyun kamerasından ayırt edilebilir | Oyun kamerası (1920×1080), 6 karakter aynı noktada: siluet maskeleri ve ikili IoU. Yan yana: ortalama renk ΔE (CIE76). Görüntü: `Docs/M4/chars_gameplay_sheet.jpg` | Her karakterin kafa aksesuarı farklı (testle zorunlu). Siluet IoU 0.81–0.94: gövde aynı rig, farkı kafa yapıyor. Ortalama renk ΔE en az **21** (P2–P4), en fazla 67; ΔE > 10 gözle net fark. Gözle 6'sı da açıkça ayırt ediliyor |
| 2 | 6 oyuncuyla 60 fps (orta seviye donanım), draw call / batch raporu | Unity'de ölçülemedi. **Tahmin:** gerçek oyun durumlarından her renderer × dolu submesh sayıldı, culling yok (en kötü durum), bkz. aşağıdaki tablo. **Ölçüm testi hazır:** PlayMode `M4_SixRiders_RenderStatsReport` 30 s boyunca frame time, batches, SetPass ve üçgen sayısını logluyor | **Tahmin: ana pass'te 171–202 draw call**, 3 materyal (SRP Batcher dostu). Chunk rebuild 0 byte allocation (test). Chunk başına en fazla ~28 bin üçgen, 38 bin vertex. Gerçek fps ölçümü sende |
| 3 | Her engel ve pickup tepki mesafesinde tanınır | Her öğe tek başına, oyun kamerası, 50 m ve 30 m, 1080p. Ekrandaki boyut ve piksel bazında yoldan renk farkı (ΔE) | Tablo aşağıda. Yolla kontrast iyi: p90 ΔE 49–87. **Zayıf olanlar:** koni 50 m'de 6×8 px, çukur 50 m'de 27×2 px (yere yassı), 30 m'de 41×4 px |
| 4 | Oyun hissi M3'e göre sanat çalışmasından etkilenmedi | `M4_4_TuningIsPinned`: tüm tuning değerleri sabitlendi. Değişenler yalnızca bölüm 1C'deki, senin istediğin 11 değer; kalan 72 alan M3 ile birebir aynı (`git diff 0fededf` ile doğrulandı) | Sanat çalışması tuning'e dokunmadı. Mekanik değişikliği bilinçli ve ayrı raporlandı |
| + | 1 tuşu tek oyuncu bırakıyor | `M4_RiderCountKeys_OneLeavesExactlyOneRider` | 6→1 her sayı doğru, 1 oyuncuyla 2 s crash yok |
| + | Eski kabul testleri yeni tuning ile | İdeal sürücü 5 seed × 10 km, endless ekipler, M1 pisti, allocation | Hepsi geçti. Önce 2'si kırıldı (sabit kodlu tepki gecikmesi); düzeltildi |

**Draw call tahmini (seed 7, 6 oyuncu):**

| mesafe | chunk | chunk DC | arka plan | board | 6 oyuncu | engeller (adet) | pickup (adet) | FX + HUD | toplam |
|---|---|---|---|---|---|---|---|---|---|
| 300 m | 5 | 15 | 2 | 10 | 30 | 27 (17) | 13 (11) | 74 | **171** |
| 1500 m | 5 | 15 | 2 | 10 | 30 | 10 (10) | 41 (39) | 74 | **182** |
| 3000 m | 4 | 12 | 2 | 10 | 30 | 1 (1) | 51 (51) | 74 | **180** |
| 6000 m | 4 | 12 | 2 | 10 | 30 | 45 (31) | 29 (28) | 74 | **202** |

Gölge pass'i gölge atan objeler için bunun üstüne eklenir. Arka plan ve coin'ler gölge atmıyor.

**Tepki mesafesi ölçümü (1080p, kamera 60 % hızda):**

| öğe | 50 m kutu | 30 m kutu | p90 ΔE (yola göre) |
|---|---|---|---|
| Koni | 6×8 | 11×13 | 83 |
| Kırmızı/beyaz bariyer | 23×12 | 36×19 | 79 |
| Park etmiş araba | 25×20 | 40×31 | 49 |
| İnşaat bariyeri | 26×14 | 40×22 | 79 |
| Çukur | 27×2 | 41×4 | 39–50 |
| Sandık | 12×12 | 19×20 | 56 |
| Kırık parça | 21×6 | 33×10 | 72–85 |
| Ayırıcı | 10×26 | 16×41 | 85 |
| Coin | 9×10 | 14×14 | 83 |
| Elmas | 7×10 | 12×16 | 74–77 |
| Nitro | 6×12 | 9×20 | 61 |
| Hareketli araba | 25×21 | 40×33 | 84–87 |

## 3. Test EDİLEMEYENLER
- **Unity içinde hiçbir şey çalıştırılmadı:** yeni view'lar, palet materyali, URP post-processing (reflection ile), gökyüzü, sis, parçacıklar, HUD'un safe-area davranışı. 9 PlayMode testinin hiçbiri koşmadı; draw call ve fps testi dahil.
- **Ekran görüntüleri Unity değil:** three.js önizlemesi, aynı geometri ve renklerle. Unity'de gölge, bloom, ACES ve sis farklı görünecek. Motion blur ve parçacıklar önizlemede yok.
- **60 fps:** orta seviye donanımda ölçülmedi; sadece draw call tahmini var.
- **Oyun hissi:** Yeni tuning'in iyi hissettirip hissettirmediği ancak oynayarak anlaşılır. Headless sayılar tepki ve hızın arttığını gösteriyor, keyfini göstermiyor.
- **Unity 6:** derleme yine 2021.3 referanslarıyla kontrol edildi.

## 4. Bilinen sorunlar
- **Koni ve çukur uzaktan küçük:** koni 50 m'de ~8 px. Çukur yere yassı olduğu için ancak ~30 m'de okunuyor. İkisi de hafif ceza (sadece yavaşlatma ve sendeleme).
- **Hız ve okuma süresi:** Hız arttığı için 50 m artık ~1.4 s tepki süresi demek. Oynayınca tepki mesafesi yetmezse `softCapSpeed` düşürülebilir ya da kamera biraz yükseltilebilir.
- **Offroad ve dekor uyumsuzluğu:** Fizikte kenar şeridi 20 m düz, ama görselde arazi 9 m'den sonra yükselmeye başlıyor. Board yoldan 9 m'den fazla çıkarsa tepenin içinden geçiyor gibi görünür.
- **Sert viraj içi:** Arazi şeritleri sert virajların iç tarafında üst üste binebilir; sisin arkasında az görünür.
- **Toplu kalan oyuncular:** İdeal sürücü ve cooperative botlar oyuncuları board'un bir yarısında topluyor. Görüntülerde üst üste görünüyorlar; insan oyuncularla dağılırlar.
- **HUD hâlâ IMGUI:** HUD'da metin değişmedikçe çöp yok, ama bitiş ekranı ve F1 debug paneli string üretiyor (sadece o ekranlarda).
- **P2 / P4 yakınlığı:** Ortalama renkleri en yakın çift (ΔE 21). Ayrım dikenli saç ve rastalar ile tişört rengiyle net.
- **Tag push:** Önceki milestone'larda tag push 403 veriyordu.

## 5. Tuning (M3 → M4, senin "daha dinamik" isteğin)
| alan | M3 | M4 |
|---|---|---|
| steeringSmoothingTime | 0.4 s | 0.28 s |
| yawRateBaseDeg | 20 | 24 |
| yawResponseTime | 0.25 s | 0.18 s |
| maxRollDeg / rollResponseTime | 18° / 0.2 s | 24° / 0.15 s |
| startSpeed / speedRampPerSecond / softCapSpeed | 8 / 0.25 / 35 m/s | 12 / 0.4 / 38 m/s |
| frontAcceleration | 4 m/s² | 6 m/s² |
| playerMoveSpeed | 3.6 m/s | 4.2 m/s |
| respawnSpeedFraction | 0.6 | 0.7 |

Kamera (BoardTuning dışında, `CameraRigDefaults`): mesafe 5.4 m, yükseklik 2.9 m, sağa 1.8 m, bakış 10 m ileri / 1.4 m yukarı, FOV 70→86.

## 6. Skills denetimi (tüm proje)
| skill | kontrol | bulgu → yapılan |
|---|---|---|
| performance-optimization | Materyal ve draw call | ~40 düz renk materyali ve chunk başına 8 renderer vardı → palet dokusu + 3 ortak materyal, chunk başına 1 renderer |
| | Kareler arası allocation | HUD her karede 6 `GUIStyle` ve 9 string üretiyordu → önbellek. Chunk rebuild 27 KB ayırıyordu (prop ararken string ve closure) → 0 byte, testle kilitlendi |
| | Pooling | Engel, pickup ve chunk view'lar zaten pooled; parçacıklar yeniden kullanılıyor |
| | Bütçe | Chunk başına üçgen bütçesi (test), draw call tavanı (PlayMode testi), palet kapasitesi (test) |
| tools-unity-profiling | ProfilerMarker | `Downhill.RunSimulation.Step` ve `Downhill.ChunkView.Rebuild` eklendi; render stats testi var |
| game-feel | Sarsıntı ve hit-stop | Sarsıntı Perlin + sönümlü (doğru); hit-stop unscaled time kullanıyor (doğru); darbe güçleri kademeli (0.25 / 0.6 / 1.0) |
| | Erişilebilirlik | "Reduce screen shake" seçeneği yoktu → F3 |
| | Geri bildirim katmanları | Tekerlek tozu ve darbe kıvılcımı eklendi; karakter pozu virajda, wobble'da ve inişte tepki veriyor |
| game-ui-ux | Ölçekleme ve safe area | 1080p referans vardı; safe area yoktu → eklendi |
| | Olay tabanlı HUD | Popup'lar zaten olay tabanlı. Değerler her karede çiziliyor (IMGUI) ama string yalnızca değer değişince üretiliyor |
| | Gamepad | Tek menü bitiş ekranı: R / Space / (A) ile çalışıyor |
| (genel) | Ölü kod | Sanat geçişinden sonra kullanılmayan `MeshWriter`, eski placeholder renkleri ve primitive üreticileri silindi |

## 7. Eksik asset listesi (Blender / sanatçı ile yapılacaklar)
1. **Karakterler:** Rig'li gerçek modeller, 6 kıyafet. Kemik animasyonları: itme, çömelme, düşme, sevinç, yere düşüp kalkma. Yüz ifadeleri; göğüs logoları (P2 taç vb.).
2. **Longboard:** Yuvarlatılmış deck ve kicktail, grip dokusu, detaylı truck ve tekerlek.
3. **Araçlar:** Pikap, taksi, sedan, van; farlar ve teker detayı.
4. **Engeller:** Gerçek jersey bariyer, trafik konisi ve LOD'ları, inşaat bariyeri, çukur decal'i.
5. **Pickup'lar:** Coin (logo kabartması), elmas, nitro şişesi (etiket dokusu), toplama VFX'i.
6. **Çevre:**
   - Modüler SF evleri: bay window'lu "painted ladies", garaj kapısı, merdiven.
   - Apartman, istinat duvarı modülleri.
   - Palmiye (gerçek yaprak kartları), ağaç ve çalı.
   - Sokak lambası, trafik lambası, levha seti, guardrail, graffiti duvarı.
7. **Uzak manzara:** Köprü, şehir silueti, ada, yelkenli, tepe kartları.
8. **Dokular:** Asfalt (yama ve çatlak), şerit boyası aşınması, kaldırım, bordür, çim, çatı kiremidi.
9. **VFX:** Toz ve kıvılcım sprite'ları, nitro alevi ve iz, hız çizgileri shader'ı, su yansıması / ocean shader.
10. **UI:** HUD ikonları (coin, elmas, oyuncu avatarları), font, rozetler, sonuç ekranı çerçevesi.
11. **Ses:** M5'te.

## 8. Sonraki adımlar
1. Unity'de oyna ve kontrol et:
   - Kamera açısı, yeni hız ve tepki, parçacıklar, post-processing.
   - Test Runner → PlayMode → `M4_SixRiders_RenderStatsReport`: çıkan batches ve frame time değerlerini bana gönder.
   - Görsel sorun olursa Unity ekran görüntüsü gönder.
2. Tepki mesafesi yetmezse: koni ve çukur için görsel büyütme ya da ikaz levhası, veya kamerayı biraz yükseltme.
3. Onay gelirse Milestone 5: netcode multiplayer, ses, polish.
