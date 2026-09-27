#!/usr/bin/env bash
# Compile-check the Cyverse C# scripts WITHOUT a Unity editor.
#
# For cloud / CI-less sessions that can't open Unity: builds the same assembly
# graph the player uses (uGUI -> TextMeshPro -> Assembly-CSharp) plus the
# PlayMode test assembly, using Roslyn on Mono against Unity's public
# reference assemblies. It catches missing types, wrong signatures and syntax
# errors; it does NOT check Editor-only code (no UnityEditor.dll) or anything
# that only shows up at runtime / in Play mode.
#
# Requirements: mono (apt: mono-devel), curl, git, unzip.
# Usage:        Tools/compile-check.sh            # release + development
# Cache:        $CYVERSE_CHECK_CACHE (default ~/.cache/cyverse-compile-check)
#
# Reference versions: UnityEngine.Modules 2021.3.33 (closest on NuGet to the
# project's 2022.3), uGUI from Unity 2021.1, TextMeshPro 3.0.9 (= manifest).
# A clean result here is necessary, not sufficient — still open the editor.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CACHE="${CYVERSE_CHECK_CACHE:-$HOME/.cache/cyverse-compile-check}"
mkdir -p "$CACHE/out"
cd "$CACHE"

fetch_nupkg() { # id version dir
  [ -d "$3" ] && return
  curl -sSfL -o "$3.nupkg" "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg"
  mkdir -p "$3" && (cd "$3" && unzip -qo "../$3.nupkg")
}
fetch_nupkg microsoft.net.compilers.toolset 4.8.0 roslyn
fetch_nupkg unityengine.modules 2021.3.33 unity
fetch_nupkg nunit 3.5.0 nunit
[ -d ugui ] || git clone -q --depth 1 --branch "1.0.0/Unity-2021.1.17f1" https://github.com/needle-mirror/com.unity.ugui ugui
[ -d tmp ]  || git clone -q --depth 1 --branch 3.0.9 https://github.com/needle-mirror/com.unity.textmeshpro tmp

FW=/usr/lib/mono/4.7.1-api
REFS=(-noconfig -nostdlib "-r:$FW/mscorlib.dll" "-r:$FW/System.dll" "-r:$FW/System.Core.dll"
      "-r:$FW/System.Xml.dll" "-r:$FW/System.Runtime.Serialization.dll" "-r:$FW/Facades/netstandard.dll")
for d in unity/lib/net45/*.dll; do REFS+=("-r:$CACHE/$d"); done
CSC=(mono "$CACHE/roslyn/tasks/net472/csc.exe" -nologo -langversion:9 -unsafe -target:library
     -nowarn:0618,0414,0649,0169,0162,0219,0067,1998,0108)
BASE_DEFINES="UNITY_2022_3_OR_NEWER;UNITY_2021_1_OR_NEWER;UNITY_2020_1_OR_NEWER;UNITY_2019_4_OR_NEWER;UNITY_2019_1_OR_NEWER;UNITY_2018_1_OR_NEWER;UNITY_5_3_OR_NEWER;UNITY_WEBGL;ENABLE_LEGACY_INPUT_MANAGER;PACKAGE_PHYSICS;PACKAGE_PHYSICS2D;PACKAGE_ANIMATION;PACKAGE_TILEMAP"

[ -f out/UnityEngine.UI.dll ] || "${CSC[@]}" "${REFS[@]}" -define:"$BASE_DEFINES" \
  -out:out/UnityEngine.UI.dll -recurse:"$CACHE/ugui/Runtime/*.cs" >/dev/null
[ -f out/Unity.TextMeshPro.dll ] || "${CSC[@]}" "${REFS[@]}" -r:out/UnityEngine.UI.dll -define:"$BASE_DEFINES" \
  -out:out/Unity.TextMeshPro.dll -recurse:"$CACHE/tmp/Scripts/Runtime/*.cs" >/dev/null

# Unity's [UnityTest] lives in UnityEngine.TestRunner, which isn't on NuGet.
cat > out/TestToolsStub.cs <<'EOF'
namespace UnityEngine.TestTools { [System.AttributeUsage(System.AttributeTargets.Method)] public class UnityTestAttribute : System.Attribute {} }
EOF

mapfile -t SCRIPTS < <(cd "$ROOT" && find Assets/Scripts -name '*.cs' | sort)
status=0
for config in release development; do
  defines="$BASE_DEFINES"; [ "$config" = development ] && defines="$defines;DEVELOPMENT_BUILD"
  echo "== Assembly-CSharp ($config)"
  if ! (cd "$ROOT" && "${CSC[@]}" "${REFS[@]}" -r:"$CACHE/out/UnityEngine.UI.dll" \
        -r:"$CACHE/out/Unity.TextMeshPro.dll" -define:"$defines" \
        -out:"$CACHE/out/Assembly-CSharp.dll" "${SCRIPTS[@]}"); then status=1; continue; fi
  echo "== Cyverse.PlayModeTests ($config)"
  (cd "$ROOT" && "${CSC[@]}" "${REFS[@]}" -r:"$CACHE/nunit/lib/net45/nunit.framework.dll" \
     -r:"$CACHE/out/UnityEngine.UI.dll" -r:"$CACHE/out/Unity.TextMeshPro.dll" \
     -r:"$CACHE/out/Assembly-CSharp.dll" -define:"$defines;UNITY_INCLUDE_TESTS" \
     -out:"$CACHE/out/Cyverse.PlayModeTests.dll" "$CACHE/out/TestToolsStub.cs" Assets/Tests/PlayMode/*.cs) || status=1
done

[ $status -eq 0 ] && echo "compile-check: OK" || echo "compile-check: FAILED"
exit $status
