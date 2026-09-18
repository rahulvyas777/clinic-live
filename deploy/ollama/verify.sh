#!/usr/bin/env bash
# Part 4 verification: apply the override, warm the models, smoke-test, prove loopback-only.
# Run as root on the server:  sudo bash deploy/ollama/verify.sh
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
mkdir -p /etc/systemd/system/ollama.service.d
cp "$HERE/ollama.service.d-override.conf" /etc/systemd/system/ollama.service.d/override.conf
systemctl daemon-reload
systemctl restart ollama
sleep 3
echo "== service"
systemctl is-active ollama
systemctl show ollama -p Environment | tr ' ' '\n' | grep OLLAMA
echo "== listening (must be 127.0.0.1 only)"
ss -ltnp | grep 11434 || echo "port not open"
echo "== warm-up (first load)"
start=$(date +%s)
bash "$HERE/warm.sh"
echo "warm-up took $(( $(date +%s) - start )) s"
echo "== smoke"
curl -s http://127.0.0.1:11434/api/version; echo
curl -s http://127.0.0.1:11434/api/generate \
  -d '{"model":"qwen2.5:14b","prompt":"In one sentence, what should a patient bring to a first appointment at a clinic?","stream":false}' \
  | sed 's/.*"response":"\([^"]*\)".*"eval_count":\([0-9]*\).*"eval_duration":\([0-9]*\).*/answer: \1\ntokens: \2  duration_ns: \3/'
echo "== GPU as seen from this box"
nvidia-smi --query-gpu=name,memory.used,power.draw --format=csv,noheader 2>/dev/null || echo "no nvidia-smi"
echo "== last journal lines"
journalctl -u ollama --no-pager -n 5 | cut -c1-150
echo "== the LAN address must refuse"
LAN=$(ip -4 addr show eth0 | grep -oP 'inet \K[\d.]+')
echo "LAN address: $LAN"
curl -s -m 3 "http://$LAN:11434/api/version" && echo "REACHABLE ON THE LAN: WRONG" || echo "connection refused on $LAN:11434 (correct)"
