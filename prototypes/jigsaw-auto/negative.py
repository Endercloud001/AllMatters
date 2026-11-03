"""Decision evidence fixtures, not production tests. Records exact rejection stage."""
import json, copy
from pathlib import Path
from verify import verify, clearance, check_walk
root=Path(__file__).parent
d=json.loads((root/'controlled.json').read_text(encoding='utf8'))
results=[]
def case(name,s,auto=True,B=15,accept=False,pool=None):
    try:
        m=verify(pool or d['pool'],s,B,auto); actual=True; reason='accepted'
    except ValueError as e: actual=False; reason=str(e)
    results.append(dict(name=name,auto=auto,B=B,expected=accept,actual=actual,reason=reason))
def subset(s,ids):
    s=copy.deepcopy(s);s['used']=ids
    s['nodes']=[n for n in s['nodes'] if n['piece']==-1 or n['piece'] in ids]
    ns={n['id'] for n in s['nodes']};s['edges']=[e for e in s['edges'] if e['a'] in ns and e['b'] in ns]
    return s
for double,base in enumerate(d['witnesses']):
    for B in [15,14,13]:case(f'controlled {double} B{B}',base,B=B,accept=True)
    small=subset(base,[0])
    case(f'single fragment manual {double}',small,auto=False,accept=True)
    case(f'single fragment automatic {double}',small)
    case(f'missing rest fragment {double}',subset(base,[0,1,2]))
    case(f'capacity upper boundary {double}',base,B=17 if not double else 16)
    case(f'capacity exact boundary {double}',base,B=16 if not double else 15,accept=True)
    case(f'shortest-distance violation {double}',base,auto=False,B=2)
    s=copy.deepcopy(base);s['edges'].append(copy.deepcopy(s['edges'][-1]));case(f'duplicate edge {double}',s)
    s=copy.deepcopy(base);s['edges']=[e for e in s['edges'] if e['internal']];case(f'isolated ordinary nodes {double}',s)
    s=copy.deepcopy(base);s['nodes'][1]['type']='shop';case(f'changed room {double}',s)
    s=copy.deepcopy(base);s['nodes'][1]['x']+=1;case(f'changed shape spacing {double}',s)
    s=copy.deepcopy(base);s['edges'].pop(0);case(f'deleted fixed edge {double}',s)
    s=copy.deepcopy(base);s['edges'].append(dict(a='N',b='0:3',internal=False));case(f'external Boss edge {double}',s)
    s=copy.deepcopy(base);s['nodes'].pop();case(f'partial fragment {double}',s)
    s=copy.deepcopy(base);s['used'].append(0);case(f'duplicate fragment {double}',s)
    p=copy.deepcopy(d['pool']);p[4]['types'][0]='rest';case(f'tampered pool quota {double}',base,pool=p)
    # Earlier four-piece construction: valid manual, enough capacity and all rests,
    # but the Boss fragment has no other direct neighbor. Reject on that condition.
    s=copy.deepcopy(base)
    coords={1:[(-3-2*j,0) for j in range(4)],2:[(-11,0),(-13,0),(-11,2),(-13,2)],3:[(-15,0),(-17,0),(-15,2),(-17,2)]}
    for n in s['nodes']:
        if n['piece'] in coords:n['x'],n['y']=coords[n['piece']][int(n['id'].split(':')[1])]
    s['edges']=[e for e in s['edges'] if e['internal']]+[dict(a=a,b=b,internal=False) for a,b in [('N','0:0'),('N','1:0'),('1:3','2:0'),('2:1','3:0')]]
    case(f'old four-piece manual {double}',s,auto=False,accept=True)
    case(f'old four-piece automatic missing Boss neighbors {double}',s)
for ta,tb,dx,dy,expected in [('monster','monster',1,0,False),('monster','monster',1,1,False),('monster','monster',2,0,True),('boss','monster',2,2,False),('boss','monster',3,0,True),('boss','boss',3,3,False),('boss','boss',4,0,True)]:
    actual=clearance(dict(x=0,y=0,type=ta),dict(x=dx,y=dy,type=tb))
    results.append(dict(name=f'clearance {ta}/{tb} {dx}/{dy}',expected=expected,actual=actual,reason='independent pair predicate'))
for double,base in enumerate(d['witnesses']):
    walk=verify(d['pool'],base,13)['walk']
    for name,mutated in [('repeat visit instead of distinct node',walk[:2]+[walk[0]]+walk[3:]),('unplaced pool node',walk[:2]+['4:0']+walk[3:]),('early Boss', [walk[-1]]+walk[1:]),('second Boss counts as first',walk[:-1]+['0:3'] if double else walk[:-1]+['N']),('unexplored branch jump',['3:3']+walk[1:])]:
        try:check_walk(base,mutated,13);actual=True;reason='accepted'
        except ValueError as e:actual=False;reason=str(e)
        results.append(dict(name=f'{name} {double}',expected=False,actual=actual,reason=reason))
(root/'negative-results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
bad=[r for r in results if r['expected']!=r['actual']]
print(json.dumps(dict(cases=len(results),mismatches=bad)))
raise SystemExit(bool(bad))
