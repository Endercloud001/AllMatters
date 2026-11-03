const fs=require('node:fs'),assert=require('node:assert/strict'),{solve}=require('./search.cjs'),data=require('./room-fixtures.json');
const rows=[];
for(const [family,d]of Object.entries(data))for(const double of [false,true])for(const seed of [1,2,27])for(const budget of [0,100,1000])for(const withWitness of [false,true]){
 const w=withWitness?d.witnesses[Number(double)]:null,a=solve(d.pool,double,13,seed,budget,w),b=solve(d.pool,double,13,seed,budget,w);
 assert.deepEqual(a.scene,b.scene);assert.equal(a.status,b.status);assert.equal(a.expanded,b.expanded);
 rows.push({family,double,seed,budget,withWitness,passed:true});
}
fs.writeFileSync(__dirname+'/reproducibility.json',JSON.stringify({algorithm:'tree-search-v2',room_rules:'pre-Boss-three-rules',cases:rows.length,passed:rows.length,fields:['scene','status','expanded'],excluded:['wall time','RSS'],rows},null,2));console.log(rows.length+' current-rule deterministic pairs passed');
