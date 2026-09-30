# M4.1 Raporu — Referans UI, heyecan, oynanabilirlik

Tarih: 2026-09-30 · İstek: "devam, iyileştir, oynanabilirliği arttır, heyecanlı ve sürükleyici olsun, UI'ı referans görseldeki gibi yap"

Ekran görüntüleri: `Docs/M4/hud_*.jpg`, `Docs/M4/game_bridge.jpg`. Bunlar headless önizleme; Unity'nin kendi renderer'ı değil. HUD, Unity'nin çizeceği **aynı draw komutlarından** çizildi.

## 1. Ne yapıldı

**Referans UI**

`Game.Hud` motordan bağımsız: `HudPresenter` koşudan HUD durumunu, `HudLayout` draw komutlarını, `HudTextures` prosedürel dokuları üretiyor. Unity `HUDController` bu komutları IMGUI ile çiziyor.

| Referanstaki öğe | Bizde |
|---|---|
| Sol üst: "1,248 m" ve "BEST 12,630 m" | Yırtık fırça darbesi panellerde; BEST camgöbeği. Canlar kalp ikonu |
| Sağ üst: coin 137, elmas 6 | Koyu hapların üstünde altın coin ve mor elmas ikonu. Sayı artınca ikon ve sayı "pop" yapıyor |
| Sağ: "COMBO x3", "NITRO READY!" | Siyah eğik banner (COMBO beyaz, çarpan sarı), altında sonraki çarpana ilerleme çizgisi; camgöbeği eğik NITRO banner. Çarpan artınca banner zıplıyor, nitro yanarken nabız atıyor |
| Sağ alt: "68 KM/H" ve sarı bar | Büyük hız sayısı ve giderek yükselen 8 eğik segment. Nitroda camgöbeği, tehlikede kırmızı |
| Sol alt: P1–P6 kafaları ve oklar | Her karakterin prosedürel kafa portresi (kendi şapkası, saçı, ten rengi), renkli etiket ve ok. Senin oyuncunun etrafında beyaz halka; düşen oyuncu soluk |

Ek olarak:
- Ortada 3-2-1-GO! sayacı (overshoot ile).
- Popup'lar: NEAR MISS, CLEAN LANDING, DIAMOND, NITRO BOOST, WIPEOUT, "HOLD ON! BALANCE!".
- Tehlikede kırmızı, nitroda camgöbeği ekran kenarı.
- Aynı stilde bitiş ekranı.

Teknik detaylar:
- Yazı tipi: Unity'de sistemden "Arial Black" / "Impact" deneniyor, bulunamazsa varsayılan font.
- Safe area içinde kalıyor; 1080p referansla ölçekleniyor.

**Heyecan ve sürükleyicilik**
- **Başlangıç:** Her koşu 3-2-1-GO! ile başlıyor; board GO'ya kadar bekliyor (`GameManager → Start Countdown`).
- **Nitro:**
  - Board'un arkasında mavi-beyaz alev izi.
  - Kamera geriye atılıyor ve hafif sarsılıyor.
  - Ekran kenarı camgöbeği; FOV +8 zaten vardı.
- **İniş:** Tüm tekerleklerden inişin sertliğine göre toz bulutu.
- **Tehlike:** Wobble başlayınca "HOLD ON! BALANCE!" uyarısı ve nabız atan kırmızı ekran kenarı. Ekip board'un devrilmek üzere olduğunu görüyor.
- **Dünya:** Yolun körfez tarafında suya kadar inen kasaba: ~1000 pastel ev, kiremit çatılar, ağaçlar.

**Oynanabilirlik** (simülasyon değerleri)

| Değer | Önce | Şimdi | Etki |
|---|---|---|---|
| `PickupField.CollectRadius` | 0.9 m | 1.2 m | Coin'ler hafif "mıknatıs" gibi, kıl payı kaçanlar da sayılıyor |
| `nearMissDistance` | 1.2 m | 1.5 m | Daha çok near miss, daha çok combo |
| `comboIdleTime` | 4 s | 5 s | Combo daha geç sönüyor |

## 2. Test edilenler

**Headless: 68/68 geçti** (4 yeni `HudLayoutTests`). Unity derleme kontrolü 0 hata ve 0 uyarı.

| Test | Sonuç |
|---|---|
| HUD 6 ekran tipinde safe area içinde, yazılar çakışmıyor (1080p, 720p, 4:3, 21:9, çentikli telefon, 4K) | Geçti: 54 komut, ölçek 0.67–2.0 |
| HUD layout her karede allocation yapmıyor | 100 çağrıda 0 byte |
| Presenter string'leri yalnızca değer değişince üretiyor; coin gelince sayaç pop'luyor | 50 karede 0 byte; pop > 0.9 |
| Tüm HUD dokuları üretiliyor, boş değil | Geçti |
| Tuning kilidi | Güncellendi: M3'e göre değişen 13 değer (senin istediklerin), kalan 70 alan birebir aynı (`git diff` ile doğrulandı) |
| Combo sönme testi | Önceden 4 s sabit kodluydu; artık tuning'den okuyor. Geçti |

Draw call tahmini (HUD dahil, culling yok):
- Toplam 245–276.
- IMGUI HUD bunun ~114'ü: kenar çizgili her yazı 5 çağrı. Dış çizgiyi 8 kopyadan 4'e indirdim.

## 3. Test EDİLEMEYENLER
- **Unity'de hiçbir şey koşmadı:** IMGUI'nin eğme/döndürme matrisi, font (Arial Black bulunacak mı), sayaç, parçacıklar, kamera tepkisi.
- **Farklı font:** Önizlemede Arial Black yok, DejaVu Sans Bold kullanıldı. Unity'de yazı genişlikleri biraz farklı olabilir; COMBO / x2 arasında pay bıraktım.
- **Oyun hissi:** Heyecanın gerçekten artıp artmadığı ancak oynayarak anlaşılır.

## 4. Bilinen sorunlar
- **HUD maliyeti:** IMGUI HUD ~114 çağrı. Mobil hedeflenirse uGUI veya atlas'lı tek mesh'e geçmek gerekir.
- **Doku üretimi:** HUD dokuları açılışta üretiliyor (headless'ta ~0.9 s). Oyun sırasında takılma olmasın diye başlangıçta hepsi önceden hazırlanıyor.
- **COMBO x1:** Combo > 0 olunca x1'de de görünüyor (ilerleme çizgisi için). İstersen sadece x2 ve üstünde göstereyim.
- **Coin yarıçapı:** Coin toplama yarıçapı büyüdü, tuning dışında sabit.

## 5. Sonraki adımlar
1. Unity'de oyna. Sayaç, HUD yerleşimi ve font, nitro alevi, tehlike uyarısı hakkında geri bildirim ve Unity ekran görüntüsü gönder.
2. Onay gelirse Milestone 5: netcode multiplayer ve ses. Ses heyecan için büyük eksik: tekerlek uğultusu, rüzgâr, nitro, coin, combo.
