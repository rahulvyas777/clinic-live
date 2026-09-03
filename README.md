# ClinicLive

A clinic front-desk system — public appointment booking, a check-in kiosk, a live
waiting-room queue board, and staff chat — built with **Blazor Server (.NET 10),
EF Core, PostgreSQL and SignalR**. Since season three it also has a patient companion
app, **ClinicLive Pocket**, built with **.NET MAUI Blazor Hybrid** for Android and
Windows, sharing its screens with a Blazor web host.

This is the companion repository for three coder000 series —
**[From Prompt to Production](https://www.coder000.com/blog#from-prompt-to-production)**,
**[From Prompt to Polish](https://www.coder000.com/series/from-prompt-to-polish)** and
**[From Prompt to Pocket](https://www.coder000.com/series/from-prompt-to-pocket)** —
the whole thing was built with AI assistance (Claude), and this repo shows the receipts:

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

## Season two: the redesign — [From Prompt to Polish](https://www.coder000.com/series/from-prompt-to-polish)

The same app, redesigned with AI — Bootstrap out, a token design system in.
`git checkout part-12` is forever the "before" photo; the `shots/` folder holds
the before/after evidence for every part.

| Tag | Series part | What changed |
|---|---|---|
| `polish-02` | Part 2 — foundations | Tokens ("porcelain & petrol"), Atkinson Hyperlegible, Bootstrap deleted, top bar, chrome-free kiosk/board |
| `polish-03` | Part 3 — the screenshot loop | `tools/shots` — the Playwright harness that photographs every surface |
| `polish-04` | Part 4 — booking (phone UX) | Grouped slots, the ticket, the copy pass |
| `polish-05` | Part 5 — kiosk (touch UX) | Giant code entry, success takeover, auto-reset |
| `polish-06` | Part 6 — board (signage UX) | Fixed dark palette, vmin sizing, live clock, status-only-when-wrong |
| `polish-07` | Part 7 — staff (pro-tool UX) | Stat headers, WAITED column, chat bubbles |
| `polish-08` | Part 8 — accessibility | Live regions, landmarks, skip link, contrast audit |
| `polish-09` | Part 9 — micro-interactions | Change-explaining motion, chat auto-scroll, reduced-motion off-switch |

## Season three: the companion app — [From Prompt to Pocket](https://www.coder000.com/series/from-prompt-to-pocket)

ClinicLive in a patient's pocket: .NET MAUI Blazor Hybrid on Android and Windows,
the same components hosted as a web app, and the clinic's backend from season one
underneath. Every native feature is a *capability interface* in the shared project,
answered honestly by each host. `shots/pocket/` holds the evidence — emulator,
Windows and browser.

| Tag | Series part | What arrived |
|---|---|---|
| `pocket-02` | Part 2 — one UI, three hosts | `Contracts`, `Pocket.Shared`, MAUI + web hosts, the capability-interface pattern, porcelain & petrol on a phone |
| `pocket-03` | Part 3 — the app needs a door | `/api/pocket` (a thin layer over the season-one services), the typed client, the real My-visit screen |
| `pocket-04` | Part 4 — live queue | The phone joins `QueueHub`; app lifecycle; a reconnect policy that never gives up |
| `pocket-05` | Part 5 — buzz when you're next | Haptics and notifications; permission asked in context; Android channel + runtime permission |
| `pocket-06` | Part 6 — push, for real | Firebase Cloud Messaging from `CallNext` to a closed app; device registration; the stopped-state lesson |
| `pocket-07` | Part 7 — find the clinic | Geolocation, a testable haversine, one-tap directions, Android 11 package visibility |
| `pocket-08` | Part 8 — scan to check in | A QR on the ticket (QRCoder), a native ZXing scanner page over the WebView |
| `pocket-09` | Part 9 — offline & settings | Connectivity, Preferences + SecureStorage, the remembered visit, the cached visit when the signal dies |
| `pocket-10` | Part 10 — on the desk | A desk-sized Windows window, one CSS breakpoint to a side rail, the Waiting-room page |
| `pocket-11` | Part 11 — ship it | Signed Android release, self-contained Windows publish, CI for both, `docs/pocket.md` |

Setup, secrets and publishing: **[docs/pocket.md](docs/pocket.md)**.

## Run it locally

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download), Docker.

```bash
docker compose up -d          # PostgreSQL 18 on localhost:5499
cd src/ClinicLive
dotnet run
```

Then open the printed URL. Seeded staff login: `reception@cliniclive.test` / `Clinic!Live1`
(demo-only credentials — everything in the seeder is fictional; `.test` addresses
can never exist).

## Reading the history

```bash
git log --reverse --format="%n=== %s ===%n%b"
```

Each body starts with `Prompt:` — the instruction given to the AI that produced the
commit. Some commits (deliberately!) contain the mistakes the series later finds and
fixes; don't cherry-pick them into anything real.

## License

MIT — use it, break it, rebuild it with your own prompts.
