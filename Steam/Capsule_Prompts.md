# Blockflow: Steam capsule prompt'ları (ChatGPT görsel)

## Tek cümlelik fikir

**Büyük bir temizlemeden hemen önceki an:** parlayan bir parça son boşluğa düşmek üzere, tamamlayacağı her satır ve sütun neon çizgiyle çevrilmiş, ilk ışık hatların uçlarından fışkırmaya başlamış. Tahtanın yanında üç parlayan kart süzülüyor. Bir saniyede bakan kişi üç şeyi okur: *blok bulmaca* (tahta), *roguelike build* (kartlar) ve *büyük ödül* (patlamak üzere olan ışık). Cevapsız soru, "düşünce ne olacak?", insanı durdurup baktıran şey.

## Nasıl çalışmalı

1. **İlk mesaja referans ekle:** `SteamShots/02_six_line_preview.png` (altın 6 hat önizlemesi), `SteamShots/03_six_line_clear.png` (patlama), `SteamShots/04_card_pick_epic.png` (kartlar). Mesaja şunu da ekle: "Use the attached screenshots only as a reference for the colors, the block style and the card style."
2. **Görseli yazısız üret.** Görsel modelleri harfleri hâlâ bozuyor, kapsülde de gerçek logo olmalı. Logoyu Figma/Photoshop'ta kendin yerleştir (ana menü logosu: dört bloklu ikon + beyaz "BLOCK" + çok renkli "FLOW").
3. ChatGPT yalnızca 3:2, 2:3 ve 1:1 veriyor. Geniş kapsülleri 3:2 üretip boyuta kes; aşağıdaki prompt'lar kesme payını bırakıyor.
4. Steam kuralı: kapsülde yalnızca görsel, oyunun adı/logosu ve resmi alt başlık olabilir. İnceleme alıntısı, ödül, "Out now", indirim ya da başka yazı olamaz. Prompt'lardaki "no numbers / no UI" bu yüzden var.

| Görsel | Boyut | Üretim oranı | Prompt |
|---|---|---|---|
| Main capsule | 1232×706 | 3:2 | A |
| Header capsule | 920×430 | 3:2, üstten ve alttan kes | A |
| Small capsule | 462×174 | A'dan, logo kareyi neredeyse dolduruyor | (kesim) |
| Vertical capsule | 748×896 (≈5:6, 2:3'ten daha kare) | 2:3 üret, yüksekliğin ~%20'sini kes; logo alanını koru | B / G |
| Library capsule | 600×900 | 2:3 | B |
| Library header | 920×430 | header ile aynı dosya | A |
| Library hero | 3840×1240, **logosuz** | 3:2, sonra genişlet | C |
| Library logo | 1280×720, şeffaf PNG | mevcut logo | yok |

---

## v2: İlk denemeden sonra (önce bunları dene)

A'nın ilk sonucu jenerik bir mobil blok oyunu reklamı gibi çıktı: rastgele renk konfetisi bir tahta, parça boşluğun üstünde değil, kart yerine kare rozetler, logo alanı dolu, her şey aynı şiddette parladığı için hiçbir şey öne çıkmıyor. Bu yüzden v2'de üç şey değişti:
1. **Işık hiyerarşisi:** tahta loş ve soğuk; yalnızca patlayacak hatlar ve parça parlıyor.
2. **Tek, gerçek bir kart:** dikey oranlı, köşelerinde elmas süsler olan bir epik kart; parçayı o "doğuruyor". Kart → blok → kombo hikâyesi tek görüntüde.
3. **Daha kısa, öncelik sıralı prompt:** model uzun listede detayları ortalıyordu.

### E: Kart parçayı doğuruyor (geniş, 3:2)

```
Cinematic key art for "Blockflow", a roguelike block-puzzle game. Landscape 3:2. No text, letters, numbers, UI or watermarks anywhere.

Most important, in order:
1. A moody, mostly dark scene with low-key lighting. Restraint: darkness is 60% of the image.
2. An 8x8 board of thick, rounded, beveled blocks, seen from a low dramatic three-quarter angle, like a monument. Its blocks are DIM: deep teal, indigo and plum tones, barely lit, matte. There is exactly one empty 3x3 hole in the middle of the board.
3. Above that hole, a 3x3 square piece made of molten, glowing gold blocks is descending straight into it. Its shape matches the hole exactly.
4. Only the 3 rows and 3 columns through the hole are lit: thin gold neon lines run along them, and those blocks glow warmer. The ends of the lines are just starting to flare. Nothing has exploded yet.
5. In the left foreground, large and slightly out of focus, a single tall collectible card (2:3 portrait proportions) with a dark navy face and a glowing violet neon frame with small diamond ornaments in its corners. It is cracking open with light, and a ribbon of golden energy flows from the card into the falling piece, as if the card summoned it.

Background: near-black navy, a few huge blurred translucent block shapes drifting far away, a faint violet haze. Light sources: the gold piece (main, warm), the violet card (secondary). The left third stays dark and simple for a logo.

Style: high-end stylized 3D render, cinematic depth of field, subtle film grain, strong contrast, premium indie game key art. Not a mobile game ad: no rainbow confetti, no flat front lighting, no clutter.
```

### F: Gerçek ekran görüntüsünden key art (en tutarlı yol)

Tahta düzeni ve parça–boşluk eşleşmesi zaten doğru olan bir ekran görüntüsünü temel al. `SteamShots/02_six_line_preview.png`'yi ekle (gerekirse önce Photoshop'ta sol paneli, skor kutularını ve alttaki havuzu sil) ve şunu yaz:

