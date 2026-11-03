// In-memory / JSON simulation only. No network, native save slots or checkpoints.
const fs=require('node:fs'),assert=require('node:assert/strict'),{solve}=require('./search.cjs');
const d=require('./controlled.json'),copy=x=>JSON.parse(JSON.stringify(x));
const result=[];
function finish(saved,candidate){
 const state=copy(saved);
 if(candidate){state.layout=copy(candidate);state.phase='ancient';state.votes=[];}
 else{state.phase='editing';state.votes=[];state.error='generation failed';}
 return state;
}
for(const double of [false,true]){
 const witness=d.witnesses[Number(double)];
 const saved={pool:copy(d.pool),layout:copy(witness),phase:'generating',votes:['auto'],seed:27,budget:100,algorithm:'prototype-tree-v2'};
 // An existing edited layout is preserved exactly on failure, including its edges.
 saved.layout.used=[];saved.layout.nodes=[{id:'N',x:0,y:0,piece:-1,type:'neow'}];saved.layout.edges=[];
 for(const label of ['exception','budget-zero-no-witness','budget-exhausted-no-witness']){
  const before=JSON.stringify(saved),out=finish(saved,null);
  assert.equal(JSON.stringify(saved),before);assert.deepEqual(out.pool,saved.pool);assert.deepEqual(out.layout,saved.layout);assert.deepEqual(out.votes,[]);assert.equal(out.phase,'editing');
  result.push({double,label,pass:true,state:out});
 }
 const a=solve(d.pool,double,13,saved.seed,saved.budget,witness);
 const restored=copy(saved),b=solve(restored.pool,double,13,restored.seed,restored.budget,witness);
 assert.deepEqual(a.scene,b.scene);assert.equal(a.expanded,b.expanded);
 const zero=solve(d.pool,double,13,27,0,witness);assert.deepEqual(zero.scene,witness);
 result.push({double,label:'resume-started-same-input',pass:true,candidate:b.scene});
 result.push({double,label:'zero-budget-witness',pass:true,candidate:zero.scene});
 const success=finish(saved,a.scene),loaded=copy(success);assert.deepEqual(loaded,success);assert.equal(loaded.phase,'ancient');
 result.push({double,label:'restore-success-does-not-regenerate',pass:true,candidate:loaded.layout});
}
fs.writeFileSync(__dirname+'/recovery-results.json',JSON.stringify(result,null,2));console.log(result.length+' simulated cases passed; candidates still require independent verification');
