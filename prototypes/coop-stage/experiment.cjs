// THROWAWAY: acceptance-order model, not a networking or save implementation.
const clone=x=>JSON.parse(JSON.stringify(x));
function initial(n=3){return {active:['甲','乙','丙'].slice(0,n),roster:['甲','乙','丙'].slice(0,n),phase:'编辑',blocks:{A:0,B:2,C:4},edges:['A-B'],held:{},votes:{},starts:0,map:'原图',rewards:[],continued:[],steps:1,entries:0,pending:null,masks:[],safe:true,combat:{},seq:0};}
function apply(s,a){s=clone(s);s.seq++;let [op,p,x,y]=a;
 const locked=b=>Object.values(s.held).some(h=>h.block===b||s.edges.some(e=>e.split('-').includes(h.block)&&e.split('-').includes(b)));
 const editable=()=>s.phase==='编辑'&&!s.votes[p]&&s.active.includes(p);
 const decide=()=>{if(s.phase==='编辑'&&s.active.length&&s.active.every(p=>s.votes[p]&&s.votes[p]===s.votes[s.active[0]])){s.phase=s.votes[s.active[0]];s.starts++;}};
 if(op==='拿'&&editable()&&!s.held[p]&&!locked(x))s.held[p]={block:x,origin:s.blocks[x]};
 if(op==='取消'&&editable())delete s.held[p];
 if(op==='落'&&editable()&&s.held[p]&&!Object.values(s.blocks).includes(x)){let b=s.held[p].block;s.blocks[b]=x;s.edges=s.edges.filter(e=>!e.split('-').includes(b));delete s.held[p];}
 if(['建','删'].includes(op)&&editable()&&!s.held[p]&&!x.split('-').some(locked)){s.edges=s.edges.filter(e=>e!==x);if(op==='建')s.edges.push(x);}
 if(op==='票'&&s.phase==='编辑'&&s.active.includes(p)&&!s.held[p]){s.votes[p]=s.votes[p]===x?null:x;decide();}
 if(op==='断线'){delete s.held[p];delete s.votes[p];s.active=s.active.filter(q=>q!==p);decide();}
 if(op==='动画结束'&&s.phase==='自动')s.subtitle='先古之民的意志正在影响高塔';
 if(op==='结果'&&['自动','检查'].includes(s.phase)){if(x==='成功'){s.map='确定图';s.phase='先古';}else {if(s.phase==='检查')delete s.blocks.C;s.phase='编辑';s.votes={};}delete s.subtitle;}
 if(op==='奖励'&&s.phase==='先古'&&s.active.includes(p)&&!s.rewards.includes(p)){s.rewards.push(p);if(s.active.every(q=>s.rewards.includes(q)))s.phase='继续';}
 if(op==='继续'&&s.phase==='继续'&&s.active.includes(p)){if(!s.continued.includes(p))s.continued.push(p);if(s.active.every(q=>s.continued.includes(q)))s.phase='探索';}
 if(op==='目的地'&&s.phase==='探索'&&!s.pending){s.votes[p]=x;if(s.active.every(q=>s.votes[q])){const tickets=s.active.map(q=>s.votes[q]);s.pending={id:s.entries+1,dest:tickets[y??0],tickets};}}
 if(op==='确认'&&s.pending&&s.pending.id===x){s.entries++;s.steps=Math.max(0,s.steps-1);s.safe=s.masks.length===0;s.current=clone(s.pending);s.pending=null;s.votes={};}
 if(op==='完成'&&s.steps===0&&s.current?.dest!=='Boss')s.masks=[...s.active];
 if(op==='战斗'&&!s.combat[x])s.combat[x]={added:s.safe?0:1,opening:s.safe?0:1,round:3,counts:{甲:7,乙:2,丙:11}};
 if(op==='末Boss胜利'){s.masks=[];s.finalWon=true;}
 return s;
}
function restore(s){s=clone(s);s.held={};s.continued=[];if(['编辑','继续','先古'].includes(s.phase))s.votes={};if(s.phase==='先古')s.rewards=[];return s;}
const scenarios=[
 ['关联保护',initial(),[['拿','甲','A'],['拿','乙','A'],['拿','丙','A'],['拿','乙','B'],['取消','甲']],s=>!Object.keys(s.held).length&&s.edges[0]==='A-B'],
 ['同落点',initial(),[['拿','甲','A'],['拿','乙','C'],['落','甲',9],['落','乙',9]],s=>s.blocks.A===9&&s.held.乙.block==='C'],
 ['重复建删',initial(),[['建','甲','B-C'],['建','乙','B-C'],['删','甲','B-C'],['删','乙','B-C']],s=>!s.edges.includes('B-C')],
 ['切换取消混票',initial(),[['票','甲','自动'],['票','甲','自动'],['票','甲','检查'],['票','乙','自动'],['票','丙','检查']],s=>s.phase==='编辑'&&s.votes.甲==='检查'],
 ['最后票先',initial(),[['票','甲','自动'],['票','乙','自动'],['票','丙','自动'],['票','甲','自动'],['拿','甲','C']],s=>s.starts===1&&s.phase==='自动'&&!s.held.甲],
 ['撤票先',initial(),[['票','甲','自动'],['票','乙','自动'],['票','甲','自动'],['票','丙','自动']],s=>s.starts===0&&s.phase==='编辑'],
 ['断线先恢复',initial(),[['拿','丙','A'],['票','甲','自动'],['票','乙','自动'],['断线','丙']],s=>s.starts===1&&!s.held.丙&&s.roster.length===3&&s.active.length===2],
 ['单人',initial(1),[['票','甲','检查'],['结果',null,'成功'],['奖励','甲'],['继续','甲']],s=>s.phase==='探索'&&s.steps===1],
 ['受控失败',initial(1),[['票','甲','自动'],['动画结束'],['结果',null,'失败']],s=>s.map==='原图'&&s.phase==='编辑'],
 ['检查失败撤回',initial(1),[['票','甲','检查'],['结果',null,'失败']],s=>s.blocks.C===undefined&&s.phase==='编辑'],
 ['探索重复和事件连续战斗',{...initial(),phase:'探索'},[['目的地','甲','店'],['目的地','乙','店'],['目的地','丙','事件',1],['加载失败'],['确认',null,1],['确认',null,1],['战斗',null,'事件战斗1'],['战斗',null,'事件战斗2']],s=>s.entries===1&&s.steps===0&&s.current.dest==='店'&&s.safe&&s.combat.事件战斗2.added===0],
 ['同店重访',{...initial(),phase:'探索'},[['目的地','甲','店'],['目的地','乙','店'],['目的地','丙','店'],['确认',null,1],['完成'],['完成'],['目的地','甲','店'],['目的地','乙','店'],['目的地','丙','店'],['确认',null,2],['战斗',null,'第二进入'],['战斗',null,'第二进入']],s=>s.entries===2&&s.masks.length===3&&!s.safe&&s.combat.第二进入.added===1],
];
if(typeof module!=='undefined')module.exports={initial,apply,restore,scenarios};
if(typeof require!=='undefined'&&require.main===module){
 const fs=require('node:fs'),os=require('node:os'),path=require('node:path');const dir=fs.mkdtempSync(path.join(os.tmpdir(),'PROTOTYPE-coop-wipe-me-'));let failures=0;const records=[];
 for(const [name,start,actions,oracle] of scenarios){let s=start;const trace=actions.map(a=>{s=apply(s,a);return {action:a,state:s};});const ok=oracle(s);failures+=!ok;records.push({name,version:'model-only',players:start.active.length,ok,trace});}
 // Explicit scratch disk roundtrips, including interrupted new candidate.
 for(const phase of ['编辑','自动','检查','先古','继续','探索']){let s=initial();s.phase=phase;s.votes={甲:'自动'};s.held={甲:{block:'A',origin:0}};s.map=phase==='编辑'?'原图':'确定图';s.rewards=phase==='继续'?s.active.slice():['甲'];s.pending={id:1,dest:'店',tickets:['店','店','事件']};s.combat={c:{added:1,opening:1,round:3,counts:{甲:7,乙:2}}};const file=path.join(dir,phase+'.json');fs.writeFileSync(file,JSON.stringify(s));fs.writeFileSync(file+'.incomplete','{"phase":');let r=restore(JSON.parse(fs.readFileSync(file)));let ok=!Object.keys(r.held).length&&r.map===s.map&&JSON.stringify(r.pending)===JSON.stringify(s.pending)&&JSON.stringify(r.combat)===JSON.stringify(s.combat)&&(phase!=='先古'||r.rewards.length===0)&&(phase!=='继续'||r.rewards.length===3)&&r.continued.length===0;records.push({name:'scratch roundtrip '+phase,version:'JSON model only',players:3,ok,before:s,after:r});failures+=!ok;}
 let s=initial();s=apply(s,['末Boss胜利']);fs.writeFileSync(path.join(dir,'boss.json'),JSON.stringify(s));let r=restore(JSON.parse(fs.readFileSync(path.join(dir,'boss.json'))));const ok=r.finalWon&&r.masks.length===0;records.push({name:'final Boss roundtrip',ok,before:s,after:r});failures+=!ok;
 fs.writeFileSync(path.join(__dirname,'results.json'),JSON.stringify({command:'node experiment.cjs',node:process.version,scratch:dir,failures,records},null,2));console.log(JSON.stringify({cases:records.length,failures,scratch:dir}));process.exitCode=failures?1:0;
}
