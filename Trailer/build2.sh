#!/usr/bin/env bash
# Blockflow trailer v2 — roguelite döngüsünü bölüm bölüm anlatan kurgu.
#   Takes/*.mp4 (TrailerDirector + Unity Recorder) → Out/${OUTNAME:-Blockflow_Trailer_v2}.mp4
#
# Yapı taşları (edl2.sh bunları sırayla çağırır):
#   title  <arka plan take> <sn> <süre> "<küçük satır>" "<BÜYÜK SATIR>" <vurgu rengi>
#          Tam ekran bölüm kartı: bulanık oyun karesi üstünde büyük yazı, vuruşla girer.
#   zseg   <take> <başlangıç> <süre> <merkez x> <merkez y> <zoom başı> <zoom sonu> ["ETİKET" "açıklama" renk]
#          Yavaş itme yakınlaştırmalı oynanış; etiket verilirse soldan kayan büyük kart adı.
#   logo   <arka plan take> <sn> <süre> "<alt satır>" "<ikinci alt satır>"
#
# Ses: take'ler müziksiz (oyun SFX'i); altta tek parça Gameplay_Loop, bölüm kartlarında darbe sesi.
set -euo pipefail
cd "$(dirname "$0")"
FF="${FFMPEG_DIR:-/c/Users/faint/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-9.0.2-full_build/bin}"
FB="../Assets/Resources/Fonts/Exo_2/static/Exo2-Black.ttf"
FS="../Assets/Resources/Fonts/Exo_2/static/Exo2-Bold.ttf"
LOGO="../Assets/Arts/UI/mainmenu_ui_assets/Library_Logo.png"
MUSIC="../Assets/Arts/Sounds/Gameplay_Loop.ogg"
THUD="../Assets/Arts/Sounds/SFX/Solidthud (4).ogg"
W=Work/build2; mkdir -p "$W" Out
ENC=(-c:v libx264 -preset slow -crf 15 -pix_fmt yuv420p -r 60 -c:a aac -b:a 256k -ar 48000 -ac 2)
calc() { awk "BEGIN{print $1}"; }

n=0
next_out() { n=$((n+1)); OUT="$W/$(printf %02d $n).mp4"; echo "$OUT" >> "$W/list.tmp"; }

# Vuruş ölçeği: 0.16 sn'de 1.38 → 1.0 (ease-out)
SLAM="(1+0.38*pow(max(0\,1-t/0.16)\,2))"

