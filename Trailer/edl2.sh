# Trailer v2 kurgu listesi — build2.sh tarafından source edilir.
# Hikâye: bir run → milestone → kart → kartlar kuralı bozar → build büyür → risk →
#         run biter → coin harca, şekil aç → daha güçlü dön.
# Yazılarda virgül ve tek tırnak kullanma (ffmpeg filtre sözdizimi).
#
#   title <bg take> <sn> <süre> "<küçük>" "<BÜYÜK>" <renk>
#   zseg  <take> <başla> <süre> <cx> <cy> <zoom0> <zoom1> ["ETİKET" "açıklama" renk]
#   logo  <bg take> <sn> <süre> "<satır1>" "<satır2>"
#
# Take'ler (hepsi gerçek run; kart sayısı = ulaşılan milestone):
#   A_early 0→2 kart · B_mid 2→4 · C_late 4→6 (x11) · D_hook 4→5
#   E_lose_shop 3 kart → hamle yok → Game Over → Shop: Z puanı + 6SQUARE kilidi
#   F_newrun 6SQUARE açık yeni run · G_effects Neon Cable / Double Strike / Bounty Hunter

CYAN=0x37e6ff; VIOLET=0xb08cff; GOLD=0xffc63a; PINK=0xff6fa5; GREEN=0x5dff8a; RED=0xff5a5a
EPIC=0xd27dff; RARE=0x6fb6ff; UNCOMMON=0x5dff8a

# Kanca: x11 combo'da temizleme → logo
zseg  C_late      22.9 1.9  1230 450 1.25 1.40
logo  D_hook      20.0 2.2

# 01 — Run başlar
title A_early     3.0  1.15 "EVERY RUN"          "STARTS SMALL"        $CYAN
zseg  A_early     1.8  3.6   960 540 1.00 1.10

# 02 — Milestone
title A_early     6.5  1.15 "SCORE ENOUGH TO"    "HIT THE MILESTONE"   $VIOLET
zseg  A_early     5.7  1.9  1500 420 1.60 1.80

# 03 — Kart seçimi
title G_effects   10.0 1.15 "EVERY MILESTONE"    "DRAFT A CARD"        $GOLD
zseg  G_effects   9.0  3.6   900 560 1.30 1.45

# 04 — Kartlar kuralı bozar
title G_effects   26.0 1.15 "AND THOSE CARDS"    "BREAK THE RULES"     $PINK
zseg  G_effects   25.2 2.0  1240 480 1.30 1.45 "NEON CABLE"    "Clear one linked tile and the other detonates" $EPIC
zseg  G_effects   36.1 2.0  1240 470 1.30 1.45 "DOUBLE STRIKE" "Clear 2+ lines at once for x1.5 score"         $RARE
zseg  G_effects   44.4 2.0  1240 470 1.30 1.45 "BOUNTY HUNTER" "+1 coin per clear but targets rise 20%"        $UNCOMMON

# 05 — Build büyür (envanter: 1 → 3 → 5 → 6 kart)
title C_late      28.0 1.0  "CARD AFTER CARD"    "STACK YOUR BUILD"    $GREEN
zseg  A_early     13.0 0.7   400 320 2.40 2.55
zseg  B_mid       16.0 0.7   400 320 2.40 2.55
zseg  D_hook      22.6 0.7   400 320 2.40 2.55
zseg  C_late      28.7 0.9   400 320 2.40 2.55

# 06 — Risk: son parçayla milestone
title B_mid       38.0 1.15 "LIMITED SHAPES"     "PUSH YOUR LUCK"      $RED
zseg  B_mid       37.2 5.6   900 600 1.05 1.18

# 07 — Run biter, ilerleme kalır
zseg  E_lose_shop 3.6  4.6   960 540 1.00 1.12
title E_lose_shop 12.0 1.15 "WHEN THE RUN ENDS"  "SPEND YOUR COINS"    $GOLD
zseg  E_lose_shop 11.0 2.6   900 520 1.00 1.08
zseg  E_lose_shop 14.0 4.4   760 680 1.00 1.15

# 08 — Daha güçlü dön
title F_newrun    2.0  1.15 "UNLOCK. UPGRADE."   "COME BACK STRONGER"  $CYAN
zseg  F_newrun    0.2  2.8  1150 600 1.08 1.18
zseg  B_mid       17.6 1.2  1230 450 1.25 1.35
zseg  D_hook      2.6  1.3  1230 450 1.25 1.35
zseg  C_late      6.9  1.4  1230 450 1.25 1.35
zseg  C_late      19.4 1.6  1230 450 1.25 1.38

# Bitiş
logo  C_late      28.5 4.6  "WISHLIST NOW"       "ON STEAM"
