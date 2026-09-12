#!/bin/bash
# Compile-check every script without taking the Unity Editor lock.
#
# Unity's batchmode refuses to run while the Editor has the project open, which it usually does.
# This drives Unity's own bundled Roslyn against the same assemblies the Editor would use, so a
# clean run here means the Editor will also compile cleanly once it reloads.
#
#   Tools/compile_check.sh          game scripts only (fast)
#   Tools/compile_check.sh --editor game + Assets/Editor  (needs UnityEditor.dll)
#
# Exits non-zero on any compile error.

set -u
cd "$(dirname "$0")/.." || exit 1

UNITY=${UNITY:-/Applications/Unity/Hub/Editor/6000.4.10f1/Unity.app/Contents}
MANAGED="$UNITY/Resources/Scripting/Managed"
RSP=$(mktemp -t lqfarm-compile)
OUT=$(mktemp -t lqfarm-out)

WITH_EDITOR=0
[ "${1:-}" = "--editor" ] && WITH_EDITOR=1

{
  echo "-target:library"
  echo "-out:$OUT.dll"
  echo "-nostdlib+"
  echo "-langversion:latest"
  # 0169/0414/0649 unused fields, 0618 obsolete, 0162 unreachable — all noise here, not defects.
  echo "-nowarn:0169,0414,0649,0618,0162"
  # The project ships with the new Input System only; game code branches on this.
  echo "-define:ENABLE_INPUT_SYSTEM"

  echo "-r:$UNITY/Resources/Scripting/NetStandard/ref/2.1.0/netstandard.dll"

  # Managed/UnityEngine/ holds BOTH the UnityEngine modules and the modular UnityEditor ones
  # (UnityEditor.CoreModule.dll and friends live there, not next to the monolithic
  # Managed/UnityEditor.dll — referencing that as well defines every editor type twice).
  # Game scripts must not see the editor assemblies at all: a UnityEditor call in Assets/Scripts
  # compiles in the Editor but fails the player build, and this check exists to catch exactly
  # that class of thing before the build does.
  for d in "$MANAGED"/UnityEngine/*.dll; do
    case "${d##*/}" in
      UnityEditor*) [ "$WITH_EDITOR" = 1 ] || continue ;;
    esac
    echo "-r:$d"
  done
  # Packages compiled by the Editor: UnityEngine.UI, InputSystem, Newtonsoft, etc.
  #
  # Assembly-CSharp*.dll must be skipped. They are the Editor's own build of the very sources
  # being compiled here, so referencing them defines every type twice and buries the real
  # diagnostics under hundreds of CS0436 "conflicts with the imported type" warnings.
  for d in Library/ScriptAssemblies/*.dll; do
    case "${d##*/}" in Assembly-CSharp.dll|Assembly-CSharp-Editor.dll) continue ;; esac
    [ -f "$d" ] && echo "-r:$PWD/$d"
  done

  find Assets/Scripts -name '*.cs'
  [ "$WITH_EDITOR" = 1 ] && find Assets/Editor -name '*.cs'
} > "$RSP"

"$UNITY/Resources/Scripting/NetCoreRuntime/dotnet" \
  "$UNITY/Resources/Scripting/DotNetSdkRoslyn/csc.dll" "@$RSP"
code=$?

rm -f "$RSP" "$OUT" "$OUT.dll"
[ $code -eq 0 ] && echo "Biên dịch sạch."
exit $code