```
Reimagine the attached game screenshot as cinematic key art for the game "Blockflow". Landscape 3:2. Keep the exact board layout: the same block colors in the same cells, the same empty 3x3 hole and the same golden outlines on its three rows and three columns.

Change everything else:
- Remove all UI: panels, score boxes, the pool at the bottom, every letter and number.
- Turn the board into a physical object with real depth: thick glossy blocks with beveled edges and visible sides, seen from a lower, more dramatic angle, slightly rotated.
- Add a golden 3x3 piece made of glowing blocks, hovering just above the hole, about to drop in, lighting the hole from above.
- Make the golden outlines brighter and let their ends just begin to flare into beams. Nothing has exploded yet.
- Lighting: dark, moody, cinematic. The board's other blocks are a little dimmer so the gold reads first.
- Background: near-black navy with huge blurred translucent block shapes far away.
- Keep the left third dark and calm for a logo.

Style: high-end stylized 3D render, cinematic depth of field, subtle film grain, premium indie key art.
```

### G: Dikey, E ile aynı dilde (2:3)

```
Cinematic vertical key art for "Blockflow", a roguelike block-puzzle game. Portrait 2:3. No text, letters, numbers, UI or watermarks anywhere.

Low-key, mostly dark scene. In the lower half, an 8x8 board of thick, rounded, beveled blocks seen from above at a steep angle, its blocks dim and matte (deep teal, indigo, plum), with exactly one empty L-shaped hole. From the top of the frame a glowing molten-gold L-shaped piece falls toward the hole, its shape matching it exactly, trailing a ribbon of golden light. The ribbon starts at a single tall collectible card with a dark navy face and a glowing violet neon frame with diamond ornaments in its corners, floating at mid-left, cracking open with light. Only the rows and columns through the hole are traced with thin gold neon lines, their ends just beginning to flare.

The top 30% stays dark and calm for a logo. Background: near-black navy, huge blurred translucent block shapes far away, faint violet haze.

Style: high-end stylized 3D render, cinematic depth of field, subtle film grain, strong contrast, premium indie key art. Not a mobile game ad: no rainbow confetti, no flat lighting, no clutter.
```

### G sonucu için düzeltme (görsel açıkken)

G'nin ilk sonucu doğru hikâyeyi ve ışığı yakaladı ama karanlık fantezi tarafına kaydı (taş kaide, yosun, kayalar, mat taş bloklar). Kompozisyonu koruyup malzemeyi oyuna geri çeken düzenleme:

