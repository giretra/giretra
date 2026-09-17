#!/usr/bin/env bash
# A/B benchmark: compare an agent in the working tree (candidate) against the
# same agent at a baseline git ref.
#
# Default mode benchmarks both versions against a common opponent with
# identical seeds, then tests the win-rate delta for statistical significance.
#
# --head-to-head mode seats the two versions at the same table instead: the
# baseline agent class and its factory are copied from the git ref into the
# working tree under a "*Baseline" name for the duration of the run, then both
# seat orders are benchmarked and the pooled win rate is tested against 50%.
# This is far more sensitive when the two versions are nearly identical.
#
# Usage:
#   ./giretra-ab.sh <Agent> [options]
#
# Options:
#   --opponent <name>   Opponent agent (default: CalculatingPlayer)
#   --baseline <ref>    Git ref for the baseline build (default: HEAD)
#   --head-to-head      Benchmark candidate vs baseline directly (no opponent)
#   -n <matches>        Matches per run (default: 1000); in head-to-head
#                       mode this is per seat order, so 2n matches in total
#   -s <seed>           Random seed, shared by both runs (default: 42)
#   -t <target>         Target score per match (default: 500)
#
# Examples:
#   ./giretra-ab.sh CuttingPlayer --opponent CalculatingPlayer -n 2000
#   ./giretra-ab.sh Eva --baseline origin/main --head-to-head -n 3000
#
# Notes:
#   - The baseline ref must include the benchmark --json option
#     (any commit from Aug 2026 onwards).
#   - In head-to-head mode only the agent class file is taken from the
#     baseline ref; shared helpers (e.g. PlayerAgentHelper) come from the
#     working tree. A warning is printed if the baseline diff touches other
#     files under src/Giretra.Core.
set -euo pipefail

usage() { sed -n '2,38p' "$0" | sed 's/^# \{0,1\}//'; }

AGENT=""
OPPONENT="CalculatingPlayer"
BASELINE="HEAD"
HEAD_TO_HEAD=0
MATCHES=1000
SEED=42
TARGET=500

while [ $# -gt 0 ]; do
  case "$1" in
    --opponent) OPPONENT="$2"; shift 2 ;;
    --baseline) BASELINE="$2"; shift 2 ;;
    --head-to-head) HEAD_TO_HEAD=1; shift ;;
    -n) MATCHES="$2"; shift 2 ;;
    -s) SEED="$2"; shift 2 ;;
    -t) TARGET="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    -*) echo "Unknown option: $1" >&2; usage >&2; exit 1 ;;
    *)
      if [ -n "$AGENT" ]; then echo "Unexpected argument: $1" >&2; exit 1; fi
      AGENT="$1"; shift ;;
  esac
done

if [ -z "$AGENT" ]; then usage >&2; exit 1; fi

REPO_ROOT="$(git rev-parse --show-toplevel)"
BASELINE_SHA="$(git -C "$REPO_ROOT" rev-parse --short "$BASELINE")"
WORK_DIR="$(mktemp -d)"
WORKTREE_DIR="$WORK_DIR/baseline-tree"
TEMP_FILES=()

