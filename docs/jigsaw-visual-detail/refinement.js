// Visual study only: native texture dots; no gameplay eligibility inference.
function nativeDots(x1,y1,x2,y2,r1=15,r2=15){const dx=x2-x1,dy=y2-y1,len=Math.hypot(dx,dy),n=Math.max(1,Math.floor((len-r1-r2)/10));return Array.from({length:n},(_,i)=>{const t=(r1+(i+.5)*(len-r1-r2)/n)/len;return `<image href="assets/map_dot.png" x="${x1+dx*t-1.68}" y="${y1+dy*t-3}" width="3.36" height="6" style="filter:brightness(0) saturate(100%)"/>`}).join('')}
const oldIcon=icon;icon=function(t,x,y,size){return oldIcon(t,x,y,size).replace(/^<rect[^>]*\/>/,'')};
const oldDraw=draw;draw=function(){oldDraw();const svg=document.getElementById('map');svg.querySelectorAll('path').forEach(p=>{const a=p.getAttribute('d').match(/-?\d+(?:\.\d+)?/g).map(Number);p.outerHTML=nativeDots(...a,a[0]===110?46:15,a[2]===750?46:15)});};draw();
document.querySelectorAll('.cards svg path').forEach(p=>{p.outerHTML=nativeDots(45,60,185,60,12,12)});
const title=document.querySelector('.pool h2');Object.assign(title.style,{display:'flex',alignItems:'center',justifyContent:'center',lineHeight:'1',paddingBottom:'10px',flexShrink:'0'});

document.querySelectorAll('.cards image[href="assets/map_dot.png"]').forEach(e=>e.style.filter='brightness(0) invert(.85) sepia(.45)');
document.querySelector('nav select').options[0].text='A · 先古对白 / 差异按钮';
document.querySelector('.annotation').textContent='视觉提案：原生点纹、先古对白及按钮组合。地图布局仅示意；正式状态、动画与 Godot 输入未验收。';
const helpOverlay=document.getElementById('overlay');document.getElementById('shell').append(helpOverlay);
helpOverlay.querySelector('.modal p:last-of-type').textContent='帮助打开期间暂停本人的地图操作。关闭后可继续编辑。';
function visualHelp(open){helpOverlay.hidden=!open;Array.from(document.getElementById('shell').children).filter(e=>e!==helpOverlay).forEach(e=>e.inert=open);if(open)helpOverlay.querySelector('button').focus();}
document.querySelector('.help').onclick=()=>visualHelp(true);helpOverlay.querySelector('button').onclick=()=>visualHelp(false);