```
Keep this exact composition, camera angle, lighting direction and the story (the card cracking open, the golden ribbon, the falling golden piece, the glowing lines). Change only these:

1. Materials: this is a sleek neon arcade puzzle game, not dark fantasy. Remove the stone pedestal, the rocks, the moss and the rubble. The board is a clean, dark navy, slightly glossy tray with thin glowing edges, floating in space.
2. Blocks: smooth glossy rounded cubes with a soft bevel and a glassy top highlight, like polished candy or acrylic, not stone. Their colors are the game's neon palette (cyan, magenta, emerald, royal blue, violet, tangerine), dimmed to about 50% so the gold still reads first, and grouped in clear same-colored tetromino clusters.
3. The falling piece and the hole must have exactly the same shape and orientation: an L of four blocks (three in a row and one on top of the left end). Make the hole that exact L, aligned with the board grid, directly under the piece.
4. The card: same size and position, but with a dark navy face, a glowing violet neon frame with diamond corners, and in its center a simple neon line-art icon of a chain of linked cubes. The golden cracks of light stay.
5. Background: dark navy space with huge blurred translucent neon block shapes and a faint violet haze, no rocks.
6. Move the card a little lower and to the left so the top 25% of the image is empty dark space for a logo.

No text, letters or numbers anywhere.
```

### G'nin 2. turu: son ince ayar

Malzeme oturdu (parlak akrilik bloklar, temiz tepsi, neon arka plan). Kalan iki sorun: düşen L ile boşluk aynı şekil/yönde değil, altın çizgiler boşluğun satır/sütunlarında değil. Düzenleme modeli çok şey değiştirmesin diye yalnızca bunu iste:

```
Keep everything in this image exactly as it is: composition, colors, lighting, the card, the ribbon, the background. Change only the hole in the board and the gold lines:

- The hole must have exactly the same shape and orientation as the falling golden piece above it: one block on top of the LEFT end of a horizontal row of three blocks. Fill the current hole with normal glossy blocks and cut the new hole directly under the falling piece, aligned with the board grid.
- The thin gold glowing lines must run only along the board row and the board columns that pass through the new hole, edge to edge, with a soft flare at their ends.

No text, letters or numbers.
```

Model yine kaçırırsa bunu Photoshop'ta elle düzeltmek daha hızlı: yanlış boşluğa tahtadaki bloklardan kopyala-yapıştır, doğru dört hücreyi koyu boş hücreyle kapat.

### Geniş kapsülü aynı sahneden türetmek

Dikey görsel oturunca geniş kapsülü sıfırdan üretme; aynı sahneyi yeniden dizdir ki ikisi aynı oyun gibi görünsün. Dikey görseli ekleyip:

```
Recreate this exact scene as a wide landscape key art (3:2): the same glossy neon board on its dark navy tray, the same cracking violet card, the same golden ribbon and falling golden piece, the same matching hole and gold lines, the same background and lighting. Re-arrange it for a wide frame: the board in the right half, seen at the same angle; the card at the center-left, slightly above the board, with the golden ribbon arcing from it to the piece above the hole. Keep the left third dark and calm for a logo. Keep everything important inside the central 80% of the height. No text, letters or numbers.
```

---

## Logo

Oyunun mevcut logosu (ana menü): 2×2 yuvarlak köşeli blok ikonu (sol üst cyan, sağ üst mor, sol alt altın, sağ alt yeşil) + beyaz "BLOCK" + her harfi ayrı renkte "FLOW" (F cyan, L mor, O yeşil, W altın). Oyuncu bu logoyu ana menüde görecek; kapsüldeki logo onunla aynı kimlikte olmalı, yeni bir marka değil. Bu yüzden prompt'lar mevcut logoyu **yükseltiyor**, yeniden icat etmiyor.

Referans: ana menünün ekran görüntüsünden logoyu kırp (veya `Assets/Arts/UI/mainmenu_ui_assets/logo_mark.png` + logonun ekran görüntüsü) ve mesaja ekle.

### L1: Mevcut logonun premium hali (önerilen)

