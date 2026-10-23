# Blockflow achievement ikonları — ChatGPT görsel promptları

Steam yalnızca **achieved** (renkli) ikonu istiyor; "Automatically generate unachieved icons" açık olduğu için gri hâlini kendisi üretiyor.

44 ikonu tek görselde istemek detayı bozar. İlk 40 için 5 parti hâlinde (Classic'in 4 ikonu ayrı: Parti 6), her seferinde **8 ikonluk 4×2 ızgara** üret. Her partide önce STİL bloğunu, sonra o partinin listesini yapıştır. Çıkan görseli 8 kareye kes, her kareyi 256×256 PNG olarak `API_ADI_ACHIEVED.png` adıyla kaydet (ör. `ACH_FIRST_CLEAR_ACHIEVED.png`) ve Steam'deki Achievement Icons alanına toplu yükle.

---

## STİL (her partide aynen yapıştır)

```
Create a sprite sheet of 8 square game achievement icons arranged in a 4x2 grid, each icon in its own equal square cell with a thin gap between cells, on a plain flat #0d0b1a background. No text, no letters, no numbers unless I list them below, no watermark.

Art style for every icon (keep it identical across all 8):
- Mobile/Steam puzzle game "Blockflow", a neon arcade block puzzle.
- Each icon is a rounded-square badge with a beveled metallic frame in deep violet (#3a2f66) with a soft purple-magenta rim glow, like a sci-fi arcade cabinet panel.
- Inside the badge: a dark indigo field (#17132b) and one bold, centered symbol built from glossy square puzzle blocks with soft bevels and a subtle inner highlight, slightly 2.5D (seen almost head-on, tiny depth).
- Block palette: sky blue #60B0F0, violet #7848D0, green #5DDA6A, amber #FFC040, plus magenta #ff8fd0 and red #ff6b7a as accents. Bright, saturated, glowing edges, light bloom.
- Strong readable silhouette at 64x64 pixels: big simple shapes, high contrast, nothing thin or tiny.
- Same lighting (top-left key light), same frame, same scale and centering in every cell.
- Family-friendly, no violence, no real-world logos.
```

---

## Parti 1 — İlerleme + Skor (ilk 2)

```
The 8 icons, left to right, top row then bottom row:
1. ACH_FIRST_CLEAR — a single horizontal row of 8 blue blocks dissolving into bright sparkles on the right side.
2. ACH_FIRST_MILESTONE — a small amber flag planted on top of a stack of three blocks.
3. ACH_STAGE_5 — a staircase of blocks climbing up with 5 steps, a glowing amber arrow at the top.
4. ACH_STAGE_10 — a taller staircase of blocks, violet and blue, with a glowing star at the top.
5. ACH_STAGE_20 — a tower of blocks reaching into a starry glow, swirling flow lines around it.
6. ACH_ALL_STAGES — a golden crown made of amber blocks sitting on a full block grid, radiant golden glow (the most premium icon of the set, extra gold rim on the frame).
7. ACH_SCORE_10K — a short stack of shiny amber coins made of block-shapes with one rising sparkle.
8. ACH_SCORE_100K — a bigger stack of amber block-coins with a bright upward burst.
```

## Parti 2 — Skor (son 2) + Beceri

```
The 8 icons, left to right, top row then bottom row:
1. ACH_SCORE_1M — a treasure pile of amber block-coins overflowing with a radiant glow.
2. ACH_SCORE_10M — a block-built rocket blasting upward off the top edge with an amber flame trail.
3. ACH_DOUBLE_CLEAR — two parallel horizontal block rows exploding at the same time with twin light beams.
4. ACH_TRIPLE_CLEAR — three parallel block rows bursting together, three light beams.
5. ACH_QUAD_CLEAR — four block rows shattering into a large bright explosion of block shards.
6. ACH_CROSSFIRE — a horizontal row and a vertical column of blocks crossing in a plus sign, glowing hot white at the intersection.
7. ACH_PERFECT_CLEAR — an empty, perfectly clean 8x8 grid with a soft shining sweep of light across it and a small sparkle.
8. ACH_MAX_CHARGE — three charge bars of increasing height, all fully lit blue, the tallest one pulsing with an electric amber glow.
```

## Parti 3 — Beceri (son 4) + Kartlar + Meta (ilk 1)

```
The 8 icons, left to right, top row then bottom row:
1. ACH_COMBO_X2 — a big glowing amber "x2" made of chunky blocks. (Text "x2" allowed here.)
2. ACH_COMBO_X5 — a big glowing violet "x5" made of chunky blocks with flames. (Text "x5" allowed here.)
3. ACH_COMBO_X10 — a big glowing "x10" made of chunky blocks, rainbow block colours, intense blaze. (Text "x10" allowed here.)
4. ACH_QUICK_MILESTONE — a lightning bolt made of amber blocks striking a small flag.
5. ACH_FIRST_CARD — a single glowing playing card with a block symbol on its face, tilted slightly.
6. ACH_CARDS_10 — a fanned hand of many glowing cards, block symbols on each.
7. ACH_EPIC_CARD — one card with a purple holographic shimmer and a gem made of violet blocks, epic aura.
8. ACH_TUTORIAL — a graduation cap made of blocks resting on a small block grid.
```

## Parti 4 — Meta + Ömür boyu

```
The 8 icons, left to right, top row then bottom row:
1. ACH_FIRST_SHAPE_UNLOCK — an open padlock next to a glowing L-shaped tetromino piece.
2. ACH_ALL_SHAPES — a display shelf holding many different tetromino-style pieces in all palette colours, golden glow.
3. ACH_SHAPE_UPGRADE — a single block piece with an upward green arrow and polishing sparkles.
4. ACH_FIRST_UPGRADE — a small green plant sprout growing from a single amber block-coin.
5. ACH_LINES_1000 — a long conveyor of block rows flowing to the right, motion streaks.
6. ACH_PIECES_5000 — a huge neat stack of mixed coloured pieces forming a tall wall.
7. ACH_COINS_25K — a treasure vault door made of blocks, slightly open, amber light pouring out.
8. ACH_RUNS_50 — a glowing circular loop arrow made of blocks around a small 8x8 board.
```

## Parti 5 — Gizli olanlar

```
These 8 are secret achievements: same style, but give each badge frame a subtle red-magenta (#ff6b7a) rim glow instead of purple, to mark them as rare secrets.
The 8 icons, left to right, top row then bottom row:
1. ACH_SECRET_LAST_GASP — an hourglass made of blocks with only one last block falling through.
2. ACH_SECRET_MONOCHROME — a full row of 8 blocks all the exact same green colour, glowing.
3. ACH_SECRET_ZERO_HERO — a painter's palette and brush made of blocks with abstract coloured blocks scattered on a canvas.
4. ACH_SECRET_DIZZY — a single T-shaped piece spinning with circular motion swirls and little stars around it.
5. ACH_SECRET_STIFF — a straight vertical I-shaped piece standing perfectly upright, a ruler line beside it.
6. ACH_SECRET_CLOSE_CALL — a block piece hanging over the edge of a cliff of blocks, saved by a glowing hand-shaped beam.
7. ACH_SECRET_LUCKY — a four-leaf clover made of green blocks beside a block die showing six.
8. ACH_SECRET_BLACK_CAT — a cute black cat silhouette made of dark blocks with glowing amber eyes beside a block die showing one.
```

## Parti 6 — Classic Mode (4 ikon)

Classic sonradan eklendi; yalnızca 4 ikon olduğu için **4×1 şerit** üretilir. STİL bloğundaki "8 square … 4x2 grid" ifadesini "4 square … 4x1 row" olarak değiştirerek yapıştır. Classic'in ayırt edici işareti: çerçevenin rim glow'u mor yerine **sakin camgöbeği (#5fd8e8)**. Classic sade ve sakin bir mod, bu ikonlar da daha az patlamalı, daha dingin olmalı. Gizli olan (No Rush) kırmızı-magenta rim alır, Parti 5'teki gibi.

```
Match the exact style of the previous sheets, but make 4 icons in a 4x1 row.
These are for the calm "Classic" mode: give icons 1-3 a soft cyan (#5fd8e8) rim glow instead of purple, and keep the mood calm and clean: fewer sparks, gentle glow, uncluttered.
Icon 4 is a secret achievement: give it the red-magenta (#ff6b7a) secret rim glow instead.
The 4 icons, left to right:
1. ACH_CLASSIC_SCORE_5K — a retro arcade-style 8x8 block board seen head-on with a single completed row glowing softly, a small amber block-coin resting on top. Simple, nostalgic, "old school".
2. ACH_CLASSIC_SCORE_20K — an ace playing-card shape built from cyan and amber blocks, a calm radiant halo behind it, one small star at the top corner. The most premium of the four: slightly brighter frame.
3. ACH_CLASSIC_LINES_100 — a zen stone cairn made of three stacked rounded blocks (violet, blue, green) balanced perfectly, a few thin horizontal light lines drifting past like calm water.
4. ACH_SECRET_CLASSIC_NO_RUSH — a sleepy turtle whose shell is made of glossy square blocks, a small analog clock made of blocks beside it, a few soft round dots floating above the turtle like sleep bubbles (no letters).
```

---

### İpuçları
- Bir parti beğenilmezse aynı sohbette "keep exactly the same style, regenerate only icon 3" de; stil korunur.
- 5 partinin aynı stilde çıkması için hepsini **aynı ChatGPT sohbetinde** sırayla üret ve 2. partiden itibaren başa "Match the exact style of the previous sheet." ekle.
