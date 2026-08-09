# ClinicLive

A clinic front-desk system — public appointment booking, a check-in kiosk, a live
waiting-room queue board, and staff chat — built with **Blazor Server (.NET 10),
EF Core, PostgreSQL and SignalR**.

This is the companion repository for the coder000 series
**[From Prompt to Production: Build a Real App with AI](https://www.coder000.com/blog#from-prompt-to-production)** —
the whole application was built with AI assistance (Claude), and this repo shows the receipts:

> **Every commit message contains the actual prompt that produced the change.**
> `git log` *is* the tutorial.

## Follow along by tag

| Tag | Series part | State of the app |
|---|---|---|
| `part-03` | Part 3 — the spec | `docs/spec.md`, nothing else |
| `part-04` | Part 4 — the schema | ER thinking + DDL notes in `docs/` |
| `part-05` | Part 5 — the skeleton | Blazor Server + EF Core + Npgsql + Identity, first migration |
| `part-06` | Part 6 — booking CRUD | Public booking + confirmation codes + staff appointment list |
| `part-07` | Part 7 — live queue | Check-in kiosk + `QueueHub` + live waiting-room board |
| `part-08` | Part 8 — staff chat | `ChatHub`, presence, typing indicator, persisted history |
| `part-09` | Part 9 — tests | xUnit unit tests + Testcontainers integration tests |
| `part-10` | Part 10 — debugging | Two real bugs found and fixed (see those commit messages!) |
| `part-11` | Part 11 — hardening | Review pass: indexes, authorization audit, validation |
| `part-12` | Part 12 — production | `deploy/` runbook (nginx + systemd) + GitHub Actions CI |

```bash
git checkout part-07   # see the app exactly as it stands at the end of Part 7
```

## Run it locally

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download), Docker.

```bash
docker compose up -d          # PostgreSQL 18 on localhost:5499
cd src/ClinicLive
dotnet run
```

Then open the printed URL. Seeded staff login: `reception@clinic.live` / `Clinic!Live1`
(see `docs/seed-data.md`).

## Reading the history

```bash
git log --reverse --format="%n=== %s ===%n%b"
```

Each body starts with `Prompt:` — the instruction given to the AI that produced the
commit. Some commits (deliberately!) contain the mistakes the series later finds and
fixes; don't cherry-pick them into anything real.

## License

MIT — use it, break it, rebuild it with your own prompts.
