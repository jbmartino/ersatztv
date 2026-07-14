#!/usr/bin/env bash
# End-to-end test for the management API: builds a whole channel over HTTP, with no browser and no SQL,
# then asserts that the channel actually streams.
#
# The path exercised: a local library of shows -> a collection -> a channel ->
# a sequential (YAML) playout -> a tuner channel Plex can consume.
#
# Usage: ETV_URL=http://localhost:8499 ETV_API_KEY=... MEDIA_ROOT=/path/to/media SCHEDULE_DIR=/path ./scripts/api-smoke-test.sh
set -euo pipefail

ETV_URL="${ETV_URL:-http://localhost:8499}"
ETV_API_KEY="${ETV_API_KEY:-}"
MEDIA_ROOT="${MEDIA_ROOT:?set MEDIA_ROOT to a folder containing 'TV Shows/<Show>/Season 01/*.mkv'}"
SCHEDULE_DIR="${SCHEDULE_DIR:?set SCHEDULE_DIR to a writable folder for the sequential schedule file}"

SHOW_TITLE="${SHOW_TITLE:-Test Toons}"
# deliberately shares no words with SHOW_TITLE, so grepping the guide for the show proves the show is scheduled
CHANNEL_NAME="${CHANNEL_NAME:-Smoke Signal Network}"
COLLECTION_NAME="${COLLECTION_NAME:-Test Toons Collection}"

pass=0
fail=0
ok()   { echo "  ✓ $1"; pass=$((pass + 1)); }
bad()  { echo "  ✗ $1"; fail=$((fail + 1)); }
check(){ if [ "$2" = "$3" ]; then ok "$1 ($2)"; else bad "$1: expected '$3', got '$2'"; fi; }

api()  { curl -sS -H "X-Api-Key: ${ETV_API_KEY}" -H "Content-Type: application/json" "$@"; }
code() { curl -sS -o /dev/null -w "%{http_code}" -H "X-Api-Key: ${ETV_API_KEY}" -H "Content-Type: application/json" "$@"; }

echo "==> 0. auth"
check "anonymous request is rejected" "$(curl -sS -o /dev/null -w '%{http_code}' "${ETV_URL}/api/channels")" "401"
check "keyed request is accepted"     "$(code "${ETV_URL}/api/channels")" "200"
# plex talks to /iptv, which must never require a key
check "iptv stays anonymous"          "$(curl -sS -o /dev/null -w '%{http_code}' "${ETV_URL}/iptv/channels.m3u")" "200"

echo "==> 1. ffmpeg profile"
profile_id=$(api "${ETV_URL}/api/ffmpeg/profiles" | python3 -c 'import json,sys; p=json.load(sys.stdin); print(p[0]["id"] if p else "")')
[ -n "$profile_id" ] && ok "using ffmpeg profile id=${profile_id}" || bad "no ffmpeg profile available"

echo "==> 2. create local library + scan"
LIBRARY_NAME="${LIBRARY_NAME:-Smoke Test Shows}"

# reuse the library if a previous run created it, so the script is re-runnable
library_id=$(api "${ETV_URL}/api/libraries" \
  | python3 -c "import json,sys; print(next((l['id'] for l in json.load(sys.stdin) if l['name']=='${LIBRARY_NAME}'), ''))")

if [ -n "$library_id" ]; then
  ok "reusing library id=${library_id}"
else
  library_id=$(api -X POST "${ETV_URL}/api/libraries" \
    -d "{\"name\":\"${LIBRARY_NAME}\",\"mediaKind\":\"Shows\",\"paths\":[\"${MEDIA_ROOT}/TV Shows\"]}" \
    | python3 -c 'import json,sys; print(json.load(sys.stdin).get("id",""))')
  [ -n "$library_id" ] && ok "created library id=${library_id}" || bad "library not created"
fi

check "queued library scan" "$(code -X POST "${ETV_URL}/api/libraries/${library_id}/scan")" "200"

echo "  waiting for the scanner to find '${SHOW_TITLE}'..."
show_id=""
for _ in $(seq 1 60); do
  show_id=$(api "${ETV_URL}/api/media/shows" \
    | python3 -c "import json,sys; print(next((s['mediaItemId'] for s in json.load(sys.stdin) if '${SHOW_TITLE}' in s['name']), ''))")
  [ -n "$show_id" ] && break
  sleep 2
done
[ -n "$show_id" ] && ok "scanner indexed the show (id=${show_id})" || bad "scanner never indexed '${SHOW_TITLE}'"

echo "==> 3. create collection and add the show"
collection_id=$(api -X POST "${ETV_URL}/api/collections" -d "{\"name\":\"${COLLECTION_NAME}\"}" \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["id"])')
[ -n "$collection_id" ] && ok "created collection id=${collection_id}" || bad "collection not created"

check "added show to collection" \
  "$(code -X POST "${ETV_URL}/api/collections/${collection_id}/items" -d "{\"showIds\":[${show_id}]}")" "200"

echo "==> 4. create channel"
channel_json=$(api -X POST "${ETV_URL}/api/channels" \
  -d "{\"name\":\"${CHANNEL_NAME}\",\"group\":\"Smoke Test\",\"ffmpegProfileId\":${profile_id}}")
