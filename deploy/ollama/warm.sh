#!/usr/bin/env bash
# Load the chat and embedding models so the first real question is not the slow one.
# Run after a restart, or from a systemd timer if the box reboots unattended.
set -e
H=${OLLAMA_HOST:-http://127.0.0.1:11434}
curl -s "$H/api/generate" -d '{"model":"qwen2.5:14b","prompt":"ready","stream":false,"keep_alive":"24h"}' > /dev/null
curl -s "$H/api/embed" -d '{"model":"nomic-embed-text","input":"ready","keep_alive":"24h"}' > /dev/null
ollama ps
