const fs=require('node:fs'),{solve}=require('./search.cjs'),d=require('./room-fixtures.json')['existing-demo'];let slowest=null;
for(let seed=1;seed<=100;seed++){
 const r=solve(d.pool,true,13,seed,10000);
 if(!slowest||r.ms>slowest.ms)slowest={seed,family:'existing-demo',config:'act3-double',method:'scratch',budget:10000,status:r.status,ms:r.ms};
 if(r.status==='UNKNOWN'){const out={seed,family:'existing-demo',config:'act3-double',method:'scratch',budget:10000,status:r.status,ms:r.ms};fs.writeFileSync(__dirname+'/live-slow-case.json',JSON.stringify(out,null,2));console.log(JSON.stringify(out));break}
 if(seed===100){fs.writeFileSync(__dirname+'/live-slow-case.json',JSON.stringify(slowest,null,2));console.log(JSON.stringify(slowest));}
}
