document.getElementById('dense')?.closest('label').remove();
const R4={long:false,many:false,step:'9'};
const r4bar=document.createElement('span');r4bar.innerHTML='<label><input id="r4long" type="checkbox">长文案</label> <label><input id="r4many" type="checkbox">五人头像</label> <label>探索提示 <select id="r4step"><option value="9">9步</option><option value="1">1步</option><option value="0">刚耗尽</option><option value="none0">0步 / 提示已消失</option></select></label>';controls.append(r4bar);
const beforeR4=draw;function adaptDraw(){beforeR4();if(R4.many&&S3.scene==='votes'){document.querySelectorAll('.vote-avatars').forEach(e=>e.innerHTML=['ironclad','silent','defect','regent','necrobinder'].map(n=>`<img src="assets/character_icon_${n}.png">`).join(''));document.querySelectorAll('.vote-avatars')[1]?.remove()}
const speech=document.getElementById('speech');if(R4.long&&!exploring)speech.textContent='你的愿景触犯了尖塔的规则……请检查碎块之间的距离与通路连接位置。已保留原布局，你可以继续调整拼图，再交由先古之民审视。';
if(!R4.long&&!exploring)speech.textContent='从左侧拿取碎块，构建你的愿景。';fitMap();if(exploring){document.getElementById('steps').innerHTML=`<strong>剩余步数 ${R4.step==='none0'?0:R4.step}</strong><span>${R4.step==='none0'?'':R4.step==='1'?'最后一步，请尽快前往Boss处。':R4.step==='0'?'建筑师的目光锁定了你...并降下惩罚。':'还可前行9步。'}</span>`}}
draw=adaptDraw;round2Draw=adaptDraw;document.getElementById('r3scene').onchange=e=>{S3.scene=e.target.value;mode(S3.scene==='explore');adaptDraw()};for(const [id,key]of [['r4long','long'],['r4many','many']])document.getElementById(id).onchange=e=>{R4[key]=e.target.checked;adaptDraw()};document.getElementById('r4step').onchange=e=>{R4.step=e.target.value;adaptDraw()};adaptDraw();
function fitMap(){const svg=document.getElementById('map'),bb=svg.getBBox(),pad=.6,z=Math.min(1,R2.zoom);svg.setAttribute('viewBox',`${bb.x-pad-(bb.width+pad*2)*(1/z-1)/2} ${bb.y-pad-(bb.height+pad*2)*(1/z-1)/2} ${(bb.width+pad*2)/z} ${(bb.height+pad*2)/z}`)}
window.addEventListener('resize',fitMap);

document.querySelector('#r2zoom option[value="1.3"]')?.remove();