cleanup() {
  git -C "$REPO_ROOT" worktree remove --force "$WORKTREE_DIR" 2>/dev/null || true
  for f in "${TEMP_FILES[@]-}"; do [ -n "$f" ] && rm -f "$f"; done
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

# ---------------------------------------------------------------------------
# Head-to-head mode
# ---------------------------------------------------------------------------
if [ "$HEAD_TO_HEAD" -eq 1 ]; then
  FACTORIES_DIR="src/Giretra.Core/Players/Factories"
  AGENTS_DIR="src/Giretra.Core/Players/Agents"

  # Resolve the agent to its factory source file (by AgentName or DisplayName).
  FACTORY_FILE="$(grep -liE "(AgentName|DisplayName) => \"$AGENT\"" \
    "$REPO_ROOT/$FACTORIES_DIR"/*.cs 2>/dev/null | head -n 1 || true)"
  if [ -z "$FACTORY_FILE" ]; then
    echo "ERROR: no factory in $FACTORIES_DIR declares AgentName or DisplayName '$AGENT'." >&2
    echo "Head-to-head mode only supports built-in Giretra.Core agents." >&2
    exit 1
  fi
  FACTORY_REL="$FACTORIES_DIR/$(basename "$FACTORY_FILE")"

  FACTORY_CLASS="$(grep -oE 'class [A-Za-z0-9_]+' "$FACTORY_FILE" | head -n 1 | awk '{print $2}')"
  AGENT_CLASS="$(grep -oE 'new [A-Za-z0-9_]+\(position' "$FACTORY_FILE" | head -n 1 | sed -E 's/new ([A-Za-z0-9_]+)\(.*/\1/')"
  AGENT_NAME="$(grep -oE 'AgentName => "[^"]+"' "$FACTORY_FILE" | head -n 1 | sed -E 's/.*"([^"]+)"/\1/')"
  DISPLAY_NAME="$(grep -oE 'DisplayName => "[^"]+"' "$FACTORY_FILE" | head -n 1 | sed -E 's/.*"([^"]+)"/\1/' || true)"
  DISPLAY_NAME="${DISPLAY_NAME:-$AGENT_NAME}"

  if [ -z "$FACTORY_CLASS" ] || [ -z "$AGENT_CLASS" ] || [ -z "$AGENT_NAME" ]; then
    echo "ERROR: could not parse factory/agent class names from $FACTORY_REL." >&2
    exit 1
  fi

  AGENT_FILE="$(grep -lE "class $AGENT_CLASS\b" "$REPO_ROOT/$AGENTS_DIR"/*.cs | head -n 1 || true)"
  if [ -z "$AGENT_FILE" ]; then
    echo "ERROR: could not find the source file declaring class $AGENT_CLASS in $AGENTS_DIR." >&2
    exit 1
  fi
  AGENT_REL="$AGENTS_DIR/$(basename "$AGENT_FILE")"

  for rel in "$AGENT_REL" "$FACTORY_REL"; do
    if ! git -C "$REPO_ROOT" cat-file -e "$BASELINE:$rel" 2>/dev/null; then
      echo "ERROR: $rel does not exist at baseline ref '$BASELINE'." >&2
      exit 1
    fi
  done

  BASE_AGENT_CLASS="${AGENT_CLASS}Baseline"
  BASE_FACTORY_CLASS="${FACTORY_CLASS}Baseline"
  BASE_AGENT_NAME="${AGENT_NAME}Baseline"
  BASE_DISPLAY_NAME="${DISPLAY_NAME}Baseline"
  BASE_AGENT_FILE="$REPO_ROOT/$AGENTS_DIR/$BASE_AGENT_CLASS.cs"
  BASE_FACTORY_FILE="$REPO_ROOT/$FACTORIES_DIR/$BASE_FACTORY_CLASS.cs"

  for f in "$BASE_AGENT_FILE" "$BASE_FACTORY_FILE"; do
    if [ -e "$f" ]; then
      echo "ERROR: $f already exists; refusing to overwrite it." >&2
      exit 1
    fi
  done

  echo "Agent:    $AGENT ($AGENT_CLASS; candidate: working tree, baseline: $BASELINE @ $BASELINE_SHA)"
  echo "Mode:     head-to-head   Matches: $MATCHES per seat order   Seed: $SEED   Target: $TARGET"
  echo

  OTHER_CHANGES="$(git -C "$REPO_ROOT" diff --name-only "$BASELINE" -- src/Giretra.Core \
    | grep -vxF -e "$AGENT_REL" -e "$FACTORY_REL" || true)"
  if [ -n "$OTHER_CHANGES" ]; then
    echo "WARNING: the working tree also differs from $BASELINE in shared Giretra.Core files;"
    echo "         the baseline copy will use the working-tree versions of these:"
    echo "$OTHER_CHANGES" | sed 's/^/           /'
    echo
  fi

  # Materialise the baseline agent and factory under "*Baseline" names.
  TEMP_FILES=("$BASE_AGENT_FILE" "$BASE_FACTORY_FILE")
  BASE_GUID="$(printf '%s' "$BASE_AGENT_NAME" | md5sum | cut -c1-32 \
    | sed -E 's/(.{8})(.{4})(.{4})(.{4})(.{12})/\1-\2-\3-\4-\5/')"

  git -C "$REPO_ROOT" show "$BASELINE:$AGENT_REL" \
    | sed -E "s/\b$AGENT_CLASS\b/$BASE_AGENT_CLASS/g" > "$BASE_AGENT_FILE"

  git -C "$REPO_ROOT" show "$BASELINE:$FACTORY_REL" \
    | sed -E "s/\b$AGENT_CLASS\b/$BASE_AGENT_CLASS/g" \
    | sed -E "s/\b$FACTORY_CLASS\b/$BASE_FACTORY_CLASS/g" \
    | sed -E "s/AgentName => \"$AGENT_NAME\"/AgentName => \"$BASE_AGENT_NAME\"/" \
    | sed -E "s/DisplayName => \"$DISPLAY_NAME\"/DisplayName => \"$BASE_DISPLAY_NAME\"/" \
    | sed -E "s/Guid\.Parse\(\"[0-9a-fA-F-]+\"\)/Guid.Parse(\"$BASE_GUID\")/" > "$BASE_FACTORY_FILE"

  echo "Building src/Giretra.Manage with the temporary baseline agent..."
  if ! (cd "$REPO_ROOT" && dotnet build src/Giretra.Manage --nologo -v quiet) >"$WORK_DIR/build.log" 2>&1; then
    echo "ERROR: build failed. Last 30 lines of the build log:" >&2
    tail -n 30 "$WORK_DIR/build.log" >&2
    exit 1
  fi

  run_h2h() { # <team1> <team2> <json-out> <log>
    (cd "$REPO_ROOT" && dotnet run --no-build --no-launch-profile --project src/Giretra.Manage -- \
      benchmark "$1" "$2" -n "$MATCHES" -s "$SEED" -t "$TARGET" \
      --quiet --no-save --json "$3") >"$4" 2>&1
  }

  echo "Running both seat orders in parallel..."
  run_h2h "$AGENT_NAME" "$BASE_AGENT_NAME" "$WORK_DIR/cand_first.json" "$WORK_DIR/cand_first.log" &
  A_PID=$!
  run_h2h "$BASE_AGENT_NAME" "$AGENT_NAME" "$WORK_DIR/base_first.json" "$WORK_DIR/base_first.log" &
  B_PID=$!
  wait "$A_PID" || true
  wait "$B_PID" || true

  FAILED=""
  for side in cand_first base_first; do
    if [ ! -f "$WORK_DIR/$side.json" ]; then
      FAILED="${FAILED:+$FAILED and }$side"
      echo "ERROR: $side benchmark run failed. Last 30 lines of its log:" >&2
      tail -n 30 "$WORK_DIR/$side.log" >&2
      echo >&2
    fi
  done
  if [ -n "$FAILED" ]; then exit 1; fi
  echo

  python3 - "$WORK_DIR/cand_first.json" "$WORK_DIR/base_first.json" "$AGENT" "$BASELINE_SHA" <<'PY'
