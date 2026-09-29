# Milestone 2 Raporu — Endless Procedural Road

Tarih: 2026-09-29 · Commit tag'i: `m2-done` (lokalde oluşturuldu; bu ortam tag push'unu reddediyor, 403)

## 1. Ne yapıldı

**Simülasyon (Unity'den bağımsız, headless test edilebilir):**
- `RoadGenerator` yolu `RoadChunkLibrary`'deki 14 chunk tipinden kuruyor: düz, hafif sol/sağ viraj, sert sol/sağ viraj, S-viraj, dar bölüm (duvarlı), inşaat alanı, köprü (bankette zemin yok, düşen crash olur), tünel (duvar + tavan), rampa, bozuk yol (çukur + moloz), trafik (aynı yönde ve ileride karşıdan gelen araçlar), yokuş aşağı kavşak (boşluk bekleyen çapraz trafik), kısmi bariyerler (zig-zag), çatal (orta ayırıcı).
- Her chunk'ın giriş/çıkış soketi, genişlik profili, kenar tipleri (çim / duvar / boşluk) ve metadata'sı var (zorluk 1–10, en erken çıkabileceği mesafe).
- Yol board'un 450 m önünde üretiliyor, 150 m arkada geri dönüştürülüyor. Havuz baştan 10 chunk ile dolu, oyun sırasında yeni nesne oluşturulmuyor.
- `RoadPlanner` her chunk'ın geçilebilir olduğunu ispatlıyor. Board merkezi için bir çizgi arıyor: asfalt, eksi board boyu kadar şişirilmiş engeller ve trafik şeritleri, yanal değişim metre başına ≤ 0.06 m. Rastgele yerleşim yolu kapatırsa kapatan engel siliniyor. Cooperative botlar ve ideal sürücü bu çizgiyi takip ediyor.
- `DifficultyManager` 8 km'de 0'dan 1'e çıkıyor. Hem hızı hem karar karmaşıklığını artırıyor: hedef chunk zorluğu 1.5 → 8, yol 14 → 10 m, engel yoğunluğu, trafik hızı, karşıdan gelen trafik (> 0.55), zincirlenmiş tehlikeler (> 0.65, ör. rampadan hemen sert viraja), hız tavanı +%25.
- `ObstacleField` sabit havuzda 512 engel tutuyor. Trafiği hareket ettiriyor (araçlar board yaklaşınca kalkıyor), çapraz trafik boşluk bekliyor. Board-engel çarpışması 2D döndürülmüş kutu (SAT) ve yükseklik ile hesaplanıyor; havadaki board alçak engelin üstünden geçer.
- Çarpışma kuralları:
  - **Hafif** (koni, inşaat bariyeri, sandık): engel savrulur, −%8 hız.
  - **Tümsek** (çukur): −%6 hız, herkes sendeler.
  - **Ağır** (beton bariyer, araç, ayırıcı, moloz): board sekip yön değiştirir, −%30 hız, sendeleme, çarpma tarafındaki oyuncular düşer.
  - **Crash:** ağır engele 16 m/s üstünde kapanma hızıyla çarpmak. Duvara yandan 11 m/s üstünde çarpmak da crash.
- `RunSimulation` tek otoriter koşu: yol + engeller + zorluk + board. M5'te host bunu sahiplenecek.
- M1 test pisti artık aynı sistemin sabit chunk dizisi (M tuşu ile açılıyor).

**Unity tarafı:**
- `BoardController` artık `RunSimulation`'ı barındırıyor. Zemin ve çarpışmalar simülasyondan geliyor; Unity fiziği ve raycast kullanılmıyor.
- `RoadView` / `ChunkView`: her chunk için havuzlu mesh. Asfalt, çim banket, çizgiler, tünel duvarı ve tavanı, portal, köprü tabliyesi, korkuluk ve ayakları, kavşak yolu ve yaya geçidi, rampa ve şeritleri. `MeshWriter` mesh'leri yeniden kullanarak allocation'sız yeniden kuruyor.
- `ObstacleViews`: her engel slotu için havuzlu primitive model. Her engel tipinin okunaklı bir silüeti var. Savrulan engeller uçarak gidiyor.
- M tuşu, debug overlay'de chunk adı / zorluk / seed, `GameManager`'da mod ve seed ayarı.

**Bu milestone'daki mekanik düzeltmeler (M1 değerlerini etkiliyor):**
1. **Arka ağırlıkla fren** yüksek hızda neredeyse etkisizdi. Sabit "yokuş çekişi" freni yiyordu: 30 m/s'de net sadece −0.6 m/s². Artık arka ağırlık hem frenliyor hem çekişi bastırıyor. M1 A3 testi: 20 m/s'den 5 s arka ağırlıkla 8.66 → 3.0 m/s.
2. **Botların yol takibi** yüksek hızda salınıma giriyordu (±7 m). M1'de 0.55 tehlike sınırı bunu gizliyordu. Yol ipucunun ileri bakış mesafesi artık hız × sistem gecikmesini aşıyor (12 + 1.1 × v). Sonuç: cooperative ekip M1 pistinde en fazla 1.4 m sapıyor, hiç offroad'a çıkmıyor, 34.9 m/s'e ulaşıyor.
3. M1 pistinin virajları yeni chunk geometrisinde (yumuşak giriş/çıkış) aynı tepe yarıçapını (~110 m) korumak için 110 m'den 210 m'ye uzatıldı. Pist toplamı 2.54 km.

## 2. Test edilenler ve nasıl (sayılarla)

Headless: `Tools/Headless/Tests` → `dotnet test`, **38/38 geçti**. Aynı dosyalar Unity Test Runner'da da derleniyor.

| # | Kabul kriteri | Sonuç |
|---|---|---|
| 1 | 10.000 m soak, her chunk birleşiminde geçerlilik kontrolü, boşluk / üst üste binme / ulaşılamayan bölüm yok | **5 seed × 10 km**: 58–62 chunk'ta 0 hata. Birleşimlerde pozisyon, yön, genişlik, yükseklik ve mesafe farkı ölçülen **0.0**. Bitişik olmayan chunk'lar arası en az 100 m. En büyük yön sapması 50.8° (sınır 55°). Her chunk'ta geçilebilir çizgi var ve çizgiyi bağımsız olarak yeniden kontrol eden test de hata bulmadı. Geçilebilirlik için seed başına 4–10 engel silindi. 14 chunk tipinin hepsi 10 km içinde çıktı. Havuz: toplamda 10 chunk nesnesi, aynı anda en fazla 6 aktif |
| 2 | Hedef yanal ağırlığı ayarlayan ideal sürücü koşuyu crash olmadan bitiriyor | **5 seed × 10 km: 0 crash.** Ortalama 27–29 m/s, en yüksek 41–43 m/s. 5 koşunun toplamında 1 ağır çarpma, 0 duvar sürtmesi, 0 düşme |
| 3 | Zorluk eğrisi loglanıyor ve yükseliyor | km başına ortalama chunk zorluğu: 2.04 · 3.18 · 3.60 · 5.83 · 6.87 · 6.86 · 8.42 · 8.12 · 8.61 · 8.96. Doğrusal eğim **+0.83 / km**. Chunk bazında log: `Docs/M2_difficulty_curve.csv` |
| 4 | Havuzlamadan kaynaklı GC spike'ı / kare düşüşü yok | Headless, simülasyon + yol üretimi, ısınmadan sonra 20.000 adım (~10 km, chunk üretimi dahil): **0 byte allocation**. Adım süresi ortalama 42 µs, p99 96 µs, en fazla 1.7 ms (60 Hz bütçesi 16.7 ms) |
| M1 | M1 kabul testleri, yeni sistemle | Hepsi geçiyor: A1–A6, pist koşuları, solo mod |

## 3. Test EDİLEMEYENLER

- **Unity içinde hiçbir şey çalıştırılmadı.** Yol ve engel görselleri, kamera ve Play Mode testleri (M2 için eklenen kare süresi / GC raporu testi dahil) hiç koşmadı.
- **Kabul 4 Unity tarafında ölçülmedi.** Gerçek kare süresi, Unity (Mono) GC'si, mesh yeniden kurmanın kare maliyeti bilinmiyor. `MeshWriter` allocation'sız olacak şekilde yazıldı ama ölçülmedi. Headless ölçüm .NET 8 üzerinde; Unity'nin Mono/IL2CPP davranışı farklı olabilir.
- Unity 6'da derleme. Kontrol yine Unity 2021.3 referanslarıyla ve Input System stub'ıyla yapıldı; hata ve uyarı yok.
- Engellerin kamera mesafesinden okunaklı olup olmadığı. Bu M4 kriteri, gözle bakılmadı.

## 4. Bilinen sorunlar

- Karışık (mixed) botlarla koşu hâlâ yolu takip edebiliyor ama çapraz trafik ve dar bölümler bot kişiliklerini zorluyor. Karışık ekiple endless yol ayrıca ölçülmedi, sadece M1 pisti ölçüldü.
- Board rampanın yan yüzeyine çarpmıyor (M1'den kalma). Zemin board'un orta hattından örnekleniyor.
- Crash'ten sonra hız başlangıç hızına (8 m/s) dönüyor ama zorluk mesafeye bağlı kalıyor. Bilinçli bir karar, oynayınca değerlendirilmeli.
- Tünel içi sadece renkle koyu; ışık kurulumu yok (M4).
- Görseller hâlâ primitive. Arka plan (binalar, deniz) M4'te.

## 5. Son tuning değerleri (değişenler)

- Yeni: `heavyImpactSpeedLoss` 0.3, `crashImpactSpeed` 16 m/s, `heavyStaggerTime` 0.7 s, `heavyFallThreshold` 0.55, `potholeSpeedLoss` 0.06, `wallScrapeSpeedLoss` 0.12, `wallCrashLateralSpeed` 11 m/s, `difficultyFullDistance` 8000 m, `difficultySpeedCapBonus` 0.25.
- Değişen davranış: arka ağırlık yokuş çekişini bastırıyor (yeni alan yok).
- Koddaki sabitler: planner yanal eğim 0.06, planner güvenlik payı 0.75 m, yol ipucu ileri bakışı 12 + 1.1 × v, cooperative bot kazancı 1.5 ve tehlike sınırı 0.68.

## 6. Önerilen sonraki adımlar

1. Unity'de açın. Önce M ile M1 pistini, sonra endless yolu deneyin; PlayMode testlerini koşturun. Özellikle `Endless_ViewsFollowSimulation_FrameTimeAndGcReport` testinin Console'a yazdığı kare süresi ve GC sayısını bana iletin.
2. Engel sertliği (ağır çarpma = −%30 hız) ve zorluk artış hızı (8 km) oynayarak ayarlanmalı.
3. Onay gelirse Milestone 3: coin / diamond / nitro, skor ve combo.
