// Real local TCP framing/order experiment; NOT native INetGameService, Steam or WAN.
const net=require('node:net'),fs=require('node:fs'),path=require('node:path');
const {initial,apply}=require('./experiment.cjs');
(async()=>{
 let state=initial(),sequence=0;const sockets=[],accepted=[],received=[[],[],[]],waiters=[];
 const server=net.createServer(socket=>{socket.setEncoding('utf8');sockets.push(socket);let buffer='';socket.on('data',bytes=>{buffer+=bytes;let end;while((end=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,end);buffer=buffer.slice(end+1);const action=JSON.parse(line);state=apply(state,action);accepted.push({sequence:++sequence,action,state});for(const peer of sockets)peer.write(JSON.stringify({sequence,state})+'\n');}});});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));const port=server.address().port;
 const clients=[];
 for(let i=0;i<3;i++){const c=net.createConnection({host:'127.0.0.1',port});c.setEncoding('utf8');let buffer='';c.on('data',b=>{buffer+=b;let end;while((end=buffer.indexOf('\n'))>=0){received[i].push(JSON.parse(buffer.slice(0,end)));buffer=buffer.slice(end+1);}for(const w of [...waiters])w();});await new Promise(r=>c.once('connect',r));clients.push(c);}
 const until=n=>new Promise((resolve,reject)=>{const timer=setTimeout(()=>reject(Error('TCP receive timeout')),5000);const check=()=>{if(received.every(r=>r.length>=n)){clearTimeout(timer);waiters.splice(waiters.indexOf(check),1);resolve();}};waiters.push(check);check();});
 const send=async(i,a)=>{const bytes=Buffer.from(JSON.stringify(a)+'\n'); // Split at byte boundaries to exercise framing (actions are ASCII here).
  clients[i].write(bytes.subarray(0,5));clients[i].write(bytes.subarray(5));await until(sequence+1);
 };
 // Concurrent arrival is observed rather than assumed to match click time.
 const a=JSON.stringify(['拿','甲','A'])+'\n',b=JSON.stringify(['拿','乙','A'])+'\n',c=JSON.stringify(['拿','丙','A'])+'\n';
 clients[0].write(a);clients[1].write(b);clients[2].write(c);await until(3);
 const winner=Object.keys(state.held)[0];
 await send(0,['取消',winner]);await send(0,['票','甲','自动',{epoch:0,request:'one'}]);await send(0,['票','甲','自动',{epoch:0,request:'one'}]);await send(1,['票','乙','自动']);await send(2,['票','丙','自动']);
 const ok=Object.keys(accepted[2].state.held).length===1&&state.starts===1&&state.phase==='自动'&&received.every(r=>JSON.stringify(r.at(-1).state)===JSON.stringify(state));
 const result={transport:'OS TCP 127.0.0.1; single process, three independent sockets; not game network',players:3,winner,ok,accepted,received};fs.writeFileSync(path.join(__dirname,'loopback-results.json'),JSON.stringify(result,null,2));
 clients.forEach(c=>c.destroy());sockets.forEach(c=>c.destroy());await new Promise(r=>server.close(r));console.log(JSON.stringify({ok,operations:sequence,winner}));process.exitCode=ok?0:1;
})().catch(e=>{console.error(e);process.exit(1);});
