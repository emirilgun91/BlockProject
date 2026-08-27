# 2.5D Sahne Kurulumu (Yön B)

Tahtayı perspektif kamera ve eğimle gösteren kurulum.

## Durum: bu artık ana oyun sahnesi

2026-08-23'te prototip onaylandı ve `Assets/Scenes/SampleScene.unity`'nin üzerine
yazıldı. **Ayrı bir prototip sahnesi yok.** Takas dosya içeriği değiştirilerek
yapıldı, yol ve GUID korunarak — Build Settings, MainMenu'nün
`_gameplaySceneName`'i ve GameOverUI'ın retry'ı tek satır değişmeden çalışıyor.

Düz 2D hâline dönüş:

```
git checkout -- Assets/Scenes/SampleScene.unity
```

veya `Backups/SampleScene_Flat2D_*.unity`.

**Silmeyin** — ana sahne bunlara bağlı:
- `Assets/Prefabs/TilePrefab_25D.prefab`
- `Assets/Settings/Prototype25D_Volume.asset`

Kurulumu yeniden uygulamak (idempotent; elle yaptığınız tilt/ışık/kart ayarlarını
sıfırlar, Volume profilini korur):
**Tools ▸ RogueBlockBlast ▸ Scene Setup ▸ Re-apply 2.5D Setup To Open Scene**

> Klasör ve namespace hâlâ `Prototype` — terfi sırasında yeniden adlandırılmadı.
> Temizlenmemiş teknik borç.

## Temel tasarım kuralı

**Tilt = 0'da kadraj mevcut sahneyle birebir aynıdır.**

Rig, kamerayı `AimPoint` etrafında döndürür ve mesafeyi şöyle seçer:

```
distance = matchOrthographicSize / tan(fieldOfView / 2)
```

Böylece odak düzlemindeki dikey görüş yarı-genişliği eski ortografik boyuta
eşit çıkar. Değerler builder tarafından sahnenin kendi kamerasından okunur
(sabit sayı gömülmez) — bu projede `orthographicSize 5`, `fieldOfView 34`,
konum `(4, 4)`.

Sonuç: prototipte **tek değişken eğimin kendisi**. "His doğru mu" sorusu başka
hiçbir farkın gölgesinde kalmaz.

## Üretilen assetler

| Dosya | Ne |
|---|---|
| `Assets/Prefabs/TilePrefab_25D.prefab` | `TilePrefab`'in varyantı (+`TileSkirt`) |
| `Assets/Settings/Prototype25D_Volume.asset` | Post-processing profili |

Tile varyantı olduğu için orijinal `TilePrefab`'te yaptığınız her değişiklik
buraya da akar — iki prefabı ayrı ayrı güncellemeniz gerekmez.

**Hâlâ değiştirilmemiş:** URP renderer'ı (`Renderer2D`), Build Settings,
`MainMenu`.

### Paylaşılan tek kod değişikliği

`BoardView`'da fare→hücre çevrimi `ScreenToWorldPoint` yerine **ışın-düzlem
kesişimi** kullanıyor (`TryGetBoardPoint`). Ortografik kamerada sonuç eskisiyle
**birebir aynıdır** — mevcut sahnenin davranışı değişmez. Perspektif kamerada
ise doğru çalışan tek yöntem budur.

## Render pipeline'a neden dokunulmadı

Sahne hâlâ URP **2D Renderer** kullanıyor. 2D Renderer perspektif kamerayla
sorunsuz render eder; eğim, foreshortening, parallax ve derinlik sıralaması
için pipeline değişikliği **gerekmiyor**.

