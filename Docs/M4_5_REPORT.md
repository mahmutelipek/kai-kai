# M4.5 Raporu — Daha büyük kaykay, high score ekranı

Tarih: 2026-09-30

İstekler:
- "Kaykay daha büyük olmalı, karakterlere göre küçük kalmış."
- "Game over high score UI'ı yok."

## 1. Kaykay daha büyük (`Docs/M4/board_size_before_after.jpg`)
- **Değişiklik:**
  - Kaykay 6 × 2.4 m'den 6.75 × 2.7 m'ye çıktı (%12.5). Hem fizik hem görünüm değişti; çarpışmalar gördüğünle aynı.
  - Riderlar görsel olarak ×0.88 ölçekle çiziliyor. Fizik boyutları aynı, sadece görünüm küçüldü.
  - Sonuç: riderlara göre kaykay yaklaşık %28 daha büyük görünüyor.
- **Neden %25 büyütmedim** (denedim, ölçtüm):
  - 7.5 × 3 m'lik kaykayda iş birlikçi botlar bariyerli ve bozuk yol bölümlerinde çarpmaya başladı. Kaykay büyüyünce oyun zorlaşıyor.
  - Rider yürüme hızını %25 artırarak telafi etmeyi denedim; bu kez botlar savruldu ve daha çok düştü. Geri aldım.
  - Seçtiğim çözüm, görünüm farkının çoğunu risksiz yoldan (rider ölçeği) almak.
- **Yol üretimi büyük kaykaya uyarlandı:**
  - Yol planlayıcısı, kaykayın fazladan genişliği kadar ekstra boşluk bırakıyor.
  - Bariyerli bölümlerdeki zikzak, kaykay genişliğine göre ayarlanıyor.
  - Sonuç: test tohumlarında iş birlikçi ekip 0 kaza yapıyor.
  - Ek 10 koşunun birinde (31337 tohumu, tek bot) bozuk yolda 1 kaza var. Eski kaykayda aynı yerde kaza yoktu. Gerçek oyunda ekip bu enkazın üstünden JUMP! çağrısıyla atlıyor, ama not olarak bırakıyorum.
- **Ciddi bir eski hata düzeltildi: tuning asset'i hiç güncellenmiyordu.**
  - Unity projede `Assets/Settings/BoardTuning.asset` ilk açılışta oluşturuluyor ve eski değerleri tutuyor.
  - Bu yüzden M4'ten beri koddaki varsayılan değişiklikleri (daha hızlı başlangıç, daha çevik direksiyon, şimdi de büyük kaykay) büyük ihtimalle senin projene hiç ulaşmadı.
  - Artık asset yüklenirken, hâlâ eski bir varsayılanda duran alanlar otomatik olarak güncelleniyor. Eski değerler git geçmişinden alındı: 15 alan.
  - Elle değiştirdiğin değerlere dokunulmuyor. Editör güncellenen asset'i kaydediyor.

## 2. High score ekranı (`Docs/M4/hud_end.jpg`, `menu_highscores.jpg`)
- **Top 10 tablosu:** skor, mesafe, ekip boyutu ve tarih tutuluyor. Eski kayıt dosyası sorunsuz okunuyor; eski rekor tablonun ilk satırı oluyor. Bozuk satırlar atlanıyor.
- **Game over ekranı:**
  - Solda koşu özeti, sağda **TOP SCORES** kartı var. Satırlar tek tek kayarak geliyor.
  - Yeni skorun satırı sarı ve yanıp sönüyor, üstünde kırmızı **NEW** etiketi var.
  - Özetin altında **RANK #3** gibi sıran yazıyor. Rekor kırılırsa NEW BEST! damgası çıkıyor.
- **Başlık menüsü:** **HIGH SCORES** ekranı eklendi (aynı tablo).
- **Kontrol:** 7 ekran tipinde (4:3, 21:9, Steam Deck dahil) güvenli alan ve çakışma testinden geçiyor. Önizlemede rank ile skor sütunlarının çakıştığını test yakaladı; düzeltildi.
- Tablo, Steam'deki küresel leaderboard'a ek olarak bilgisayardaki yerel tablo.

## 3. Test
- **Headless: 103/103 geçti.**
  - Yeni testler: `HighScoreTableTests` (3 test), tuning göçü, bitiş ekranı tablosu.
  - Güncellenenler: menü testleri, tuning sabitleme testi.
- **Unity derleme kontrolü:** 0 hata, 0 uyarı.
- **Unity'de koşmadı.** Ekran görüntüleri headless önizleme. Önizlemedeki tablo örnek veri.

## 4. Ek tur ("top score podyum gibi olmalı; kaykay hâlâ küçük, karakterler büyük")
- **Podyum:**
  - İlk üç skor podyumda duruyor: ortada en yüksek altın #1, solda gümüş #2, sağda bronz #3.
  - Bloklar sırayla yükseliyor. Üstlerinde madalya, skor, mesafe ve ekip bilgisi var.
  - 4–10 altta liste halinde. Yeni skor podyumdaysa bloğu nabız gibi atıyor ve NEW etiketi taşıyor; listedeyse satırı sarı yanıyor.
  - Game over ekranında ve HIGH SCORES menüsünde aynı görünüm kullanılıyor.
- **Karakterler:** Görsel ölçek 0.88'den **0.72**'ye düştü; sadece görünüm değişti, fizik aynı. Kaykay artık riderlara göre yaklaşık 1.56 kat büyük görünüyor. Karşılaştırma: `Docs/M4/board_size_before_after.jpg`.
- **Test:** Headless 103/103 geçti, derleme kontrolü 0 hata ve 0 uyarı. Unity'de koşmadı.

## 5. Ek tur ("zıplamalar kaykayı hafif etkilesin; kaykay hâlâ küçük")
- **Zıplama tepkisi (DeckFlex):**
  - Rider zıplarken ayağının altındaki güverte hafifçe iner, inişte tekrar çöker.
  - Kenarda zıplayan rider kaykayı o tarafa, burunda zıplayan öne yatırıyor.
  - Ölçüldü: sol ön köşeden bir zıplama ~5 cm iniş, ~2.6° yan, ~0.8° öne yatma. İki küçük sekmeyle ~1 saniyede duruluyor.
  - Riderlar güverteyle birlikte sallanıyor. Sadece görsel; direksiyon ve fizik değişmedi.
- **Oranlar:** Rider görsel ölçeği 0.72 → **0.60**. Kamera biraz yaklaştı (mesafe 5.4 → 4.7 m, yükseklik 2.9 → 2.6 m), kaykay ekranda belirgin şekilde daha büyük.
- **Test:** Headless 104/104 (yeni `DeckFlexTests`), derleme kontrolü 0 hata / 0 uyarı. Unity'de koşmadı.
