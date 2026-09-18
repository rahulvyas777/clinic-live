#!/usr/bin/env bash
# Part 11: install the warm-up timer on the clinic's server.
# Run as root:  sudo bash deploy/ollama/install-warm-timer.sh
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"

install -m 0755 "$HERE/warm.sh" /usr/local/bin/ollama-warm.sh
install -m 0644 "$HERE/ollama-warm.service" /etc/systemd/system/ollama-warm.service
install -m 0644 "$HERE/ollama-warm.timer" /etc/systemd/system/ollama-warm.timer

systemctl daemon-reload
systemctl enable --now ollama-warm.timer

echo "== timer"
systemctl list-timers --all | grep warm
echo "== one run now, to prove the unit works"
systemctl start ollama-warm.service
systemctl show ollama-warm.service -p Result -p ExecMainStatus
journalctl -u ollama-warm.service --no-pager -n 12 | cut -c1-160
