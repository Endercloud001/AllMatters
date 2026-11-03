from pathlib import Path
import json
root=Path(__file__).parent;s=(root/'prototype.html').read_text(encoding='utf8')
panel='''<section id="experiment" style="padding:10px;display:flex;gap:8px;flex-wrap:wrap">
<span style="width:100%">一次性原型：实时搜索→独立验收→成功替换。比较布局、多样性、房型与真实等待；恢复仅模拟本机任务。</span>
<select id="family"><option value="controlled">受控池</option><option value="existing-demo">既有演示池</option></select>
<select id="config"><option value="act1">幕1单Boss</option><option value="act2">幕2单Boss</option><option value="act3">幕3单Boss</option><option value="act3-double">幕3双Boss</option></select>
<select id="method"><option value="extend">有界扩展</option><option value="witness">仅见证</option><option value="scratch">给池从零搜索</option></select>
<select id="budget"><option>0</option><option>100</option><option selected>1000</option><option>10000</option></select>
<label>种子 <input id="seed" type="number" value="1" style="width:70px"></label>
<label><input type="checkbox" id="withWitness" checked>已有同池见证</label>
<select id="fault"><option value="">正常</option><option value="exception">模拟生成异常</option><option value="invalid-output">模拟错误输出</option></select>
<button id="runLive">同池重新装配</button><button id="editLive">模拟已有编辑：整体旋转</button><button id="resumeLive" disabled>中断并按原输入重做</button>
<small style="width:100%">体验顺序：①正常生成后同池换种子；②旋转已有图→取消“已有见证”→预算0，观察失败保留；③从零搜索/10000预算，生成中点击“中断并重做”。错误输出与异常由故障选择框注入。</small>
<div id="liveState" aria-live="polite" style="width:100%;overflow-wrap:anywhere"></div></section>'''
s=s.replace('<div class="app">',panel+'<div class="app">').replace('</style>','#tabs,#regenerate,.act-select{display:none!important}.app{height:calc(100dvh - 250px)}#experiment{background:#242521}#seed{background:#30332d;color:#eee}#busy{display:none;position:absolute;inset:0;background:#090b09;color:#e9dfcc;z-index:10;align-items:center;justify-content:center;text-align:center;padding:20px}.waiting #busy{display:flex}</style>')
s=s.replace('<div class="map-wrap" id="mapWrap">','<div class="map-wrap" id="mapWrap"><div id="busy" role="status">先古之民的意志正在影响高塔<br>正在搜索并独立验收…</div>')
script='''
const livePools=POOLS,liveCases=CASES;let jobId=null,savedRequest=null,running=false,epoch=0;
const fields=['family','config','method','budget','seed','withWitness','fault'];
const post=(url,data)=>fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)}).then(r=>r.json());
function showLive(r){
 if(r.status==='SAT'){
  D.pool=livePools[savedRequest.family];current=savedRequest.config==='act3-double'?2:1;B=r.state.B;
  const s=r.state.layout,m=r.metrics;s.metrics={...m,total:6,required:m.required.length,neighbors:m.boss_neighbors};s.seed=savedRequest.seed;s.title='独立验收通过';s.subtitle=savedRequest.family+' / '+savedRequest.method;currentScene=s;renderAll();
 }
 const phase={ancient:'先古之民选择',editing:'拼图编辑',generating:'正在生成'}[r.state.phase]||r.state.phase;
 $('liveState').textContent=`${r.status} · 阶段 ${phase} · ${r.status==='SAT'?'验收后替换':'保留原布局和原池'} · 扩展${r.expanded??'—'} · ${r.server_total_ms?.toFixed(1)??'—'}ms服务端完整耗时（含任务启动、独立验收） · ${r.metrics?JSON.stringify(r.metrics.rooms):r.state.error} · 不代表原生联机或过场`;
}
async function startLive(request){
 if(!Number.isSafeInteger(request.seed)){$('liveState').textContent='种子须为安全范围内的整数，原布局和原池保留。';return}
 const mine=++epoch;running=true;savedRequest=JSON.parse(JSON.stringify(request));$('resumeLive').disabled=false;$('runLive').disabled=true;$('editLive').disabled=true;
 for(const id of fields)$(id).disabled=true;
 $('mapWrap').classList.add('waiting');
 $('liveState').textContent='生成中：先古之民的意志正在影响高塔。原图仍保留，候选独立验收通过后替换。';
 try{
 const began=performance.now(),j=await post('/start',savedRequest);if(mine!==epoch)return;if(!j.id)throw Error(j.error||'无法开始本次实验');jobId=j.id;
 while(mine===epoch){
  const state=await fetch('/job/'+jobId).then(r=>r.json());if(mine!==epoch)return;if(state.done){if(state.result){showLive(state.result);$('liveState').textContent+=` · 页面等待${(performance.now()-began).toFixed(1)}ms`;}break}
  await new Promise(resolve=>setTimeout(resolve,40));
 }
 }catch(e){if(mine===epoch)$('liveState').textContent='实验任务错误，原布局和原池保留：'+e.message}
 finally{if(mine===epoch){running=false;$('mapWrap').classList.remove('waiting');$('runLive').disabled=false;$('editLive').disabled=false;$('resumeLive').disabled=true;for(const id of fields)$(id).disabled=false;}}
}
$('runLive').onclick=()=>startLive({family:$('family').value,config:$('config').value,method:$('method').value,budget:+$('budget').value,seed:+$('seed').value++,withWitness:$('withWitness').checked,fault:$('fault').value,layout:JSON.parse(JSON.stringify(currentScene))});
$('resumeLive').onclick=async()=>{if(!running||!jobId)return;epoch++;await post('/interrupt',{id:jobId});startLive(savedRequest)};
$('editLive').onclick=()=>{for(const n of currentScene.nodes)[n.x,n.y]=[-n.y,n.x];renderAll();$('liveState').textContent='已有编辑布局：整体旋转；自动失败须保留这份布局及全部边。'};
function resetCase(){savedRequest={family:$('family').value,config:$('config').value,method:'witness',seed:1};showLive(liveCases[savedRequest.family][savedRequest.config]);$('liveState').textContent='已切换实验配置并载入该原池的独立合格见证。点击同池重新装配；失败保留这张图。'}
$('family').onchange=resetCase;$('config').onchange=resetCase;resetCase();
window.live={get layout(){return currentScene},get request(){return savedRequest},get running(){return running},start:startLive};
'''
# Keep the original map renderer and assets; no route playback.
pools={name:json.loads((root/file).read_text(encoding='utf8'))['pool'] for name,file in [('controlled','controlled.json'),('existing-demo','input.json')]}
script=script.replace('POOLS',json.dumps(pools,ensure_ascii=False))
from pipeline import fixtures
from verify import verify
cases={}
for family,d in fixtures().items():
    cases[family]={}
    for config,double,B in [('act1',False,15),('act2',False,14),('act3',False,13),('act3-double',True,13)]:
        layout=d['witnesses'][double];m=verify(d['pool'],layout,B)
        cases[family][config]=dict(status='SAT',state=dict(layout=layout,B=B,phase='editing'),metrics=m)
script=script.replace('CASES',json.dumps(cases,ensure_ascii=False))
s=s.replace('regenerate(1203);window.demo=','regenerate(1203);'+script+'\nwindow.demo=')
(root/'live.html').write_text(s,encoding='utf8');print('live.html generated')
