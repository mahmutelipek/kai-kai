#!/usr/bin/env bash
# Compile-checks Game.Runtime, Game.Editor and Game.Tests against Unity reference assemblies (see fetch-refs.sh).
set -euo pipefail
cd "$(dirname "$0")"
[ -d refs ] || ./fetch-refs.sh
for p in Runtime/Game.Runtime.csproj Editor/Game.Editor.csproj Tests/Game.Tests.csproj Steam/Game.Steam.csproj; do
  echo "== $p"; dotnet build "$p" -nologo -v q -clp:NoSummary
done
