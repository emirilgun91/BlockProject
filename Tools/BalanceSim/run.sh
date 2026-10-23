#!/usr/bin/env bash
cd "$(dirname "$0")"
UNITY="${UNITY_EDITOR_DIR:-/c/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor}"
"$UNITY/Data/NetCoreRuntime/dotnet.exe" bin/BalanceSim.dll "$@"
