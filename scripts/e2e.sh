#!/usr/bin/env bash
# End-to-end run of the .NET SDK against real AgentOven control planes.
#
#   scripts/e2e.sh                 # OSS only
#   scripts/e2e.sh --pro           # OSS, then Pro (needs DATABASE_URL and a license, see below)
#
# Inputs (environment):
#   AGENTOVEN_OSS_BIN    control-plane server binary (default: built from $AGENTOVEN_REPO/control-plane)
#   AGENTOVEN_REPO       checkout of github.com/agentoven/agentoven (default: ../agentoven)
#   AGENTOVEN_PRO_BIN    Pro server binary (default: built from $AGENTOVEN_PRO_REPO)
#   AGENTOVEN_PRO_REPO   checkout of agentoven-pro (default: ../agentoven-pro)
#   DATABASE_URL         PostgreSQL with pgvector, for Pro
#   AGENTOVEN_LICENSE_KEY / AGENTOVEN_LICENSE_SECRET  Pro license (eval "$(go run ./_tools/gen_license)" in agentoven-pro)
#
# The two servers run one after the other: both hand out agent-process ports from 9100.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
WORK="$(mktemp -d)"
UPSTREAM_PORT=19100
OSS_PORT=18180
PRO_PORT=18190
RUN_PRO=false
[[ "${1:-}" == "--pro" ]] && RUN_PRO=true

PIDS=()
cleanup() {
  for pid in "${PIDS[@]}"; do kill "$pid" 2>/dev/null || true; done
  wait 2>/dev/null || true
  rm -rf "$WORK"
}
trap cleanup EXIT

wait_for() { # url
  for _ in $(seq 1 120); do curl -fsS --noproxy '*' "$1" >/dev/null 2>&1 && return 0; sleep 0.5; done
  echo "timed out waiting for $1" >&2; return 1
}

build_server() { # repo dir, output
  (cd "$1" && go build -o "$2" ./cmd/server)
}

echo "==> building samples"
dotnet build "$ROOT/samples/EchoAgent" -c Release -v q -nologo >/dev/null
dotnet build "$ROOT/samples/ChatAgent" -c Release -v q -nologo >/dev/null
export AGENTOVEN_E2E_ECHO_AGENT="$ROOT/samples/EchoAgent/bin/Release/net10.0/EchoAgent.dll"
export AGENTOVEN_E2E_CHAT_AGENT="$ROOT/samples/ChatAgent/bin/Release/net10.0/ChatAgent.dll"
export AGENTOVEN_E2E_UPSTREAM="http://127.0.0.1:$UPSTREAM_PORT"
export AGENTOVEN_E2E_API_KEY=e2e-key

echo "==> starting the fake model upstream on :$UPSTREAM_PORT"
python3 "$ROOT/tests/e2e/fake_upstream.py" "$UPSTREAM_PORT" & PIDS+=($!)
wait_for "http://127.0.0.1:$UPSTREAM_PORT/v1/models"

run_suite() { # filter class, label
  echo "==> running the $2 suite"
  dotnet test --project "$ROOT/tests/AgentOven.IntegrationTests" -c Release -f net10.0 -- --filter-class "$1"
}

# ── OSS ─────────────────────────────────────────────────────────────
OSS_BIN="${AGENTOVEN_OSS_BIN:-}"
if [[ -z "$OSS_BIN" ]]; then
  OSS_BIN="$WORK/agentoven-oss"
  echo "==> building the OSS control plane"
  build_server "${AGENTOVEN_REPO:-$ROOT/../agentoven}/control-plane" "$OSS_BIN"
fi
mkdir -p "$WORK/oss-home"
echo "==> starting the OSS control plane on :$OSS_PORT"
(cd "$WORK" && HOME="$WORK/oss-home" AGENTOVEN_DATA_DIR="$WORK/oss-home/.agentoven" AGENTOVEN_PORT=$OSS_PORT \
  AGENTOVEN_API_KEYS=e2e-key AGENTOVEN_API_KEY_ROLE=admin exec "$OSS_BIN" >"$WORK/oss.log" 2>&1) & OSS_PID=$!
PIDS+=($OSS_PID)
wait_for "http://127.0.0.1:$OSS_PORT/health"
AGENTOVEN_E2E_URL="http://127.0.0.1:$OSS_PORT" run_suite "AgentOven.IntegrationTests.OssEndToEndTests" OSS \
  || { echo "--- OSS server log (tail)"; tail -50 "$WORK/oss.log"; exit 1; }
kill "$OSS_PID"; wait "$OSS_PID" 2>/dev/null || true

# ── Pro ─────────────────────────────────────────────────────────────
if $RUN_PRO; then
  : "${DATABASE_URL:?Pro needs DATABASE_URL (PostgreSQL with pgvector)}"
  : "${AGENTOVEN_LICENSE_KEY:?Pro needs AGENTOVEN_LICENSE_KEY / AGENTOVEN_LICENSE_SECRET}"
  PRO_BIN="${AGENTOVEN_PRO_BIN:-}"
  if [[ -z "$PRO_BIN" ]]; then
    PRO_BIN="$WORK/agentoven-pro"
    echo "==> building the Pro control plane"
    build_server "${AGENTOVEN_PRO_REPO:-$ROOT/../agentoven-pro}" "$PRO_BIN"
  fi
  mkdir -p "$WORK/pro-home" "$WORK/pro-scenarios" "$WORK/pro-skills"
  echo "==> starting the Pro control plane on :$PRO_PORT"
  (cd "$WORK" && HOME="$WORK/pro-home" AGENTOVEN_DATA_DIR="$WORK/pro-home/.agentoven" AGENTOVEN_PORT=$PRO_PORT \
    AGENTOVEN_API_KEYS=e2e-key AGENTOVEN_API_KEY_ROLE=admin AGENTOVEN_REALTIME_TOKEN_SECRET=e2e-realtime \
    AGENTOVEN_SCENARIO_DIR="$WORK/pro-scenarios" AGENTOVEN_SKILLS_STORAGE_DIR="$WORK/pro-skills" \
    exec "$PRO_BIN" >"$WORK/pro.log" 2>&1) & PIDS+=($!)
  wait_for "http://127.0.0.1:$PRO_PORT/health"
  AGENTOVEN_E2E_PRO_URL="http://127.0.0.1:$PRO_PORT" run_suite "AgentOven.IntegrationTests.ProEndToEndTests" Pro \
    || { echo "--- Pro server log (tail)"; tail -50 "$WORK/pro.log"; exit 1; }
fi

echo "==> end-to-end suites passed"
