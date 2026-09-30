# M4.3 Raporu — Oyun hissi: hareketler, hız efektleri, ses

Tarih: 2026-09-30

İstek: "Her şey İngilizce olsun, oyunu oyun gibi hissettirecek mekanikler ekleyelim, nitroya basınca efekt olsun, hızlanınca sağdan soldan rüzgârlar, her detayı ilerletelim."

Ekran görüntüleri (headless önizleme, Unity'nin çizeceği aynı HUD komutları):
- `Docs/M4/hud_nitro.jpg`: nitronun ateşlendiği an
- `Docs/M4/hud_nitro_wind.jpg`: 1 saniye sonra, tam rüzgâr
- `Docs/M4/menu_settings.jpg` ve `Docs/M4/menu_pause.jpg`: İngilizce menüler

## 1. Ne yapıldı

### Her şey İngilizce
- Ayarlar'daki dil seçeneği kaldırıldı ve oyun İngilizce'ye sabitlendi.
- Menü, HUD, popup'lar ve bitiş ekranı tamamen İngilizce.
- Türkçe çeviri tablosu kodda duruyor. Dil desteği eklenecekse tek satırla geri açılıyor.

### Yeni hareketler (her biri popup, ses, efekt ve puan × combo veriyor)

**Crew Ollie (ekip zıplaması)**
- Ekibin en az %60'ı 0.35 saniye içinde zıplarsa tahta havaya kalkıyor: yaklaşık 0.7 saniye havada kalıyor.
- Geri kalan riderlar zıplamadan sonraki 0.15 saniye içinde katılırsa hareket **PERFECT OLLIE** oluyor: ekstra kaldırma, yaklaşık 0.85 saniye havada kalma ve çift puan.
- Tek kişiyle her zıplama bir ollie.
- Çukur, enkaz ve alçak engellerin üstünden atlamak ve havadaki elmasları almak için kullanılıyor.

**Carve Boost (viraj fırlatması)**
- Sert bir virajı 0.8 saniye tutup temiz bir şekilde düzelince tahta 1.2 saniye boyunca +5 m/s hızlanıyor.
- İki kat uzun tutulan viraj **MEGA** boost veriyor.
- Tahta sallanırsa (wobble) ya da devrilmeye başlarsa şarj kayboluyor. Risk ve ödül dengesi var: viraj önce hız kaybettiriyor.

**Slipstream (rüzgâr tüneli)**
- Aynı yöne giden bir arabanın 16 metre arkasında ve onun çizgisindeyken 0.5 saniyede +5 m/s'ye kadar hız kazanılıyor.
- Çarpmadan önce çizgiden çıkmak gerekiyor.

### Nitro ve hız efektleri ("sağdan soldan rüzgâr")

Hepsi tek bir motor-bağımsız kaynaktan (`SpeedFeel`) besleniyor. Bu yüzden HUD, kamera, parçacıklar ve ses aynı anda ve aynı şiddette tepki veriyor.

- **Ekran kenarı rüzgâr çizgileri:** anime tarzı çizgiler, sol ve sağ kenardan dışarı doğru akıyor.
  - Hız ve sert ivmelenmeyle artıyor.
  - Nitroda tam güç ve **cyan**, carve boost'ta **turuncu**.
  - Ekranın ortası (tahta ve ilerideki yol) hep temiz kalıyor.
- **Nitro ateşlenince:**
  - Ekran kenarlarından içeri cyan bir parlama.
  - Kamerada FOV "punch" (+6.5°) ve lens bükülmesi (-0.31), chromatic aberration ve ekstra motion blur. Hepsi yaklaşık 1 saniyede sönüp nitro boyunca hafif bir seviyede kalıyor.
  - Yolda yatay bir şok dalgası halkası.
  - Arka tekerleklerden cyan ışık izleri.
  - Kamera geri tepmesi.
- **3D rüzgâr çizgileri:** kameranın iki yanından geçen, gerilmiş parlak parçacıklar. Eski gri küp çizgilerin yerini aldı ve tek draw call. Slipstream'de ortadan da ince bir akış geliyor.
- **Carve boost:** turuncu halka, turuncu izler, küçük FOV punch ve kamera itmesi.
- **Ollie:** tekerleklerden toz patlaması ve kamera sıçraması.
- **Reduce Motion:** açıkken kamera hareketi yapan efektleri (FOV punch, lens, chroma, motion blur, 3D rüzgâr) kapatıyor. HUD çizgileri kalıyor.

### Ses (ilk kez)

Bütün sesler oyun açılışında koddan sentezleniyor (`Art/Audio/Synth.cs`). Asset dosyası ve lisans sorunu yok.

- **Döngüler:** tekerlek (hızla perdesi yükseliyor), rüzgâr (hız ve nitroyla artıyor), nitro jeti.
- **21 efekt:**
  - Coin: art arda toplanınca perdesi tırmanıyor.
  - Diamond, nitro alma ve ateşleme, carve boost, ollie "pop", perfect ollie.
  - İniş: hava süresine göre daha derin ve yüksek.
  - Çarpma, kaza, near miss "vuuş"u, combo artışı, slipstream, 3-2-1-GO, menü tık/onay/geri, lobiye katılma, respawn.
- **Müzik:** 128 BPM, 4 ölçü döngü (C–G–Am–F): kick, clap, hi-hat, bas, akor ve arpej. Kazada kısılıp geri geliyor.
- **Davranış:**
  - Kaza slow-motion'ında sesler de yavaşlıyor.
  - Duraklatınca döngüler sönüyor, müzik kısılıyor.
  - Başlık ekranının arkasındaki bot sürüşü kısık çalıyor.
- **Ayarlar:** Master, Music ve Effects ses ayarları artık gerçekten bağlı.

### Bulunan ve düzeltilen hatalar
1. **Duraklatma tam durmuyordu (önceki milestone'dan).**
   - `GameFeel` her karede `Time.timeScale = 1` yazıyordu. Duraklatma menüsü açıkken simülasyon duruyordu ama geri sayım, parçacıklar ve efektler akmaya devam ediyordu.
   - Duraklatma artık zamanın sahibi. PlayMode testine `timeScale == 0` kontrolü eklendi (Unity'de koşmadı).
2. **Perfect Ollie insanlar için imkânsızdı.**
   - Ollie eşik anında tetiklendiği için "herkes birlikte" koşulu, son riderların eşik rider'ıyla aynı 1/60 saniyelik karede basmasını gerektiriyordu.
   - 0.15 saniyelik "geç katılma" penceresi eklendi. Ollie gecikmesiz kalkıyor, geç gelenler onu perfect'e yükseltiyor.
3. **Ses döngüsünde tık sesi.** Döngülerin sonu tek seferlik seslerdeki gibi sessizliğe sönüyordu; düzeltildi. Testte bu hata yakalandı.

## 2. Test edilenler

**Headless testler: 91/91 geçti.**
- Yeni testler: `MovesTests` 6, `SpeedFeelTests` 4, `AudioTests` 5, `HudLayoutTests`'e 2 test.
- Tam test takımı art arda 3 kez koşuldu, her seferinde geçti.

**Unity derleme kontrolü:** Runtime, Editor, Tests ve Steam için 0 hata, 0 uyarı (uyarılar hata sayılıyor).

| Test | Sonuç |
|---|---|
| 6 kişiden 2'si zıplıyor | Ollie yok |
| 6 kişinin hepsi aynı anda zıplıyor | Perfect, 0.87 s hava |
| Son 2 rider 0.6 s geç zıplıyor | Normal ollie, 0.72 s hava |
| Son 2 rider 0.1 s geç zıplıyor | Perfect'e yükseltme, 0.85 s hava |
| Carve boost | 2.13 s viraj, sonra MEGA boost: 19.3 → 25.0 m/s |
| Slipstream | Aynı yöne giden arabanın arkasında +5.0 m/s |
| İdeal sürücü, hareketler açık, 6 km | 0 kaza, ortalama 112 km/h |
| Rüzgâr şiddeti (0–1) | Hızlı gidişte 0.38, sert ivmelenmede 0.69, nitroda 1.00 |
| Nitro ateşleme anı | FOV +6.5°, lens -0.31, chroma 0.66. 1 s sonra: FOV +0, lens -0.10, chroma 0.33 |
| Reduce Motion | FOV, lens, chroma ve blur 0; HUD rüzgârı kalıyor |
| Kaza, run sonu, duraklatma | Kazada ve run bitince rüzgâr sıfırlanıyor; duraklatmada her şey donuyor |
| HUD rüzgâr çizgileri | Sayı rüzgârla artıyor (0 / 18 / 36 / 60); ekran ortasına girmiyor; karede 0 allocation |
| 21 efektin hepsi | Duyulur seviyede (RMS > 0.02); clipping yok (tepe ≤ 0.95); DC kayması yok; sonda tık yok |
| 4 döngü | Döngü noktasındaki sıçrama, döngü içindeki adımların %99.9'undan küçük (tık yok) |
| Müzik uzunluğu | Tam 4 ölçü |
| Müzik ritmi | 15/15 vuruşta kick var |
| Müzik frekans dengesi | %77 bas, %15 orta, %6.5 tiz |
| Ses mikseri | Tekerlek sesi hızla artıyor ve perdesi yükseliyor; havadayken susuyor; nitro jeti çalıyor; kazada müzik kısılıp geri geliyor |
| Tekrar eden sesler | Coin zinciri tırmanıyor; her yeni combo çarpanı tek bir ses; sert iniş daha yüksek ve daha derin; adım başına 0 allocation |
| Ses üretim süresi | Tüm sesler yaklaşık 0.5 s'de (headless ölçüm) |

Perdeler spektrumdan kontrol edildi:
- Coin 988 → 1319 Hz (B5 → E6).
- Geri sayım 880 Hz.
- Elmas C7'ye kadar çıkıyor.

## 3. Test EDİLEMEYENLER (dürüstçe)
- **Unity'de hiçbir şey koşmadı:**
  - Parçacıklar (şok dalgası, 3D rüzgâr, toz), trail'ler, URP post efektleri (chromatic aberration ve lens distortion reflection ile ekleniyor) ve kamera FOV punch sadece derleme kontrolünden geçti.
  - Önizleme sadece HUD katmanını gösteriyor. 3D rüzgâr, izler, halka ve FOV punch ekran görüntülerinde yok.
- **Sesleri kimse dinlemedi.** Sadece sayısal olarak ölçüldüler (seviye, clipping, tık, perde, ritim, frekans dengesi). Hepsini WAV olarak dışa aktardım, dinleyip beğenmediklerini söyle. Kendin üretmek için: `dotnet run --project Tools/ArtPreview -- audio <klasör>`.
- **Hareketlerin hissi** gerçek oyuncularla denenmedi. Özellikle 6 kişilik perfect ollie zamanlaması ve carve boost eşiği oynanarak ayarlanmalı. Değerlerin hepsi F2 tuning panelinde.

## 4. Bilinen sorunlar ve kararlar
- **Sesler "placeholder".** Tutarlı ve temiz ama sentez sesi. Yayın için müziği ve birkaç ana efekti (nitro, kaza) gerçek kayıt veya bir besteciyle değiştirmeni öneririm. Kod her sesi tek tek değiştirmeye hazır.
- **Botlar carve boost ve slipstream kullanmıyor.** İdeal sürücü testinde 6 km'de 0 boost. Bunlar insan becerisi için tasarlandı. İstersen bot davranışlarına da eklerim.
- **Reduce Motion artık motion blur'u da kapatıyor.** Erişilebilirlik için doğru olduğunu düşünüyorum.
- **Efekt şiddetleri tek yerde:** `SpeedFeel.cs`. Lens bükülmesi Unity'de fazla gelirse oradan düşürülür.
- **Önizleme araçlarında Türkçe sahneler duruyor:** `menu_pause_tr` ve `hud_curve_tr`. Dil sonradan eklendiğinde kontrol için bıraktım.

## 5. Sıradaki öneriler
1. Unity'de bir tur oyna. Nitro, rüzgâr, ses seviyeleri ve lens şiddeti hakkında görüş ver; ince ayarı ona göre yaparım.
2. Hareketleri öğreten kısa bir "How to play" ekranı: jump together, hold a carve, draft behind cars.
3. Gerçek müzik ve ses kayıtları.
4. Botların hareketleri de kullanması, böylece attract mode daha canlı görünür.
