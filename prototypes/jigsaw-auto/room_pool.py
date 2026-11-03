"""Experiment input preparation only. Never called to repair a given search pool."""
import copy,random
QUOTA=['monster']*20+['unknown']*16+['elite']*6+['shop']*4+['rest']*6+['chest']*4
def make_pool(template,seed,rest_pieces=None):
    rng=random.Random(seed);pool=copy.deepcopy(template)
    for attempt in range(10000):
        rests=rest_pieces if rest_pieces is not None else rng.sample(range(14),6)
        assigned={i*4+rng.choice([0,1] if i==0 else [0,1,2,3]):'rest' for i in rests}
        remaining=[t for t in QUOTA if t!='rest'];rng.shuffle(remaining);it=iter(remaining)
        types=[assigned[i] if i in assigned else next(it) for i in range(56)]
        for i,p in enumerate(pool):p['types']=types[4*i:4*i+4]
        if any(pool[0]['types'][j] not in {'monster','unknown','elite'} for j in [2,3]):continue
        if any(len(set(p['types']))==1 for p in pool):continue
        if any(p['types'][a]==p['types'][b] and p['types'][a] in {'shop','rest','chest','elite'} for p in pool for a,b in p['edges']):continue
        return pool
    raise RuntimeError('input preparation exhausted; no search pool was supplied')
