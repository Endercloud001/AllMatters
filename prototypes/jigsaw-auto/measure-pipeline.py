"""100 seeds/config, end-to-end verified transaction timing. Run serially."""
import gzip,json,time,statistics,platform
from collections import defaultdict,Counter
from pipeline import Pipeline,fixtures,ROOT
start=time.perf_counter();f=fixtures();fixture_ms=(time.perf_counter()-start)*1000
p=Pipeline();groups=defaultdict(list)
try:
    with gzip.open(ROOT/'pipeline-raw.jsonl.gz','wt',encoding='utf8') as out:
        for family,data in f.items():
            for config,double,B in [('act1',False,15),('act2',False,14),('act3',False,13),('act3-double',True,13)]:
                original=dict(pool=data['pool'],double=double,B=B,layout={'edited':'preserve on failure'},votes=['auto'])
                for seed in range(1,101):
                    for budget in [0,100,1000,10000]:
                        for method in ['witness','extend','scratch']:
                            r=p.run(original,method,budget,seed,data['witnesses'][double])
                            record=dict(family=family,config=config,B=B,double=double,seed=seed,budget=budget,method=method,**r)
                            out.write(json.dumps(record)+'\n');groups[(family,config,method,budget)].append(record)
                print(family,config,'complete',flush=True)
    summary=[]
    for key,rows in groups.items():
        t=sorted(r['timing']['total_ms'] for r in rows);ok=[r for r in rows if r['status']=='SAT']
        summary.append(dict(family=key[0],config=key[1],method=key[2],budget=key[3],samples=len(rows),valid=len(ok),UNKNOWN=len(rows)-len(ok),UNSAT=0,p50_ms=statistics.median(t),p95_ms=t[94],fallbacks=sum(r['fallback'] for r in rows),rejections=sum(bool(r['rejection']) for r in rows),blocks=dict(Counter(r['metrics']['blocks'] for r in ok)),rss_max=max(r['rss'] or 0 for r in rows)))
    (ROOT/'pipeline-summary.json').write_text(json.dumps(dict(fixture_load_ms=fixture_ms,node_process_spawn_ms=p.startup_ms,platform=platform.platform(),groups=summary),indent=2),encoding='utf8')
    # Separately measure the old demo's cold witness construction; not budgeted search.
    bootstrap=[]
    data=f['existing-demo']
    for double,B in [(False,15),(False,14),(False,13),(True,13)]:
        original=dict(pool=data['pool'],double=double,B=B,layout={},votes=[])
        for seed in range(1,101):
            bootstrap.append(dict(double=double,B=B,seed=seed,**p.run(original,'bootstrap',0,seed)))
    with gzip.open(ROOT/'bootstrap-timing.jsonl.gz','wt',encoding='utf8') as out:
        for r in bootstrap:out.write(json.dumps(r)+'\n')
    print('9600 verified transactions and 400 old-bootstrap measurements complete',flush=True)
finally:p.close()
