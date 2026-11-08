from pathlib import Path
import re,json
root=Path(__file__).resolve().parent.parent
out=Path(__file__).parent/'jigsaw-visual-detail'
out.mkdir(exist_ok=True)
s=(root/'AllMatters_prototype65/prototypes/jigsaw-interaction/interaction-prototype.html').read_text(encoding='utf-8')
d=json.loads(re.search(r'(?:const|let|var) D\s*=\s*(\{.*?\});',s,re.S).group(1))
html='''<!doctype html><html lang="zh"><meta charset="utf-8"><title>AllMatters · 视觉细化稿</title><style>
*{box-sizing:border-box}body{margin:0;background:#101b23;color:#eedab0;font:16px 'Microsoft YaHei',sans-serif}nav{padding:16px 24px;display:flex;gap:12px;align-items:center;flex-wrap:wrap}button,select{background:#24424b;color:#f4dfad;border:1px solid #79918a;padding:10px 18px;cursor:pointer}nav span{margin-left:auto;font-size:13px;color:#b6bbb5}.hud{height:65px;background:#203b46;padding:20px 28px;border-bottom:2px solid #6d817c;color:#c6c7b5}.shell{height:calc(100vh - 145px);min-height:540px;padding:20px;display:grid;grid-template-columns:260px 1fr;grid-template-rows:1fr 100px;gap:18px}.pool{grid-row:1;min-height:0;display:flex;flex-direction:column;border:2px solid #426573;padding:12px;background:#142d37}.pool h2{font:24px serif;text-align:center;margin:2px 0 12px}.cards{overflow:auto;min-height:0;margin-top:12px}.card{height:126px;border:2px solid #587986;margin:0 0 10px;background:#10212a;position:relative}.map{position:relative;min-height:0;background:#c8ad79;overflow:hidden;border-radius:9px}.map svg{width:100%;height:100%}.help{position:absolute;right:16px;top:14px}.steps{position:absolute;left:20px;top:18px;color:#493820;font-weight:bold}.dialog{grid-column:1 / 3;display:grid;grid-template-columns:1fr 280px 280px;gap:18px}.speech{background:#cdb481;color:#49341c;padding:22px;font:22px serif;border-radius:7px}.dialog button{font:24px serif}.explore .pool,.explore .vote{display:none}.explore .map{grid-column:1 / 3}.explore .dialog{grid-template-columns:1fr}.badge{fill:#193f4a;stroke:#dcb46b;stroke-width:2}.annotation{padding:20px 28px;line-height:1.8;background:#172832}.overlay{position:absolute;inset:0;background:#101b23bb;display:grid;place-items:center}.overlay[hidden]{display:none}.modal{width:min(520px,85%);background:#203b46;padding:32px;border:2px solid #b59b6c;max-height:85%;overflow:auto}@media(max-width:1000px){.shell{grid-template-columns:210px 1fr;gap:12px;padding:12px;grid-template-rows:1fr 96px}.dialog{grid-template-columns:1fr 210px 210px;gap:10px}.dialog button{font-size:19px}.speech{font-size:18px}}
</style><nav><b>视觉细化稿 · 提案 V1</b><button onclick="mode(false)">拼图编辑</button><button onclick="mode(true)">探索地图</button><label><input id="held" type="checkbox" onchange="draw()">持有整块</label><label><input id="dense" type="checkbox" onchange="draw()">密集布局</label><span>视觉提案，非玩法验收 · 浏览器缩放查看不同尺寸</span></nav><div class="hud">原版 HUD 保留区域 · 此处仅标示空间，不另造 HUD</div><main class="shell" id="shell"><aside class="pool"><h2>拼图碎块</h2><button>归还碎块</button><div class="cards" id="cards"></div></aside><section class="map" id="paper"><span class="steps" id="steps" hidden>还可前行 9 步。</span><button class="help" onclick="document.getElementById('overlay').hidden=false">帮助</button><svg id="map" viewBox="0 0 1000 540"></svg><div class="overlay" id="overlay" hidden><div class="modal"><h2>拼图帮助</h2><p>左键拿放 · 持有时右键旋转 · 空手右键连拆线</p><p>中键平移 · 滚轮缩放 · Esc 取消</p><p>帮助打开时，背景地图不接受输入。这里展示遮罩和面板层级，不证明 Godot 输入隔离。</p><button onclick="document.getElementById('overlay').hidden=true">返回</button></div></div></section><footer class="dialog"><div class="speech" id="speech">从左侧拿取碎块，构建你的愿景。</div><button class="vote">由先古之民审视</button><button class="vote">令先古之民决定</button></footer></main><section class="annotation">普通节点：一格占地；Boss／先古：三格占地。深色连线位于节点下层。蓝金圆章“人”仅代示整块持有人头像，正式接原版玩家头像。地图背景与房型图片来自既有原型缓存；框体为 CSS 版式示意，正式素材对应见设计说明。</section><script>const assets=ASSETS;
let exploring=false;const $=id=>document.getElementById(id);function mode(v){exploring=v;$('shell').classList.toggle('explore',v);$('steps').hidden=!v;$('speech').textContent=v?'探索状态由现有游戏逻辑提供。':'从左侧拿取碎块，构建你的愿景。';draw()}
const types=['ancient','monster','unknown','rest','elite','shop','treasure','boss'];
function icon(t,x,y,size){let a=assets[t]||assets[t==='treasure'?'chest':t==='ancient'?'neow':t];return `<rect x="${x-size/2}" y="${y-size/2}" width="${size}" height="${size}" rx="4" fill="#e0c897" stroke="#725a39" stroke-width="2"/>`+(a?`<image href="${a}" x="${x-size*.42}" y="${y-size*.42}" width="${size*.84}" height="${size*.84}" preserveAspectRatio="xMidYMid meet"/>`:`<text x="${x}" y="${y+5}" text-anchor="middle" fill="#49341c">${t}</text>`)}
function draw(){let ns=[[110,280,0],[230,280,1],[310,280,2],[390,280,3],[390,200,4],[470,200,2],[550,200,5],[550,280,1],[630,280,6],[750,280,7]],es=[[0,1],[1,2],[2,3],[3,4],[4,5],[5,6],[6,7],[7,8],[8,9]];if($('dense').checked){ns.push([230,360,2],[310,360,1],[390,360,4],[470,360,3]);es.push([1,10],[10,11],[11,12],[12,13],[3,12])}let h=es.map(([a,b],i)=>`<path d="M${ns[a][0]},${ns[a][1]} L${ns[b][0]},${ns[b][1]}" stroke="${exploring&&i<3?'#9a601e':'#493b29'}" stroke-width="${exploring&&i<3?5:3}" fill="none"/>`).join('');ns.forEach(([x,y,t],i)=>{h+=icon(types[t],x,y,t===0||t===7?108:36);if(exploring&&i===1)h+=`<ellipse cx="${x}" cy="${y}" rx="25" ry="24" fill="none" stroke="#59402a" stroke-width="3"/>`;if(exploring&&i===2)h+=`<rect x="${x-23}" y="${y-23}" width="46" height="46" rx="8" fill="none" stroke="#b27b21" stroke-width="3"/>`});if($('held').checked&&!exploring)h+='<rect x="365" y="175" width="210" height="130" rx="12" fill="#345c6518" stroke="#356b78" stroke-width="3"/><circle class="badge" cx="577" cy="170" r="18"/><text x="577" y="176" text-anchor="middle" fill="#ecd4a0">人</text>';$('map').innerHTML=h}
$('cards').innerHTML=Array.from({length:7},(_,i)=>`<div class="card"><svg viewBox="0 0 230 120" width="100%" height="100%"><path d="M45,60 H185" stroke="#dcc591" stroke-width="3"/>${[45,92,138,185].map((x,j)=>icon(types[1+(i+j)%6],x,60,28)).join('')}</svg></div>`).join('');if(assets.paper)$('paper').style.backgroundImage=`url(${assets.paper})`;$('paper').style.backgroundSize='100% 100%';draw();</script></html>'''
(out/'index.html').write_text(html.replace('ASSETS',json.dumps(d['assets'],ensure_ascii=False)),encoding='utf-8')
print(out/'index.html')

