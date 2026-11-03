// Actual worker interruption; scratch JSON input, not a native game save.
const {Worker,isMainThread,parentPort,workerData}=require('node:worker_threads');
const fs=require('node:fs'),assert=require('node:assert/strict'),{solve}=require('./search.cjs');
if(!isMainThread){
 const d=workerData;
 const r=solve(d.pool,d.double,d.B,d.seed,d.budget,d.witness,n=>{if(n===10)parentPort.postMessage({expanded:n})});
 parentPort.postMessage({result:r});
}else (async()=>{
 const d=require('./controlled.json'),results=[];
 for(const double of [false,true]){
  const input={pool:d.pool,double,B:13,seed:27,budget:10000,witness:d.witnesses[Number(double)],algorithm:'prototype-tree-v1',phase:'generating',oldLayout:{nodes:[],edges:[],used:[]}};
  const checkpoint=__dirname+'/PROTOTYPE-worker-checkpoint.json';fs.writeFileSync(checkpoint,JSON.stringify(input));
  const worker=new Worker(__filename,{workerData:input});
  const expanded=await new Promise((resolve,reject)=>{worker.once('message',m=>resolve(m.expanded));worker.once('error',reject)});
  assert.equal(expanded,10);await worker.terminate();
  const restored=JSON.parse(fs.readFileSync(checkpoint,'utf8'));
  assert.deepEqual(restored,input);
  const a=solve(restored.pool,double,restored.B,restored.seed,restored.budget,restored.witness);
  const b=solve(input.pool,double,input.B,input.seed,input.budget,input.witness);
  assert.deepEqual(a.scene,b.scene);assert.equal(a.expanded,b.expanded);assert.deepEqual(restored.oldLayout,input.oldLayout);
  results.push({double,interrupted_after_notification:expanded,status:a.status,expanded:a.expanded,candidate:a.scene,pool_unchanged:true,layout_unchanged_until_success:true});
 }
 fs.writeFileSync(__dirname+'/worker-recovery-results.json',JSON.stringify(results,null,2));
 console.log('two real worker interruptions and deterministic restarts passed; native multiplayer saves excluded');
})().catch(e=>{console.error(e);process.exitCode=1});
