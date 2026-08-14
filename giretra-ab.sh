#!/usr/bin/env bash
# A/B benchmark: compare an agent in the working tree (candidate) against the
# same agent at a baseline git ref, by benchmarking both against a common
# opponent with identical seeds, then testing the win-rate delta for
# statistical significance.
#
# Usage:
#   ./giretra-ab.sh <Agent> [options]
#
# Options:
#   --opponent <name>   Opponent agent (default: CalculatingPlayer)
#   --baseline <ref>    Git ref for the baseline build (default: HEAD)
#   -n <matches>        Matches per run (default: 1000)
#   -s <seed>           Random seed, shared by both runs (default: 42)
#   -t <target>         Target score per match (default: 500)
#
# Example:
#   ./giretra-ab.sh CuttingPlayer --opponent CalculatingPlayer -n 2000
#
# Note: the baseline ref must include the benchmark --json option
# (any commit from Aug 2026 onwards).
set -euo pipefail

usage() { sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'; }

AGENT=""
OPPONENT="CalculatingPlayer"
BASELINE="HEAD"
MATCHES=1000
SEED=42
TARGET=500

while [ $# -gt 0 ]; do
  case "$1" in
    --opponent) OPPONENT="$2"; shift 2 ;;
    --baseline) BASELINE="$2"; shift 2 ;;
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

cleanup() {
  git -C "$REPO_ROOT" worktree remove --force "$WORKTREE_DIR" 2>/dev/null || true
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

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
