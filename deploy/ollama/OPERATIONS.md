# Operations runbook — the clinic's private assistant (Part 11)

Who this is for: whoever keeps the clinic's server running. Everything below was run once on
the reference server (Ubuntu 24.04, systemd, NVIDIA GPU) and the outputs are the real ones,
trimmed to the lines that matter. Timestamps and digests will differ on your box; shapes will not.

The runtime is Ollama on `127.0.0.1:11434` (Part 4). The application is its only client.
Nothing in this document reaches the network, and nothing in it needs an internet connection
except the two "update" sections.

---

## Daily: nothing

There is no daily task. The models stay loaded (`OLLAMA_KEEP_ALIVE=24h` in the systemd
override), and a timer re-loads them after a reboot or an eviction so the first question of the
day is never the slow one.

### The warm-up timer

`ollama-warm.timer` starts `ollama-warm.service` **60 s after boot and every 6 hours**. The
service is a oneshot that runs `warm.sh` — one tiny generate against `qwen2.5:14b`, one embed
against `nomic-embed-text`, both with `keep_alive: 24h`, then `ollama ps`.

Install it (copies `warm.sh` to `/usr/local/bin/ollama-warm.sh`, installs both units,
reloads systemd, enables and starts the timer):

```
sudo bash deploy/ollama/install-warm-timer.sh
systemctl list-timers --all | grep warm
```

Expected output:

```
NEXT                        LEFT      LAST                        PASSED   UNIT               ACTIVATES
Fri 2026-09-18 14:36:03 UTC 5h 59min  Fri 2026-09-18 08:35:49 UTC 12s ago  ollama-warm.timer  ollama-warm.service
```

And the run it just did, after a reboot at 08:34:53 — the timer fired 56 s later:

```
Sep 18 08:35:49 MSI systemd[1]: Starting ollama-warm.service - Warm the Ollama chat and embedding models...
Sep 18 08:36:10 MSI ollama-warm.sh[793]: NAME                       ID              SIZE      PROCESSOR    CONTEXT    UNTIL
Sep 18 08:36:10 MSI ollama-warm.sh[793]: nomic-embed-text:latest    0a109f422b47    323 MB    100% GPU     2048       24 hours from now
Sep 18 08:36:10 MSI ollama-warm.sh[793]: qwen2.5:14b                7cdf5a0187d5    10 GB     100% GPU     4096       24 hours from now
Sep 18 08:36:10 MSI systemd[1]: ollama-warm.service: Deactivated successfully.
Sep 18 08:36:10 MSI systemd[1]: Finished ollama-warm.service - Warm the Ollama chat and embedding models.
```

A run takes about 20 s cold and under 2 s warm. If the service ever reports
`Result=exit-code`, read the journal before assuming the models are down: the warm-up can
fail *after* both models are loaded (see the `$HOME` note in `ollama-warm.service`).

---

## Updating the runtime

Re-run the installer. It upgrades in place: it replaces the binary under `/usr/local`,
keeps the systemd unit and the drop-in override, and restarts the service.

```
curl -fsSL https://ollama.com/install.sh | sh
ollama --version
sudo systemctl start ollama-warm.service      # the restart dropped the models; put them back
ollama ps
```

Expected output (tail of the installer, then the checks):

```
>>> Cleaning up old version at /usr/local/lib/ollama
>>> Installing ollama to /usr/local
>>> Downloading ollama-linux-amd64.tar.zst
>>> Adding ollama user to render group...
>>> Adding ollama user to video group...
>>> Creating ollama systemd service...
>>> Enabling and starting ollama service...
>>> The Ollama API is now available at 127.0.0.1:11434.
>>> Install complete. Run "ollama" from the command line.

ollama version is 0.34.2
active

NAME                       ID              SIZE      PROCESSOR    CONTEXT    UNTIL
nomic-embed-text:latest    0a109f422b47    323 MB    100% GPU     2048       24 hours from now
qwen2.5:14b                7cdf5a0187d5    10 GB     100% GPU     4096       24 hours from now
```

The log must end with `Install complete.` If it ends anywhere else, the old binary may already
be gone — re-run it before staff arrive, not after.

**Models are not touched by a runtime upgrade.** The installer writes to `/usr/local`; the
models live under `/usr/share/ollama/.ollama` and are left exactly as they were. The only thing
you lose is the loaded state in VRAM, which the warm-up above restores.

An upgrade *can* change how a model is run (context defaults, flash attention, quantisation
handling), so treat it like a model change: re-run the exam before staff use it.

---

## Updating a model

`ollama pull qwen2.5:14b` does not fetch "the same thing again" — the tag moves. A pull can
bring a newer build of the same model under the same name, with different weights and
different behaviour.

See what you have now:

```
ollama list
```

```
NAME                       ID              SIZE      MODIFIED
llama3.1:8b                46e0c10c039e    4.9 GB    28 minutes ago
nomic-embed-text:latest    0a109f422b47    274 MB    31 minutes ago
qwen2.5:14b                7cdf5a0187d5    9.0 GB    31 minutes ago
```

The `ID` column is the manifest digest. Write it down before you pull; if the digest is the same
afterwards, nothing changed.

**Keep the old build before you pull.** A tag is a pointer, so copying the tag pins the current
build under a dated name you can fall back to:

```
ollama cp qwen2.5:14b qwen2.5:14b-2026-09
ollama list
ollama pull qwen2.5:14b          # only now
```

```
copied 'qwen2.5:14b' to 'qwen2.5:14b-2026-09'

NAME                       ID              SIZE      MODIFIED
qwen2.5:14b-2026-09        7cdf5a0187d5    9.0 GB    Less than a second ago
llama3.1:8b                46e0c10c039e    4.9 GB    28 minutes ago
nomic-embed-text:latest    0a109f422b47    274 MB    31 minutes ago
qwen2.5:14b                7cdf5a0187d5    9.0 GB    31 minutes ago
```

The copy is free: same digest, same blobs on disk, a second manifest pointing at them. The
`9.0 GB` in both rows is the same 9.0 GB counted twice. To roll back, point the application's
`Ai:ChatModel` at the dated tag, or `ollama cp qwen2.5:14b-2026-09 qwen2.5:14b`.

Removing the dated copy only removes that manifest:

```
ollama rm qwen2.5:14b-2026-09
ollama list
```

```
deleted 'qwen2.5:14b-2026-09'

NAME                       ID              SIZE      MODIFIED
llama3.1:8b                46e0c10c039e    4.9 GB    28 minutes ago
nomic-embed-text:latest    0a109f422b47    274 MB    31 minutes ago
qwen2.5:14b                7cdf5a0187d5    9.0 GB    31 minutes ago
```

### The rule

> After **any** model change — a pull, a rollback, a runtime upgrade that changes how the model
> is run — re-run `tools/private-bench/eval.py` (Part 10) and read the scores before staff use it.

A new build of the same tag can be better at prose and worse at refusing, and refusing is the
part the clinic depends on. The exam is the only thing standing between a quiet upstream change
and an assistant that starts inventing opening hours. Keep the old tag until the exam passes.

---

## Updating the knowledge

The clinic's documents are markdown files in the repo under `knowledge/public/` and
`knowledge/staff/`. To change what the assistant knows, edit the markdown and re-ingest:

```
# 1. edit knowledge/public/fees-and-payment.md (or any other file)
# 2. re-ingest from the dev box, against the clinic's database
dotnet run --project src/ClinicLive -- ingest
```

Expected output — ingest is keyed on the SHA-256 of each file, so unchanged documents are
skipped without a single embedding call:

```
slug                          audience  chunks  status
----------------------------  --------  ------  --------
fees-and-payment              public         4  skipped
patient-information           public         6  skipped
vaccinations-and-tests        public         3  skipped
front-desk-procedures         staff          6  skipped
suppliers-and-internal-rates  staff          4  skipped

5 documents, 23 chunks, 5 skipped.
```

A document you edited shows `embedded` instead, with its new chunk count; its old chunks are
deleted and rebuilt, never diffed. Moving a document between `public/` and `staff/` changes its
audience on the next ingest, and the retrieval filter follows immediately — no restart.

### Changing the embedding model re-embeds everything

Vectors from two different embedding models are not comparable. If `Ai:EmbeddingModel` ever
changes, every chunk in the database is stale, and the skip-by-hash logic will not notice,
because the markdown did not change. Force the rebuild:

```
docker compose exec db psql -U cliniclive -d cliniclive -c "update knowledge_document set content_hash = ''"
dotnet run --project src/ClinicLive -- ingest     # now every document reports "embedded"
```

If the new model has a different dimension count, the `vector(768)` column has to change with it
(migration), and the old rows cannot be kept at all. Budget an evening, not a coffee break, and
re-run the exam afterwards.

---

## Backups

**The knowledge needs no backup job of its own.** It lives in the same PostgreSQL database as
the appointments and the patient records, in `knowledge_document` and `knowledge_chunk`, so the
clinic's normal database backup already covers it. Adding a second backup for it would just be
one more thing to forget.

If you ever want the two tables on their own — to move them to a new box, or to prove to
somebody that they are in there:

```
docker compose exec db pg_dump -U cliniclive -d cliniclive -t knowledge_document -t knowledge_chunk --data-only | head -20
```

