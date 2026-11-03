"""Independent Python verifier. No imports from the JS generator or old verifier.
Input: original pool, placed nodes, explicit edges, used piece IDs and double flag.
Raises ValueError on rejection. Does not withdraw disconnected pieces (submit does).
"""
from collections import Counter, deque
from itertools import combinations


def need(ok, reason):
    if not ok:
        raise ValueError(reason)


def clearance(a, b):
    radius=lambda n: int(n['type'] in {'neow','boss','boss2'})
    return max(abs(a['x']-b['x']),abs(a['y']-b['y'])) >= radius(a)+radius(b)+2


def check_walk(s, walk, budget):
    by={n['id']:n for n in s['nodes']};done={'N'}
    g={n:set() for n in by}
    for e in s['edges']:g[e['a']].add(e['b']);g[e['b']].add(e['a'])
    need(len(walk)==budget, 'walk length')
    for index,n in enumerate(walk):
        need(n in by and n not in done, 'walk repeated/unplaced node')
        need(by[n]['type'] not in {'neow','boss2'}, 'walk excluded target')
        need((by[n]['type']=='boss') == (index==budget-1), 'walk Boss timing')
        need(bool(g[n]&done), 'walk crosses unexplored region')
        done.add(n)


def auto_requirements(pool, s, budget, reachable_normal, distance):
    """Auto-only stage. Geometry and graph validity are prerequisites, not bypasses."""
    used=set(s['used']);by={n['id']:n for n in s['nodes']}
    required={p['id'] for p in pool if 'rest' in p['types']}
    boss_piece=next(n['piece'] for n in s['nodes'] if n['type']=='boss')
    direct=False;neighbors=set()
    for e in s['edges']:
        if e['internal']:continue
        owners={by[e['a']]['piece'],by[e['b']]['piece']}
        direct |= owners=={-1,boss_piece}
        if boss_piece in owners:neighbors |= owners-{-1,boss_piece}
    need(required<=used,'missing rest fragment')
    need(not direct or len(neighbors)>=2,'boss neighbors')
    need(len(reachable_normal)>=budget-1,'full budget capacity')
    need(distance<=budget,'step limit')
    return direct,neighbors


def verify(pool, s, budget, auto=True):
    p = {x['id']: x for x in pool}
    need(len(p) == 14 and Counter(x['shape'] for x in pool) == Counter({k: 2 for k in 'IOTSZJL'}), 'pool shapes')
    need(Counter(t for x in pool for t in x['types']) == Counter(monster=20, unknown=16, elite=6, shop=4, rest=6, chest=4), 'pool quota')
    for x in pool:
        need(len(x['pts']) == len(x['types']) == 4, 'pool size')
        canonical={'I':[(0,0),(1,0),(2,0),(3,0)],'O':[(0,0),(1,0),(0,1),(1,1)],'T':[(0,0),(1,0),(2,0),(1,1)],'S':[(1,0),(2,0),(0,1),(1,1)],'Z':[(0,0),(1,0),(1,1),(2,1)],'J':[(0,0),(0,1),(1,1),(2,1)],'L':[(2,0),(0,1),(1,1),(2,1)]}
        norm=lambda pts: sorted((a-min(v[0] for v in pts),b-min(v[1] for v in pts)) for a,b in pts)
        pts=canonical[x['shape']];rotations=[]
        for _ in range(4):
            rotations.append(norm(pts));pts=[(-b,a) for a,b in pts]
        need(norm(x['pts']) in rotations, 'pool shape/mirror')
        need(len({tuple(v) for v in x['pts']}) == 4, 'pool duplicate cell')
        g = {i: set() for i in range(4)}
        for a, b in x['edges']:
            need(a in g and b in g and a != b, 'pool edge ID')
            need(sum(abs(x['pts'][a][k]-x['pts'][b][k]) for k in (0, 1)) == 1, 'pool skeleton edge')
            need(not (x['types'][a] == x['types'][b] and x['types'][a] in {'rest', 'shop', 'chest'}), 'special internal edge')
            g[a].add(b); g[b].add(a)
        seen = {0}
        for _ in range(4):
            seen |= set().union(*(g[i] for i in seen))
        need(len(seen) == 4, 'pool internal connectivity')
    nodes = s['nodes']; by = {n['id']: n for n in nodes}
    used = set(s['used']); double = s['double']
    need(len(by) == len(nodes) and len(used) == len(s['used']) and used <= p.keys(), 'duplicate/unknown ID')
    need('N' in by and (by['N']['x'], by['N']['y'], by['N']['piece'], by['N']['type']) == (0, 0, -1, 'neow'), 'origin')
    need(set(by) == {'N'} | {f'{i}:{j}' for i in used for j in range(4)}, 'whole fragments')
    expected_edges = set()
    for i in used:
        ns = [by[f'{i}:{j}'] for j in range(4)]
        need(all(n['piece'] == i for n in ns), 'owner')
        d = 4 if i == 0 and double else 3 if i == 0 else 2
        pts = [(x*d, y*d) for x, y in p[i]['pts']]
        legal = False
        for _ in range(4):
            tx, ty = ns[0]['x']-pts[0][0], ns[0]['y']-pts[0][1]
            legal |= all((n['x'], n['y']) == (x+tx, y+ty) for n, (x, y) in zip(ns, pts))
            pts = [(-y, x) for x, y in pts]
        need(legal, 'rotation/spacing')
        for j, n in enumerate(ns):
            t = ('boss2' if double else 'boss') if i == 0 and j == 3 else 'boss' if i == 0 and j == 2 and double else p[i]['types'][j]
            need(n['type'] == t, 'room mutation')
            if t.startswith('boss'):
                need(p[i]['types'][j] in {'monster', 'elite', 'unknown'}, 'boss replacement')
        expected_edges |= {tuple(sorted((f'{i}:{a}', f'{i}:{b}'))) for a, b in p[i]['edges']}
    need(sum(n['type'] in {'boss','boss2'} for n in nodes) == (2 if double else 1), 'boss count')
    g,boss,neighbors,direct=geometry_stage(nodes,s['edges'],expected_edges)
    target,routes=topology_stage(by,g,boss,double,budget)
    required = {x['id'] for x in pool if 'rest' in x['types']}
    walk = []
    normal = by.keys()-boss-{'N'}
    if auto:
        auto_requirements(pool,s,budget,normal,len(routes[target]))
        walk = routes[target][:-1]; done = {'N', *walk}
        while len(walk) < budget-1:
            options = sorted(n for n in normal-done if g[n] & done)
            need(bool(options), 'exploration stuck'); walk.append(options[0]); done.add(options[0])
        walk.append(target); done = {'N'}
        for n in walk:
            need(n not in done and bool(g[n] & done), 'invalid exploration'); done.add(n)
        check_walk(s,walk,budget)
    return dict(blocks=len(used), normal=len(normal), distance=len(routes[target]), walk=walk,
                rooms=dict(Counter(n['type'] for n in nodes)), walk_rooms=dict(Counter(by[n]['type'] for n in walk)),
                rest=sum(n['type'] == 'rest' for n in nodes), required=sorted(required), covered=sorted(required & used),
                boss_neighbors=len(neighbors), direct=direct)

