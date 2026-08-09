# ClinicLive — Database Design

> Part 4 artifact: the spec's data-model sketch turned into real PostgreSQL DDL.
> In Part 5 this becomes EF Core entities + a migration; this document records the
> *thinking*, so the schema isn't just something the AI handed us.

## Entities and relationships

```
Patient 1 ──── * Appointment 1 ──── 0..1 QueueEntry
                                            (a queue entry exists only after check-in)
AspNetUsers (staff, via ASP.NET Identity) 1 ──── * ChatMessage
```

- A **Patient** is created the first time a phone number books. No login.
- An **Appointment** belongs to one patient, occupies one 15-minute slot on one day,
  and carries a 6-character `confirmation_code` (the patient's only credential).
- A **QueueEntry** is created at kiosk check-in. Queue order = slot time, then
  check-in time. Splitting it from Appointment keeps "the schedule" and "the queue"
  as different ideas — the board only cares about QueueEntries.
- **ChatMessage** references the Identity user who sent it.

## The DDL (design draft)

```sql
CREATE TABLE patient (
    id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    full_name    varchar(200) NOT NULL,
    phone        varchar(30)  NOT NULL,
    email        varchar(200),
    created_at   timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ix_patient_phone ON patient (phone);

CREATE TABLE appointment (
    id                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    patient_id        bigint       NOT NULL REFERENCES patient(id),
    starts_at         timestamptz  NOT NULL,
    status            varchar(20)  NOT NULL DEFAULT 'Booked',
    confirmation_code varchar(6)   NOT NULL,
    created_at        timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX ix_appointment_patient  ON appointment (patient_id);
CREATE UNIQUE INDEX ix_appointment_slot ON appointment (starts_at)
    WHERE status NOT IN ('Cancelled', 'NoShow');
CREATE UNIQUE INDEX ix_appointment_code ON appointment (confirmation_code);

CREATE TABLE queue_entry (
    id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    appointment_id bigint      NOT NULL UNIQUE REFERENCES appointment(id),
    checked_in_at  timestamptz NOT NULL DEFAULT now(),
    called_at      timestamptz
);

CREATE TABLE chat_message (
    id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    sender_id  text        NOT NULL,   -- AspNetUsers.Id
    body       varchar(2000) NOT NULL,
    sent_at    timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_chat_message_sent ON chat_message (sent_at);
```

## Decisions worth defending

| Decision | Why |
|---|---|
| `timestamptz` everywhere | The waiting room does not care what timezone the server thinks it is in. Store UTC, render local. (Part 10 shows what happens when you forget this.) |
| Partial unique index on `starts_at` | Two *active* bookings can't share a slot, but a cancelled one frees it — a plain UNIQUE would block re-booking a freed slot. |
| Unique `confirmation_code` | It's the patient's credential; collisions would check in the wrong person. Generated from an unambiguous alphabet (no 0/O, 1/I). |
| Queue split from Appointment | The board subscribes to queue changes only; the schedule page doesn't re-render when someone checks in. |
| `varchar` status, not a PG enum | EF Core maps string enums painlessly; a check constraint arrives in Part 11's hardening pass. |

## What the schema reviewer caught

The AI's *first* draft had no index on `appointment.patient_id` and used
`timestamp` (without time zone). Pasting the draft into coder000's free
[schema reviewer](https://www.coder000.com/tools/schema-review) flagged both —
the fixes are already folded into the DDL above. Trust, but verify.