```
--
-- PostgreSQL database dump
--

-- Dumped from database version 18.6 (Debian 18.6-1.pgdg12+2)
-- Dumped by pg_dump version 18.6 (Debian 18.6-1.pgdg12+2)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);

--
-- Data for Name: knowledge_document; Type: TABLE DATA; Schema: public; Owner: cliniclive
--

COPY public.knowledge_document (id, slug, title, audience, source_path, content_hash, updated_at) FROM stdin;
1	fees-and-payment	Fees and payment	public	public/fees-and-payment.md	e7a88f62c5a1ca...
2	patient-information	Patient information	public	public/patient-information.md	e23bb245f1a11b...
```

The two tables are 280 kB for 5 documents and 23 chunks; the vectors dominate that, not the text.

What is *actually* the source of truth, and what is not:

| Thing | Backed up? | Why |
|---|---|---|
| `knowledge/*.md` in the repo | yes, by git | **the true source.** The database is a derived index; ingest rebuilds it from these files. |
| `knowledge_document`, `knowledge_chunk` | yes, by the clinic's normal database backup | cheaper to restore than to re-embed, and it rides along for free |
| Model files under `/usr/share/ollama/.ollama` | **no** | 14 GB of re-downloadable blobs. `ollama pull` restores them. Backing them up wastes the clinic's backup window. |

Restoring, in order: restore the database, `ollama pull` the three models, run the warm-up. If
the database restore is ever incomplete, `dotnet run -- ingest` rebuilds the knowledge from the
markdown in minutes.

---

## Logs

```
journalctl -u ollama -n 20 --no-pager
journalctl -u ollama -f            # follow, while somebody asks a question
```

### What normal looks like

Every request ends with one `[GIN]` line: timestamp, status, duration, client address, method
and path. The client address is always `127.0.0.1` — anything else means the loopback binding
was undone.

```
Sep 18 08:35:19 MSI ollama[166]: slot      release: id  1 | task 18 | stop processing: n_tokens = 72, truncated = 0
Sep 18 08:35:19 MSI ollama[166]: srv  update_slots: all slots are idle
Sep 18 08:35:19 MSI ollama[166]: [GIN] 2026/09/18 - 08:35:19 | 200 |  626.242118ms |       127.0.0.1 | POST     "/api/generate"
Sep 18 08:34:30 MSI ollama[844]: [GIN] 2026/09/18 - 08:34:30 | 200 |  1.950191277s |       127.0.0.1 | POST     "/api/embed"
```

Reading it: `200`, a duration in the hundreds of milliseconds to a few seconds, and
`all slots are idle` afterwards. A duration in the *tens* of seconds on the first request of the
day means the model was cold — check the warm-up timer. `POST "/api/embed"` lines appear during
ingest and once per question (the question itself is embedded).

### What an error looks like

Ask for a model that is not installed:

```
curl -s http://127.0.0.1:11434/api/generate -d '{"model":"nope:latest","prompt":"x"}'
```

```
{"error":"model 'nope:latest' not found"}
```

and in the journal:

```
Sep 18 08:34:31 MSI ollama[844]: [GIN] 2026/09/18 - 08:34:31 | 404 |     274.664µs |       127.0.0.1 | POST     "/api/generate"
```

`404` in a quarter of a millisecond: the runtime is healthy and the *caller* is wrong. That is
the shape of a typo in `Ai:ChatModel`, or a model that was removed but is still configured.
Compare with `500` (the runtime failed) and with a request that never appears in the log at all
(the application never reached the runtime — check the endpoint and the service).

---

## Disk

```
du -sh /usr/share/ollama/.ollama
df -h /usr/share/ollama | tail -1
ollama list
```

```
14G	/usr/share/ollama/.ollama
/dev/sdd       1007G   18G  939G   2% /

NAME                       ID              SIZE      MODIFIED
llama3.1:8b                46e0c10c039e    4.9 GB    28 minutes ago
nomic-embed-text:latest    0a109f422b47    274 MB    31 minutes ago
qwen2.5:14b                7cdf5a0187d5    9.0 GB    31 minutes ago
```

Three models, 14 GB, and it does not grow on its own: nothing about answering questions writes
to disk. It grows when you pull. Dated copies (`qwen2.5:14b-2026-09`) cost nothing extra —
they share blobs — but two *different* builds of the same model are two full downloads, so
clear out fallback tags you have stopped trusting. Leave at least 30 GB free so a pull of a new
model never lands on a full disk mid-download.

---

## Health check

Sixty seconds, any time somebody says "the assistant is slow":

```
curl -s http://127.0.0.1:11434/api/version
ollama ps
systemctl is-active ollama
```