# ── Bölüm kartı ─────────────────────────────────────────────────────────────
title() {
  local take="$1" at="$2" d="$3" small="$4" big="$5" col="$6"
  next_out
  local fo; fo=$(calc "$d-0.12")
  local bigsize=190; [[ ${#big} -gt 13 ]] && bigsize=150; [[ ${#big} -gt 17 ]] && bigsize=124
  "$FF/ffmpeg" -v error -y -ss "$at" -t "$d" -i "Takes/$take.mp4" \
    -f lavfi -t "$d" -i "color=c=black@0:s=1920x1080:r=60,format=rgba" \
    -i "$THUD" \
    -filter_complex "\
[0:v]setpts=PTS-STARTPTS,gblur=sigma=26,eq=brightness=-0.17:saturation=0.9,vignette=PI/3.6[bg];\
[1:v]drawtext=expansion=none:fontfile='$FB':text='$big':fontsize=$bigsize:fontcolor=$col:x=(w-text_w)/2:y=(h-text_h)/2+40,gblur=sigma=22[glow];\
[1:v]drawtext=expansion=none:fontfile='$FS':text='$small':fontsize=58:fontcolor=white@0.92:x=(w-text_w)/2:y=(h/2)-150,\
drawtext=expansion=none:fontfile='$FB':text='$big':fontsize=$bigsize:fontcolor=$col:borderw=6:bordercolor=0x050a16:x=(w-text_w)/2:y=(h-text_h)/2+40[txt];\
[glow][txt]overlay[layer];\
[layer]scale=w='trunc(1920*$SLAM/2)*2':h=-2:eval=frame[sl];\
[bg][sl]overlay=x=(W-w)/2:y=(H-h)/2[ov];\
color=c=$col:s=560x12:r=60:d=$d[bar];\
[ov][bar]overlay=x='(W-560*min(1\,t/0.25))/2':y=H/2+168,fade=t=in:st=0:d=0.07:color=white,fade=t=out:st=$fo:d=0.12[v];\
[2:a]atrim=0:$d,apad=whole_dur=$d,volume=1.1[a]" \
    -map "[v]" -map "[a]" -t "$d" "${ENC[@]}" "$OUT"
}

# ── Yakınlaştırmalı oynanış (+ isteğe bağlı kart etiketi) ─────────────────────
zseg() {
  local take="$1" ss="$2" d="$3" cx="$4" cy="$5" z0="$6" z1="$7" lab="${8:-}" sub="${9:-}" col="${10:-0x37e6ff}" ly="${LABEL_Y:-800}"
  next_out
  local frames; frames=$(calc "int($d*60)")
  local zexpr="$z0+($z1-$z0)*on/$frames"
  local vf="setpts=PTS-STARTPTS,scale=3840:-2:flags=lanczos,zoompan=z='$zexpr':x='max(0\,min(iw-iw/zoom\,$cx*2-iw/zoom/2))':y='max(0\,min(ih-ih/zoom\,$cy*2-ih/zoom/2))':d=1:s=1920x1080:fps=60"
  if [[ -n "$lab" ]]; then
    # soldan kayan etiket paneli: vurgu çubuğu + büyük kart adı + açıklama
    local sx="(-1150+1150*min(1\,max(0\,(t-0.15)/0.22)))"
    vf="$vf,drawbox=x='$sx':y=$ly:w=1150:h=200:color=0x060b18@0.88:t=fill,drawbox=x='$sx':y=$ly:w=16:h=200:color=$col:t=fill"
    vf="$vf,drawtext=expansion=none:fontfile='$FB':text='$lab':fontsize=86:fontcolor=$col:x='$sx+60':y=$((ly+18))"
    vf="$vf,drawtext=expansion=none:fontfile='$FS':text='$sub':fontsize=42:fontcolor=white:x='$sx+62':y=$((ly+130))"
  fi
  "$FF/ffmpeg" -v error -y -ss "$ss" -t "$d" -i "Takes/$take.mp4" -vf "$vf" \
    -af "asetpts=PTS-STARTPTS,afade=t=in:d=0.03,afade=t=out:st=$(calc "$d-0.05"):d=0.05" \
    -t "$d" "${ENC[@]}" "$OUT"
}

# ── Logo kartı ──────────────────────────────────────────────────────────────
logo() {
  local take="$1" at="$2" d="$3" l1="${4:-}" l2="${5:-}"
  next_out
  local fo; fo=$(calc "$d-0.35")
  local extra=""
  [[ -n "$l1" ]] && extra="$extra,drawtext=expansion=none:fontfile='$FB':text='$l1':fontsize=96:fontcolor=white:borderw=4:bordercolor=0x050a16:x=(w-text_w)/2:y=h*0.62:alpha='min(1\,max(0\,(t-0.35)/0.25))'"
  [[ -n "$l2" ]] && extra="$extra,drawtext=expansion=none:fontfile='$FS':text='$l2':fontsize=54:fontcolor=0x7fe8ff:x=(w-text_w)/2:y=h*0.62+120:alpha='min(1\,max(0\,(t-0.6)/0.25))'"
  "$FF/ffmpeg" -v error -y -ss "$at" -t "$d" -i "Takes/$take.mp4" -loop 1 -t "$d" -i "$LOGO" -i "$THUD" \
    -filter_complex "\
[0:v]setpts=PTS-STARTPTS,gblur=sigma=30,eq=brightness=-0.28:saturation=0.7,vignette=PI/3.4[bg];\
[1:v]scale=1500:-1,format=rgba,scale=w='trunc(1500*$SLAM/2)*2':h=-2:eval=frame[lg];\
[bg][lg]overlay=x=(W-w)/2:y=(H-h)/2-110:shortest=1$extra,fade=t=in:st=0:d=0.07:color=white,fade=t=out:st=$fo:d=0.35[v];\
[2:a]atrim=0:$d,apad=whole_dur=$d[a]" \
    -map "[v]" -map "[a]" -t "$d" "${ENC[@]}" "$OUT"
}

rm -f "$W/list.tmp" "$W"/*.mp4
source "./${EDL:-edl2.sh}"

sed 's|^Work/build2/|file |' "$W/list.tmp" > "$W/list.txt"
"$FF/ffmpeg" -v error -y -f concat -safe 0 -i "$W/list.txt" -c copy "$W/joined.mp4"
total=$("$FF/ffprobe" -v error -show_entries format=duration -of csv=p=0 "$W/joined.mp4")
fo=$(calc "$total-2.5")
# müzik 58 sn; iki kopya 2 sn çapraz geçişle birleşir (sert döngü dikişi duyulmasın)
"$FF/ffmpeg" -v error -y -i "$W/joined.mp4" -i "$MUSIC" -i "$MUSIC" -filter_complex \
  "[1:a][2:a]acrossfade=d=2:c1=tri:c2=tri,atrim=0:${total},asetpts=PTS-STARTPTS,volume=0.75,afade=t=in:d=0.3,afade=t=out:st=${fo}:d=2.5[m];\
[0:a]volume=0.9[s];[m][s]amix=inputs=2:duration=first:normalize=0,alimiter=limit=0.95[a]" \
  -map 0:v -map "[a]" -c:v copy -c:a aac -b:a 256k Out/${OUTNAME:-Blockflow_Trailer_v2}.mp4
echo "Out/${OUTNAME:-Blockflow_Trailer_v2}.mp4  (${total}s)"
