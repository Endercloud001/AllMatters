"""Targeted acceptance-stage fixtures plus full-layout controls. No generator imports."""
import copy,json
from pathlib import Path
from verify import verify,geometry_stage,auto_requirements,check_walk,topology_stage
root=Path(__file__).parent;d=json.loads((root/'controlled.json').read_text(encoding='utf8'));rows=[]
def run(name,stage,fn,reason=None):
    try:fn();actual=None
    except ValueError as e:actual=str(e)
    rows.append(dict(name=name,stage=stage,expected=reason,actual=actual,passed=actual==reason))
def n(id,x,y,type='monster',piece=0):return dict(id=id,x=x,y=y,type=type,piece=piece)
def geom(ns,pairs):
    by={x['id']:x for x in ns}
    es=[dict(a=a,b=b,internal=by[a]['piece']==by[b]['piece']) for a,b in pairs]
    return lambda:geometry_stage(ns,es,{tuple(sorted((e['a'],e['b']))) for e in es if e['internal']})
run('crossing without nodes at intersection','geometry',geom([n('a',-2,0),n('b',2,0),n('c',0,-2),n('d',0,2)],[('a','b'),('c','d')]),'path collision')
run('edge through nonendpoint','geometry',geom([n('a',0,0),n('b',4,0),n('c',2,0)],[('a','b')]),'edge through node')
run('overlap/shared direction','geometry',geom([n('a',0,0),n('b',4,0),n('c',6,0)],[('a','b'),('a','c')]),'port conflict')
run('3x3 corner is not a port','geometry',geom([n('a',0,0,'neow',-1),n('b',3,1)],[('a','b')]),'axis')
run('3x3 midpoint port accepted','geometry',geom([n('a',0,0,'neow',-1),n('b',3,0)],[('a','b')]))
for count in [2,3]:
    ns=[n(f'{side}{i}',x,2*i,piece=owner) for side,x,owner in [('a',0,0),('b',2,1)] for i in range(count)]
    run(f'{count} external edges between same pieces','geometry',geom(ns,[(f'a{i}',f'b{i}') for i in range(count)]),None if count==2 else 'missing internal/pair cap')
for double,base in enumerate(d['witnesses']):
    for turn in range(4):
        s=copy.deepcopy(base)
        for _ in range(turn):
            for node in s['nodes']:node['x'],node['y']=-node['y'],node['x']
        run(f'full rigid rotation {double}/{turn}','full layout',lambda s=s:verify(d['pool'],s,13))
    normal={x['id'] for x in base['nodes'] if x['type'] not in {'neow','boss','boss2'}}
    for B in [15,14,13]:
        for count in [B-2,B-1]:
            run(f'capacity threshold B{B} N{count} double{double}','auto-only stage',lambda B=B,count=count: auto_requirements(d['pool'],base,B,set(sorted(normal)[:count]),3),None if count==B-1 else 'full budget capacity')
    def auto(s):return lambda:auto_requirements(d['pool'],s,13,normal,3)
    s=copy.deepcopy(base);s['edges']=[e for e in s['edges'] if e['internal'] or {e['a'],e['b']}!={'0:0','2:0'}]
    run(f'only ancient and one other fragment {double}','auto-only stage',auto(s),'boss neighbors')
    s['edges'].append(dict(a='0:1',b='1:1',internal=False))
    run(f'two distinct edges to same neighbor {double}','auto-only stage',auto(s),'boss neighbors')
    s['edges'].append(copy.deepcopy(s['edges'][-1]))
    run(f'duplicate edge cannot create second neighbor {double}','auto-only stage',auto(s),'boss neighbors')
    # All remaining pieces can be reachable indirectly; no direct second neighbor.
    s['edges'].append(dict(a='1:3',b='2:1',internal=False))
    run(f'indirect neighbor insufficient {double}','auto-only stage',auto(s),'boss neighbors')
    run(f'spatially adjacent undeclared neighbor insufficient {double}','auto-only stage',auto(s),'boss neighbors')
    s['edges']=[e for e in s['edges'] if {e['a'],e['b']}!={'N','0:0'}]
    run(f'not direct ancient: no two-neighbor requirement {double}','auto-only stage',auto(s))
    # Full geometry/control: change root attachment, translating every fragment.
    s=copy.deepcopy(base)
    for node in s['nodes']:
        if node['piece']!=-1:node['y']-=2
    s['edges']=[e for e in s['edges'] if {e['a'],e['b']}!={'N','0:0'}]+[dict(a='N',b='1:0',internal=False)]
    run(f'not direct ancient full-layout positive {double}','full layout',lambda s=s:verify(d['pool'],s,13))
    s=copy.deepcopy(base);s['used'].remove(1);s['nodes']=[x for x in s['nodes'] if x['piece']!=1];ids={x['id'] for x in s['nodes']};s['edges']=[e for e in s['edges'] if e['a'] in ids and e['b'] in ids]
    run(f'missing whole block containing two rests {double}','full layout',lambda s=s:verify(d['pool'],s,13),'missing rest fragment')
    s=copy.deepcopy(base);s['nodes']=[x for x in s['nodes'] if x['id']!='1:2']
    run(f'only rest node removed cannot use partial block {double}','full layout',lambda s=s:verify(d['pool'],s,13),'whole fragments')
    walk=verify(d['pool'],base,13)['walk'];pool=copy.deepcopy(d['pool'])
    # A shop revisit is still a repeated node, even though revisits are legal gameplay.
    s=copy.deepcopy(base);shop=walk[0];next(x for x in s['nodes'] if x['id']==shop)['type']='shop'
    repeated=[shop,shop]+walk[2:]
    run(f'shop revisit cannot fill distinct capacity {double}','exploration stage',lambda s=s,w=repeated:check_walk(s,w,13),'walk repeated/unplaced node')
    run(f'unreachable/behind Boss nodes excluded from capacity {double}','auto-only stage',lambda:auto_requirements(d['pool'],base,13,set(list(normal)[:11]),3),'full budget capacity')
