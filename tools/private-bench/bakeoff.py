#!/usr/bin/env python3
"""From Prompt to Private, Part 2: the model bake-off.

Runs every question in questions.json against each model through Ollama's chat API with the
clinic sheet as system context, scores the answers by required / forbidden phrases, tests the
native tools API, and checks the embedding model on hit/miss pairs. Prints a scoreboard and
writes results-<model>.json next to this file. Nothing here touches patient data: the clinic
sheet is fictional.

Usage: python bakeoff.py llama3.1:8b qwen2.5:14b gemma3:12b
"""
import json, sys, time, os, math, urllib.request, urllib.error

HOST = os.environ.get('OLLAMA_HOST', 'http://127.0.0.1:11434')
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = json.load(open(os.path.join(HERE, 'questions.json'), encoding='utf-8'))
EMBED_MODEL = 'nomic-embed-text'

SYSTEM = ("You are the assistant of Sunrise Family Clinic. Answer only from the clinic information below. "
          "If the information does not answer the question, say that you do not know and suggest asking reception. "
          "Never give medical advice, doses or medicine names; for anything medical tell the patient to speak to the doctor, "
          "and in an emergency to call 108. Be brief.\n\nCLINIC INFORMATION:\n" + DATA['clinic_doc'])

def post(path, body, timeout=600):
    req = urllib.request.Request(HOST + path, json.dumps(body).encode(), {'Content-Type': 'application/json'})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read())

def chat(model, question, tools=None):
    body = {'model': model, 'stream': False, 'keep_alive': '10m',
            'messages': [{'role': 'system', 'content': SYSTEM}, {'role': 'user', 'content': question}],
            'options': {'temperature': 0.1, 'num_predict': 300}}
    if tools:
        body['tools'] = tools
    t0 = time.time()
    d = post('/api/chat', body)
    d['wall'] = time.time() - t0
    return d

def score(q, answer):
    a = answer.lower()
    missing = [m for m in q['must'] if m.lower() not in a]
    anyof = q.get('must_any') or []
    if anyof and not any(m.lower() in a for m in anyof):
        missing.append('one of: ' + ' / '.join(anyof))
    forbidden = [m for m in q['must_not'] if m.lower() in a]
    return (not missing and not forbidden), missing, forbidden

TOOLS = [{'type': 'function', 'function': {
    'name': 'get_queue',
    'description': 'Returns the patients currently waiting in the clinic, with how long each has waited, longest first.',
    'parameters': {'type': 'object', 'properties': {'status': {'type': 'string', 'enum': ['waiting', 'called', 'done'],
                   'description': 'Which queue status to list'}}, 'required': ['status']}}}]

def cosine(a, b):
    dot = sum(x * y for x, y in zip(a, b))
    return dot / (math.sqrt(sum(x * x for x in a)) * math.sqrt(sum(y * y for y in b)))

def embed(text):
    return post('/api/embed', {'model': EMBED_MODEL, 'input': text})['embeddings'][0]

def run_model(model):
    print(f'\n===== {model}')
    results = {'model': model, 'answers': [], 'tools': []}
    kinds = {}
    total_wall = 0.0
    for q in DATA['questions']:
        d = chat(model, q['q'])
        text = d['message']['content'].strip()
        ok, missing, forbidden = score(q, text)
        kinds.setdefault(q['kind'], [0, 0]); kinds[q['kind']][1] += 1; kinds[q['kind']][0] += int(ok)
        total_wall += d['wall']
        results['answers'].append({'id': q['id'], 'kind': q['kind'], 'q': q['q'], 'answer': text, 'ok': ok,
                                   'missing': missing, 'forbidden': forbidden, 'seconds': round(d['wall'], 1),
                                   'tokens': d.get('eval_count')})
        flag = 'ok ' if ok else 'XX '
        print(f"{flag} {q['id']:>2} {q['kind']:<10} {round(d['wall'],1):>5}s  {text[:90].replace(chr(10),' ')}")
    passed = sum(v[0] for v in kinds.values()); n = sum(v[1] for v in kinds.values())
    results['score'] = {'passed': passed, 'of': n, 'by_kind': kinds, 'avg_seconds': round(total_wall / n, 1)}
    print(f"score {passed}/{n}  " + '  '.join(f"{k} {v[0]}/{v[1]}" for k, v in kinds.items()) + f"  avg {total_wall / n:.1f}s")

    # native tools API
    for tq in DATA['tool_questions']:
        try:
            d = chat(model, tq['q'], TOOLS)
            calls = d['message'].get('tool_calls') or []
            name = calls[0]['function']['name'] if calls else None
            args = calls[0]['function'].get('arguments') if calls else None
            ok = (name == tq['expect_tool']) if tq['expect_tool'] else (not calls)
            results['tools'].append({'id': tq['id'], 'q': tq['q'], 'called': name, 'arguments': args, 'ok': ok,
                                     'text': d['message']['content'][:120]})
            print(f"tool {tq['id']} {'ok ' if ok else 'XX '} called={name} args={args}")
        except urllib.error.HTTPError as e:
            msg = e.read().decode()[:120]
            results['tools'].append({'id': tq['id'], 'q': tq['q'], 'error': msg, 'ok': False})
            print(f"tool {tq['id']} XX  error: {msg}")
    json.dump(results, open(os.path.join(HERE, f"results-{model.replace(':', '-')}.json"), 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
    return results

def run_embeddings():
    print(f'\n===== embeddings: {EMBED_MODEL}')
    t0 = time.time()
    rows = []
    for q, passage, kind in DATA['embedding_pairs']:
        s = cosine(embed(q), embed(passage))
        rows.append((kind, round(s, 3), q, passage[:50]))
        print(f"{kind:<4} {s:.3f}  {q[:40]:<40} | {passage[:50]}")
    hits = [r[1] for r in rows if r[0] == 'hit']; misses = [r[1] for r in rows if r[0] == 'miss']
    print(f"lowest hit {min(hits):.3f} vs highest miss {max(misses):.3f} -> {'separable' if min(hits) > max(misses) else 'NOT separable'}; "
          f"{len(rows) * 2} embeddings in {time.time() - t0:.1f}s")
    json.dump(rows, open(os.path.join(HERE, 'results-embeddings.json'), 'w'), indent=1)

if __name__ == '__main__':
    models = [a for a in sys.argv[1:] if not a.startswith('--')]
    for m in models:
        run_model(m)
    if '--no-embed' not in sys.argv:
        run_embeddings()
