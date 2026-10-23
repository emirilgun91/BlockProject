# Kurgu listesi — build.sh tarafından source edilir.
# seg  <take> <başlangıç sn> <süre sn> "<üst yazı>" [flash]
# card <take> <arka plan karesi sn> <süre sn> "<satır 1>" "<satır 2>"
#
# Take'ler (TrailerDirector, her biri gerçek bir run'ın kesintisiz kaydı):
#   A_early  0 → 2 kart, puan 0 → 1.732, combo x1.1 → x2.4
#   B_mid    2 → 4 kart, puan 1.557 → 7.072 (son anda kurtarış: "2 left" → milestone)
#   C_late   4 → 6 kart, puan 7.777 → 20.840, combo x6.9 → x11.4

# Kanca: geç oyundan çarpıcı bir temizleme, beyaz flaşla açılır
seg  D_hook  23.7 2.2  ""                              flash
card C_late  20.0 2.8  "A ROGUELITE BLOCK PUZZLE"     ""

# Temel döngü
seg  A_early 1.8  5.2  "PLACE SHAPES. CLEAR LINES."
seg  A_early 12.0 5.6  "CHAIN CLEARS. STACK THE COMBO."

# Milestone → kart seçimi → kart etkileri
seg  B_mid   6.5  6.0  "HIT MILESTONES. DRAFT CARDS."
seg  B_mid   12.0 6.2  "BEND THE RULES."
seg  B_mid   33.5 9.4  "EVERY PIECE COUNTS."

# Geç oyun: büyük sayılar
seg  C_late  2.0  8.0  "BUILD A BROKEN RUN."
seg  C_late  15.8 9.0  "CHASE THE MULTIPLIER."

# Bitiş kartı
card C_late  28.5 5.5  "WISHLIST NOW ON STEAM"       "PLAY THE FREE DEMO"
