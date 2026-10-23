# Trailer v3 kurgu listesi (~85 sn) — EDL=edl3.sh OUTNAME=Blockflow_Trailer_v3 bash build2.sh
# Hikâye: run → milestone → kart → kartlar kuralı bozar → build büyür → risk → run biter →
#         coin harca (şekil + kalıcı upgrade) → daha güçlü dön.
# Yazılarda virgül ve tek tırnak kullanma (ffmpeg filtre sözdizimi).
#
# Take'ler (hepsi karo düzeltmesinden sonra, gerçek run; kart sayısı = ulaşılan milestone):
#   A_early 0→2 kart · B_mid 2→4 · C_late 4→6 (x9.6, 6SQUARE açık) · D_hook 5→6 (x10.3)
#   E_lose_shop 3 kart → hamle yok → Shop: Z puanı + 6SQUARE kilidi · F_newrun 6SQUARE ilk havuzda
#   G_effects Neon Cable / Double Strike / Bounty Hunter · H_upgrades menü: 3 kalıcı upgrade
#   I_clutch 3 şekil kala 3475/3500 → milestone

CYAN=0x37e6ff; VIOLET=0xb08cff; GOLD=0xffc63a; PINK=0xff6fa5; GREEN=0x5dff8a; RED=0xff5a5a; ORANGE=0xff9a3c
EPIC=0xd27dff; RARE=0x6fb6ff; UNCOMMON=0x5dff8a

# Kanca: x9.2 combo'da çift satır, Double Strike ile +2901 → logo
zseg  D_hook      3.4  2.2  1230 450 1.25 1.40
logo  D_hook      6.0  2.6

# 01 — Run başlar
title A_early     3.0  1.4  "EVERY RUN"          "STARTS SMALL"        $CYAN
zseg  A_early     0.3  5.0   960 540 1.00 1.10

# 02 — Milestone
title A_early     6.0  1.4  "SCORE ENOUGH TO"    "HIT THE MILESTONE"   $VIOLET
zseg  A_early     5.0  2.8  1500 420 1.60 1.80

# 03 — Kart seçimi
title G_effects   10.0 1.4  "EVERY MILESTONE"    "DRAFT A CARD"        $GOLD
zseg  G_effects   9.0  3.8   900 560 1.30 1.45

# 04 — Kartlar kuralı bozar
title G_effects   26.0 1.4  "AND THOSE CARDS"    "BREAK THE RULES"     $PINK
zseg  G_effects   24.9 2.8  1240 480 1.28 1.42 "NEON CABLE"    "Clear one linked tile and the other detonates" $EPIC
zseg  G_effects   35.7 2.8  1240 470 1.28 1.42 "DOUBLE STRIKE" "Clear 2+ lines at once for x1.5 score"         $RARE
zseg  G_effects   44.0 2.8  1240 470 1.28 1.42 "BOUNTY HUNTER" "+1 coin per clear but targets rise 20%"        $UNCOMMON

# 05 — Build büyür (envanter: 1 → 3 → 5 → 6 kart)
title C_late      28.0 1.3  "CARD AFTER CARD"    "STACK YOUR BUILD"    $GREEN
zseg  A_early     12.0 1.0   400 320 2.40 2.55
zseg  B_mid       24.0 1.0   400 320 2.40 2.55
zseg  D_hook      1.0  1.0   400 320 2.40 2.55
zseg  C_late      28.4 1.3   400 320 2.40 2.55

# 06 — Risk: son şekillerle milestone
title I_clutch    1.0  1.4  "LIMITED SHAPES"     "PUSH YOUR LUCK"      $RED
zseg  I_clutch    0.0  4.8   900 560 1.05 1.18

# 07 — Run biter, ilerleme kalır
zseg  E_lose_shop 3.6  4.4   960 540 1.00 1.12
title E_lose_shop 12.0 1.4  "WHEN THE RUN ENDS"  "SPEND YOUR COINS"    $GOLD
zseg  E_lose_shop 11.0 2.6   900 520 1.00 1.08
zseg  E_lose_shop 14.2 4.2   760 680 1.00 1.15

# 08 — Kalıcı upgrade'ler (ana menü)
title H_upgrades  6.0  1.4  "AND INVEST IN"      "PERMANENT UPGRADES"  $ORANGE
zseg  H_upgrades  0.4  2.0   960 540 1.00 1.06
LABEL_Y=40 zseg H_upgrades 3.6 5.0  960 560 1.00 1.06 "POOL CAPACITY" "Each level adds 2 shapes to your pool"        $ORANGE
LABEL_Y=40 zseg H_upgrades 8.8 3.8  960 560 1.00 1.06 "COMBO POWER"   "Start every run with a higher multiplier"     $ORANGE
LABEL_Y=40 zseg H_upgrades 12.8 4.4 960 560 1.00 1.06 "COMBO BAR"     "One more charge slot so your combo holds longer" $ORANGE

# 09 — Daha güçlü dön
title F_newrun    2.0  1.4  "UNLOCK. UPGRADE."   "COME BACK STRONGER"  $CYAN
zseg  F_newrun    0.2  3.4  1150 600 1.08 1.18
zseg  B_mid       13.4 1.6  1230 450 1.25 1.35
zseg  C_late      2.5  1.8  1230 450 1.25 1.35
zseg  C_late      5.6  1.6  1230 450 1.25 1.38
zseg  C_late      9.8  1.6  1230 450 1.25 1.35
zseg  D_hook      7.6  1.8  1230 450 1.25 1.38

# Bitiş
logo  C_late      20.0 5.0  "WISHLIST NOW"       "ON STEAM"
