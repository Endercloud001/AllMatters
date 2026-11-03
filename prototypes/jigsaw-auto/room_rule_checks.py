import copy,json
from pipeline import fixtures,ROOT
from verify import verify,pool_room_rules,geometry_stage,auto_requirements,check_walk,clearance,topology_stage
def main(kind):
    data=fixtures();rows=[]
    def case(name,stage,fn,reason=None,reject=False):
        try:fn();actual=None
        except ValueError as e:actual=str(e)
        rows.append(dict(name=name,stage=stage,expected='rejection' if reject else reason,actual=actual,passed=bool(actual) if reject else actual==reason))
    if kind=='negative':
        for family,d in data.items():
            for double,base in d['witnesses'].items():
                for B in [15,14,13]:
                    for turn in range(4):
                        s=copy.deepcopy(base)
                        for _ in range(turn):
                            for n in s['nodes']:n['x'],n['y']=-n['y'],n['x']
                        case(f'{family}/{double}/B{B}/rotate{turn}','full layout',lambda s=s,B=B:verify(d['pool'],s,B))
                small=copy.deepcopy(base);small['used']=[0];small['nodes']=[n for n in small['nodes'] if n['piece'] in [-1,0]];ids={n['id'] for n in small['nodes']};small['edges']=[e for e in small['edges'] if e['a'] in ids and e['b'] in ids]
                case(f'{family}/{double} Boss only manual','full layout',lambda:verify(d['pool'],small,13,False))
                case(f'{family}/{double} Boss only auto','full layout',lambda:verify(d['pool'],small,13),reject=True)
                case(f'{family}/{double} limit exceeded','full layout',lambda:verify(d['pool'],base,2),'step limit')
                pool=copy.deepcopy(d['pool']);pool[6]['pts']=[[-x,y] for x,y in pool[6]['pts']]
                case(f'{family}/{double} mirrored S pool','full layout',lambda:verify(pool,base,13),'pool shape/mirror')
                for name,mutate,reason in [('duplicate edge',lambda s:s['edges'].append(copy.deepcopy(s['edges'][-1])),'duplicate/invalid edge'),('partial fragment',lambda s:s['nodes'].pop(),'whole fragments'),('Boss external',lambda s:s['edges'].append(dict(a='N',b='0:3',internal=False)),'external boss/owner')]:
                    s=copy.deepcopy(base);mutate(s);case(f'{family}/{double} {name}','full layout',lambda s=s:verify(d['pool'],s,13),reason)
                walk=verify(d['pool'],base,13)['walk']
                for name,w in [('repeat',walk[:1]+walk[:1]+walk[2:]),('unplaced',walk[:1]+['unplaced']+walk[2:]),('early Boss',[walk[-1]]+walk[1:]),('second Boss',walk[:-1]+['0:3'] if double else walk[:-1]+['N'])]:case(f'{family}/{double} {name}','exploration stage',lambda w=w:check_walk(base,w,13),reject=True)
        legacy=json.loads((ROOT/'legacy-room-inputs.json').read_text(encoding='utf8'))
        for double,s in enumerate(legacy['controlled']['witnesses']):case(f'legacy multi-rest witness {double}','full layout',lambda s=s:verify(legacy['controlled']['pool'],s,13),reject=True)
        d=data['controlled']
        for name,assignments,reason in [('four combats before Boss',[(j,'monster') for j in range(4)],'four identical pre-Boss rooms'),('adjacent elites become Boss(es)',[(2,'elite'),(3,'elite')],'adjacent pre-Boss elites'),('second rest in same fragment',[(next(j for j,t in enumerate(d['pool'][0]['types']) if t!='rest'),'rest')],'at most one rest per fragment')]:
            pool=copy.deepcopy(d['pool'])
            for j,t in assignments:
                if pool[0]['types'][j]==t:continue
                donor=next((p,i) for p in pool[1:] for i,kind in enumerate(p['types']) if kind==t);p,i=donor
                p['types'][i],pool[0]['types'][j]=pool[0]['types'][j],p['types'][i]
            for double,s in d['witnesses'].items():case(f'{name} double{double}','full layout with quota-preserving input mutation',lambda pool=pool,s=s:verify(pool,s,13),reason)
        pool=copy.deepcopy(d['pool']);pool[13]['types'][0]='rest'
        case('changed quota','full pool',lambda:verify(pool,d['witnesses'][0],13),'pool quota')
    else:
        case('three Elite leaves separated by ordinary center allowed','pre-Boss room stage',lambda:pool_room_rules([dict(types=['elite','monster','elite','elite'],edges=[[0,1],[1,2],[1,3]])]))
        for types,edges,reason,name in [(['rest','monster','rest','unknown'],[[0,1],[1,2],[2,3]],'at most one rest per fragment','two rests even without direct edge'),(['monster']*4,[[0,1],[1,2],[2,3]],'four identical pre-Boss rooms','four combats before Boss replacement'),(['unknown']*4,[[0,1],[1,2],[2,3]],'four identical pre-Boss rooms','four questions'),(['rest','monster','elite','elite'],[[0,1],[1,2],[2,3]],'adjacent pre-Boss elites','both future Boss slots elite'),(['elite','elite','monster','unknown'],[[0,2],[2,3],[3,1]],None,'skeleton neighbors without internal edge allowed'),(['elite','monster','elite','unknown'],[[0,1],[1,2],[2,3]],None,'separated elites allowed')]:case(name,'pre-Boss room stage',lambda types=types,edges=edges:pool_room_rules([dict(types=types,edges=edges)]),reason)
        d=data['controlled']
        for double,base in d['witnesses'].items():
            normal={n['id'] for n in base['nodes'] if n['type'] not in ['neow','boss','boss2']}
            for B in [15,14,13]:
                for count in [B-2,B-1]:case(f'capacity {double}/B{B}/N{count}','auto stage',lambda B=B,count=count:auto_requirements(d['pool'],base,B,set(sorted(normal)[:count]),3),None if count==B-1 else 'full budget capacity')
            s=copy.deepcopy(base);s['edges']=[e for e in s['edges'] if e['internal']]+[dict(a='N',b='0:0',internal=False),dict(a='0:0',b='1:0',internal=False),dict(a='0:1',b='1:1',internal=False)]
            for name,extra in [('same neighbor two edges',[]),('duplicate edge',[s['edges'][-1]]),('indirect/undeclared second neighbor',[dict(a='1:0',b='2:0',internal=False)])]:
                t=copy.deepcopy(s);t['edges']+=extra;case(f'{name} {double}','auto stage',lambda t=t:auto_requirements(d['pool'],t,13,normal,3),'boss neighbors')
            s['edges']=[e for e in s['edges'] if 'N' not in [e['a'],e['b']]];case(f'not direct ancient {double}','auto stage',lambda:auto_requirements(d['pool'],s,13,normal,3))
            t=copy.deepcopy(base);t['used'].remove(5);case(f'missing rest fragment {double}','auto stage',lambda:auto_requirements(d['pool'],t,13,normal,3),'missing rest fragment')
            by={n['id']:n for n in base['nodes']};boss={k for k,n in by.items() if n['type'] in ['boss','boss2']};target='0:2' if double else '0:3'
            def graph():
                g={k:set() for k in by}
                for e in base['edges']:g[e['a']].add(e['b']);g[e['b']].add(e['a'])
                return g
            g=graph();g[target].add('1:3');g['1:3'].add(target)
            case(f'Boss bridge {double}','topology stage',lambda:topology_stage(by,g,boss,bool(double),13),'first boss terminal')
            g=graph()
            for a in g:
                for b in list(g[a]):
                    if (by[a]['piece']==5)!=(by[b]['piece']==5):g[a].remove(b)
            case(f'isolated ordinary region {double}','topology stage',lambda:topology_stage(by,g,boss,bool(double),13),'connectivity')
            if double:
                g=graph();g['0:3'].add('1:3');g['1:3'].add('0:3')
                case('second Boss extra exit','topology stage',lambda:topology_stage(by,g,boss,True,13),'second boss terminal')
        def n(id,x,y,type='monster',piece=0):return dict(id=id,x=x,y=y,type=type,piece=piece)
        def geom(ns,pairs):
            by={x['id']:x for x in ns};es=[dict(a=a,b=b,internal=by[a]['piece']==by[b]['piece']) for a,b in pairs];return lambda:geometry_stage(ns,es,{tuple(sorted((e['a'],e['b']))) for e in es if e['internal']})
        for name,ns,pairs,reason in [('cross',[n('a',-2,0),n('b',2,0),n('c',0,-2),n('d',0,2)],[('a','b'),('c','d')],'path collision'),('through node',[n('a',0,0),n('b',4,0),n('c',2,0)],[('a','b')],'edge through node'),('overlap/port',[n('a',0,0),n('b',4,0),n('c',6,0)],[('a','b'),('a','c')],'port conflict'),('corner port',[n('a',0,0,'neow',-1),n('b',3,1)],[('a','b')],'axis'),('midpoint port',[n('a',0,0,'neow',-1),n('b',3,0)],[('a','b')],None)]:case(name,'geometry stage',geom(ns,pairs),reason)
        for count in [2,3]:case(f'pair cap {count}','geometry stage',geom([n(f'{side}{i}',x,i*2,piece=owner) for side,x,owner in [('a',0,0),('b',2,1)] for i in range(count)],[(f'a{i}',f'b{i}') for i in range(count)]),None if count==2 else 'missing internal/pair cap')
        case('cross-fragment Elite edge allowed','geometry stage',geom([n('a',0,0,'elite',0),n('b',2,0,'elite',1)],[('a','b')]))
        for ta,tb,dx,dy,expected in [('monster','monster',1,0,False),('monster','monster',1,1,False),('monster','monster',2,0,True),('boss','monster',2,2,False),('boss','monster',3,0,True),('boss','boss',3,3,False),('boss','boss',4,0,True)]:
            actual=clearance(n('a',0,0,ta),n('b',dx,dy,tb));rows.append(dict(name=f'clearance {ta}/{tb}/{dx}/{dy}',stage='geometry predicate',expected=expected,actual=actual,passed=actual==expected))
    (ROOT/(kind+'-results.json')).write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding='utf8');bad=[r for r in rows if not r['passed']];print(json.dumps(dict(kind=kind,cases=len(rows),failures=bad)));assert not bad