def geometry_stage(nodes,scene_edges,expected_edges):
    by={n['id']:n for n in nodes}
    big = lambda n: n['type'] in {'neow', 'boss', 'boss2'}
    boss = {n['id'] for n in nodes if n['type'] in {'boss', 'boss2'}}
    for a, b in combinations(nodes, 2):
        need(clearance(a,b), 'global clearance')
    boxes = {n['id']: {(n['x']+dx, n['y']+dy) for dx in range(-int(big(n)), int(big(n))+1) for dy in range(-int(big(n)), int(big(n))+1)} for n in nodes}
    need(all(type(n[k]) is int for n in nodes for k in ('x', 'y')), 'integer coordinates')
    occupied = set().union(*boxes.values()); paths = set(); ports = set(); edges = set(); internal = set(); pairs = Counter()
    g = {n: set() for n in by}; neighbors = set(); direct = False
    for e in scene_edges:
        a, b = e['a'], e['b']; pair = tuple(sorted((a, b)))
        need(a in by and b in by and a != b and pair not in edges, 'duplicate/invalid edge')
        edges.add(pair); na, nb = by[a], by[b]
        dx, dy = nb['x']-na['x'], nb['y']-na['y']
        need((dx == 0) != (dy == 0), 'axis')
        length = abs(dx)+abs(dy); ux, uy = (dx > 0)-(dx < 0), (dy > 0)-(dy < 0)
        if e['internal']:
            need(na['piece'] == nb['piece'] and pair in expected_edges, 'invented internal')
            internal.add(pair)
        else:
            need(na['piece'] != nb['piece'] and a not in boss and b not in boss, 'external boss/owner')
            need(length-int(big(na))-int(big(nb)) == 2, 'external gap')
            owners = {na['piece'], nb['piece']}
            if -1 not in owners:
                pairs[tuple(sorted(owners))] += 1
            if owners == {-1, 0}: direct = True
            if 0 in owners and -1 not in owners: neighbors |= owners-{0}
        for n, direction in ((a, (ux, uy)), (b, (-ux, -uy))):
            need((n, direction) not in ports, 'port conflict'); ports.add((n, direction))
        line = {(na['x']+ux*k, na['y']+uy*k) for k in range(length+1)}
        for n in by.keys()-{a, b}: need(not line & boxes[n], 'edge through node')
        line -= boxes[a] | boxes[b]
        need(not line & (paths | occupied), 'path collision'); paths |= line
        g[a].add(b); g[b].add(a)
    need(internal == expected_edges and all(n <= 2 for n in pairs.values()), 'missing internal/pair cap')
    return g,boss,neighbors,direct


def topology_stage(by,g,boss,double,budget):
    target = next(n for n in boss if by[n]['type'] == 'boss')
    need(len(g[target]) == (2 if double else 1) and len(g[target]-boss) == 1, 'first boss terminal')
    need(all(by[v]['piece'] == by[target]['piece'] for v in g[target]), 'boss bridge')
    if double:
        second = (boss-{target}).pop()
        need(g[second] == {target} and by[second]['piece'] == by[target]['piece'], 'second boss terminal')
    q = deque(['N']); routes = {'N': []}
    while q:
        a = q.popleft()
        for b in sorted(g[a]):
            if b not in routes: routes[b] = routes[a]+[b]; q.append(b)
    need(len(routes) == len(by), 'connectivity')
    reach = {'N'}
    for _ in by:
        reach |= set().union(*(g[n]-boss for n in reach))
    need(reach == by.keys()-boss, 'ordinary behind boss')
    need(len(routes[target]) <= budget, 'step limit')
    return target,routes
