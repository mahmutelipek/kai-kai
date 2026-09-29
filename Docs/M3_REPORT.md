# Milestone 3 Raporu — Pickups, Scoring, Combo (+ M2 düzeltmeleri ve oyun hissi)

Tarih: 2026-09-29 · Tag: `m3-done` (lokalde; bu ortamda tag push'u 403 ile reddediliyor)

## 1. Ne yapıldı

**A. M2'den kalan sorunların düzeltilmesi**
- **Botların yol takibi:** cooperative botlar ve ideal sürücü artık aynı "tahminli" kontrolcüyü kullanıyor. Kontrolcü, board'un ~1 s tepki gecikmesinden sonraki pozuna bakıp planlı çizgiyi izliyor. Ayrıca yol yeni bir `BrakeHint` üretiyor: sıkı viraj veya zikzak öncesinde ne kadar yavaşlamak gerektiğini söylüyor. Cooperative botlar bu durumda yığılmadan board'un arka yarısına geçiyor.
- **Karışık botların kişilikleri:** yol farkındalıkları yeniden ayarlandı; hâlâ zamanın ~%25'inde direksiyon için kavga ediyorlar.
- **Rampa yanı:** zemin artık 4 tekerlekten örnekleniyor. Tek tarafı rampaya çıkan board o tarafa yatıyor (görsel eğim, tehlikeye sayılmıyor).
- **Crash sonrası hız:** board önceki cruise hızının %60'ıyla devam ediyor (`respawnSpeedFraction`), 8 m/s'e düşmüyor.

**B. Oyun hissi** (sadece görsel/kamera; `BoardTuning` değişmedi)
- **Kamera:** hızla geriye çekiliyor ve FOV açıyor, nitro'da FOV +8. Viraja önceden bakıyor, sert inişte aşağı dalıyor, çarpmada geri tepiyor, board wobble yaparken titriyor.
- **Hit-stop ve slow-motion:** ağır çarpmada ~70 ms duraklama, crash'te 0.8 s ağır çekim.
- **Speed lines:** yüksek hızda ekran kenarlarında çizgiler.
- **Board:** inişte squash-and-stretch.
- **Karakterler:** zıplarken uzuyorlar, temiz inişte kollarını kaldırıp seviniyorlar.

**C. Milestone 3 (simülasyonda, headless test edilebilir)**
- **Coin, diamond, nitro:**
  - Coin sıraları güvenli planlı çizgi üzerinde.
  - Diamond riskli yerlerde: bariyerin hemen yanında, sert virajın dış kenarında, duvar veya molozun dibinde, ya da rampa inişinin üstünde havada (sadece zıplayarak alınabiliyor).
  - Nitro yaklaşık her km'de bir. Toplanınca saklanıyor (en fazla 2), herhangi bir oyuncu **E / gamepad X** ile ateşliyor: 3 s boyunca +12 m/s ve %35 daha az stabilite.
- **Pickup yerleşiminin ayrı rastgelelik akışı var.** Pickup ayarı değişince aynı seed'in yolu değişmiyor. Bu, testlerde gerçek bir hataya yol açmıştı.
- **`ScoreManager`:** puan = (mesafe + coin + diamond + near miss + havada kalma süresi) × o anki combo çarpanı.
  - Combo artışı: coin +1, diamond +3, near miss +2, temiz iniş +2, zor bölümü atlatma +3, nitro alma +1.
  - Çarpan: her 10 combo'da +1, en fazla ×6.
  - 4 s olay olmazsa combo saniyede 2 azalıyor. Hafif çarpma combo'yu yarıya indiriyor. Ağır çarpma, oyuncu düşmesi veya crash sıfırlıyor.
- **Near miss:** katı bir engel veya aracın yanından 1.2 m'den yakın ve ≥10 m/s hızla, değmeden geçmek. Her engel için bir kez sayılıyor.
- **Temiz iniş:** board dengeli iniyor (wobble bölgesinin altında), kimse düşmüyor, crash yok. Sert iniş herkesi sendeletiyor ama inişi bozmuyor. Önceki tanımda normal hızdaki her rampa iniş "sert" sayıldığı için rampalar combo veremiyordu; tasarım hatası düzeltildi.
- **Can ve koşu sonu:** 3 can (`livesPerRun`, 0 = sonsuz antrenman). Crash'ler, sebebi ne olursa olsun, tek bir yerden sayılıyor. Canlar bitince koşu bitiyor ve rekor kaydediliyor.
- **`HighScoreManager` + `FileHighScoreStore`:** en iyi skor ve en iyi mesafe bir dosyada tutuluyor (atomik yazma, dil ayarından bağımsız). Unity'de dosya `persistentDataPath/highscores.txt`.
- **Unity:**
  - `PickupViews`: dönen coin, mor diamond, mavi nitro şişesi; toplanınca patlama animasyonu.
  - `HUDController`, referans düzeninde: sol üst mesafe + BEST + can, üst orta skor, sağ üst coin/diamond, sağda COMBO ×N ve NITRO READY!, sağ alt km/h ve segmentli çubuk, sol alt P1–P6 rozetleri ve "YOU" işareti.
  - Olay popup'ları: NEAR MISS, CLEAN LANDING, SECTION CLEARED, DIAMOND, NITRO, OUCH, WIPEOUT.
  - Koşu sonu ekranı: puan dökümü, en iyi skor / YENİ REKOR, R / Space / (A) ile yeniden başlama.
  - Greedy bot, yol düzken nitro ateşleyebiliyor.

## 2. Test edilenler (headless, **57/57 geçti**)

| # | Kabul kriteri | Sonuç |
|---|---|---|
| 1 | Puan ve combo matematiği için birim testler (tüm artış ve sıfırlanma durumları) | 6 test: olay başına baz puanlar ve tam metre hesabı; olay tipine göre combo artışı; ×1 → ×2 geçişi ve çarpanın mesafeye uygulanması; üst sınır; boşta azalma (3.8 s'de kayıp yok, sonra ~2/s, en iyi combo korunuyor); hafif çarpma yarıya indiriyor (9 → 4); ağır çarpma / düşme / crash sıfırlıyor ama puan geri alınmıyor. 400 rastgele olayda döküm toplamı skora birebir eşit (25.681 = 25.681) |
| 2 | Near miss ve havada kalma senaryolarla test | Near miss: 0.4 m @25 m/s → **1** (bir kez); 1.7 m → 0; yavaş → 0; temas eden (çarpma) → 0; park etmiş araç 0.6 m → 1; koni → 0. Havada kalma: M1 rampası @22 m/s ölçülen **0.517 s = raporlanan 0.517 s**, temiz iniş, combo +2. 3 m'lik düşüş: sert iniş ve 1.0 s havada kalma; puan alıyor, combo vermiyor. Temiz iniş kuralının 4 durumu ayrıca test edildi |
| 3 | Nitro, `BoardTuning` değerlerine göre hızı ölçülebilir şekilde artırıyor ve stabiliteyi düşürüyor | 20 m/s'den 3 s: **19.8 → 32.0 m/s (+12.3)**, tuning +12. Aynı ağırlıkta tehlike oranı **1.350**, tuning 1.35. Süre bitince kapanıyor. Pickup → Action tuşu → nitro zinciri uçtan uca test edildi |
| 4 | Rekor, restart ve uygulama yeniden açılışından sonra korunuyor | Dosya deposu 3 ayrı "açılış" (yeni instance) boyunca doğru kaldı; kötü bir koşu rekoru değiştirmiyor; skor ve mesafe bağımsız tutuluyor. Son can bitince koşu bitiyor → rekor kaydediliyor → `Reset` sonrasında ve yeni bir instance'ta (relaunch) aynı değer okunuyor |
| + | Yerleşim | 10 km: 479 coin (hepsi güvenli çizgide), 13 diamond (hiçbiri güvenli çizgide değil), 10 nitro |
| + | Endless ekipler | 5 km × 3 seed: solo ve 6 cooperative **0 crash**, en fazla 2.5 m sapma. Karışık ekip her seed'de bitiriyor, ≤1 crash |
| + | İdeal sürücü (M2 testi, yeniden) | 5 seed × 10 km, 0 crash (pickup'lar yolu değiştirmediği için aynı yollar) |
| + | Allocation (M2 testi, skor dahil) | 20.000 adımda 0 byte |
| + | Örnek koşu (ideal sürücü, 5 km) | ~35–47 bin puan, 200+ coin, 2–5 diamond, 2–4 near miss, 3–6 temiz iniş |

Unity scriptleri Unity 2021.3 referanslarına karşı 0 hata / 0 uyarı ile derleniyor.

## 3. Test EDİLEMEYENLER
- **Unity içinde hiçbir şey çalıştırılmadı.** HUD, bitiş ekranı, pickup görselleri, oyun hissi efektleri, kamera ve 8 PlayMode testi (M3 için eklenen koşu sonu testi dahil) hiç koşmadı.
- Oyun hissi değişiklikleri tamamen görsel. İyi hissettirip hissettirmediği ancak oynayarak anlaşılır.
- IMGUI HUD yazı tipi ve boyutları gerçek ekranda görülmedi.
- Unity 6'da derleme; kontrol yine 2021.3 referanslarıyla yapıldı.

## 4. Bilinen sorunlar
- HUD şimdilik IMGUI placeholder; M4'te gerçek UI'ya geçecek.
- Test PlayMode'da çalıştırılırsa, koşu sonu testi gerçek `highscores.txt` dosyasına yazabilir.
- Combo dengesi (her 10'da ×1, üst sınır ×6) ve 3 can oynanarak ayarlanmalı.
- Coin sıraları M1 test pistinde de üretiliyor.

## 5. Tuning (yeni)
- Nitro: süre 3 s, hız +12 m/s, ivme 10 m/s², instability 1.35.
- Puan: metre 1, coin 10, diamond 100, near miss 50, havada kalma 100/s.
- Combo: çarpan için her 10'da +1, en fazla ×6, 4 s boşta beklemeden sonra saniyede 2 azalma.
- Near miss: 1.2 m ve ≥10 m/s. Puanlanan en kısa havada kalma 0.35 s. 3 can. `respawnSpeedFraction` 0.6.

## 6. Sonraki adımlar
1. Unity'de oynayın; özellikle nitro, combo hızı ve HUD okunabilirliği için geri bildirim verin.
2. Onay gelirse Milestone 4: stilize görsel pass. Blender yok, dolayısıyla primitive/prosedürel asset'ler ve eksik asset listesi.
