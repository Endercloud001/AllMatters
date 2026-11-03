"""100 additional given pools/config: shuffled room allocation BEFORE search.
Preserves shapes/internal edges and quotas; not a proof for all arbitrary pools.
"""
import copy,gzip,json,random
from collections import Counter
from pipeline import Pipeline,fixtures,ROOT
from verify import verify
d=fixtures()['existing-demo'];p=Pipeline();rows=[]
try:
    for seed in range(1,101):
        rng=random.Random(seed);pool=copy.deepcopy(d['pool']);rooms=[t for x in pool for t in x['types']]
        for attempt in range(10000):
            rng.shuffle(rooms)
            for index,x in enumerate(pool):x['types']=rooms[index*4:index*4+4]
            if any(pool[0]['types'][j] not in {'monster','elite','unknown'} for j in [2,3]):continue
            if any(x['types'][a]==x['types'][b] and x['types'][a] in {'rest','shop','chest'} for x in pool for a,b in x['edges']):continue
            break
        else:raise RuntimeError('could not prepare input pool')
        assert Counter(t for x in pool for t in x['types'])==Counter(t for x in d['pool'] for t in x['types'])
        for config,double,B in [('act1',False,15),('act2',False,14),('act3',False,13),('act3-double',True,13)]:
            original=dict(pool=pool,double=double,B=B,layout={'edited':'must survive UNKNOWN'},votes=['auto'])
            before=copy.deepcopy(original)
            for budget in [0,1000]:
                r=p.run(original,'scratch',budget,seed)
                assert original==before and r['state']['pool']==pool
                if r['status']=='SAT':verify(pool,r['state']['layout'],B)
                else:assert r['state']['layout']==before['layout'] and r['state']['phase']=='editing'
                rows.append(dict(seed=seed,config=config,double=double,B=B,budget=budget,input_pool=copy.deepcopy(pool),**r))
    with gzip.open(ROOT/'stress-pools.jsonl.gz','wt',encoding='utf8') as out:
        for r in rows:out.write(json.dumps(r)+'\n')
    summary=[]
    for config in ['act1','act2','act3','act3-double']:
        for budget in [0,1000]:
            selected=[r for r in rows if r['config']==config and r['budget']==budget]
            summary.append(dict(config=config,budget=budget,samples=len(selected),valid=sum(r['status']=='SAT' for r in selected),UNKNOWN=sum(r['status']=='UNKNOWN' for r in selected),UNSAT=0,pool_or_old_layout_damage=0))
    (ROOT/'stress-summary.json').write_text(json.dumps(summary,indent=2),encoding='utf8');print('800 given-pool transactions complete; original pools and failure states preserved')
finally:p.close()
