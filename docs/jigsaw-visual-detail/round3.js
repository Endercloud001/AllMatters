// Preset visual states only; no eligibility or vote resolution logic.
const S3={scene:'held'};
function portrait(name,x,y){return `<image class="state-avatar" href="assets/character_icon_${name}.png" x="${x}" y="${y}" width=".85" height=".85"/>`}
const baseR3=round2Draw;
function stateDraw(){baseR3();const svg=document.getElementById('map');let h='';document.querySelectorAll('.vote-avatars').forEach(e=>e.remove());
if(S3.scene==='held'){h='<path d="M6.35 .65 V-2.65 H11.65 V-1.35 H7.65 V.65 Z" fill="#d2573b22" stroke="#b9482c" stroke-width=".09"/>'+portrait('ironclad',11.6,-3.5);}
if(S3.scene==='focus'){h='<image href="assets/map_monster_outline.png" x="2.48" y="-.52" width="1.04" height="1.04" style="filter:sepia(1)"/><path d="M4.35,-.35 V-.65 H4.65 M5.35,-.65 H5.65 V-.35 M5.65,.35 V.65 H5.35 M4.65,.65 H4.35 V.35" fill="none" stroke="#fff5c6" stroke-width=".1"/>';}
if(S3.scene==='votes'){document.querySelectorAll('.vote').forEach((b,i)=>{const p=document.createElement('span');p.className='vote-avatars';p.innerHTML=(i?['defect']:['ironclad','silent']).map(n=>`<img src="assets/character_icon_${n}.png">`).join('');b.append(p)})}
if(S3.scene==='explore'){h='<image href="assets/map_circle_4.png" x="2.3" y="-.7" width="1.4" height="1.4" style="filter:brightness(0)"/>'+portrait('ironclad',4.1,-1.65)+portrait('silent',5,-1.65)+portrait('defect',10.55,-3.6);const icons=[...svg.querySelectorAll('image')].filter(e=>!e.getAttribute('href').includes('map_dot'));[2,6,8].forEach(i=>{if(icons[i]){icons[i].classList.add('state-pulse');icons[i].style.transformBox='fill-box';icons[i].style.transformOrigin='center'}});h+='<path d="M4.35,-.35 V-.65 H4.65 M5.35,-.65 H5.65 V-.35 M5.65,.35 V.65 H5.35 M4.65,.65 H4.35 V.35" fill="none" stroke="#fff5c6" stroke-width=".1"/>';}
if(S3.scene==='focus')h+=rIcon([3,0,'monster']);
if(S3.scene==='held'){[...svg.querySelectorAll('image')].filter(e=>!e.getAttribute('href').includes('map_dot')).slice(3,7).forEach(e=>e.style.opacity='.65')}
svg.insertAdjacentHTML('beforeend',h);if(S3.scene==='explore')document.getElementById('speech').textContent='还可前行9步。';document.querySelector('.annotation').textContent={held:'状态提案：铁甲战士持有完整四节点碎块，仅一个头像；整体轮廓为提案。',focus:'状态提案：左侧战斗节点悬停轮廓，右侧问号节点独立四角焦点。',votes:'状态提案：两位玩家选择审视，一位选择自动；没有玩家同时持有碎块。',explore:'状态提案：普通已访问节点用原生圈；问号、商店与Boss可前往时脉动。商店/Boss/先古无访问圈。头像表示预设选择，未运行分歧动画。'}[S3.scene]+' HTML仅表达视觉；真实Godot状态、动画及输入未验收。';}
const stateLabel=document.createElement('label');stateLabel.innerHTML='状态审阅 <select id="r3scene"><option value="held">持有整块</option><option value="focus">悬停 / 焦点</option><option value="votes">结束选择</option><option value="explore">探索叠加</option></select>';document.querySelector('.r2-controls').append(stateLabel);
document.getElementById('r3scene').onchange=e=>{S3.scene=e.target.value;mode(S3.scene==='explore');stateDraw()};round2Draw=stateDraw;draw=stateDraw;stateDraw();
helpOverlay.querySelector('.modal p:last-of-type').textContent='帮助打开期间暂停本人的地图操作。关闭后返回地图。';const originalHelp=visualHelp;let returnFocus;visualHelp=function(open){if(open)returnFocus=document.activeElement;originalHelp(open);if(!open&&returnFocus?.isConnected)returnFocus.focus()};

