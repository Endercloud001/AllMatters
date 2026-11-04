// THROWAWAY: compare input navigation, not production UI/network/auto generation.
D.pool = inputFixture.pool;
Model.fresh = function(mode=0) {
  const wantDouble=mode===2;
  const fixture=inputFixture.witnesses.find(w=>w.nodes.some(n=>n.type==='boss2')===wantDouble);
  return {...clone(fixture),double:wantDouble,votes:[null,null],hands:[null,null],select:[null,null],blanks:[0,0],phase:'编辑',message:''};
};
document.title='AllMatters · 手柄输入比较（一次性原型）';
const css=document.createElement('style');
css.textContent=`header{height:72px;padding:8px 20px}header h1{font-size:22px}.layout{height:calc(100vh - 72px)}main{gap:6px;padding:10px 16px 62px}#tabs,#guide,.toolbar label,.toolbar select{display:none}#pool button{height:135px}.piece-preview{height:86px}#state{max-height:90px}#input-guide{font-size:13px;color:#e9dfcc;line-height:1.6}#input-summary{font-size:12px;color:#d9b568;white-space:pre-wrap}#switcher{position:fixed;bottom:8px;left:50%;transform:translateX(-50%);z-index:20;background:#20231f;border:1px solid #d9b568;border-radius:9px;padding:6px;display:flex;gap:8px;align-items:center}#virtual-pointer{position:fixed;width:18px;height:18px;border:3px solid #151515;outline:2px solid #fff;border-radius:50%;pointer-events:none;z-index:35;transform:translate(-50%,-50%)}.pad-focus{outline:3px solid #fff!important;outline-offset:-4px!important}#input-help{position:fixed;inset:12% 18%;padding:28px;background:#242521;border:2px solid #d9b568;z-index:40;overflow:auto;box-shadow:0 0 0 100vmax #000a}#input-help h2{font-size:24px}#input-help button{display:block;margin-top:20px}.note{font-size:12px}.board-title{top:8px;font-size:15px}`;
document.head.append(css);
document.querySelector('.note').innerHTML='一次性手柄比较 · 仅本机两人模拟<br>浏览器输入 ≠ 正式Godot/联机验收';
document.querySelector('h1').textContent='手柄如何拼出愿景？';
const intro=document.createElement('div');intro.id='input-guide';
intro.textContent='问题：虚拟指针还是区域焦点＋网格光标更顺手？试拿块、旋转、取消、连拆线、打开帮助。Start帮助；Select切方案。';
document.querySelector('main').prepend(intro);
const summary=document.createElement('div');summary.id='input-summary';intro.after(summary);
const pointer=document.createElement('div');pointer.id='virtual-pointer';pointer.hidden=true;document.body.append(pointer);
const auto=document.createElement('button');auto.textContent='令先古之民决定';auto.onclick=()=>vote('auto');document.querySelector('.submit').before(auto);
const helpButton=document.createElement('button');helpButton.textContent='帮助';helpButton.onclick=()=>toggleHelp();document.querySelector('.toolbar').append(helpButton);
const picker=document.createElement('button');picker.textContent='切换模拟玩家';picker.onclick=()=>{p=1-p;draw()};document.querySelector('.toolbar').append(picker);
const help=document.createElement('section');help.id='input-help';help.hidden=true;
help.innerHTML='<h2>输入实验帮助</h2><p>打开帮助已按取消规则恢复／归还本人碎块、清除待连端点，保留结束按钮选择。只阻挡本人操作，另一模拟玩家仍可编辑。</p><p>A：拿取／放下／激活按钮。X：持有时逆时针旋转；空手时选端连线或拆线。B：取消当前拿取或待连端点。Y：持有时归还池。右摇杆平移，LT／RT缩放。LB／RB切区域，Start关闭帮助，Select切比较方案。</p><p>网格方案：空手左摇杆／方向键按方向选节点；持有时逐格移动。池中逐卡选择，操作区逐按钮选择。虚拟指针方案：左摇杆连续移动指针，方向键微调；A／X按指针所指目标操作。LB／RB可将指针跳到另一区域。</p><p>实验键盘映射仅用于检查：方向键／WASD模拟方向，Enter=A，X=X，Esc=B，Y=Y，Q／E=LB／RB，H=Start，V=Select，IJKL=镜头，-／+=缩放。不承诺纯键盘正式支持。</p><p>这不是完整玩法或正式教程：自动按钮只演示已有共识入口，不运行生成；探索、先古之民奖励、联机、保存不在本页验证。手柄按标准布局映射，浏览器若未发现设备，可先按任意手柄按钮或改用本地HTTP运行。</p><button id="close-input-help">返回拼图（B / Start）</button>';
document.body.append(help);$('close-input-help').onclick=()=>toggleHelp(false);
const bar=document.createElement('div');bar.id='switcher';
bar.innerHTML='<button id="previous-variant">◀</button><span id="variant-name"></span><button id="next-variant">▶</button><button id="reset-input">重置练习</button><button id="guide-input">引导场景</button>';
document.body.append(bar);
const nav={variant:new URLSearchParams(location.search).get('variant')==='pointer'?'pointer':'grid',region:0,index:0,node:'N',x:0,y:0,px:650,py:400,device:'尚未检测到手柄',last:'待操作',scenario:0};
const regionNames=['碎块池','地图','操作区'];
const originalDraw=draw;
draw=function(){originalDraw();paintInput()};
function poolButtons(){return [...$('pool').querySelectorAll('button')]}
function actionButtons(){return [...document.querySelectorAll('.toolbar button')]}
function changeVariant(){nav.variant=nav.variant==='grid'?'pointer':'grid';const url=new URL(location.href);url.searchParams.set('variant',nav.variant);history.replaceState(null,'',url);nav.last='已切换方案；布局和持有状态保留';draw()}
$('previous-variant').onclick=changeVariant;$('next-variant').onclick=changeVariant;
function resetInput(){s=Model.fresh();p=0;nav.region=0;nav.index=0;nav.node='N';nav.x=0;nav.y=0;help.hidden=true;fit();nav.last='已重置为最新池的合法见证布局';draw()}
$('reset-input').onclick=resetInput;
const scenarios=[
 ['拿放与取消','在池中选一块，A拿取，到地图移动，X旋转，B取消。再拿地图中已有块，移动后B，观察原边恢复。'],
 ['精确连拆线','在地图选先古之民与其直接相连节点，X各一次拆跨块线；再X两端连回。比较找节点的负担。'],
 ['帮助与确认','拿着碎块时Start打开帮助；返回后应空手、原布局保留。空手选择结束按钮后打开帮助，选择应保留。'],
 ['边缘与镜头','右摇杆平移、LT/RT缩放，再在远处拿放和连线。网格光标移出视区会带动镜头。']
];
$('guide-input').onclick=()=>{resetInput();const [title,desc]=scenarios[nav.scenario++%scenarios.length];intro.textContent=title+'：'+desc;draw()};
function paintInput(){
  document.querySelectorAll('.pad-focus').forEach(el=>el.classList.remove('pad-focus'));
  const rect=svg.getBoundingClientRect();
  if(nav.variant==='grid'){
    pointer.hidden=true;
    if(nav.region===1){
      const h=s.hands[p],n=s.nodes.find(n=>n.id===nav.node);
      const x=h?nav.x:n?.x??nav.x,y=h?nav.y:n?.y??nav.y;
      const circle=document.createElementNS('http://www.w3.org/2000/svg','circle');
      for(const [k,v]of Object.entries({cx:x,cy:y,r:1.05,fill:'none',stroke:'#121212','stroke-width':.14,'stroke-dasharray':'.25 .15','pointer-events':'none'}))circle.setAttribute(k,v);
      svg.append(circle);
      document.querySelector('.board').classList.add('pad-focus');
    }else{const list=nav.region===0?poolButtons():actionButtons();nav.index=Math.max(0,Math.min(nav.index,list.length-1));list[nav.index]?.classList.add('pad-focus');list[nav.index]?.scrollIntoView({block:'nearest'})}
  }else{pointer.hidden=!help.hidden;pointer.style.left=nav.px+'px';pointer.style.top=nav.py+'px'}
  $('variant-name').textContent=nav.variant==='grid'?'B · 区域焦点＋网格光标':'A · 虚拟指针';
  const h=s.hands[p];
  summary.textContent=`${nav.device}｜${p?'乙':'甲'}｜${regionNames[nav.region]}｜${h?'持有 '+D.pool[h.id].name:'空手'}｜首端 ${s.select[p]||'无'}｜确认 ${s.votes[p]||'未选择'}\n${nav.last}`;
}
function toggleHelp(open=help.hidden){if(open){Model.cancel(s,p);help.hidden=false;nav.last='帮助打开：已取消持有/待连；确认票保留';$('close-input-help').focus()}else{help.hidden=true;nav.last='帮助关闭'}draw()}
function gridMove(dx,dy){
  if(nav.region!==1){const list=nav.region===0?poolButtons():actionButtons();nav.index=(nav.index+(dy||dx)+list.length)%Math.max(1,list.length);return}
  const h=s.hands[p];
  if(h){nav.x+=dx;nav.y+=dy;Model.move(s,p,nav.x,nav.y);ensureVisible(nav.x,nav.y);return}
  const at=s.nodes.find(n=>n.id===nav.node)||s.nodes[0];
  const choices=s.nodes.filter(n=>n!==at&&((n.x-at.x)*dx+(n.y-at.y)*dy)>0).map(n=>({n,score:Math.hypot(n.x-at.x,n.y-at.y)+3*Math.abs((n.x-at.x)*dy-(n.y-at.y)*dx)})).sort((a,b)=>a.score-b.score);
  if(choices[0]){nav.node=choices[0].n.id;nav.x=choices[0].n.x;nav.y=choices[0].n.y;ensureVisible(nav.x,nav.y)}
}
function ensureVisible(x,y){if(x<view[0]+2)view[0]=x-2;if(x>view[0]+view[2]-2)view[0]=x-view[2]+2;if(y<view[1]+2)view[1]=y-2;if(y>view[1]+view[3]-2)view[1]=y-view[3]+2}
function setRegion(delta){nav.region=(nav.region+delta+3)%3;nav.index=0;if(nav.variant==='pointer'){const el=nav.region===0?$('pool'):nav.region===1?svg:document.querySelector('.toolbar');const r=el.getBoundingClientRect();nav.px=r.left+r.width/2;nav.py=r.top+r.height/2}nav.last='切到'+regionNames[nav.region]}
function targetAtPointer(){return document.elementFromPoint(nav.px,nav.py)}
function syncHandAtPointer(){if(!s.hands[p])return;const r=svg.getBoundingClientRect();if(nav.px>=r.left&&nav.px<=r.right&&nav.py>=r.top&&nav.py<=r.bottom){const pt=point({clientX:nav.px,clientY:nav.py});Model.move(s,p,Math.floor(pt.x+.5),Math.floor(pt.y+.5))}}
function command(key){
  if(key==='help'){toggleHelp();return}
  if(!help.hidden){if(key==='cancel'||key==='primary')toggleHelp(false);return}
  nav.last=key;
  if(key==='variant'){changeVariant();return}
  if(key==='previous'||key==='next'){setRegion(key==='previous'?-1:1);draw();return}
  if(key==='cancel'){if(s.hands[p]||s.select[p])Model.cancel(s,p);else nav.last='原版暂停菜单入口（本页仅标示，不退出拼图、不撤票）';draw();return}
  if(key==='return'){if(Model.edit(s,p))Model.remove(s,p);draw();return}
  if(key==='secondary'){
    if(!Model.edit(s,p))return;
    if(!s.hands[p]&&nav.variant==='grid'&&nav.region!==1)return;
    if(s.hands[p])Model.rotate(s,p);
    else{const id=nav.variant==='grid'&&nav.region===1?nav.node:targetAtPointer()?.closest('[data-id]')?.dataset.id;Model.link(s,p,id)}
    draw();return;
  }
  if(key==='primary'){
    if(nav.variant==='grid'){
      if(nav.region===0){poolButtons()[nav.index]?.click();if(s.hands[p]){nav.region=1;nav.x=0;nav.y=-7;Model.move(s,p,nav.x,nav.y)}}
      else if(nav.region===2)actionButtons()[nav.index]?.click();
      else if(Model.edit(s,p)){if(s.hands[p]){Model.put(s,p)}else{const n=s.nodes.find(n=>n.id===nav.node);if(n?.piece>=0){Model.take(s,p,n.piece);if(s.hands[p]){nav.x=s.hands[p].preview[0].x;nav.y=s.hands[p].preview[0].y}}}}
    }else{
      const target=targetAtPointer();
      if(target?.closest('#map')){syncHandAtPointer();if(Model.edit(s,p)){if(s.hands[p])Model.put(s,p);else{const n=s.nodes.find(n=>n.id===target.closest('[data-id]')?.dataset.id);if(n?.piece>=0)Model.take(s,p,n.piece)}}}
      else target?.closest('button,#return-zone')?.click();
    }
    draw();
  }
}
document.addEventListener('pointerdown',e=>{if(!help.hidden&&!help.contains(e.target)&&!bar.contains(e.target)){e.preventDefault();e.stopImmediatePropagation()}},true);
document.addEventListener('contextmenu',e=>{if(!help.hidden){e.preventDefault();e.stopImmediatePropagation()}},true);
document.addEventListener('wheel',e=>{if(!help.hidden&&!help.contains(e.target)){e.preventDefault();e.stopImmediatePropagation()}},{capture:true,passive:false});
document.onkeydown=e=>{
 const k=e.key.toLowerCase(),map={enter:'primary',x:'secondary',escape:'cancel',y:'return',q:'previous',e:'next',h:'help',v:'variant'};
 if(map[k]){e.preventDefault();command(map[k]);return}
 const dirs={arrowleft:[-1,0],a:[-1,0],arrowright:[1,0],d:[1,0],arrowup:[0,-1],w:[0,-1],arrowdown:[0,1],s:[0,1]};
 if(dirs[k]&&help.hidden){e.preventDefault();const [dx,dy]=dirs[k];if(nav.variant==='grid')gridMove(dx,dy);else{nav.px+=dx*8;nav.py+=dy*8;syncHandAtPointer()}draw()}
 if(help.hidden&&'ijkl'.includes(k)){view[0]+=k==='j'?-1:k==='l'?1:0;view[1]+=k==='i'?-1:k==='k'?1:0;draw()}
 if(help.hidden&&(k==='-'||k==='='||k==='+')){const f=k==='-'?1.08:.92;view[2]*=f;view[3]*=f;draw()}
};
let previousButtons=[],lastMove=0,lastFrame=performance.now();
const mapping={0:'primary',1:'cancel',2:'secondary',3:'return',4:'previous',5:'next',8:'variant',9:'help'};
function gamepadFrame(now){
 const dt=Math.min((now-lastFrame)/1000,.05);lastFrame=now;
 const pad=[...(navigator.getGamepads?.()||[])].find(Boolean);
 if(pad){
  const before=[...view,nav.px,nav.py,nav.x,nav.y,nav.node,nav.index].join(',');
  nav.device=(pad.mapping==='standard'?'标准手柄：':'非标准映射（需核对）：')+pad.id.slice(0,38);
  const buttons=pad.buttons.map(b=>b.pressed);
  for(const [i,action]of Object.entries(mapping))if(buttons[i]&&!previousButtons[i])command(action);
  if(help.hidden){
   const ax=pad.axes[0]||0,ay=pad.axes[1]||0,dx=buttons[14]?-1:buttons[15]?1:0,dy=buttons[12]?-1:buttons[13]?1:0;
   if(nav.variant==='pointer'){
    nav.px=Math.max(2,Math.min(innerWidth-2,nav.px+(Math.abs(ax)>.18?ax*460*dt:0)));
    nav.py=Math.max(2,Math.min(innerHeight-2,nav.py+(Math.abs(ay)>.18?ay*460*dt:0)));
    if((dx||dy)&&now-lastMove>110){nav.px+=dx*5;nav.py+=dy*5;lastMove=now}
    syncHandAtPointer();
    const hovered=targetAtPointer();if(hovered?.closest('#pool')){const pr=$('pool').getBoundingClientRect();if(nav.py>pr.bottom-30)$('pool').scrollTop+=240*dt;if(nav.py<pr.top+30)$('pool').scrollTop-=240*dt}
   }else if((dx||dy||Math.max(Math.abs(ax),Math.abs(ay))>.55)&&now-lastMove>150){const mx=dx||(!dy&&Math.abs(ax)>Math.abs(ay)?Math.sign(ax):0),my=dy||(!dx&&Math.abs(ay)>=Math.abs(ax)?Math.sign(ay):0);gridMove(mx,my);lastMove=now}
   if(Math.abs(pad.axes[2]||0)>.2)view[0]+=pad.axes[2]*view[2]*dt;
   if(Math.abs(pad.axes[3]||0)>.2)view[1]+=pad.axes[3]*view[3]*dt;
   const zoom=(pad.buttons[6]?.value||0)-(pad.buttons[7]?.value||0);if(Math.abs(zoom)>.1){const f=Math.exp(zoom*dt);view[2]*=f;view[3]*=f}
  }
  previousButtons=buttons;
  const after=[...view,nav.px,nav.py,nav.x,nav.y,nav.node,nav.index].join(',');
  if(help.hidden&&before!==after)draw();else paintInput();
 }else{nav.device='尚未检测到手柄（先按任意按钮）';previousButtons=[];paintInput()}
 requestAnimationFrame(gamepadFrame);
}
window.inputExperiment={nav,command,gridMove,reset:resetInput,toggleHelp,get helpOpen(){return !help.hidden}};
resetInput();requestAnimationFrame(gamepadFrame);
