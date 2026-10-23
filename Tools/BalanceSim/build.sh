#!/usr/bin/env bash
# Unity'nin içindeki Roslyn + .NET 6 runtime ile derler; ayrı .NET SDK gerekmez.
#   ./build.sh            → bin/BalanceSim.dll
#   ./run.sh baseline 1000
set -e
cd "$(dirname "$0")"
UNITY="${UNITY_EDITOR_DIR:-/c/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor}"
NET="$UNITY/Data/NetCoreRuntime"
FW=$(ls -d "$NET"/shared/Microsoft.NETCore.App/* | head -1)
mkdir -p bin
RSP=bin/build.rsp
echo "-nologo -noconfig -nostdlib -optimize+ -langversion:latest -nullable:disable -target:exe -out:bin/BalanceSim.dll" > $RSP
for f in "$FW"/System*.dll "$FW"/mscorlib.dll "$FW"/netstandard.dll; do
  case "$(basename "$f")" in
    *Native*) ;;
    *) echo "-r:\"$(cygpath -w "$f")\"" >> $RSP ;;
  esac
done
for f in *.cs; do echo "$f" >> $RSP; done
"$NET/dotnet.exe" "$UNITY/Data/DotNetSdkRoslyn/csc.dll" @$RSP
VER=$(basename "$FW")
cat > bin/BalanceSim.runtimeconfig.json <<JSON
{ "runtimeOptions": { "tfm": "net6.0", "framework": { "name": "Microsoft.NETCore.App", "version": "$VER" },
  "configProperties": { "System.GC.Server": true, "System.GC.Concurrent": false } } }
JSON
echo "built bin/BalanceSim.dll ($VER)"
