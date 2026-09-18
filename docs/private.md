# Season four — From Prompt to Private

ClinicLive gets an AI assistant that runs on the clinic's own server. No token about a patient
ever leaves the building. This document is the spec the build parts follow; the posts on
coder000.com tell the story, `git log` carries the prompts, `.build-log.md` (never committed)
carries the mistakes and the meter.

## Scope

- **Model runtime:** Ollama on Linux, systemd service, bound to 127.0.0.1 only. The application
  is the only client. If the runtime ever lives on a different box, it goes behind nginx with
  TLS and a shared key (pattern in `deploy/ollama/`), never on the open port.
- **Models:** Qwen 2.5 14B (primary, tools, JSON), Llama 3.1 8B (fallback for 8 GB cards and
  CPU-only), nomic-embed-text (embeddings, 768 dims). Chosen in Part 2 by the exam in
  `tools/private-bench`.
- **Two assistants, one pipeline.** Staff assistant inside `/staff/*` (all documents, may call
  `get_queue`). Patient assistant on `/kiosk` and in the Pocket app (public documents only, no
  tools, rate-limited). Same retrieval code, different document visibility and system prompt.
- **Knowledge:** the clinic's own markdown documents under `knowledge/` (fictional: Sunrise
  Family Clinic), chunked, embedded, stored in PostgreSQL with pgvector, retrieved by cosine
  similarity, cited back to the document. Visibility is a column on the chunk, not a prompt.
- **Non-goals:** fine-tuning, multi-GPU, medical advice of any kind, voice, hosted models.

## Configuration (`Ai:` section, appsettings)

```
"Ai": {
  "Endpoint": "http://127.0.0.1:11434",
  "ChatModel": "qwen2.5:14b",
  "EmbeddingModel": "nomic-embed-text",
  "KeepAlive": "24h",
  "MaxContextChunks": 6
}
```

## Parts

| Part | Tag | Delivers |
|---|---|---|
| 2 | private-02 | `tools/private-bench`: exam, scorer, results |
| 3 | private-03 | `tools/private-bench/sizing.py`, sizing results, this doc |
| 4 | private-04 | `deploy/ollama/`: install notes, systemd override, nginx pattern, smoke test |
| 5 | private-05 | `Services/Ai/`: `IAiChat` over OllamaSharp via Microsoft.Extensions.AI, streaming; `/staff/assistant` page |
| 6 | private-06 | pgvector: `knowledge_document`, `knowledge_chunk` tables; `knowledge/*.md`; `dotnet run -- ingest` |
| 7 | private-07 | retrieval + grounded answer + citations + refusal; visibility (`public` / `staff`) |
| 8 | private-08 | tool calling: `get_queue`; guard against unasked tool use |
| 9 | private-09 | `/api/pocket/assistant` (public docs only, rate limit), kiosk panel, Pocket screen |
| 10 | private-10 | `tools/private-bench/eval.py`: 50-question set, three models scored |
| 11 | private-11 | `deploy/ollama/OPERATIONS.md`: updates, backups, logs, cost per month |
| 12 | — | retro post only |

## Rules that hold for every part

- Snake_case tables and `ix_<table>_<cols>` indexes (EFCore.NamingConventions); `timestamptz`.
- Every AI answer shown to a user carries either a citation to a document or the sentence
  "I don't know that; please ask reception." Never both, never neither.
- The patient assistant never sees a `staff` chunk. Enforced in the SQL, tested in xUnit.
- No medicine names, doses or diagnoses from either assistant. Refusal text is fixed, not
  generated.
- Screenshots are real (Playwright harness or the window harness), and every one is looked at
  before it is committed.