```
Design a premium game logo for the video game "Blockflow", based on the attached logo. Keep its identity exactly: the same layout (a square icon on the left, the word on the right), the same 2x2 block icon colors (top-left cyan, top-right purple, bottom-left gold, bottom-right green) and the same word coloring ("BLOCK" white; in "FLOW" the F is cyan, the L purple, the O green, the W gold).

The word is exactly "BLOCKFLOW", one word, in capital letters, spelled B-L-O-C-K-F-L-O-W. No other text, tagline, letters or symbols.

Upgrade the craft, not the design:
- Letters: very heavy, chunky, slightly rounded geometric sans-serif, tight letter spacing, each letter looks like a solid glossy block with a soft bevel, a thin bright top highlight and a little depth on its lower edge.
- Icon: four glossy rounded cubes like polished acrylic candy, with a soft inner glow; the gold block sits a tiny bit lifted and glows brighter than the others, as if it has just snapped into place, with a faint gold light line along the seam.
- Readability: a thin dark navy outline around every letter and the icon, plus a soft dark drop shadow, so the logo reads on any dark or busy background and stays legible when very small.
- A subtle outer glow in the letter colors, kept tight; no big halos.

Front view, perfectly straight, centered, with generous empty margin around it. Transparent background (PNG with alpha). Clean vector-like edges, high-end polish, matching a sleek neon arcade puzzle game.
```

### L2: Hikâyeli ikon (kapsülle aynı an)

İkon, kapsüldeki "son parça boşluğa düşüyor" anını tekrar ediyor. L1 tutarsa buna gerek yok; ikisini yan yana görmek istersen dene.

```
Design a premium game logo for the video game "Blockflow", based on the attached logo, keeping its colors and word coloring ("BLOCK" white; "FLOW" with F cyan, L purple, O green, W gold). The word is exactly "BLOCKFLOW", one word, capital letters, spelled B-L-O-C-K-F-L-O-W. No other text.

Change only the icon: a 2x2 grid of glossy rounded cubes where three are in place (top-left cyan, top-right purple, bottom-right green) and the bottom-left slot is empty; a glowing gold cube is dropping into that slot from slightly above, with a short trail of gold light and two thin gold light lines running along the row and the column it is about to complete.

Letters: very heavy, chunky, slightly rounded geometric sans-serif, glossy with a soft bevel and a thin top highlight. A thin dark navy outline and a soft dark drop shadow around everything for readability on busy art. Tight outer glow only.

Front view, straight, centered, generous margin. Transparent background (PNG with alpha). Clean, high-end, sleek neon arcade puzzle game.
```

### Logo için notlar

- **Harfleri tek tek kontrol et.** Model "BLOCKFLOW"u "BLOCKFLOOW", "BLOKFLOW" gibi bozabiliyor. Bozuksa: "Fix the spelling to exactly BLOCKFLOW, keep everything else identical."
- **Şeffaflık gelmezse:** "Put the logo on a solid pure black background" diye iste, sonra Photoshop'ta Remove Background ile ayır. Parlamayı korumak için katmanı karanlık kapsülün üstüne koyup kenarları kontrol et.
- **Küçük boyut testi:** logoyu 462×174 small capsule'a yerleştir. Okunmuyorsa ikon küçülür veya kaldırılır, kelime büyür.
- **Son sürüm:** AI çıktısı güçlü bir taslak; Steam'e çıkmadan önce kelimeyi Figma'da gerçek bir fontla (aynı kalınlık ve renklerle) temizletmek en sağlamı. Böylece her boyutta keskin kalır ve ana menüdeki logo da aynı dosyadan güncellenebilir.
- **Library logo:** 1280×720 şeffaf PNG; logo karenin içinde ortalı, çevresinde boşluk olacak şekilde.

---

## A: Ana key art (geniş: main capsule + header)

