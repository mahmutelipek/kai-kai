# M4.4 Raporu — Final polish: mekanik düzeltmesi, öğretici, menüler

Tarih: 2026-09-30 · İstek: "UI'ı polishle, mekanikleri iyileştir, her şeyi finalize seviyede iyileştir." Ses işi bu turda yok.

Ekran görüntüleri (headless önizleme, Unity'nin çizeceği aynı draw komutları), hepsi `Docs/M4/` altında:
- `menu_title.jpg`: başlık ekranı
- `menu_howto.jpg`: nasıl oynanır
- `menu_credits.jpg`: emeği geçenler
- `hud_moves.jpg`: hareket göstergesi ve ipucu
- `hud_jump.jpg`: ekibin "JUMP!" çağrısı
- `hud_end.jpg`: bitiş ekranı ve NEW BEST damgası

## 1. En önemli düzeltme: ekip zıplaması botlarla çalışmıyordu
- **Sorun:** Ekip ollie'si için riderların %60'ı birlikte zıplamalı. Botlar ise sadece rastgele zıplıyordu, insanın zıplamasına hiç tepki vermiyordu. Bu yüzden oyunun en yaygın oynanış şekli olan **1 insan + 5 bot ile ollie hiç yapılamıyordu.**
- **Çözüm: "JUMP!" çağrısı.**
  - İnsan zıpladığında botlar 0.06–0.3 saniyelik insan benzeri bir tepki süresiyle katılıyor.
  - Her bot kişiliğinin katılma olasılığı farklı: iş birlikçi %97, inatçılar %70, diğerleri %80–90.
  - Ekip yolda çukur, enkaz ya da koni gördüğünde çağrıyı kendisi yapıyor ve zamanlaması hesaplanmış bir ollie ile üstünden atlıyor. İnsan oynarken ekranda büyük bir **JUMP!** çıkıyor; sen de katılırsan PERFECT olur.
- **Ölçüldü:** 1 insan + 5 karışık bot ile 35 zıplamanın 31'i ollie oldu, 8'i perfect.
  - Botlar katılmazsa ollie hiç olmuyor. Bu durum, düzeltilen hatanın kanıtı olarak testte duruyor.
  - Yoldaki koni testi: çağrı yoksa tahta koniye çarpıyor; çağrı varsa ekip zıplayıp temiz geçiyor.
- **Fizik notu:** Normal ollie yaklaşık 1.06 m yükseliyor ve koniyi aşıyor. 1 metrelik bir kasayı sadece **PERFECT ollie** (1.6 m) aşıyor, yani beceri ödüllendiriliyor.

## 2. HUD polish
- **Hareket göstergesi:** Hız göstergesinin üstünde eğik bir bar.
  - Viraj tutarken **CARVE** doluyor. Hazır olunca yanıp sönen **STRAIGHTEN!** yazıyor; iki kat uzun tutunca **MEGA! STRAIGHTEN!** oluyor.
  - Boost sırasında azalan bir **BOOST!** barı, bir arabanın arkasındayken **DRAFTING** barı gösteriliyor.
- **İlk oyun ipuçları:** Her mekanik için bir ipucu, ilk kez önemli olduğu anda veriliyor:
  - Direksiyon: GO'dan hemen sonra.
  - Birlikte zıplama: ilk engel çağrısında.
  - Carve: ilk viraj şarjında.
  - Nitro: ilk nitro alındığında.
  - Draft: bir süre sürdükten sonra.
  - İpuçları üst ortada sarı bir şerit olarak çıkıyor, en az 7 saniye arayla ve her biri **bir kez**. Görülenler ayarlarda saklanıyor (Steam Cloud ile senkronlanıyor). Sadece insan oynarken çıkıyor, başlık ekranındaki bot sürüşünde çıkmıyor.
  - Ayarlar'da **RIDING TIPS** ON/OFF var. Tekrar açınca ipuçları baştan gösteriliyor.
- **Bitiş ekranı:**
  - Kart büyüyerek açılıyor, satırlar kayarak tek tek geliyor.
  - Ardından skor, varsa kırmızı **NEW BEST!** damgası, en son yanıp sönen "tekrar sür" yazısı çıkıyor.
  - Puan getirmeyen (+0) satırlar soluk gösteriliyor.
  - Oval panel yerine köşeleri yumuşak bir kart kullanılıyor.

## 3. Menüler
- **Başlık menüsü:** PLAY, **HOW TO PLAY**, SETTINGS, **CREDITS**, QUIT.
- **Nasıl oynanır:** 6 kart: yönlendirme, ekip ollie, carve boost, draft, nitro, combo. Duraklatma menüsünden de açılıyor, yani oyun ortasında bakılabiliyor.
- **Credits:** Font lisansı (Apache 2.0) dahil. Stüdyo adını `FrontendContent.Credits` ve `BuildScript.companyName` içinde değiştirmen gerekiyor; şu an "DOWNHILL PARTY TEAM".
- Menüler 16:9, 21:9, 4:3, 4K ve Steam Deck'te güvenli alan ve çakışma testinden geçiyor.

## 4. Steam
- **4 yeni başarım:**
  - `ACH_OLLIE` Lift Off
  - `ACH_PERFECT_OLLIE` In Sync
  - `ACH_MEGA_CARVE` Slingshot
  - `ACH_DRAFT_5` Tailgater
- Toplam 18 başarım. `STEAM_RELEASE.md` tablosu güncellendi; Steamworks'e bu API adlarıyla girmen gerekiyor.

## 5. Test edilenler
**Headless: 99/99 geçti.** **Unity derleme kontrolü:** Runtime, Editor, Tests ve Steam için 0 hata, 0 uyarı.

Yeni testler:
- `CrewCallTests`: 4 test (insan + bot ollie oranı, botsuz kontrol, koni üstünden atlama, çağrı tekrarı ve bekleme süresi).
- HUD'a 3 test: gösterge, JUMP! ve ipucu 7 ekranda; bitiş ekranı animasyonu ve damga; ipucu mantığı.
- Menü gezinme testine: nasıl oynanır, ipucu ayarı ve başlık menüsü.

## 6. Test EDİLEMEYENLER
- **Unity'de hiçbir şey koşmadı.** Görüntüler, Unity'nin çizeceği aynı komutların headless önizlemesi.
- **Botların zıplama zamanlaması** simülasyonda ölçüldü, gerçek oyuncuyla denenmedi. Perfect oranı (~%23) ve tepki süreleri oynanarak ayarlanabilir; değerler `BotBrain` içinde, kişilik başına.
- **Engel çağrısı** koni, çukur ve enkaz için var. Arabalar ve bariyerler için bilerek yok; onlardan direksiyonla kaçılıyor.

## 7. Sıradaki öneriler
1. Unity'de oynayıp bot tepki süreleri, ipucu metinleri ve bitiş ekranı hızı hakkında görüş ver.
2. Credits'e gerçek stüdyo ve kişi adları.
3. Steamworks'e 18 başarımı ve ikonlarını gir.
