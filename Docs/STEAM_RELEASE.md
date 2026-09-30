# Steam'de yayın rehberi — Downhill Party Board

Bu dosya oyunu Steam'e çıkarmak için yapılması gerekenleri sırasıyla listeler. **[KOD HAZIR]** olan maddeler projede zaten var. **[SEN]** olanlar Steamworks sitesinde veya Unity'de senin yapman gerekenler.

## 1. Steamworks hesabı ve uygulama [SEN]

1. **Hesap:** partner.steamgames.com'da Steamworks hesabı aç. Kimlik, vergi ve banka bilgilerini doldur.
2. **Steam Direct ücreti:** 100 USD ödeyince bir **App ID** alırsın. Ücret, oyun 1000 USD gelir yapınca geri ödenir.
3. **Süreler:**
   - Ödemeden sonra en az **30 gün** geçmeden yayın yapılamaz.
   - Mağaza sayfası çıkıştan en az **2 hafta** önce "Coming Soon" olarak yayında olmalı.
   - Mağaza sayfası ve build ayrı ayrı Valve incelemesinden geçer; her biri birkaç iş günü sürer.
4. **App ID'yi projeye yaz:** `Assets/Scripts/Runtime/Platform/SteamSettings.cs` → `AppId`. Şu an 480 (Valve'ın "Spacewar" test uygulaması), geliştirme için çalışır.
5. **Stüdyo adı:** `Assets/Scripts/Editor/BuildScript.cs` → `companyName` (TODO satırı).
6. **Unity lisansı:** Unity 6 Personal yıllık 200.000 USD gelire kadar ücretsiz. Üstünde Unity Pro gerekir.

## 2. Steamworks.NET kurulumu [SEN, 2 dakika]

Kod hazır ve derleniyor: `Assets/Scripts/Steam/SteamPlatform.cs`, NuGet'teki Steamworks.NET 2024.8.0 API'sine karşı kontrol edildi. Paket kurulunca kendiliğinden devreye giriyor, kurulmazsa oyun Steam'siz çalışıyor.

1. **Paketi ekle:** Unity → Window → Package Manager → **+** → *Add package from git URL*:
   `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2024.8.0`
   Bu yöntem bilgisayarında Git kurulu olmasını ister. Git yoksa Steamworks.NET GitHub "Releases" sayfasından `.unitypackage` indirip çift tıkla.
2. **Kontrol et:** Steam açıkken Play'e bas. Console'da `Downhill: platform = Steam` görmelisin.

## 3. Başarımlar (Stats & Achievements) [SEN tanımlar, KOD HAZIR]

Steamworks → App Admin → Stats & Achievements. **API adları birebir aynı olmalı.**

- Her başarım için 2 ikon lazım (açık/kapalı, 256×256 JPG).
- Türkçe ad ve açıklamayı da gir.

| API adı | Ad | Açıklama |
|---|---|---|
| ACH_FIRST_RIDE | Drop In | Finish your first run. |
| ACH_KM_1 | Downhill Rookie | Ride 1 km in one run. |
| ACH_KM_5 | Hill Bomber | Ride 5 km in one run. |
| ACH_KM_10 | Endless Summer | Ride 10 km in one run. |
| ACH_COMBO_MAX | Combo King | Reach the maximum combo multiplier. |
| ACH_COINS_100 | Coin Hoarder | Collect 100 coins in one run. |
| ACH_DIAMONDS_5 | Gem Hunter | Collect 5 diamonds in one run. |
| ACH_NEAR_MISS_10 | Close Shave | Get 10 near misses in one run. |
| ACH_CLEAN_LANDINGS_5 | Butter Landing | Land 5 clean jumps in one run. |
| ACH_NITRO | Nitro Rush | Fire a nitro boost. |
| ACH_FULL_CREW | Full Crew | Ride with six human players. |
| ACH_NO_CRASH_3KM | Unstoppable | Ride 3 km without a crash. |
| ACH_SCORE_50K | High Roller | Score 50,000 points in one run. |
| ACH_WIPEOUT | Epic Wipeout | Crash the board for the first time. |

**Stat:** `STAT_TOTAL_METRES`, tipi INT, Set by: Client. Toplam sürülen metreyi tutar.

**Leaderboard'lar:** `best_distance` ve `best_score`.
- Sort: Descending, Display: Numeric.
- Kod bunları gerekirse kendisi oluşturur (`FindOrCreateLeaderboard`), ama sitede önceden oluşturmak daha temiz.
- Kod her zaman en iyi skoru tutar ("keep best").

Başarımlar yalnızca en az bir **insan** oyuncu varken açılır; başlık ekranının arkasındaki bot sürüşü sayılmaz.

## 4. Steam Cloud [SEN]

App Admin → Steam Cloud → **Auto-Cloud**. Kota: 1 MB, 10 dosya.

| Platform | Root | Alt klasör | Dosyalar |
|---|---|---|---|
| Windows | `WinAppDataLocalLow` | `<ŞirketAdı>/Downhill Party Board` | `highscores.txt`, `settings.txt` |
| Linux / Steam Deck | `LinuxHome` | `.config/unity3d/<ŞirketAdı>/Downhill Party Board` | aynı dosyalar |

Oyun bu dosyaları zaten `Application.persistentDataPath` içine yazıyor **[KOD HAZIR]**.

## 5. Kontrolcü, Steam Deck, Remote Play Together

