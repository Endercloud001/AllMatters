const fs=require('node:fs'),vm=require('node:vm');
const html=fs.readFileSync(__dirname+'/prototype.html','utf8'),original=JSON.parse(html.match(/<script id="payload" type="application\/json">([\s\S]*?)<\/script>/)[1]);
const prepared=require('./prepared-pools.json'),result={};
for(const [family,pool]of Object.entries(prepared)){
 const D=JSON.parse(JSON.stringify(original));D.pool=pool;
 for(const scene of D.scenarios)for(const n of scene.nodes){if(n.piece>=0&&!['boss','boss2'].includes(n.type))n.type=pool[n.piece].types[+n.id.split(':')[1]];}
 const c=vm.createContext({D});vm.runInContext(html.slice(html.indexOf('function seeded('),html.indexOf('function renderPool('))+';globalThis.build=assemble;',c);
 const witnesses=[];
 for(const double of [false,true]){
  const s=c.build(1,double?2:1,13);
  if(family==='controlled'){
   const used=new Set([0,1,2,3,4,5]);s.used=s.used.filter(i=>used.has(i));s.nodes=s.nodes.filter(n=>n.piece<0||used.has(n.piece));
   const ids=new Set(s.nodes.map(n=>n.id));s.edges=s.edges.filter(e=>ids.has(e.a)&&ids.has(e.b));
  }
  witnesses.push(s);
 }
 result[family]={pool,witnesses};
}
fs.writeFileSync(__dirname+'/room-fixtures.json',JSON.stringify(result,null,2));
console.log('new input pools and explicit six-piece/other witnesses prepared; independent verification required');