Gerçek 3D ışık ve gölge (yön B'nin tam hâli) için `UniversalRenderer`'a geçmek
gerekir, ama o adım `Global Light 2D`'yi öldürür ve sprite materyallerini
etkiler. Prototipin cevaplaması gereken soru bu değil — o yüzden faz 2'ye
bırakıldı. Geçiş yapılacağı zaman doğru yol: URP asset'ine **ikinci** bir
renderer eklemek ve yalnızca prototip kamerasının `Renderer Index`'ini ona
çevirmek. Böylece mevcut sahne index 0'da (2D) kalır.

## UI

Gameplay HUD'ı **Screen Space Overlay** canvas üzerinde — kamera projeksiyonu
değişse de hiç etkilenmez. Skor, combo, milestone, pool ve kart envanteri
yerlerinde kalır.

Pool parçaları UGUI `Image` olduğu için **düz** kalır, tahta eğilir. Bu bilinçli:
HUD düz, dünya eğimli. Kart sisteminin fiziksel nesnelere dönmesi (yön C) faz
2'nin konusu.

`ScorePopup` `WorldToScreenPoint` kullandığı için perspektifte de doğru çalışır.

## Bilinen sınırlar (prototip olduğu için kabul edilenler)

- **`TileSkirt` gerçek mesh değil.** Tile kökünü kameraya doğru kaldırıp aynı
  sprite'ın koyu bir kopyasını tahta düzleminde bırakıyor. Eğimli kamerada blok
  yan yüzü gibi okunur. Kalıcı çözümde tile'lar gerçek mesh olacağı için bu
  bileşen tamamen kalkar.
- **VFX hizası.** `LineClearVFX` ve skor popup'ları `GetTileWorldPosition`'dan
  gelen `z = 0` konumunu kullanıyor; tile'lar ise skirt yüzünden `z = -depth`'te.
  Eğimde küçük bir kayma oluşur. `TileSkirt.Depth`'i düşürmek azaltır.
- **Boş hücre tespiti parlaklık eşiğiyle.** `TileSkirt`, hücrenin dolu olup
  olmadığını `BoardModel`'den değil rengin parlaklığından çıkarıyor
  (boş `#1c2132` ≈ 0.13, dolu palet renkleri ≈ 0.45+). Paleti çok koyulaştırırsanız
  eşiği (`_emptyLuminanceThreshold`) güncelleyin.

## Ayar noktaları

`Main Camera ▸ Board Camera Rig`

| Alan | Varsayılan | Not |
|---|---|---|
| `Tilt Degrees` | 26 | Asıl değerlendirme kolu. **Pozitif = üst kenar geriye kaçar** (masa hissi). Negatif değerler yukarıdan bakış verir — karşılaştırmak için −25'e kadar açık. |
| `Field Of View` | sahneden | Düşürmek perspektifi yatıştırır, yükseltmek abartır |
| `Idle Sway` | açık | `GameSettings.ReduceMotion` açıkken kendiliğinden kapanır |
| `Mouse Parallax` | açık | 0.22 kuvvet; abartmayın, hızla ucuzlaşır |

`TilePrefab_25D ▸ Tile Skirt`

| Alan | Varsayılan | Not |
|---|---|---|
| `Depth` | 0.22 | `CellSize 0.85` için 0.18–0.30 arası iyi okunuyor |
| `Darken` | 0.55 | Yan yüzün koyuluğu |

## Post-processing

Builder, prototip kamerasında post-processing'i açar ve
`Assets/Settings/Prototype25D_Volume.asset` profilini bağlar: **Bloom**,
**Vignette**, **Color Adjustments**.

URP 2D Renderer post-processing'i destekliyor — bu adım için 3D renderer'a
geçmek gerekmiyor. Kamerada varsayılan olarak kapalıydı; "her şey tek düzey
aydınlıkta" hissinin büyük kısmı bundan geliyordu.

Profil bir kez oluşturulur; sonraki Build'lerde **dokunulmaz**, elle yaptığınız
ayarlar korunur. Sıfırdan istiyorsanız asset'i silip Build'i tekrar çalıştırın.

## Işık

Builder tahtanın üstüne sıcak bir anahtar ışık koyar (`Key Light 2D (25D)`) ve
`Global Light 2D`'yi 0.45'e kısar. Konsept görseldeki "tek sıcak kaynak" etkisi
bu ikisinin dengesinden çıkıyor — global ışık kısılmazsa tepe ışığı okunmaz.

Tile'lar `Sprite-Lit` materyali kullandığı için bu **2D Renderer altında bugün
çalışıyor**; renderer geçişi gerekmiyor.

### İki tuzak

**`Light2D` ≠ `Light`.** Hierarchy menüsündeki *Light ▸ Point Light* standart
**3D** ışığı verir ve URP 2D Renderer altında hiçbir etkisi yoktur. Doğru
bileşen `Light 2D` (Inspector'da `Universal Additional Light Data` görüyorsanız
yanlış olanı eklemişsiniz). Builder sahnedeki 3D `Light` bileşenlerini uyarı
basarak temizler.

**Sorting layer hedeflemesi.** Bir Light2D'nin hangi sorting layer'ları
aydınlattığı serileştirilmiş bir alandır; yanlış hedeflenen ışık hiçbir şeyi
aydınlatmaz ve sessizce çalışmaz görünür. Builder bu yüzden anahtar ışığı
sıfırdan yaratmaz — **Global Light 2D'yi kopyalar**, böylece hedefleme hazır gelir.

### Ayarlar

| Ayar | Değer |
|---|---|
| Position | tahtanın üst kenarının 1.5 hücre üstü |
| Color | `#F0BB4C` |
| Intensity | 1.6 |
| Inner / Outer Radius | 1.5 / 9.5 |
| Falloff Intensity | 0.7 |
| Global Light 2D | 0.32'ye kısılır |

Outer radius tahtanın yüksekliğine (8 × 0.85 = 6.8) yakın tutuluyor. Çok büyük
yarıçapta düşüş tahtaya yayılmadan bitiyor ve üst–alt farkı okunmuyor.

### Işığı boş tahtada değerlendirmeyin

Boş hücre rengi `#1c2132` — parlaklığı 0.11. Bu kadar koyu bir zeminde ışığın
çarpımsal etkisi gözle görülmez, ışık doğru kurulmuş olsa bile "çalışmıyor" gibi
durur. Değerlendirmeden önce **F1 debug paneliyle tahtayı doldurun**; doygun
palet renkleri ışığın düşüşünü asıl gösteren şey.

Zemin (`Board Pedestal (25D)`) de bilerek biraz açık renkte — ışığın düşüşünü
yakalayan asıl yüzey o.

## Fiziksel kartlar

Kart seçimi 2.5D sahnesinde dünya-uzayında, fiziksel kartlarla yapılır.

### Neden havuz mantığı kopyalanmadı

`CardSelectionUI` iki iş yapıyordu: hangi kartların çıkacağına karar vermek
(kilit filtresi, unique kontrolü, ağırlıklı seçim, reroll) ve göstermek.
Birincisi kopyalanırsa kart kuralları iki yerde ayrışır. O yüzden yalnızca
**sunum** ayrıldı: `ICardPresenter`.

`CardSelectionUI.ExternalPresenter` **null bırakılırsa davranış eskisiyle
birebir aynıdır** — mevcut sahne etkilenmez. `PhysicalCardPresenter` kendini
`Start()`'ta bağlar; sahnede yoksa eski UGUI paneli çalışır.

### Kart görselleri yeniden çizilmedi

Mevcut `CardView_0` prefabı bir **World Space Canvas** içine konuyor. İkon,
isim, açıklama, rarity çerçevesi ve tıklama (Button + `GraphicRaycaster`)
olduğu gibi geliyor — ama kart artık sahnede gerçek bir nesne.

```
Card Root                 ← PhysicalCard: uçuş, yaylanma, hover
 └── Canvas (World Space, scale 0.003, sorting layer "UI")
      ├── Shadow          ← kardeş, kartın arkasında
      └── CardView_0      ← mevcut prefab, değiştirilmeden
```

Gölge **aynı canvas'ın içinde**, kartın arkasındaki kardeş olarak duruyor.
Ayrı bir `SpriteRenderer` olsaydı sorting layer'ını elle yönetmek gerekirdi;
canvas içinde sıra otomatik. Kart yükseldikçe gölge uzaklaşıp soluyor.

### Yerleşim kameraya göre, dünya koordinatına göre değil

İlk sürüm yelpazeyi sabit dünya koordinatına koyuyordu ve bu üç sorunu birden
üretti: kartlar devasa çıktı, ekranda yukarı kaçtı, tahtanın arkasında kaldı.

Sebep şu: kamera `Euler(-tilt, 0, 0)` ile eğik. Kartları `z = -3.5` ile kameraya
doğru itmek, kamera uzayında `y = +0.438 × 3.5` (ekranda **yukarı**) ve
`z = -0.899 × 3.5` (kameraya **yaklaşma → büyüme**) demek. Tek bir z değeri hem
konumu hem boyutu bozuyor.

Artık yelpaze `camera.position + camera.forward × mesafe` ile kuruluyor ve
kartlar kameranın `right`/`up` eksenlerinde diziliyor. Kadraj, eğim veya FOV ne
olursa olsun kartların ekrandaki boyutu ve yeri sabit kalıyor.

### Kart ölçüsü tahmin edilmez

Prefabın gerçek ölçüsü **685 × 1016 px** ve çocukları (`CardBody` 531×430,
`DescriptionText` 502×401) **nokta anchor** kullanıyor — yani köke başka bir
`sizeDelta` yazmak onları küçültmez, kartın dışına taşırır. İlk sürümde ölçü
420×560 varsayılmıştı ve açıklama metni komşu kartın üstüne akıyordu.

Artık ölçü prefabdan okunuyor ve `viewRect.sizeDelta`'ya hiç dokunulmuyor;
kart yalnızca ortalanıyor.

Dünya ölçeği de sabit değil, istenen ekran oranından hesaplanıyor:

```
visibleHeight = 2 × cameraDistance × tan(fov / 2)
canvasScale   = (visibleHeight × cardScreenHeight) / cardHeightPx
slotSpacing   = cardWidthPx × canvasScale × fanSpacingFactor
```

`Card Screen Height` (0.45) ve `Fan Spacing Factor` (1.1) tek ayar noktası —
mesafe veya FOV değişse de kartlar aynı büyüklükte ve aynı aralıkta kalır.

### Sorting layer

Tahta **Board** layer'ında. Kart canvas'ı varsayılan **Default**'ta bırakılırsa
— Default listede ilk sırada olduğu için — tahtanın arkasında kalır. Bu yüzden
canvas `UI` layer'ına, order 100'e alınıyor (`Sorting Layer` / `Sorting Order`
alanlarından değiştirilebilir).

Not: Gameplay HUD'ı Screen Space **Overlay** olduğu için her şeyin üstünde
çizilmeye devam eder; yelpaze HUD'ın altında kalacak şekilde konumlandırılmalı.

### Davranış

| Faz | Ne olur |
|---|---|
| Dağıtım | Kartlar yuvadan parabol çizerek, dönerek ve sırayla (stagger) gelir |
| Yerleşme | Yelpaze dizilimi — uçtakiler alçalır ve merkeze eğilir |
| Hover | Kart kameraya doğru yükselir, doğrulur, büyür; gölgesi yayılır |
| Seçim | Seçilen kart envantere uçar, diğerleri yuvaya geri düşer |

Her şey `Time.unscaledDeltaTime` ile çalışır — kart seçiminde
`Time.timeScale = 0`.

### Prototip kapsamı dışı

- **Reroll butonu** dünya uzayında çizilmiyor. Hak varsa Console'a not düşer,
  kartlar normal gelir. (Eski panelde çalışmaya devam ediyor.)
- **"Yeni kart" rozeti** aynı şekilde henüz yok.
- Kapanış animasyonu oyunu **bloklamaz** — oyun mantığı seçim anında devam
  eder, uçuş kozmetiktir.

### Ayar noktaları

`Physical Cards (25D) ▸ Physical Card Presenter` — yelpaze aralığı/kavisi/eğimi,
dağıtım süresi ve yayı, hover yüksekliği, canvas ölçeği. Konumlar builder
tarafından tahtaya göre hesaplanır.

## Atmosfer

`BoardAtmosphere` sahnenin tamamını oyun durumuna bağlar.

İlke: **atmosfer parlaklık değil tepki.** Karanlık bir sahne oyuncuya cevap
veriyorsa canlıdır. Bileşen komboyu tek bir 0–1 "ısı" değerine indirger ve o
ısıyla dört şeyi birlikte sürer:

| Sürülen | Soğuk (ısı 0) | Sıcak (ısı 1) |
|---|---|---|
| Anahtar ışık | sahnedeki renk/şiddet | turuncuya kayar, ×1.55 şiddet |
| Bloom | taban | ×1.7 |
| Vignette | taban | +0.10 (kenarlar kapanır) |
| Boş hücre nabzı | 0.16 şiddet, yavaş | 0.50 şiddet, hızlı, sıcak tonda |

Isı iki sinyalin büyüğüdür: **şarj doluluğu** (kısa vadeli, her temizlemede
oynar) ve **çarpan** (uzun vadeli, run'ın gidişatı). Böylece hem anlık başarı
hem birikmiş ivme ekrana yansır.

Büyüğünün alınmasının yan faydası: oyuncu şarjı kaybettiğinde (temizlemesiz
yerleştirme) ısı tamamen çökmez, çünkü çarpan durur — "hâlâ iyi bir run'dasın"
bilgisi ekranda kalır.

### Combo barı 3 de olabilir 5 de

Şarj oranı `Charges / MaxCharge` olarak hesaplanır; `upgrade_combo_bar` ile
`MaxCharge` değiştiğinde kendini ayarlar. 5 barlı kurulumda tam ısıya ulaşmak
daha çok temizleme ister — doğrusu bu.

Çarpanın ise **tavanı yok**: `ComboSystem.OnLineClear` her temizlemede
`Multiplier += bonus` yapar, sınırsız büyür. Bu yüzden sabit bir "tam ısı
çarpanı" tanımlanamaz — öyle yapılırsa çarpan o eşiği geçtiği an ısı 1.0'a
yapışır ve üst uçtaki ifade kaybolur. Onun yerine doyuma ulaşmayan üstel bir
eğri kullanılır:

```
multRatio = 1 − exp(−(Multiplier − 1) / softness)
```

1'e asla tam varmaz, yani çarpan büyüdükçe biraz daha ısı gelmeye devam eder.

### Taban değerler sahneden okunur

`Start()`'ta ışığın ve post-fx'in o anki değerleri taban kabul edilir; sabit
sayı gömülmez. Işık veya Volume ayarını elle değiştirmek bileşeni bozmaz,
yeni taban olur.

### Boş hücre nabzı

Tahtanın en büyük ölü yüzeyi 64 aynı koyu kare. `BoardView` artık boş hücreleri
çapraz ilerleyen bir dalgayla tonluyor (`EmptyPulseAmount`). **Varsayılan 0 —
mevcut sahne etkilenmez**, yalnızca atmosfer bileşeni onu açar.

Dalga `Time.unscaledTime` kullanır: kart seçiminde `timeScale = 0` olsa da
tahta nefes almaya devam eder.

### Erişilebilirlik

`GameSettings.ReduceMotion` açıkken nabız durur ama renk kayması kalır —
bilgi kaybolmaz, yalnızca hareket kalkar. `GameSettings.VfxIntensity` tüm
etkiyi ölçekler.

## Sırada ne var: Faz 2 — gerçek 3D (his onaylanırsa)

1. URP asset'ine **ikinci** renderer (`UniversalRenderer`) ekle, prototip
   kamerasının Renderer Index'ini ona çevir. Mevcut sahne index 0'da (2D) kalır.
2. Tile'ları gerçek mesh'e çevir, `TileSkirt`'i kaldır.
3. Directional light + gölge; tahta altına gerçek zemin.
4. `LineClearVFX` ve popup'ları tile'ın yeni yüksekliğine hizala.
5. Ancak bundan sonra yön C (diegetic kabin) — kart sisteminin world-space'e
   taşınması.
