// One throwaway Node process, JSON lines. Python alone accepts results.
const readline=require('node:readline'),{solve}=require('./search.cjs');
const {data,api}=require('./extract.cjs');
const fs=require('node:fs'),vm=require('node:vm');
const html=fs.readFileSync(__dirname+'/prototype.html','utf8');
const context=vm.createContext({D:data});vm.runInContext(html.slice(html.indexOf('function seeded('),html.indexOf('function renderPool('))+';globalThis.build=assemble;',context);
readline.createInterface({input:process.stdin}).on('line',line=>{
 try{const q=JSON.parse(line);if(q.fault==='exception')throw Error('injected generation exception');
 const t=performance.now();
 const r=q.method==='bootstrap'?{status:'SAT',scene:context.build(q.seed,q.double?2:1,q.B),expanded:null,ms:performance.now()-t,rss:process.memoryUsage().rss}:solve(q.pool,q.double,q.B,q.seed,q.method==='witness'?0:q.budget,q.method==='scratch'?null:q.witness);
 if(q.fault==='invalid-output'&&r.scene)r.scene.nodes[1].type='shop';
 console.log(JSON.stringify({ok:true,...r}));
 }catch(e){console.log(JSON.stringify({ok:false,error:e.message}));}
});
