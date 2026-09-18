# Ollama on the clinic's server (Part 4)

The model runtime lives on the same Linux box as ClinicLive and listens on the loopback
interface only. The application is its only client. Nothing here is reachable from the network.

## Install

```
curl -fsSL https://ollama.com/install.sh | sh
systemctl is-active ollama          # active
ss -ltnp | grep 11434               # 127.0.0.1:11434 ... users:(("ollama",...))
```

The installer creates a system user `ollama`, a systemd unit `ollama.service`, and stores models
under `/usr/share/ollama/.ollama`. The default bind address is already `127.0.0.1:11434`; the
override below makes that explicit so a future default change cannot open the port by surprise.

## Configure

```
sudo mkdir -p /etc/systemd/system/ollama.service.d
sudo cp ollama.service.d-override.conf /etc/systemd/system/ollama.service.d/override.conf
sudo systemctl daemon-reload && sudo systemctl restart ollama
```

What the override sets, and why:

| Setting | Value | Why |
|---|---|---|
| `OLLAMA_HOST` | `127.0.0.1:11434` | loopback only, never `0.0.0.0` |
| `OLLAMA_KEEP_ALIVE` | `24h` | a cold load costs 10–70 s; the clinic must not pay it per question |
| `OLLAMA_NUM_PARALLEL` | `2` | two staff questions at once without doubling memory four times |
| `OLLAMA_MAX_LOADED_MODELS` | `2` | the chat model and the embedding model stay resident together |

## Models

```
ollama pull qwen2.5:14b
ollama pull nomic-embed-text
./warm.sh                 # loads both so the first real question is not the slow one
```

## Firewall

The port is bound to loopback, so the firewall has nothing to allow. Keep it that way:

```
sudo ufw status            # 11434 must NOT appear
```

## If the runtime must live on another machine

Only then does the port need to cross a network, and only behind TLS and a shared key. The
pattern is `nginx-ai-gateway.conf`: nginx terminates TLS (certbot as for the site), rejects
any request without the `X-Ai-Key` header value, and proxies to the loopback port with
buffering off so streamed tokens arrive as they are produced. Put the key in the application's
server-side configuration, never in the repo.

## Smoke test

```
curl -s http://127.0.0.1:11434/api/version
curl -s http://127.0.0.1:11434/api/generate -d '{"model":"qwen2.5:14b","prompt":"Say ready.","stream":false}' | head -c 300
ollama ps                  # both models, 100% GPU
```

Logs: `journalctl -u ollama -f`. Memory: `nvidia-smi` for the GPU, `free -h` for the CPU fallback.

## Once it is running

`OPERATIONS.md` is the runbook for the clinic: the warm-up timer (`install-warm-timer.sh`,
`ollama-warm.service`, `ollama-warm.timer`), runtime and model updates, knowledge updates,
backups, logs, disk, the health check and the monthly cost.