- **Tam kontrolcü desteği [KOD HAZIR]:**
  - Menüler, lobi, oyun ve duraklatma tamamen gamepad ile oynanıyor.
  - Her gamepad lobide (A) ile kendi oyuncusunu alıyor.
  - Start duraklatıyor; kontrolcü koparsa oyun duraklıyor.

  **[SEN]:** Steamworks'te *Controller support: Full*. Steam Input varsayılan ayarı "Gamepad".
- **Steam Deck [KOD HAZIR, test SEN]:**
  - HUD 1280×800 (16:10) dahil 7 ekran tipinde test edildi.
  - En küçük yazı ~19 px (Valve alt sınırı 9 px).
  - Gamepad ile tam oynanış var, başlatıcı (launcher) yok.
  - Linux build seçeneği var; Windows build Proton ile de çalışır.

  **[SEN]:** Steamworks → Steam Deck Compatibility → incelemeye gönder.
- **Remote Play Together:**
  - Oyun yerel co-op olduğu için bu özellik arkadaşlarınla internetten oynamanın en kolay yolu. Ağ kodu gerekmez.
  - Arkadaşların, Steam'in kendilerine yansıttığı gamepad'lerle lobide (A)'ya basarak katılır.

  **[SEN]:** Steamworks → Remote Play → *Remote Play Together*'ı işaretle. Mağaza etiketleri: *Shared/Split Screen*, *Remote Play Together*.
- **Tek klavyede 2 oyuncu [KOD HAZIR]:** WASD + Boşluk + E, ve oklar + Enter + Sağ Shift.

## 6. Build ve yükleme [KOD HAZIR + SEN]

1. **Build al:** Unity → **Downhill → Build → Windows x64 (Steam release)**. Linux için ayrıca **Linux x64 (Steam Deck)**. Çıktı `Builds/Steam/…` altına gider.
2. **Oyuncu ayarları:** Build script şunları otomatik ayarlıyor:
   - Borderless fullscreen, pencere yeniden boyutlanabilir, tek kopya.
   - Linear renk uzayı, Mono backend, Unity logosu kapalı (lisansın izin veriyorsa).
3. **Geliştirme build'i:** `steam_appid.txt` dosyasını ekler; yükleme scriptleri bu dosyayı hariç tutar.
4. **Yükleme:** `Tools/Steam/README.md` → `app_build.vdf` / `depot_*.vdf` dosyalarında App ve Depot ID'leri doldur, sonra `steamcmd +run_app_build` ile yükle.
5. **Yayına alma:** Build'i önce `beta` branch'inde test et, sonra `default`'a al.

## 7. Mağaza sayfası [SEN]

Boyutları Steamworks'teki güncel şablonlardan doğrula; Valve zaman zaman değiştiriyor.

| Varlık | Boyut |
|---|---|
| Header capsule | 920×430 |
| Small capsule | 462×174 |
| Main capsule | 1232×706 |
| Vertical capsule | 748×896 |
| Library capsule | 600×900 |
| Library hero | 3840×1240 |
| Library logo | 1280×720, şeffaf PNG |
| Ekran görüntüsü | En az 5 adet, 1920×1080 |
| Trailer | 30–60 sn, ilk 5 saniyede oyun görünsün |

Diğer maddeler:
- **Açıklama:** kısa + uzun, EN ve TR.
- **Etiketler:** Casual, Party, Local Co-Op, Sports, Skateboarding, Physics, Funny, Colorful.
- **Diller:** English + Turkish (Arayüz: tam, Ses: yok).
- **Yaş derecelendirmesi:** Steamworks içindeki IARC anketi (şiddet yok → düşük).
- **Fiyat ve bölgeler:** Önerilen fiyatları kontrol et. Parti oyunları genelde 5–15 USD aralığında.
- **Gizlilik:** Oyun kişisel veri toplamıyor. Sadece yerel dosya ve Steam istatistikleri.

## 8. Çıkış öncesi oyun eksikleri (öncelik sırasıyla)

1. **Ses ve müzik** (M5): tekerlek, rüzgâr, nitro, coin, combo, düşme; yüksek enerjili müzik. En büyük eksik bu.
2. **"Nasıl oynanır" ekranı:** "Tahtayı yönlendirmek için olduğun yerde dur" fikri ilk 30 saniyede anlaşılmalı.
3. **Gerçek Unity ekran görüntüleri ve trailer:** şu an sadece headless önizleme var.
4. **Oyun testi:** 60 fps ölçümü, Deck'te test, Remote Play Together'da 4+ kişi.
5. **Görsel içerik:** Blender ile gerçek karakter ve araç modelleri (liste: `Docs/M4_REPORT.md` §7).
6. **Credits ekranı** ve üçüncü taraf lisansları (`Docs/THIRD_PARTY_NOTICES.md`, build'e kopyalanıyor).

## 9. Yükleme öncesi kontrol listesi

- [ ] `SteamSettings.AppId` = senin App ID'n (480 değil)
- [ ] Release build (Development değil), içinde `steam_appid.txt` yok
- [ ] Steam'den başlatınca Console'da `platform = Steam` görünüyor, en az bir başarım açılıyor
- [ ] Leaderboard'a skor düşüyor
- [ ] Steam Cloud: başka bilgisayarda rekor ve ayarlar geliyor
- [ ] Sadece gamepad ile başlat → lobi → oyun → duraklat → ayarlar → çıkış yapılabiliyor
- [ ] Steam overlay (Shift+Tab) oyunu duraklatıyor
- [ ] Deck'te (veya 1280×800 pencerede) yazılar okunuyor
- [ ] Remote Play Together ile bir arkadaş lobide katılabiliyor
- [ ] Türkçe ve İngilizce dil seçimi çalışıyor