import json, math, sys

a = json.load(open(sys.argv[1]))   # candidate = team1, baseline = team2
b = json.load(open(sys.argv[2]))   # baseline = team1, candidate = team2
agent, sha = sys.argv[3], sys.argv[4]

rows = [
    ("candidate first", a["team1"]["wins"], a["team2"]["wins"], a["totalMatches"]),
    ("baseline first",  b["team2"]["wins"], b["team1"]["wins"], b["totalMatches"]),
]
cand_w = sum(r[1] for r in rows)
base_w = sum(r[2] for r in rows)
n = sum(r[3] for r in rows)
p = cand_w / n
delta = p - 0.5

se = math.sqrt(p * (1 - p) / n) if 0 < p < 1 else 0.0
margin = 1.96 * se
z = delta / math.sqrt(0.25 / n)
p_value = math.erfc(abs(z) / math.sqrt(2))

print(f"=== Head-to-head: {agent} (working tree) vs {agent} @ {sha} ===")
print(f"{'seat order':18}{'cand wins':>11}{'base wins':>11}{'matches':>9}{'cand rate':>11}")
for name, cw, bw, m in rows:
    print(f"{name:18}{cw:>11}{bw:>11}{m:>9}{cw / m:>10.1%}")
print(f"{'pooled':18}{cand_w:>11}{base_w:>11}{n:>9}{p:>10.1%}")
print()

# Announcer win rate per game mode, pooled across both seat orders.
modes = {}
for res, cand_key, base_key in ((a, "team1Announced", "team2Announced"), (b, "team2Announced", "team1Announced")):
    for gm in res.get("gameModes", []):
        m = modes.setdefault(gm["gameMode"], [0, 0, 0, 0])
        m[0] += gm[cand_key]["announced"]; m[1] += gm[cand_key]["announcerWins"]
        m[2] += gm[base_key]["announced"]; m[3] += gm[base_key]["announcerWins"]
if modes:
    print(f"{'announcer win rate':22}{'candidate':>11}{'baseline':>11}{'delta':>9}")
    for name, (ca, cw, ba, bw) in modes.items():
        if ca == 0 or ba == 0:
            continue
        cr, br = cw / ca, bw / ba
        print(f"{name:22}{cr:>10.1%}{br:>11.1%}{cr - br:>+9.1%}")
    print()

print(f"candidate win rate: {p:.1%}  (95% CI {p - margin:.1%} .. {p + margin:.1%})")
print(f"delta vs 50%:       {delta:+.1%}")
print(f"p-value:            {p_value:.4f}")

if p_value < 0.05:
    verdict = "IMPROVEMENT" if delta > 0 else "REGRESSION"
    print(f"verdict:            significant {verdict} (p < 0.05)")
