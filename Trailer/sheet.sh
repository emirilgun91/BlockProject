#!/usr/bin/env bash
# Bir take'ten zaman damgalı kare dizisi (contact sheet) üretir — kesim noktası seçmek için.
# Kullanım: Trailer/sheet.sh Takes/B_mid.mp4 [aralık_sn=2] [başlangıç_sn=0]
set -euo pipefail
cd "$(dirname "$0")"
FF="${FFMPEG_DIR:-/c/Users/faint/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-9.0.2-full_build/bin}"
in="$1"; step="${2:-2}"; start="${3:-0}"
name="$(basename "$in" .mp4)"
mkdir -p Work
font="../Assets/Resources/Fonts/Exo_2/static/Exo2-Bold.ttf"
"$FF/ffmpeg" -v error -y -ss "$start" -i "$in" -vf \
  "fps=1/${step},scale=480:-1,drawtext=fontfile='${font}':text='%{pts\:hms\:${start}}':x=8:y=8:fontsize=28:fontcolor=yellow:box=1:boxcolor=black@0.6,tile=4x5" \
  -frames:v 1 "Work/${name}_sheet_${start}.png"
echo "Work/${name}_sheet_${start}.png"
