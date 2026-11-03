// Throwaway deterministic candidate search. Each attempted attachment costs one expansion.
const {data,api}=require('./extract.cjs');
const boss=n=>n.type==='boss'||n.type==='boss2';
const clone=x=>JSON.parse(JSON.stringify(x));
function* candidates(s,pool,seed){
 const rng=api.seeded(seed),used=new Set(s.used);
 const owner=id=>id==='N'?-1:Number(id.split(':')[0]),neighbors=new Set();let direct=false;
 for(const e of s.edges)if(!e.internal){const a=owner(e.a),b=owner(e.b);if((a===-1&&b===0)||(b===-1&&a===0))direct=true;if(a===0&&b>0)neighbors.add(b);if(b===0&&a>0)neighbors.add(a);}
 const needsBossNeighbors=direct&&neighbors.size<2;
 const order=api.shuffle(pool.filter(p=>!used.has(p.id)),rng);
 order.sort((a,b)=>Number(b.id===0)-Number(a.id===0)||Number(b.types.includes('rest'))-Number(a.types.includes('rest')));
 for(const p of order)for(const turn of api.shuffle([0,1,2,3],rng)){
  const d=p.id===0?(s.double?4:3):2;
  let pts=p.pts.map(([x,y])=>[x*d,y*d]);for(let k=0;k<turn;k++)pts=pts.map(([x,y])=>[-y,x]);
  const oldOrder=api.shuffle(s.nodes.filter(n=>!boss(n)),rng);if(needsBossNeighbors)oldOrder.sort((a,b)=>Number(b.piece===0)-Number(a.piece===0));
  for(const old of oldOrder)for(const j of api.shuffle([0,1,2,3],rng)){
   const type=i=>p.id===0&&i===3?(s.double?'boss2':'boss'):p.id===0&&i===2&&s.double?'boss':p.types[i];
   if(type(j).startsWith('boss'))continue;
   for(const [ux,uy]of api.shuffle([[1,0],[-1,0],[0,1],[0,-1]],rng)){
    const gap=old.type==='neow'?3:2,tx=old.x+ux*gap-pts[j][0],ty=old.y+uy*gap-pts[j][1];
    const ns=pts.map(([x,y],i)=>({id:p.id+':'+i,x:x+tx,y:y+ty,piece:p.id,type:type(i)}));
    const es=p.edges.map(([a,b])=>({a:p.id+':'+a,b:p.id+':'+b,internal:true})).concat({a:old.id,b:p.id+':'+j,internal:false});
    yield {next:{nodes:s.nodes.concat(ns),edges:s.edges.concat(es),used:s.used.concat(p.id),double:s.double},ns,es};
   }
  }
 }
}
function solve(pool,double,B,seed,budget,witness=null,onExpand=null,fullGeometry=false){
 const start=performance.now();let expanded=0,best=null,exhausted=false;
 if(pool.some(p=>p.types.filter(t=>t==='rest').length>1||new Set(p.types).size===1||p.edges.some(([a,b])=>p.types[a]==='elite'&&p.types[b]==='elite')))return {status:'UNKNOWN',scene:null,expanded:0,exhausted:false,reason:'invalid pre-Boss room allocation',fallback:false,ms:performance.now()-start,rss:process.memoryUsage().rss};
 // Old JS validator is only a generator filter. Python is the acceptance authority.
 const previous=data.pool;data.pool=pool;
 const required=pool.filter(p=>p.types.includes('rest')).map(p=>p.id);
 function valid(s){if(!s.used.includes(0)||required.some(id=>!s.used.includes(id))||s.nodes.length-1-(double?2:1)<B-1)return false;try{api.validateScene(s,B);return true}catch{return false}}
 if(witness&&valid(witness))best=clone(witness);
 const witnessed=!!best;
 function extendState(state,ns,es){
  const by={...state.by,...Object.fromEntries(ns.map(n=>[n.id,n]))},occ=new Set(state.occ),paths=new Set(state.paths),ports=new Set(state.ports);
  for(const n of ns)occ.add(n.x+','+n.y);
  for(const e of es){const a=by[e.a],b=by[e.b],dx=Math.sign(b.x-a.x),dy=Math.sign(b.y-a.y),distance=Math.abs(b.x-a.x)+Math.abs(b.y-a.y),ra=a.type==='neow'?1:0;
   ports.add(a.id+':'+dx+','+dy);ports.add(b.id+':'+(-dx)+','+(-dy));
   for(let i=ra+1;i<distance;i++)paths.add((a.x+dx*i)+','+(a.y+dy*i));
  }return{by,occ,paths,ports};
 }
 function visit(s,depth,state=null){
  // A geometry-checked leaf attachment to a verified witness preserves all graph,
  // Boss, rest-coverage and capacity conditions. Final acceptance is still Python.
  // Scenes are immutable during search; retain the last valid candidate by reference.
  if((witnessed&&!fullGeometry)||valid(s)){best=s;if(!witness||s.used.length===14)return true;}
  if(!state)state=api.geometry(s.nodes,s.edges);
  for(const {next,ns,es} of candidates(s,pool,seed+depth*104729)){
   if(expanded>=budget){exhausted=true;return true}expanded++;if(onExpand)onExpand(expanded);
   let childState;
   if(fullGeometry||ns.some(boss)){try{childState=api.geometry(next.nodes,next.edges)}catch{continue}}
   else{if(!api.canAdd(state,ns,es))continue;childState=extendState(state,ns,es);}
   if(visit(next,depth+1,childState))return true;
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
