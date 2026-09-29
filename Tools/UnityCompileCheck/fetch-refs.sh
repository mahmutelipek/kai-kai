#!/usr/bin/env bash
# Downloads Unity reference assemblies from NuGet into ./refs so the Unity-side scripts can be
# compile-checked without a Unity install. NOTE: these are Unity 2021.3 (engine) / 2021.1 (editor)
# references, not Unity 6 - a pass here is a strong smoke test, not proof that Unity 6 compiles it.
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p refs/tmp
fetch() { # id version
  local f="refs/tmp/$1.$2.nupkg"
  [ -f "$f" ] || curl -sSL -o "$f" "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg"
  unzip -oq "$f" -d "refs/tmp/$1"
}
fetch unityengine.modules 2021.3.33
fetch unity3d.sdk 2021.1.14.1
cp refs/tmp/unityengine.modules/lib/netstandard2.0/*.dll refs/
cp refs/tmp/unity3d.sdk/lib/UnityEditor.dll refs/
echo "Unity reference assemblies in $(pwd)/refs"
