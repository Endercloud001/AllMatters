const fs=require('node:fs'),{solve}=require('./search.cjs');
const {pool,witnesses}=require('./controlled.json');
const fd=fs.openSync(__dirname+'/controlled-raw.jsonl','w');
for(const [config,double,B]of [['act1',false,15],['act2',false,14],['act3',false,13],['act3-double',true,13]]){
 for(let seed=1;seed<=100;seed++)for(const budget of [0,100,1000,10000])for(const method of ['witness','extend','scratch']){
  const start=performance.now(),witness=JSON.parse(JSON.stringify(witnesses[Number(double)]));
  for(let k=0;k<seed%4;k++)for(const n of witness.nodes)[n.x,n.y]=[-n.y,n.x];
  const r=method==='witness'?{status:'SAT',scene:witness,expanded:0,ms:performance.now()-start,rss:process.memoryUsage().rss,fallback:true}:solve(pool,double,B,seed,budget,method==='extend'?witness:null);
  fs.writeSync(fd,JSON.stringify({config,double,B,seed,budget,method,pool_family:'controlled',...r})+'\n');
 }console.log(config+' complete');
}fs.closeSync(fd);