pool=copy.deepcopy(d['pool']);pool[1]['types'][1],pool[1]['types'][2]=pool[1]['types'][2],pool[1]['types'][1]
run('same-type internal rest adjacency','pool/full layout',lambda:verify(pool,d['witnesses'][0],13),'special internal edge')
for kind in ['shop','chest']:
    pool=copy.deepcopy(d['pool'])
    for j in [0,1]:
        donor=next((p,i) for p in pool for i,t in enumerate(p['types']) if t==kind and (p['id']!=4 or i not in [0,1]))
        p,i=donor;p['types'][i],pool[4]['types'][j]=pool[4]['types'][j],p['types'][i]
    run(f'same-type internal {kind} adjacency','pool/full layout',lambda pool=pool:verify(pool,d['witnesses'][0],13),'special internal edge')
pool=copy.deepcopy(d['pool']);pool[6]['pts']=[[-x,y] for x,y in pool[6]['pts']]
run('S mirror cannot substitute Z','pool/full layout',lambda:verify(pool,d['witnesses'][0],13),'pool shape/mirror')
from pipeline import fixtures
existing=fixtures()['existing-demo']
s=copy.deepcopy(existing['witnesses'][False]);pi=next(i for i in s['used'] if existing['pool'][i]['shape'] in {'S','Z','J','L'});anchor=next(n['x'] for n in s['nodes'] if n['id']==f'{pi}:0')
for node in s['nodes']:
    if node['piece']==pi:node['x']=2*anchor-node['x']
run('placed chiral fragment mirror rejected','full layout',lambda:verify(existing['pool'],s,13),'rotation/spacing')
for double,base in enumerate(d['witnesses']):
    by={n['id']:n for n in base['nodes']};boss={x for x in by if by[x]['type'] in {'boss','boss2'}};target='0:2' if double else '0:3'
    def graph(s):
        g={x:set() for x in by}
        for e in s['edges']:g[e['a']].add(e['b']);g[e['b']].add(e['a'])
        return g
    g=graph(base);g[target].add('1:3');g['1:3'].add(target)
    run(f'Boss bridge to ordinary region {double}','topology',lambda g=g,by=by,boss=boss,double=double:topology_stage(by,g,boss,double,13),'first boss terminal')
    if double:
        g=graph(base);g['0:3'].add('1:3');g['1:3'].add('0:3')
        run('second Boss cannot have ordinary exit','topology',lambda:topology_stage(by,g,boss,True,13),'second boss terminal')
(root/'coverage-results.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8')
bad=[r for r in rows if not r['passed']];print(json.dumps(dict(cases=len(rows),failures=bad)));raise SystemExit(bool(bad))
