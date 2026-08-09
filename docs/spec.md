# ClinicLive — Specification

> This spec was produced in Part 3 of the coder000 series
> [From Prompt to Production](https://www.coder000.com/blog#from-prompt-to-production):
> a plain-English conversation with Claude, turned into a written plan before any code.

## What we are building

A front-desk system for a small clinic. Three surfaces:

1. **Public booking** — patients book an appointment from their phone. No account,
   no password: they get a short confirmation code.
2. **Check-in kiosk** — a tablet at the door. The patient types their code (or name),
   and they appear on the waiting-room queue instantly.
3. **Staff area** (login required) — today's appointments, a live queue board
   ("now serving…"), and a staff chat with presence, so reception and practitioners
   coordinate without shouting down the corridor.

The queue board and chat are **real-time**: when a patient checks in, every screen
updates within a second, with no refresh button anywhere.

## Personas

| Persona | What they need |
|---|---|
| **Patient** | Book in under a minute; know their place in the queue |
| **Receptionist** | See who's waiting, call the next patient, fix mistakes |
| **Practitioner** | See their own day; tell reception "running 10 min late" via chat |

## User stories (v1)

- As a patient, I can pick a free 15-minute slot on a day and book it with my
  name and phone number, and I receive a 6-character confirmation code.
- As a patient, I can cancel with my code.
- As a patient at the kiosk, I can check in with my code and see my queue position.
- As a receptionist, I can see today's appointments and who has checked in.
- As a receptionist, I can call the next patient; the waiting-room board updates live.
- As staff, I can chat with other staff and see who is online.

## Non-goals (v1)

- No patient accounts or passwords — the confirmation code is the credential.
- No payments, no insurance, no medical records. This is front-desk software.
- No SMS/email sending (the code is shown on screen; notifications are a v2 idea).
- No multi-clinic tenancy. One clinic, one queue.

## Data model (sketch)

- **Patient** — name, phone, email (optional). Created on first booking.
- **Appointment** — patient, date, 15-min slot, status
  (Booked → CheckedIn → InProgress → Done / Cancelled / NoShow), confirmation code.
- **QueueEntry** — appointment, checked-in time, called time. Queue order =
  appointment slot first, check-in time as tiebreaker.
- **ChatMessage** — sender (staff user), text, sent time.
- **Staff** — ASP.NET Identity users; roles: `Reception`, `Practitioner`.

## Technical decisions

| Decision | Choice | Why |
|---|---|---|
| UI | Blazor Server (.NET 10) | C# everywhere; server render fits an internal tool; **it already runs on SignalR** |
| Real-time | SignalR hubs (`QueueHub`, `ChatHub`) | Server push to kiosk, board and chat |
| Database | PostgreSQL 18 via EF Core + Npgsql | The series' database of choice; runs in Docker locally |
| Auth | ASP.NET Core Identity, cookie auth | Staff only; boring and reliable |
| Tests | xUnit + Testcontainers | Integration tests hit a real PostgreSQL, not a fake |
| Hosting | Linux VPS, nginx + systemd | Part 12 deploys exactly this |

## Definition of done (v1)

A patient can book on their phone, check in at the kiosk, watch the board call
them — while reception sees it all live and chats with the practitioner — and the
whole thing survives a server restart with data intact in PostgreSQL.
