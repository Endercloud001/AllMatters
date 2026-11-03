"""Embed independently accepted recorded layouts in a standalone comparison demo."""
import json
from pathlib import Path
root=Path(__file__).parent
records=[]
for prefix in ['', 'controlled-']:
    for line in (root/(prefix+'checked.jsonl')).open(encoding='utf8'):
        r=json.loads(line)
        if r['seed']<=5: records.append(r)
legacy=json.loads((root/'legacy-room-inputs.json').read_text(encoding='utf8'))
pool=legacy['existing']['pool']
controlled=legacy['controlled']['pool']
s=(root/'prototype.html').read_text(encoding='utf8')
s=s.replace('<div class="header-note">','<div class="header-note">一次性体验原型 · 已验证实验记录回放<br>')
panel='''<div style="padding:12px;display:flex;gap:8px;flex-wrap:wrap;background:#242521">
<span>比较问题：同池的布局、多样性、房型分布及等待是否可用？</span>
<select id="family"><option value="existing-demo">既有演示池</option><option value="controlled">受控池</option></select>
<select id="config"><option value="act1">第一幕单Boss</option><option value="act2">第二幕单Boss</option><option value="act3">第三幕单Boss</option><option value="act3-double">第三幕双Boss</option></select>
<select id="method"><option value="witness">仅构造见证</option><option value="extend">有界扩展</option><option value="scratch">从零回溯</option></select>
<select id="budget"><option>0</option><option>100</option><option selected>1000</option><option>10000</option></select>
<button id="nextRecord">同池下一种装配（5个记录种子）</button><button id="waitRecord">回放记录中的等待</button>
<span id="evidence" aria-live="polite"></span></div>'''
s=s.replace('<div class="app">',panel+'<div class="app">')
s=s.replace('</style>','#tabs,#regenerate,.act-select{display:none!important}.app{height:calc(100dvh - 175px)}#evidence{width:100%;font-size:12px;overflow-wrap:anywhere;min-width:0}</style>')
script='''
const records=RECORDS,pools=POOLS;let recordSeed=1;
function recorded(wait=false){
 const family=$('family').value,config=$('config').value,method=$('method').value,budget=+$('budget').value;
 const r=records.find(r=>r.pool_family===family&&r.config===config&&r.method===method&&r.budget===budget&&r.seed===recordSeed);
 const show=()=>{
  if(!r.scene||!r.independent){$('evidence').textContent=`种子 ${recordSeed} · ${r.status} · 保留上张图；无独立通过结果，不能自动成功。`;return}
  D.pool=pools[family];current=r.double?2:1;B=r.B;
  const s=JSON.parse(JSON.stringify(r.scene)),m=r.metrics;
  s.metrics={...m,total:6,required:m.required.length,neighbors:m.boss_neighbors};s.seed=r.seed;s.title=config+' / '+method;s.subtitle='同池实验记录 · 独立Python验证通过';currentScene=s;renderAll();
  $('evidence').textContent=`种子 ${recordSeed} · ${r.status} · ${m.blocks}块 / ${m.normal}非Boss节点 · 最短Boss距离${m.distance} · 扩展${r.expanded} · ${method==='witness'&&family==='existing-demo'?'旧构造耗时未测':r.ms.toFixed(2)+'ms'} · 房型 ${JSON.stringify(m.rooms)}。记录回放，不是原生过场或实时求解。`;
 };
 if(wait){$('evidence').textContent='先古之民的意志正在影响高塔（实验计时回放）';$('map').style.visibility='hidden';setTimeout(()=>{$('map').style.visibility='';show()},r.ms)}else show();
}
for(const id of ['family','config','method','budget'])$(id).onchange=()=>{recordSeed=1;recorded()};
$('nextRecord').onclick=()=>{recordSeed=recordSeed%5+1;recorded()};$('waitRecord').onclick=()=>recorded(true);recorded();
'''.replace('RECORDS',json.dumps(records,ensure_ascii=False)).replace('POOLS',json.dumps({'existing-demo':pool,'controlled':controlled},ensure_ascii=False))
s=s.replace('regenerate(1203);window.demo=',script+'\nwindow.demo=')
s=s.replace('<body>','<body><div style="padding:12px;background:#542820;color:white">历史房型规则档案：旧结果不满足新规则。请使用实时原型。</div>')
(root/'review.html').write_text(s,encoding='utf8')
print('review.html: '+str(len(records))+' records embedded')