else:
    print("verdict:            no significant difference")
    if abs(delta) > 1e-9:
        # Total matches for 80% power to detect the observed delta vs 50% at alpha=0.05.
        n_needed = math.ceil((1.96 + 0.84) ** 2 * 0.25 / delta ** 2)
        print(f"                    (a delta of {delta:+.1%} would need ~{math.ceil(n_needed / 2)} matches per seat order to confirm)")
PY
  exit 0
fi

# ---------------------------------------------------------------------------
# Common-opponent mode
# ---------------------------------------------------------------------------
echo "Agent:    $AGENT (candidate: working tree, baseline: $BASELINE @ $BASELINE_SHA)"
echo "Opponent: $OPPONENT   Matches: $MATCHES per run   Seed: $SEED   Target: $TARGET"
echo

git -C "$REPO_ROOT" worktree add --detach --quiet "$WORKTREE_DIR" "$BASELINE"

run_bench() { # <dir> <json-out> <log>
  (cd "$1" && dotnet run --no-launch-profile --project src/Giretra.Manage -- \
    benchmark "$AGENT" "$OPPONENT" -n "$MATCHES" -s "$SEED" -t "$TARGET" \
    --quiet --no-save --json "$2") >"$3" 2>&1
}

echo "Running candidate and baseline benchmarks in parallel..."
run_bench "$WORKTREE_DIR" "$WORK_DIR/baseline.json" "$WORK_DIR/baseline.log" &
BASELINE_PID=$!
run_bench "$REPO_ROOT" "$WORK_DIR/candidate.json" "$WORK_DIR/candidate.log" &
CANDIDATE_PID=$!

wait "$CANDIDATE_PID" || true
wait "$BASELINE_PID" || true

# Old builds may ignore the --json option and exit 0, so a run only counts
# as successful if it produced its JSON output file.
FAILED=""
for side in candidate baseline; do
  if [ ! -f "$WORK_DIR/$side.json" ]; then
    FAILED="${FAILED:+$FAILED and }$side"
    echo "ERROR: $side benchmark run failed. Last 30 lines of its log:" >&2
    tail -n 30 "$WORK_DIR/$side.log" >&2
    echo >&2
  fi
done

if [ -n "$FAILED" ]; then
  case "$FAILED" in *baseline*)
    echo "Hint: the baseline ref must support 'benchmark --json'." >&2
    echo "If '$BASELINE' predates that option, pick a newer baseline ref." >&2
  esac
  exit 1
fi
echo

python3 - "$WORK_DIR/candidate.json" "$WORK_DIR/baseline.json" "$AGENT" "$OPPONENT" <<'PY'
import json, math, sys

cand = json.load(open(sys.argv[1]))
base = json.load(open(sys.argv[2]))
agent, opponent = sys.argv[3], sys.argv[4]

def stats(r):
    return r["team1"]["wins"], r["totalMatches"], r["team1"]["winRate"]

w1, n1, p1 = stats(cand)   # candidate
w2, n2, p2 = stats(base)   # baseline
delta = p1 - p2

# Two-proportion z-test (pooled), two-sided.
pooled = (w1 + w2) / (n1 + n2)
se_pooled = math.sqrt(pooled * (1 - pooled) * (1 / n1 + 1 / n2))
if se_pooled > 0:
    z = delta / se_pooled
    p_value = math.erfc(abs(z) / math.sqrt(2))
else:
    p_value = 1.0

# 95% CI on the delta (unpooled).
se_delta = math.sqrt(p1 * (1 - p1) / n1 + p2 * (1 - p2) / n2)
margin = 1.96 * se_delta

print(f"=== A/B result: {agent} vs {opponent} ===")
print(f"{'':12}{'win rate':>10}{'wins':>8}{'matches':>9}{'avg deals':>11}")
print(f"{'candidate':12}{p1:>9.1%}{w1:>8}{n1:>9}{cand['averageDealsPerMatch']:>11.1f}")
print(f"{'baseline':12}{p2:>9.1%}{w2:>8}{n2:>9}{base['averageDealsPerMatch']:>11.1f}")
print()
print(f"delta:    {delta:+.1%}  (95% CI {delta - margin:+.1%} .. {delta + margin:+.1%})")
print(f"p-value:  {p_value:.4f}")

if p_value < 0.05:
    verdict = "IMPROVEMENT" if delta > 0 else "REGRESSION"
    print(f"verdict:  significant {verdict} (p < 0.05)")
else:
    print("verdict:  no significant difference")
    if abs(delta) > 1e-9:
        # Matches per run for 80% power to detect the observed delta at alpha=0.05.
        pbar = (p1 + p2) / 2
        n_needed = math.ceil(2 * (1.96 + 0.84) ** 2 * pbar * (1 - pbar) / delta ** 2)
        print(f"          (a delta of {delta:+.1%} would need ~{n_needed} matches per run to confirm)")
    sys.exit(0)
PY
