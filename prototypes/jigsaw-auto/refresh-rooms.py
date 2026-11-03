import json,re,subprocess
from pathlib import Path
from room_pool import make_pool
from verify import verify
root=Path(__file__).parent
old=json.loads((root/'input.json').read_text(encoding='utf8'))
if not (root/'legacy-room-inputs.json').exists():
    (root/'legacy-room-inputs.json').write_text(json.dumps(dict(existing=old,controlled=json.loads((root/'controlled.json').read_text(encoding='utf8'))),ensure_ascii=False),encoding='utf8')
prepared={'controlled':make_pool(old['pool'],69031,list(range(6))),'existing-demo':make_pool(old['pool'],69032)}
(root/'prepared-pools.json').write_text(json.dumps(prepared),encoding='utf8')
subprocess.run(['node',str(root/'refresh-witnesses.cjs')],check=True)
data=json.loads((root/'room-fixtures.json').read_text(encoding='utf8'))
for family,d in data.items():
    for s in d['witnesses']:
        for B in [15,14,13]:verify(d['pool'],s,B)
    assert sum('rest' in p['types'] for p in d['pool'])==6
(root/'controlled.json').write_text(json.dumps(data['controlled'],ensure_ascii=False,indent=2),encoding='utf8')
old['pool']=data['existing-demo']['pool'];(root/'input.json').write_text(json.dumps(old,ensure_ascii=False,indent=2),encoding='utf8')
# Update preserved demo's inputs, so bootstrap and pool previews obey the same rule revision.
p=root/'prototype.html';s=p.read_text(encoding='utf8');m=re.search(r'(<script id="payload" type="application/json">)([\s\S]*?)(</script>)',s)
payload=json.loads(m[2]);payload['pool']=old['pool']
for scene in payload['scenarios']:
    for n in scene['nodes']:
        if n['piece']>=0 and n['type'] not in ['boss','boss2']:n['type']=old['pool'][n['piece']]['types'][int(n['id'].split(':')[1])]
s=s[:m.start(2)]+json.dumps(payload,ensure_ascii=False)+s[m.end(2):];p.write_text(s,encoding='utf8')
print('12 independent layout/config checks passed; both pools have exactly six required rest fragments')
