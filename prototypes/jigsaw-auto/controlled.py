"""Reuses the revised I/I/O/O coordinates; constructs a restricted pool family."""
import json
from pathlib import Path
from collections import Counter
from verify import verify

root=Path(__file__).parent
pool=json.loads((root/'input.json').read_text(encoding='utf8'))['pool']
pool[3]['pts']=[[0,0],[1,0],[0,-1],[1,-1]]
for p in pool: p['types']=['monster']*4
for i, slots in [(1,[0,2]),(2,[0,3]),(3,[0,3])]:
    for j in slots: pool[i]['types'][j]='rest'
slots=[]
for p in pool[4:]:
    counts=Counter((x+y)%2 for x,y in p['pts'])
    color=max(counts,key=counts.get)
    slots.extend((p,j) for j,(x,y) in enumerate(p['pts']) if (x+y)%2==color)
for (p,j),kind in zip(slots,['shop']*4+['chest']*4): p['types'][j]=kind
remaining=[(p,j) for p in pool[4:] for j in range(4) if p['types'][j]=='monster']
for (p,j),kind in zip(remaining,['unknown']*16+['elite']*6):p['types'][j]=kind
witnesses=[]
for double in [False,True]:
    d=4 if double else 3
    coords={0:[(3+i*d,0) for i in range(4)],1:[(3,2+2*i) for i in range(4)],2:[(3,-2),(5,-2),(3,-4),(5,-4)],3:[(5,-6),(7,-6),(5,-8),(7,-8)]}
    nodes=[dict(id='N',x=0,y=0,piece=-1,type='neow')]
    for i,pts in coords.items():
        for j,(x,y) in enumerate(pts):
            t=('boss2' if double else 'boss') if i==0 and j==3 else 'boss' if i==0 and j==2 and double else pool[i]['types'][j]
            nodes.append(dict(id=f'{i}:{j}',x=x,y=y,piece=i,type=t))
    edges=[dict(a=f'{i}:{a}',b=f'{i}:{b}',internal=True) for i in coords for a,b in pool[i]['edges']]
    edges += [dict(a=a,b=b,internal=False) for a,b in [('N','0:0'),('0:0','1:0'),('0:0','2:0'),('2:3','3:0')]]
    s=dict(nodes=nodes,edges=edges,used=list(coords),double=double)
    for B in [15,14,13]: verify(pool,s,B)
    witnesses.append(s)
(root/'controlled.json').write_text(json.dumps(dict(pool=pool,witnesses=witnesses),ensure_ascii=False,indent=2),encoding='utf8')
print('controlled pool: six independent checks passed')
