#!/usr/bin/env bash
# Blockflow trailer kurgusu — Takes/*.mp4 (TrailerDirector + Unity Recorder) → Out/Blockflow_Trailer.mp4
#
# Segment tablosu aşağıda: take, başlangıç (sn), süre (sn), üst yazı. Kesimleri değiştirmek
# için yalnızca tabloyu düzenle ve script'i yeniden çalıştır. Kesim noktalarını seçmek için
# Trailer/sheet.sh ile zaman damgalı kare dizisi çıkar.
#
# Ses: take'ler müziksiz kaydedilir (yalnızca oyun SFX'i); tek parça Gameplay_Loop altta akar.
set -euo pipefail
cd "$(dirname "$0")"
FF="${FFMPEG_DIR:-/c/Users/faint/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-9.0.2-full_build/bin}"
FONT_B="../Assets/Resources/Fonts/Exo_2/static/Exo2-Black.ttf"
FONT_S="../Assets/Resources/Fonts/Exo_2/static/Exo2-SemiBold.ttf"
LOGO="../Assets/Arts/UI/mainmenu_ui_assets/Library_Logo.png"
MUSIC="../Assets/Arts/Sounds/Gameplay_Loop.ogg"
W=Work/build; mkdir -p "$W" Out
ENC=(-c:v libx264 -preset slow -crf 15 -pix_fmt yuv420p -r 60 -c:a aac -b:a 256k -ar 48000 -ac 2)

# ── Yazı: üstte ortalı, kısa kayarak girer, söner ───────────────────────────
caption() {   # $1 metin, $2 süre → drawtext filtresi
  local txt="$1" d="$2"
  local a="if(lt(t,0.3),t/0.3,if(gt(t,${d}-0.35),max(0,(${d}-t)/0.35),1))"
  printf "drawtext=fontfile='%s':text='%s':fontsize=66:fontcolor=white:alpha='%s':x=(w-text_w)/2:y=62-18*max(0\\,1-t/0.3):shadowcolor=0x05101e@0.85:shadowx=0:shadowy=4:borderw=2:bordercolor=0x0a1a30@0.9" \
    "$FONT_B" "$txt" "$a"
}

# ── Oynanış segmenti ────────────────────────────────────────────────────────
n=0
seg() {   # take start dur "caption" [flash]
  local take="$1" ss="$2" d="$3" cap="${4:-}" flash="${5:-}"
  n=$((n+1)); local out="$W/$(printf %02d $n).mp4"
  local vf="setpts=PTS-STARTPTS"
  [[ -n "$cap" ]] && vf="$vf,$(caption "$cap" "$d")"
  [[ "$flash" == flash ]] && vf="$vf,fade=t=in:st=0:d=0.18:color=white"
  "$FF/ffmpeg" -v error -y -ss "$ss" -t "$d" -i "Takes/$take.mp4" \
    -vf "$vf" -af "asetpts=PTS-STARTPTS,afade=t=in:d=0.03,afade=t=out:st=$(awk "BEGIN{print $d-0.05}"):d=0.05" \
    "${ENC[@]}" "$out"
  echo "$out" >> "$W/list.txt.tmp"
}

# ── Logo kartı: oynanış karesi bulanık + karartılmış, üstünde logo ve alt yazı ─
card() {  # take time dur "line1" "line2"
  local take="$1" at="$2" d="$3" l1="$4" l2="${5:-}"
  n=$((n+1)); local out="$W/$(printf %02d $n).mp4"
  local fo="$(awk "BEGIN{print $d-0.4}")"
  local t2=""
  [[ -n "$l2" ]] && t2=",drawtext=fontfile='${FONT_S}':text='${l2}':fontsize=40:fontcolor=0x7fe8ff:x=(w-text_w)/2:y=h*0.68:alpha='min(1,max(0,(t-0.5)/0.4))'"
  "$FF/ffmpeg" -v error -y -ss "$at" -t "$d" -i "Takes/$take.mp4" -loop 1 -t "$d" -i "$LOGO" \
    -f lavfi -t "$d" -i anullsrc=r=48000:cl=stereo \
    -filter_complex "[0:v]setpts=PTS-STARTPTS,gblur=sigma=26,eq=brightness=-0.22:saturation=0.8[bg];\
[1:v]scale=1240:-1,format=rgba,fade=t=in:st=0:d=0.35:alpha=1[lg];\
[bg][lg]overlay=x=(W-w)/2:y=(H-h)/2-70:shortest=1,\
drawtext=fontfile='${FONT_B}':text='${l1}':fontsize=46:fontcolor=white:x=(w-text_w)/2:y=h*0.60:alpha='min(1,max(0,(t-0.25)/0.4))'${t2},\
fade=t=out:st=${fo}:d=0.4[v]" \
    -map "[v]" -map 2:a "${ENC[@]}" "$out"
  echo "$out" >> "$W/list.txt.tmp"
}

rm -f "$W/list.txt.tmp" "$W"/*.mp4

# ═════════════════════════ KURGU ═════════════════════════════════════════
source ./edl.sh
# ═════════════════════════════════════════════════════════════════════════

# birleştir
sed 's/^/file /; s|file Work/build/|file |' "$W/list.txt.tmp" > "$W/list.txt"
"$FF/ffmpeg" -v error -y -f concat -safe 0 -i "$W/list.txt" -c copy "$W/joined.mp4"
total=$("$FF/ffprobe" -v error -show_entries format=duration -of csv=p=0 "$W/joined.mp4")

# müzik: baştan, sonda 2.5 sn sönerek; SFX müziğin altında ama duyulur
fo=$(awk "BEGIN{print $total-2.5}")
"$FF/ffmpeg" -v error -y -i "$W/joined.mp4" -stream_loop -1 -i "$MUSIC" -filter_complex \
  "[1:a]atrim=0:${total},asetpts=PTS-STARTPTS,volume=0.80,afade=t=in:d=0.4,afade=t=out:st=${fo}:d=2.5[m];\
[0:a]volume=0.85[s];[m][s]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.95[a]" \
  -map 0:v -map "[a]" -c:v copy -c:a aac -b:a 256k Out/Blockflow_Trailer.mp4
echo "Out/Blockflow_Trailer.mp4  (${total}s)"