```
Create a premium key art illustration for an indie video game called Blockflow, a roguelike block-placement puzzle game. Landscape 3:2. Do NOT render any text, letters, numbers, logos, UI, buttons or watermarks anywhere in the image.

THE MOMENT: the split second before a massive line clear. An 8x8 puzzle board floats in a dark space, seen in a dynamic three-quarter perspective, tilted toward the viewer and slightly rotated, so it feels like a physical object with depth. The board is almost full of glossy, chunky square blocks in saturated neon candy colors: cyan, hot magenta, gold, emerald green, royal blue, crimson red, tangerine orange and violet. The blocks are grouped in clear tetromino-like clusters of the same color, so you can read the pieces that were placed. Each block is a rounded square with a soft bevel, a visible thickness on its sides, a subtle inner glow and a glassy highlight on top. The empty cells are dark recessed sockets.

There is a 3x3 square-shaped hole in the board. A single glowing golden 3x3 piece hovers just above that hole, a little lifted and casting warm light down into the gap, about to drop in. Thin bright neon outlines in the same gold trace the three rows and three columns this piece will complete, forming a glowing grid of light across the board, and those lines are starting to ignite: the first beams of white-gold light shoot out from their ends past the edges of the board, tiny sparks and a few cube-shaped shards begin to lift off. It is the instant BEFORE the explosion, full of anticipation, not the explosion itself.

Beside the board, three collectible cards float in a slight fan, tilted at different angles, partly overlapping the board's edge to create depth. They are dark navy cards with glowing neon frames: one electric blue, one vivid purple, one teal. Each card shows a simple glowing neon line-art icon on a dark tile: a chain of linked cubes, a curved neon cable with two plugs, a crossed X made of arrows. Faint streams of light flow from the cards toward the board, as if the cards are powering the coming combo. The cards have no writing on them.

BACKGROUND: deep navy to near-black space with a subtle vignette, filled with large, soft, out-of-focus translucent tetromino shapes drifting at different depths (bokeh), in muted versions of the block colors. A soft cyan and magenta rim light on the board, a warm gold glow from the hovering piece in the middle.

COMPOSITION: the board and the hovering piece sit in the right two-thirds of the frame and are clearly the hero. The cards overlap the lower-left edge of the board. The LEFT THIRD of the image is calmer and darker with only soft background bokeh, kept clear for a game logo to be placed later. Keep all important elements inside the central 80% of the height, because the top and bottom will be cropped to a wider banner.

STYLE: polished stylized 3D render with a painterly finish, crisp readable shapes, high contrast, rich bloom and glow, cinematic depth of field, juicy and satisfying, like a premium modern indie puzzle game. Clean and readable at a small thumbnail size: big shapes, no tiny clutter.
```

**Varyant A2 (başlıklı, hızlı test için).** İlk paragraftaki "Do NOT render any text..." cümlesini bununla değiştir:
`Render the title "BLOCKFLOW" in the empty left third, in a bold, heavy, slightly rounded sans-serif: "BLOCK" in white, and in "FLOW" each letter has its own color: F cyan, L purple, O green, W gold. Next to it, a small icon of four rounded blocks in a 2x2 grid (cyan, purple, gold, green). No other text anywhere.`
Sonucu taslak say; son sürüme yine gerçek logo konacak.

---

## B: Dikey (vertical capsule + library capsule)

```
Create a premium vertical key art (portrait 2:3) for an indie roguelike block-placement puzzle game called Blockflow. Do NOT render any text, letters, numbers, logos, UI or watermarks.

Seen from slightly above, an 8x8 puzzle board floats in the lower-middle of the frame, tilted toward the viewer in strong perspective so its near edge feels close. The board is almost full of glossy chunky rounded square blocks in saturated neon candy colors (cyan, hot magenta, gold, emerald green, royal blue, crimson, tangerine, violet), grouped in readable same-colored tetromino clusters, each block with a soft bevel, visible side thickness and a glassy top highlight. Empty cells are dark recessed sockets.

A glowing golden L-shaped piece descends from above toward the last gap in the board, trailing a soft streak of light. Bright gold neon outlines trace the rows and columns it will complete, and the first white-gold beams are bursting out of their ends, with sparks and small cube shards lifting off. The instant before a huge clear: anticipation.

Three dark navy collectible cards with glowing neon frames (electric blue, vivid purple, teal) fan out around the lower part of the board at different angles and depths, one slightly overlapping the board's near edge and a little out of focus for depth. Each shows only a simple neon line-art icon (a chain of cubes, a neon cable with two plugs, an arrow X); no writing.

Background: deep navy to near-black space, large soft out-of-focus translucent tetromino shapes drifting at different depths, cyan and magenta rim light, warm gold glow from the falling piece.

COMPOSITION: the TOP 30% of the image is calm, darker space with only soft bokeh, kept clear for a game logo. The falling piece sits just below that zone, leading the eye down into the board. Keep everything important away from the outer 5% edges.

STYLE: polished stylized 3D render with a painterly finish, crisp readable shapes, high contrast, rich bloom, cinematic depth of field, juicy and satisfying, premium modern indie puzzle game. Must stay readable as a small thumbnail.
```

---