# Round 1 presentation override: independent HTML, never production code.
page=(out/'index.html').read_text(encoding='utf-8')
page=page.replace('</style>','</style><link rel="stylesheet" href="round1.css">',1)
page=page.replace('视觉细化稿 · 提案 V1','第一轮 · 真实素材组合（待决定）')
page=page.replace('<label><input id="held"', '<select onchange="location.search=\'variant=\'+this.value"><option value="a">A · 纸质对话</option><option value="b">B · 原生对话</option></select><label style="display:none"><input id="held"')
page=page.replace('<section class="map" id="paper">','<section class="map" id="paper"><div class="paper-layer"><img class="top" src="assets/map_top_overgrowth.png"><div class="middle"></div><img class="bottom" src="assets/map_bottom_overgrowth.png"></div>')
start=page.index('<section class="annotation">')
end=page.index('</section>',start)
page=page[:start]+'<section class="annotation">第一轮只评审纸张、框体、按钮和字排。节点底板、连线、地图几何仍是旧示意，留待第二轮；不作为实现样例。HUD 仅留空间，未重制游戏 HUD。素材来源及缩放差异见 DESIGN.md 和素材联系表。'+page[end:]
page=page.replace('</html>','<script>const variant=new URLSearchParams(location.search).get("variant")||"a";document.body.classList.toggle("variant-b",variant==="b");document.querySelector("select").value=variant;</script></html>')
(out/'index.html').write_text(page,encoding='utf-8')

page=(out/'index.html').read_text(encoding='utf-8')
page=page.replace('</html>','<script src="refinement.js"></script></html>')
(out/'index.html').write_text(page,encoding='utf-8')
page=(out/'index.html').read_text(encoding='utf-8')
page=page.replace('</html>','<link rel="stylesheet" href="round2.css"><script src="round2.js"></script></html>')
(out/'index.html').write_text(page,encoding='utf-8')
page=(out/'index.html').read_text(encoding='utf-8')
page=page.replace('</html>','<link rel="stylesheet" href="round3.css"><script src="round3.js"></script></html>')
(out/'index.html').write_text(page,encoding='utf-8')
page=(out/'index.html').read_text(encoding='utf-8')
page=page.replace('</html>','<link rel="stylesheet" href="round4.css"><script src="round4.js"></script></html>')
(out/'index.html').write_text(page,encoding='utf-8')
