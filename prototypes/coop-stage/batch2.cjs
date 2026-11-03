// Experimental oracles, not production tests or a proposed protocol.
const {initial,apply,restore}=require('./experiment.cjs');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const scratch=fs.mkdtempSync(path.join(os.tmpdir(),'PROTOTYPE-coop-batch2-wipe-me-'));
const records=[];let trace=[];
const act=(s,a)=>{let r=apply(s,a);trace.push({action:a,state:r});return r;};
function record(name,expected,actual){records.push({name,expected,actual,ok:JSON.stringify(expected)===JSON.stringify(actual),trace});trace=[];}
function round(s){const p=path.join(scratch,'checkpoint.json');fs.writeFileSync(p,JSON.stringify(s));return restore(JSON.parse(fs.readFileSync(p)));}
let s=initial();
s=act(s,['票','甲','自动',{epoch:0,request:'vote-1'}]);s=act(s,['票','甲','自动',{epoch:0,request:'vote-1'}]);record('重复选择请求不解释为撤票','自动',s.votes.甲);
s=act(s,['票','乙','自动']);s=act(s,['票','丙','自动']);s=act(s,['结果',null,'失败']);s=act(s,['拿','甲','C',{epoch:0,request:'old-grab'}]);record('失败回编辑后旧请求拒绝',{},s.held);
for(const op of ['任务取消','任务超时']){s=initial(1);s=act(s,['票','甲','自动']);s=act(s,['动画结束']);s=round(s);s=act(s,[op]);record(op+'受控任务恢复出口',['编辑','原图',{},undefined], [s.phase,s.map,s.votes,s.subtitle]);}
s=initial(1);s=act(s,['票','甲','自动']);s=round(s);s=act(s,['动画结束']);s=act(s,['结果',null,'成功']);record('已授权执行恢复不重投票',[1,'先古','确定图'],[s.starts,s.phase,s.map]);
s={...initial(),phase:'探索'};for(const [p,d] of [['甲','店'],['乙','店'],['丙','事件']])s=act(s,['目的地',p,d,2]);s=act(s,['加载失败']);s=round(s);const picked=s.pending.dest;s=act(s,['确认',null,1]);s=act(s,['确认',null,1]);s=act(s,['客户端加载失败']);record('加载失败恢复固定目的地且成功只扣一次',['事件','事件',1,0],[picked,s.current.dest,s.entries,s.steps]);
for(let index=0;index<3;index++){s={...initial(),phase:'探索'};for(const [p,d] of [['甲','店'],['乙','店'],['丙','事件']])s=act(s,['目的地',p,d,index]);record('按玩家票列表索引'+index,['店','店','事件'][index],s.pending.dest);}
// Model reward effects explicitly; restore the complete pre-choice snapshot, not just a flag.
const before={...initial(),phase:'先古',map:'确定图',players:{甲:{gold:10,relics:[],deck:['Strike']},乙:{gold:20,relics:[],deck:['Defend']},丙:{gold:30,relics:[],deck:[]}}};
const partial=JSON.parse(JSON.stringify(before));partial.players.甲.gold+=100;partial.players.甲.relics.push('reward');partial.players.甲.deck.push('reward-card');partial.rewards=['甲'];
s=round(before);s.active=['甲','乙'];record('部分奖励整点回退并保留地图',{map:'确定图',gold:10,relics:[],deck:['Strike'],active:2,roster:3},{map:s.map,...s.players.甲,active:s.active.length,roster:s.roster.length});
const completed=JSON.parse(JSON.stringify(partial));completed.phase='继续';completed.rewards=['甲','乙','丙'];completed.continued=['甲'];s=round(completed);s.active=['甲','乙'];s=act(s,['奖励','甲']);s=act(s,['继续','甲']);s=act(s,['继续','乙']);record('完成奖励不重发且缺人共同继续',[110,1,'探索',3],[s.players.甲.gold,s.players.甲.relics.length,s.phase,s.roster.length]);
// Simulate write failure before replacing the complete file; no native save or OS crash claim.
const p=path.join(scratch,'atomic.json');fs.writeFileSync(p,JSON.stringify(before));let failure='';try{fs.writeFileSync(p+'.pending','{"players":');throw Error('injected before replacement');}catch(e){failure=e.message;}s=JSON.parse(fs.readFileSync(p));record('注入写失败不混用候选文件',['injected before replacement',before],[failure,s]);
s={...initial(),phase:'探索',masks:['甲','乙','丙'],safe:false,combat:{battle:{added:1,opening:1,stage:3,round:6,scrutiny:true,counts:{甲:11,乙:3,丙:8}}}};s=round(s);s=act(s,['战斗',null,'battle']);record('追猎完整计数占位恢复不重复开场',{added:1,opening:1,stage:3,round:6,scrutiny:true,counts:{甲:11,乙:3,丙:8}},s.combat.battle);
s=act(s,['末Boss胜利']);s=round(s);record('末Boss完整点不返还面具',[true,[]],[s.finalWon,s.masks]);
s={...initial(),phase:'探索'};for(const p of s.active)s=act(s,['目的地',p,'事件']);s=act(s,['确认',null,1]);s=act(s,['必要流程完成','甲']);s=act(s,['必要流程完成','乙']);record('0步本房安全且未全员完成不发面具',[0,true,[]],[s.steps,s.safe,s.masks]);s=act(s,['必要流程完成','丙']);s=act(s,['必要流程完成','丙']);record('全员完成各一份重复完成不叠加',['甲','乙','丙'],s.masks);
s={...initial(),phase:'探索'};for(const p of s.active)s=act(s,['目的地',p,'第一Boss']);s=act(s,['确认',null,1]);s=act(s,['完成']);record('最后一步第一Boss不发面具',[0,[]],[s.steps,s.masks]);
s={...initial(),phase:'探索'};for(const p of s.active)s=act(s,['目的地',p,'店']);s=act(s,['放弃进入']);for(const p of s.active)s=act(s,['目的地',p,'店']);s=act(s,['确认',null,1]);record('放弃后新进入拒绝旧确认',[0,2],[s.entries,s.pending.id]);s=act(s,['确认',null,2]);record('新进入确认仅一次',1,s.entries);
s=round(initial());s=act(s,['拿','甲','A',{epoch:0,request:'before-reload'}]);record('恢复隔离前会话旧请求',{},s.held);
const failures=records.filter(r=>!r.ok).length;fs.writeFileSync(path.join(__dirname,'batch2-results.json'),JSON.stringify({runtime:process.version,scratch,failures,records},null,2));console.log(JSON.stringify({cases:records.length,failures,scratch}));process.exitCode=failures?1:0;