## C: Library hero (geniş, logosuz)

```
Create a wide cinematic background banner (landscape 3:2) for the indie puzzle game Blockflow. No text, letters, numbers, logos, UI or watermarks.

A tilted 8x8 board of glossy neon candy-colored rounded blocks (cyan, magenta, gold, green, royal blue, crimson, tangerine, violet) in the RIGHT half of the frame, at the exact moment of a massive line clear: three rows and three columns explode at once into long horizontal and vertical beams of white-gold light that stretch past the board, hundreds of cube-shaped shards and sparks in the block colors fly outward, a soft circular shockwave ring expands from the center of the board. Two or three glowing neon-framed dark cards drift in the far background, blurred.

The LEFT half is calm, dark navy space with only large soft out-of-focus floating tetromino shapes and faint light streaks, kept empty for a logo. The vertical center band is the most important; the top and bottom will be cropped to a very wide banner, so keep the board and the explosion within the middle 60% of the height.

Style: polished stylized 3D render, painterly finish, high contrast, rich bloom, cinematic depth of field, premium modern indie puzzle game.
```
Sonra görsel açıkken ChatGPT'ye: *"Extend this image to the left and right to a much wider panoramic format, continuing the dark space and floating blocks seamlessly. Do not change the board."* Ya da Photoshop'ta Generative Expand ile 3840 px'e genişletip 1240 px yüksekliğe kes.

---

## D: Maskotlu varyant (daha sıcak, daha karakterli)

Gözlüklü, çekiçli altın küp karakter (`Assets/Arts/UI/Characters/win.png`, `Milestone.png`) sıcaklık ve akılda kalıcılık katıyor. Risk: o 2D çizgi film, oyun ise 2.5D neon; aynı render stilinde yeniden çizilmesi gerekiyor. `win.png`'yi referans olarak ekle ve A prompt'una şu paragrafı ekle:

```
On the left side of the board, partly in front of it, stands the game's mascot: a cheerful golden-yellow cube character with a rounded bevel, big black-rimmed glasses, freckles, a wide happy grin, thin brown arms with gloves and small brown boots (use the attached character only as the design reference, but render him in the same polished stylized 3D look as the blocks). He has just thrown the glowing golden piece toward the gap with one hand and holds a small hammer in the other, leaning back with excitement as the lines light up. He is about one third of the board's height, so the board stays the hero.
```
Bu durumda logo alanı sol üste kayar. İki kapsülü yan yana denemek istersen bu varyantı kullan (Steam kapsüller için A/B testine izin veriyor).

---

## Düzeltme prompt'ları (görsel açıkken)

Neyin ters gittiğine göre tek tek kullan:

- **Küçük boyutta okunmuyor:** "Simplify: fewer background shapes, bigger blocks, stronger contrast between the board and the background. Keep the composition."
- **Tahta karmakarışık:** "Make the blocks on the board form clearer, larger same-colored tetromino groups, and make the 3x3 gap and the hovering golden piece obviously match in shape."
- **Yazı veya sayı çıktı:** "Remove every letter, number and symbol from the image, including on the cards. Keep everything else identical."
- **Patlama erken olmuş:** "Pull the moment back: the beams are only just starting at the ends of the outlined lines; most of the board is still intact and the piece has not landed yet."
- **Fazla düz / mobil oyun gibi:** "Give the blocks more physical depth: visible side thickness, glassy top highlights, soft shadows on the board, stronger perspective and depth of field."
- **Logo alanı kalabalık:** "Make the left third darker and calmer, move the cards slightly right, nothing important in the left third."
- **Renk fazla:** "Limit the palette: gold glow is the main accent, cyan and magenta secondary, the other block colors more muted."

## Kontrol listesi (Steam'e yüklemeden önce)

- Logo 462×174'te hâlâ okunuyor mu? (Photoshop'ta küçültüp bak.)
- Tahta, düşmek üzere olan parça ve en az bir kart 1 saniyelik bakışta görünüyor mu?
- Logo dışında yazı, sahte UI ya da sayı yok mu?
- Boşluk parçanın şekline uyuyor mu? (Görsel modelleri bunu sık karıştırıyor, kontrol et.)
- Dikey ve geniş kapsüller aynı renk ve ışık dilinde mi?