channel_id=$(echo "$channel_json" | python3 -c 'import json,sys; print(json.load(sys.stdin)["channelId"])')
[ -n "$channel_id" ] && ok "created channel id=${channel_id}" || bad "channel not created"

channel_number=$(api "${ETV_URL}/api/channels" \
  | python3 -c "import json,sys; print(next(c['number'] for c in json.load(sys.stdin) if c['id']==${channel_id}))")
ok "channel number auto-assigned: ${channel_number}"

# a channel the ui would reject must be rejected here too
check "rejects invalid channel number" \
  "$(code -X POST "${ETV_URL}/api/channels" -d "{\"name\":\"Bad\",\"number\":\"not-a-number\",\"ffmpegProfileId\":${profile_id}}")" "400"
check "rejects nameless channel" \
  "$(code -X POST "${ETV_URL}/api/channels" -d "{\"name\":\"\",\"ffmpegProfileId\":${profile_id}}")" "400"

echo "==> 5. sequential (YAML) playout: scheduling as code"
schedule_file="${SCHEDULE_DIR}/smoke-test.yml"
# the yaml type discriminator IS the value key: 'collection: <name>' both selects the collection content
# type and names the collection
cat > "${schedule_file}" <<YAML
content:
  - collection: "${COLLECTION_NAME}"
    key: TOONS
    order: shuffle

playout:
  - all:
    content: TOONS
  - repeat: true
YAML
ok "wrote ${schedule_file}"

playout_id=$(api -X POST "${ETV_URL}/api/playouts/sequential" \
  -d "{\"channelId\":${channel_id},\"scheduleFile\":\"${schedule_file}\"}" \
  | python3 -c 'import json,sys; print(json.load(sys.stdin)["playoutId"])')
[ -n "$playout_id" ] && ok "created sequential playout id=${playout_id}" || bad "playout not created"

# a channel may only have one playout; the handler rejects this
dup_code=$(code -X POST "${ETV_URL}/api/playouts/sequential" \
  -d "{\"channelId\":${channel_id},\"scheduleFile\":\"${schedule_file}\"}")
case "$dup_code" in
  4*|5*) ok "rejects a second playout on the same channel (${dup_code})" ;;
  *)     bad "second playout on the same channel was allowed (${dup_code})" ;;
esac

echo "  waiting for the playout to build..."
built="false"
for _ in $(seq 1 90); do
  built=$(api "${ETV_URL}/api/playouts/${playout_id}" \
    | python3 -c 'import json,sys; d=json.load(sys.stdin); print(str(d.get("lastBuildSuccess")).lower())')
  [ "$built" = "true" ] && break
  sleep 2
done
check "playout built successfully" "$built" "true"

echo "==> 6. the channel is a real tuner channel"
if curl -sS "${ETV_URL}/iptv/channels.m3u" | grep -q "${CHANNEL_NAME}"; then
  ok "channel appears in /iptv/channels.m3u (what Plex reads)"
else
  bad "channel missing from /iptv/channels.m3u"
fi

# guide regeneration is queued behind the build on the same background worker, so on a busy box it can
# trail the build by a couple of minutes
guide_ok="no"
for _ in $(seq 1 100); do
  if curl -sS "${ETV_URL}/iptv/xmltv.xml" | grep -q "${SHOW_TITLE}"; then guide_ok="yes"; break; fi
  sleep 3
done
if [ "$guide_ok" = "yes" ]; then
  ok "the scheduled show appears in the XMLTV guide (what Plex builds its EPG from)"
else
  bad "show missing from the XMLTV guide"
  echo "    --- guide sample ---"
  curl -sS "${ETV_URL}/iptv/xmltv.xml" | head -25 | sed 's/^/    /'
fi

echo "  tuning in (transcode)..."
# a .ts tune-in is an endless stream, so cap it and treat "curl exited on the time limit" as success
stream_out=$(curl -sS -m 25 -o /dev/null \
  -w '%{http_code} %{size_download}' "${ETV_URL}/iptv/channel/${channel_number}.ts" 2>/dev/null || true)
stream_code=$(echo "$stream_out" | awk '{print $1}')
stream_bytes=$(echo "$stream_out" | awk '{print $2+0}')
check "stream responds 200" "${stream_code:-000}" "200"
if [ "${stream_bytes:-0}" -gt 100000 ]; then
  ok "stream delivered real video bytes (${stream_bytes})"
else
  bad "stream delivered only ${stream_bytes:-0} bytes"
fi

echo "==> 7. rebuild + teardown"
check "queued a playout rebuild" "$(code -X POST "${ETV_URL}/api/playouts/${playout_id}/build?mode=Reset")" "202"
check "deleted playout"          "$(code -X DELETE "${ETV_URL}/api/playouts/${playout_id}")" "200"
check "deleted channel"          "$(code -X DELETE "${ETV_URL}/api/channels/${channel_id}")" "200"
check "deleted collection"       "$(code -X DELETE "${ETV_URL}/api/collections/${collection_id}")" "200"

echo
echo "================================"
echo "  passed: ${pass}   failed: ${fail}"
echo "================================"
[ "$fail" -eq 0 ]