```
{"version":"0.34.2"}

NAME                       ID              SIZE      PROCESSOR    CONTEXT    UNTIL
nomic-embed-text:latest    0a109f422b47    323 MB    100% GPU     2048       24 hours from now
qwen2.5:14b                7cdf5a0187d5    10 GB     100% GPU     4096       24 hours from now
active
```

Three things must be true: the version responds, **both** models are listed, and both say
`100% GPU`.

- **A model is missing from `ollama ps`.** It was evicted or the service restarted. Run
  `sudo systemctl start ollama-warm.service` and it comes back in about 20 s.
- **`PROCESSOR` says `CPU`, or `47%/53% CPU/GPU`.** The model did not fit in VRAM — something
  else on the box is using the card. This is the one failure that makes the assistant unusable
  rather than merely slow: 14B on CPU is tens of seconds per answer. Fix it in this order:
  1. `nvidia-smi` and see who else holds VRAM. Stop that process (a training job, a game, a
     second runtime), then `sudo systemctl restart ollama` and warm up.
  2. If the card is simply too small for 14B now, switch `Ai:ChatModel` to `llama3.1:8b` — it is
     already pulled for exactly this, scores lower but fits — and restart the application.
  3. Re-run the exam if the 8B model is going to stay.
- **`curl` gives connection refused.** `systemctl status ollama`, then
  `journalctl -u ollama -n 50 --no-pager`.

---

## What it costs per month

Two numbers, both worth knowing: what the clinic's own box costs to run, and what the same
questions would have cost hosted.

> **Prices below are placeholders and they perish.** Electricity tariffs change yearly and model
> prices change with every release. Substitute today's numbers before quoting anything to
> anybody.

### Running it yourself (electricity)

From Part 3's measurements on the reference box: **35 W idle**, and **120 W above idle while
answering** (roughly 155 W at the wall during generation).

```
idle kWh      = 35 W   x 720 h / 1000                              (the box is on all month)
answering kWh = 120 W  x (Q x S x D) / 3600 / 1000                 (only while it generates)
monthly cost  = (idle kWh + answering kWh) x P                     (P = your price per kWh)

Q = questions per day, S = seconds per answer, D = days per month
```

Worked example — 500 questions a day, 5 s each, 30 days:

```
idle       = 35 W x 720 h            = 25.2 kWh
answering  = 120 W x 20.8 h          =  2.5 kWh      (500 x 5 s x 30 = 75,000 s = 20.8 h)
total                                = 27.7 kWh per month

at P = $0.15/kWh   ->  $4.16 per month
at P = 8/kWh (INR) ->  221 per month
```

The striking part is the split: **91% of the electricity is the box being switched on**, not the
assistant thinking. The clinic's server is already running for the appointments; the assistant
adds 2.5 kWh a month — a few cups of tea. The honest costs of the private option are the GPU
(a one-off purchase, not in this formula) and somebody's time, which is what this runbook is for.

### The hosted comparison

```
monthly cost = D x Q x (T_in x P_in + T_out x P_out) / 1,000,000

T_in, T_out = tokens in and out per question   P_in, P_out = price per million tokens
```

Worked example — the same 500 questions a day for 30 days, 800 input tokens (the retrieved
chunks plus the system prompt plus the question) and 200 output tokens each, at **$3 per million
input** and **$15 per million output**:

```
questions   = 500 x 30                    = 15,000 per month
input       = 15,000 x 800  = 12,000,000  = 12 M tokens  x $3   = $36
output      = 15,000 x 200  =  3,000,000  =  3 M tokens  x $15  = $45
total                                                            = $81 per month
```

So roughly **$4 of electricity against $81 of API**, at these placeholder prices, before the GPU
is paid off — and the GPU is the whole argument only if you ignore the real reason for this
project. The reason is the first sentence of the spec: no token about a patient leaves the
building. The arithmetic is a bonus, and it is the part that will be out of date first.

---

## What to tell the clinic owner

- **Nothing about your patients leaves the building.** The assistant runs on the server in your
  back office. It has no internet connection while it answers, and there is no account with
  anyone to cancel.
- **There is no daily chore and no monthly bill.** The server warms itself up after a power cut
  and looks after itself the rest of the time. It adds a couple of hundred rupees of electricity
  a month at today's tariff, and nothing else.
- **It only knows what you wrote down.** Its answers come from your own clinic documents, and
  every answer shows which document it came from. If it doesn't know, it says so and sends the
  patient to reception; it is not allowed to guess.
- **When your information changes, someone edits a document and re-runs one command.** Fees,
  opening hours, what to bring to a first appointment — the change is live the same afternoon.
  There is no retraining and no waiting on a supplier.
- **Software updates are a ten-minute job, and they are tested before anyone uses them.** After
  any change to the model, we re-run a fixed 50-question exam and compare the scores. If the new
  version is worse at saying "I don't know", we go back to the old one the same day.
