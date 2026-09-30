# M4.2 Raporu — Steam'e hazırlık

Tarih: 2026-09-30 · İstek: "Steam'de yayınlayacağım, ona göre"

Adım adım yayın rehberi: **`Docs/STEAM_RELEASE.md`**. Ekran görüntüleri (headless önizleme, Unity'nin çizeceği aynı komutlar): `Docs/M4/menu_*.jpg`, `Docs/M4/hud_curve_tr.jpg`.

## 1. Ne yapıldı

**Oyun akışı (Steam oyunu gibi)**
- **Başlık ekranı:** Arkada botların sürdüğü canlı bir sürüş (attract mode) dönüyor. Menü: OYNA / AYARLAR / ÇIKIŞ.
- **Lobi ("KİMLER BİNİYOR?"):**
  - Her gamepad **(A)** ile kendi oyuncusunu alıyor, **(B)** ile bırakıyor.
  - Klavyede **Boşluk** 1. oyuncuyu (WASD) alıyor, **Enter** 2. oyuncuyu (oklar). Tek klavyede iki kişi oynanabiliyor.
  - Ekip boyutu seçilebiliyor; boş yerleri isteğe bağlı botlar dolduruyor.
  - Steam **Remote Play Together** ile uzaktaki arkadaşlar gamepad'leriyle aynı şekilde katılabiliyor.
- **Oyun ve duraklatma:**
  - 3-2-1-BAŞLA! ile başlıyor.
  - **Esc / Start** ile duraklatma menüsü: Devam, Yeniden başla, Ayarlar, Ana menü, Çıkış.
  - Pencere odağı kaybolunca (alt-tab, Steam overlay) ya da kontrolcü kopunca oyun kendiliğinden duruyor.
  - Duraklatılınca geri sayım da bekliyor.
- **Ayarlar:**
  - Ekran modu (tam ekran / çerçevesiz / pencere), çözünürlük, dikey senkron, kalite (düşük/orta/yüksek).
  - Hareketi azalt; dil (English / **Türkçe**); ana / müzik / efekt sesi.
  - `settings.txt` dosyasına kaydediliyor; Steam Cloud ile senkronlanabilir. İlk açılışta dil sistem dilinden seçiliyor.
- **Menü navigasyonu:** Tüm menüler klavye ve gamepad ile geziliyor (yön tuşları basılı tutulunca tekrar ediyor). Her ekran açıldığında bir öğe seçili geliyor. Bu, Steam Deck ve Big Picture için şart.
- **Geliştirici tuşları:** (Tab, B, C, 1–6, M, F1, F2) sadece editörde ve development build'de çalışıyor. Yayın build'inde oyuncu her şeyi menü ve lobiden yapıyor.

**Steam entegrasyonu (`Assets/Scripts/Steam`)**
- **Özellikler:**
  - 14 başarım: API adları ve metinleri `STEAM_RELEASE.md`'de, siteye birebir girilecek.
  - `STAT_TOTAL_METRES` istatistiği.
  - `best_distance` / `best_score` leaderboard'ları (hep en iyi skoru tutuyor).
  - Rich presence ("menüde", "N oyuncuyla sürüyor").
- **Kurulum:** Steamworks.NET paketi kurulunca kendiliğinden derleniyor. Paket yoksa ya da Steam kapalıysa oyun `NullPlatform` ile sorunsuz çalışıyor.
- **Başarım kuralı:** Başarımlar yalnızca en az bir **insan** oyuncu varken açılıyor; başlıktaki bot sürüşü sayılmıyor.
- **App ID:** `SteamSettings.AppId`. Şu an 480 (Valve test uygulaması).

**Build ve yükleme**
- **Build menüsü:** `Downhill → Build → Windows x64 (Steam) / Windows dev (steam_appid.txt) / Linux x64 (Steam Deck) / macOS`.
- **Otomatik ayarlanan oyuncu ayarları:** Borderless fullscreen, pencere yeniden boyutlanabilir, tek kopya, Linear renk uzayı, Mono backend, sürüm 0.5.0.
- **Yükleme:** `Tools/Steam/` altında SteamPipe `app_build.vdf` ve `depot_*.vdf` şablonları. `steam_appid.txt` ve debug dosyaları yüklemeden hariç tutuluyor.

**Görünüm ve lisanslar**
- **Font:** "Luckiest Guy" (Apache-2.0) projeye gömüldü. Referanstaki çizgi film fontuna çok yakın, Türkçe karakterleri tam destekliyor. Artık her bilgisayarda (Windows, Linux, Deck) aynı görünüyor.
- **Lisanslar:** `Docs/THIRD_PARTY_NOTICES.md` her build'e kopyalanıyor.

## 2. Test edilenler

**Headless: 74/74 geçti.** Yeniler:
- `FrontendTests`: 6 test.
- `HudLayoutTests`: Steam Deck ekranı eklendi.

Unity derleme kontrolü Runtime + Editor + Tests + **Steam** için 0 hata, 0 uyarı. Steam kodu NuGet'teki gerçek Steamworks.NET 2024.8.0 API'sine karşı derlendi.

| Test | Sonuç |
|---|---|
| Ayar dosyası kaydedilip geri okunuyor; bozuk değerlerde varsayılana dönüyor | Geçti |
| Türkçe çeviri: menüler, HUD, popup'lar ve bitiş ekranındaki tüm metinler | Eksik 0 |
| Menü navigasyonu: odak döngüsü; oyuncu yokken BAŞLAT çalışmıyor; Ayarlar'dan geri Duraklat'a dönüyor; Duraklat'ta geri = devam; dil değişikliği bildiriliyor | Geçti |
| Lobi: aynı cihaz iki kez katılamıyor, en fazla 6 kişi, botlar ekibi dolduruyor | Geçti |
| Başarımlar, ideal sürücüyle 3.2 km'lik gerçek koşu | Full Crew, Combo King, Downhill Rookie, Coin Hoarder, Unstoppable açıldı. Her biri bir kez; Wipeout açılmadı (crash yok) |
| Menüler 5 ekran boyutunda ekran içinde; Steam Deck'te en küçük yazı | Başlık 20.7 px, Lobi 19.3 px, Duraklat 37 px, Ayarlar 28.1 px (Valve alt sınırı 9 px) |
| HUD 7 ekran tipinde (Steam Deck 1280×800 dahil) güvenli alanda, yazılar çakışmıyor | Geçti |

PlayMode'a yeni test eklendi: `Frontend_TitleLobbyCountdownPauseResume`. Başlık → lobi → geri sayım → duraklat → devam akışını kontrol ediyor. **Unity'de koşmadı.**

## 3. Test EDİLEMEYENLER
- **Unity ve Steam:** Unity'de hiçbir şey koşmadı: menüler, lobi, gamepad ile katılma, çözünürlük ve pencere değişimi, kalite seviyeleri. Steam'in kendisi (başarım popup'ı, leaderboard, overlay, Cloud, Remote Play Together) test edilemedi; bunlar gerçek Steam istemcisi ister.
- **Steamworks.NET:** Unity paketi kurulmadı. Kod NuGet sürümüne karşı derlendi; Unity paketi aynı API'yi sunuyor.
- **Steam Deck:** Gerçek cihazda denenmedi; sadece 1280×800 ekran yerleşimi test edildi.

## 4. Bilinen sorunlar ve kararlar
- **Steamworks.NET manifest'te değil:** Git URL ile kurulan bir paket, bilgisayarında Git yoksa Unity'de hata verir. Bu yüzden `manifest.json`'a eklemedim; kurulum 2 adım ve `STEAM_RELEASE.md` §2'de. İstersen ekleyeyim.
- **Settings ekranı:** Ses ayarları şimdilik sadece ana ses seviyesini etkiliyor. Müzik ve efekt sesi M5'teki ses sistemiyle bağlanacak.
- **Kalite seviyeleri:** Kalite ayarı projedeki Unity kalite seviyelerini düşük/orta/yüksek olarak eşliyor. Seviyelerin içeriği (gölge mesafesi vb.) Unity'de ayarlanmalı.
- **Menü allocation:** Menüler çizilirken küçük string'ler üretiliyor. Sadece menü açıkken; oyun sırasında HUD hâlâ 0 allocation.
- **PlayMode testi:** Yeni test `persistentDataPath/settings.txt` dosyasına yazabilir.

## 5. Çıkış öncesi kalan en önemli işler
1. **Ses ve müzik** (M5): en büyük eksik.
2. **Senin Steamworks adımların:** App ID, başarımlar ve ikonları, Cloud, Deck incelemesi, Remote Play Together, mağaza görselleri. Liste `STEAM_RELEASE.md`'de.
3. **Kısa "nasıl oynanır" ekranı**, credits ekranı.
4. **Gerçek Unity ekran görüntüleri ve trailer.**
5. **Karakter ve araç modelleri** (Blender veya sanatçı).

Online çok oyunculu için netcode yazmak yerine önce **Remote Play Together**'ı öneriyorum. Bu oyun türünde bedava ve güvenilir çalışıyor; gerçek netcode çıkıştan sonra düşünülebilir.
