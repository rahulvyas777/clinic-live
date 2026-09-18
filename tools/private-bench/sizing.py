#!/usr/bin/env python3
"""From Prompt to Private, Part 3: what server does a clinic need.

Measures, on this machine through Ollama:
  1. CPU-only runs (num_gpu=0) at 4 and 8 threads for each model: prompt-reading and generation
     speed on a realistic grounded prompt (the clinic sheet plus a question, ~600 tokens).
  2. GPU concurrency: 1, 2, 4 and 8 simultaneous requests, aggregate tokens/s and per-request wait.
  3. GPU power draw sampled with nvidia-smi while generating (idle vs busy).
Writes sizing-results.json next to this file. Usage: python sizing.py qwen2.5:14b llama3.1:8b qwen2.5:3b
"""
import json, sys, os, time, threading, statistics, subprocess, urllib.request, uuid

HOST = os.environ.get('OLLAMA_HOST', 'http://127.0.0.1:11434')
HERE = os.path.dirname(os.path.abspath(__file__))
DATA = json.load(open(os.path.join(HERE, 'questions.json'), encoding='utf-8'))
SHEET = DATA['clinic_doc']
PROMPT = ("Use only the clinic information below to answer, briefly.\n\n" + SHEET + "\n\n" + SHEET.replace('Sunrise', 'Sunrise (repeated for length)') +
          "\n\nQuestion: I am 70 and want a follow-up visit 5 days after my first visit. What will I pay, and what should I bring?")

def gen(model, opts, keep='5m'):
    # a unique prefix defeats the prompt cache, so prompt-reading speed is measured, not remembered
    body = {'model': model, 'prompt': f'[request {uuid.uuid4()}]\n' + PROMPT, 'stream': False, 'keep_alive': keep,
            'options': {'temperature': 0.1, 'num_predict': 120, **opts}}
    req = urllib.request.Request(HOST + '/api/generate', json.dumps(body).encode(), {'Content-Type': 'application/json'})
    t0 = time.time()
    with urllib.request.urlopen(req, timeout=1800) as r:
        d = json.loads(r.read())
    d['wall'] = time.time() - t0
    return d

def unload(model):
    urllib.request.urlopen(urllib.request.Request(HOST + '/api/generate', json.dumps({'model': model, 'keep_alive': 0}).encode(), {'Content-Type': 'application/json'})).read()

def rates(d):
    pe = d['prompt_eval_count'] / (d['prompt_eval_duration'] / 1e9) if d.get('prompt_eval_duration') else None
    ge = d['eval_count'] / (d['eval_duration'] / 1e9) if d.get('eval_duration') else None
    return {'prompt_tokens': d.get('prompt_eval_count'), 'prompt_tps': round(pe, 1) if pe else None,
            'first_word_s': round(d.get('prompt_eval_duration', 0) / 1e9, 1), 'gen_tps': round(ge, 1) if ge else None,
            'wall_s': round(d['wall'], 1)}

def resident():
    out = subprocess.run([os.path.expandvars(r'%LOCALAPPDATA%\Programs\Ollama\ollama.exe'), 'ps'], capture_output=True, text=True).stdout
    return [l for l in out.splitlines()[1:] if l.strip()]

class Power:
    def __init__(self):
        self.samples = []; self.stop = False
    def run(self):
        while not self.stop:
            try:
                w = subprocess.run(['nvidia-smi', '--query-gpu=power.draw', '--format=csv,noheader,nounits'], capture_output=True, text=True, timeout=5).stdout.strip()
                self.samples.append(float(w))
            except Exception:
                pass
            time.sleep(0.5)

results = {'cpu': [], 'gpu_concurrency': [], 'power': {}}
models = [a for a in sys.argv[1:] if not a.startswith('--')]

# ---- 1. CPU-only ----
for model in ([] if '--gpu-only' in sys.argv else models):
    for threads in (4, 8):
        unload(model); time.sleep(2)
        d = gen(model, {'num_gpu': 0, 'num_thread': threads})   # cold: includes load
        d = gen(model, {'num_gpu': 0, 'num_thread': threads})   # warm measurement
        r = rates(d); r.update({'model': model, 'threads': threads, 'resident': resident()})
        results['cpu'].append(r)
        print(f"CPU {model} {threads} threads: prompt {r['prompt_tokens']} tok read at {r['prompt_tps']} tok/s -> first word after {r['first_word_s']} s; generation {r['gen_tps']} tok/s; total {r['wall_s']} s", flush=True)
    unload(model)

# ---- 2/3. GPU concurrency + power ----
idle = Power(); t = threading.Thread(target=idle.run, daemon=True); t.start(); time.sleep(5); idle.stop = True
results['power']['idle_w'] = round(statistics.mean(idle.samples), 1) if idle.samples else None
for model in ([] if '--cpu-only' in sys.argv else models):
    if model.endswith(':3b'):
        continue
    unload(model); time.sleep(2)
    gen(model, {})  # load
    for n in (1, 2, 4, 8):
        pw = Power(); pt = threading.Thread(target=pw.run, daemon=True); pt.start()
        outs = [None] * n
        def worker(i):
            outs[i] = gen(model, {})
        t0 = time.time()
        ths = [threading.Thread(target=worker, args=(i,)) for i in range(n)]
        [x.start() for x in ths]; [x.join() for x in ths]
        wall = time.time() - t0
        pw.stop = True; pt.join(timeout=2)
        toks = sum(o['eval_count'] for o in outs)
        per = [rates(o) for o in outs]
        row = {'model': model, 'parallel': n, 'wall_s': round(wall, 1), 'aggregate_tps': round(toks / wall, 1),
               'per_request_gen_tps': round(statistics.mean(p['gen_tps'] for p in per), 1),
               'slowest_wall_s': round(max(p['wall_s'] for p in per), 1),
               'power_mean_w': round(statistics.mean(pw.samples), 1) if pw.samples else None,
               'power_max_w': round(max(pw.samples), 1) if pw.samples else None}
        results['gpu_concurrency'].append(row)
        print(f"GPU {model} x{n}: {row['aggregate_tps']} tok/s aggregate, {row['per_request_gen_tps']} tok/s each, slowest waited {row['slowest_wall_s']} s, power mean {row['power_mean_w']} W max {row['power_max_w']} W", flush=True)
    unload(model)

tag = 'cpu' if '--cpu-only' in sys.argv else 'gpu' if '--gpu-only' in sys.argv else 'all'
json.dump(results, open(os.path.join(HERE, f'sizing-results-{tag}.json'), 'w', encoding='utf-8'), indent=1)
print('idle power', results['power']['idle_w'], 'W; saved sizing-results.json')
