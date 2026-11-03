// Throwaway deterministic candidate search. Each attempted attachment costs one expansion.
const {data,api}=require('./extract.cjs');
const boss=n=>n.type==='boss'||n.type==='boss2';
const clone=x=>JSON.parse(JSON.stringify(x));
function* candidates(s,pool,seed){
 const rng=api.seeded(seed),used=new Set(s.used);
 const order=api.shuffle(pool.filter(p=>!used.has(p.id)),rng);
 order.sort((a,b)=>Number(b.id===0)-Number(a.id===0)||Number(b.types.includes('rest'))-Number(a.types.includes('rest')));
 for(const p of order)for(const turn of api.shuffle([0,1,2,3],rng)){
  const d=p.id===0?(s.double?4:3):2;
  let pts=p.pts.map(([x,y])=>[x*d,y*d]);for(let k=0;k<turn;k++)pts=pts.map(([x,y])=>[-y,x]);
  for(const old of api.shuffle(s.nodes.filter(n=>!boss(n)),rng))for(const j of api.shuffle([0,1,2,3],rng)){
   const type=i=>p.id===0&&i===3?(s.double?'boss2':'boss'):p.id===0&&i===2&&s.double?'boss':p.types[i];
   if(type(j).startsWith('boss'))continue;
   for(const [ux,uy]of api.shuffle([[1,0],[-1,0],[0,1],[0,-1]],rng)){
    const gap=old.type==='neow'?3:2,tx=old.x+ux*gap-pts[j][0],ty=old.y+uy*gap-pts[j][1];
    const ns=pts.map(([x,y],i)=>({id:p.id+':'+i,x:x+tx,y:y+ty,piece:p.id,type:type(i)}));
    const es=p.edges.map(([a,b])=>({a:p.id+':'+a,b:p.id+':'+b,internal:true})).concat({a:old.id,b:p.id+':'+j,internal:false});
    yield {nodes:s.nodes.concat(ns),edges:s.edges.concat(es),used:s.used.concat(p.id),double:s.double};
   }
  }
 }
}
function solve(pool,double,B,seed,budget,witness=null){
 const start=performance.now();let expanded=0,best=null,exhausted=false;
 // Old JS validator is only a generator filter. Python is the acceptance authority.
 const previous=data.pool;data.pool=pool;
 function valid(s){try{api.validateScene(s,B);return true}catch{return false}}
 if(witness&&valid(witness))best=clone(witness);
 function visit(s,depth){
  if(valid(s)){best=clone(s);if(!witness||s.used.length===14)return true;}
  for(const next of candidates(s,pool,seed+depth*104729)){
   if(expanded>=budget){exhausted=true;return true}expanded++;
   try{api.geometry(next.nodes,next.edges)}catch{continue}
   if(visit(next,depth+1))return true;
  }return false;
 }
 try{visit(best||{nodes:[{id:'N',x:0,y:0,piece:-1,type:'neow'}],edges:[],used:[],double},0)}finally{data.pool=previous}
 return {status:best?'SAT':'UNKNOWN',scene:best,expanded,exhausted,reason:best?null:exhausted?'budget exhausted':'restricted enumeration exhausted; not global UNSAT',fallback:!!witness&&best?.used.length===witness.used.length,ms:performance.now()-start,rss:process.memoryUsage().rss};
}
module.exports={solve};
if(require.main===module){
 const fs=require('node:fs'),out=fs.openSync(__dirname+'/raw.jsonl','w');
 const html=fs.readFileSync(__dirname+'/prototype.html','utf8'),vm=require('node:vm');
 const code=html.slice(html.indexOf('function seeded('),html.indexOf('function renderPool('));
 const c=vm.createContext({D:data});vm.runInContext(code+';globalThis.build=assemble;',c);
 for(const [config,double,B]of [['act1',false,15],['act2',false,14],['act3',false,13],['act3-double',true,13]]){
  for(let seed=1;seed<=100;seed++){
   let witness=null,error=null;try{witness=c.build(seed,double?2:1,B)}catch(e){error=e.message}
   for(const budget of [0,100,1000,10000])for(const method of ['witness','extend','scratch']){
    const r=method==='witness'?{status:witness?'SAT':'UNKNOWN',scene:witness,expanded:0,ms:0,rss:process.memoryUsage().rss,fallback:true,error}:solve(data.pool,double,B,seed,budget,method==='extend'?witness:null);
    fs.writeSync(out,JSON.stringify({config,double,B,seed,budget,method,pool_family:'existing-demo',...r})+'\n');
   }
  }console.log(config+' complete');
 }fs.closeSync(out);
}
