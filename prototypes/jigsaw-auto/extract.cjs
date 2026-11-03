// Preserve the prior research demo as input; never execute its DOM shell in Node.
const fs=require('node:fs'),vm=require('node:vm');
const html=fs.readFileSync(__dirname+'/prototype.html','utf8');
const data=JSON.parse(html.match(/<script id="payload" type="application\/json">([\s\S]*?)<\/script>/)[1]);
const logic=html.slice(html.indexOf('function seeded('),html.indexOf('function renderPool('));
const end=logic.indexOf('function assemble(');
const context=vm.createContext({D:data});
vm.runInContext(logic.slice(0,end)+';globalThis.api={geometry,canAdd,seeded,shuffle,validateScene};',context);
module.exports={data,api:context.api};
if(require.main===module){fs.writeFileSync(__dirname+'/input.json',JSON.stringify({pool:data.pool,scenarios:data.scenarios},null,2));console.log(data.scenarios.map(s=>({title:s.title,used:s.used})));}
