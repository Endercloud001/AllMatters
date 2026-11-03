const fs=require('node:fs'),assert=require('node:assert/strict'),{solve}=require('./search.cjs'),d=require('./room-fixtures.json');
const rows=[];
for(const [family,data]of Object.entries(d))for(const double of [false,true])for(const seed of [1,2,3])for(const budget of [100,1000])for(const withWitness of [false,true]){
 const w=withWitness?data.witnesses[Number(double)]:null;
 const a=solve(data.pool,double,13,seed,budget,w,null,true),b=solve(data.pool,double,13,seed,budget,w);
 assert.deepEqual(a.scene,b.scene);assert.equal(a.expanded,b.expanded);assert.equal(a.status,b.status);
 rows.push({family,double,seed,budget,withWitness,full_ms:a.ms,incremental_ms:b.ms,status:a.status,expanded:a.expanded,passed:true});
}
fs.writeFileSync(__dirname+'/geometry-equivalence.json',JSON.stringify(rows,null,2));console.log(rows.length+' full/incremental comparisons have identical scene, status and expansion count');
