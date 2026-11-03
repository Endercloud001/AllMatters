"""Run after search.cjs. Keep rejected outputs as evidence, never count them as SAT."""
import json, statistics, platform, sys
from pathlib import Path
from collections import defaultdict, Counter
from verify import verify

root = Path(__file__).parent
controlled = '--controlled' in sys.argv
prefix = 'controlled-' if controlled else ''
pool = json.loads((root/('controlled.json' if controlled else 'input.json')).read_text(encoding='utf8'))['pool']
groups = defaultdict(list)
errors = []
with (root/(prefix+'checked.jsonl')).open('w',encoding='utf8') as output:
    for line in (root/(prefix+'raw.jsonl')).open(encoding='utf8'):
        r = json.loads(line)
        if r['scene']:
            try:
                r['metrics'] = verify(pool, r['scene'], r['B'])
                r['independent'] = True
            except ValueError as e:
                r['independent'] = False; r['rejection'] = str(e)
                errors.append({k:r[k] for k in ('config','seed','budget','method','rejection')})
        # This search omits optional non-tree cross edges. Exhaustion is not global UNSAT.
        if r['status'] == 'UNSAT':
            r['status'] = 'UNKNOWN'; r['reason'] = 'restricted enumeration exhausted'
        groups[(r['config'],r['method'],r['budget'])].append(r)
        output.write(json.dumps(r)+'\n')
summary = dict(environment=dict(python=sys.version, platform=platform.platform()), rejections=errors, groups=[])
def signature(scene):
    coords={n['id']:(n['x'],n['y']) for n in scene['nodes']}
    edges=sorted((min(e['a'],e['b']),max(e['a'],e['b']),e['internal']) for e in scene['edges'])
    variants=[]
    for _ in range(4):
        variants.append(json.dumps((sorted(coords.items()),edges)))
        coords={k:(-y,x) for k,(x,y) in coords.items()}
    return min(variants)
for key, rows in groups.items():
    ok = [r for r in rows if r.get('independent')]
    times = sorted(r['ms'] for r in rows)
    layouts = Counter(json.dumps(dict(nodes=sorted(r['scene']['nodes'],key=lambda n:n['id']), edges=sorted(r['scene']['edges'],key=lambda e:(e['a'],e['b']))),sort_keys=True) for r in ok)
    summary['groups'].append(dict(config=key[0],method=key[1],budget=key[2],samples=len(rows),valid=len(ok),UNSAT=0,UNKNOWN=sum(r['status']=='UNKNOWN' for r in rows),rejected=sum(r.get('independent') is False for r in rows),p50_ms=statistics.median(times),p95_ms=times[int(.95*(len(times)-1))],max_process_rss=max(r['rss'] for r in rows),unique_layouts=len(layouts),unique_up_to_global_rotation=len({signature(r['scene']) for r in ok}),fallbacks=sum(bool(r.get('fallback')) for r in rows),blocks=dict(Counter(r['metrics']['blocks'] for r in ok)),distances=dict(Counter(r['metrics']['distance'] for r in ok))))
(root/(prefix+'summary.json')).write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(dict(rows=sum(map(len,groups.values())),rejections=len(errors),groups=len(groups))))
