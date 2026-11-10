#!/usr/bin/env bash
# Runs the EditMode tests in the open Editor and prints the totals and any failures.
# Unity's test runner can't start while the Editor is in Play mode ("This cannot be used during play mode"), so this
# leaves Play mode first and waits until the Editor is idle. Usage: Tools/unity-test.sh [timeout-seconds]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
U=(unity command --project-path "$ROOT")
RESULTS="$HOME/.config/unity3d/Rushing Technologies/Last Tide/TestResults.xml"

unity status --until-ready --project-path "$ROOT" >/dev/null 2>&1 || true
"${U[@]}" script eval --result-only 'UnityEditor.EditorApplication.isPlaying = false; return 0;' >/dev/null
for _ in $(seq 1 60); do
  if "${U[@]}" script eval --result-only 'return UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling;' | grep -q '"result": false'; then break; fi
  sleep 1
done

before=$(stat -c %Y "$RESULTS" 2>/dev/null || echo 0)
"${U[@]}" test run editor --timeout "${1:-600}" --result-only >/dev/null
after=$(stat -c %Y "$RESULTS" 2>/dev/null || echo 0)
if [ "$after" = "$before" ]; then echo "tests did not run (no new $RESULTS): see the Editor log"; exit 1; fi
python3 -I - "$RESULTS" <<'PY'
import re, sys
t = open(sys.argv[1]).read()
run = re.search(r'<test-run[^>]*>', t).group(0)
get = lambda k: re.search(k + r'="([^"]*)"', run).group(1)
print("tests: %s passed, %s failed, %s skipped (of %s)" % (get("passed"), get("failed"), get("skipped"), get("total")))
for name in re.findall(r'<test-case[^>]*fullname="([^"]*)"[^>]*result="Failed"', t): print("  FAILED", name)
sys.exit(0 if get("result") == "Passed" else 1)
PY
