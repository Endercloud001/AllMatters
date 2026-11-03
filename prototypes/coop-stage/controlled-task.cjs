// Controlled asynchronous work only: no generator algorithm or native cutscene.
const {initial,apply,restore}=require('./experiment.cjs');
const fs=require('node:fs'),path=require('node:path');
(async()=>{
 const records=[];
 for(const outcome of ['success','cancel','timeout','reload']){
  let state=apply(initial(1),['票','甲','自动']);state=apply(state,['动画结束']);
  const captured=state.epoch??0;let release;
  const pending=new Promise(r=>release=r).then(()=>{state=apply(state,['结果',null,'成功',{epoch:captured,request:'controlled-job'}]);});
  const waiting=state.phase==='自动'&&state.map==='原图'&&!!state.subtitle;
  if(outcome==='cancel')state=apply(state,['任务取消']);
  if(outcome==='timeout')state=apply(state,['任务超时']);
  if(outcome==='reload')state=restore(state);
  release();await pending;
  const staleIgnored=outcome==='success'||state.map==='原图';
  if(outcome==='reload')state=apply(state,['结果',null,'成功',{epoch:state.epoch,request:'restarted-job'}]);
  const expected=['success','reload'].includes(outcome)?'先古':'编辑';
  records.push({outcome,waiting,staleIgnored,ok:waiting&&staleIgnored&&state.phase===expected,state});
 }
 const failures=records.filter(r=>!r.ok).length;
 fs.writeFileSync(path.join(__dirname,'controlled-task-results.json'),JSON.stringify({runtime:process.version,failures,records},null,2));console.log(JSON.stringify({cases:records.length,failures}));process.exitCode=failures?1:0;
})();
