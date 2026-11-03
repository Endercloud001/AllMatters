const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/Endercloud/.codex/skills/blcaptain-style-skill/node_modules/playwright-core');
const fs=require('node:fs'),assert=require('node:assert/strict'),slow=require('./live-slow-case.json');
let activeBrowser;
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||'C:/Users/Endercloud/AppData/Local/ms-playwright/chromium-1228/chrome-win64/chrome.exe'});
 activeBrowser=browser;
 const page=await browser.newPage({viewport:{width:1440,height:1100}}),errors=[],rows=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('http://127.0.0.1:8769');
 const run=async()=>{await page.click('#runLive');await page.waitForFunction(()=>!window.live.running,{},{timeout:120000});return page.locator('#liveState').textContent()};
 for(const family of ['controlled','existing-demo'])for(const config of ['act1','act2','act3','act3-double']){
  await page.selectOption('#family',family);await page.selectOption('#config',config);
  for(const method of ['witness','extend','scratch']){
   await page.selectOption('#method',method);await page.selectOption('#budget',method==='scratch'?'100':'1000');const text=await run();
   assert.match(text,/^(SAT|UNKNOWN)/);rows.push({family,config,method,text});
  }
 }
 await page.selectOption('#method','extend');await page.selectOption('#budget','0');await page.uncheck('#withWitness');
 await page.click('#editLive');const old=await page.evaluate(()=>JSON.stringify(window.live.layout));
 assert.match(await run(),/^UNKNOWN/);assert.equal(await page.evaluate(()=>JSON.stringify(window.live.layout)),old);
 rows.push({case:'budget zero preserves edited layout',passed:true});
 for(const fault of ['exception','invalid-output']){
  await page.selectOption('#fault',fault);await page.selectOption('#method',fault==='invalid-output'?'scratch':'extend');await page.selectOption('#budget','10000');await page.fill('#seed','27');
  assert.match(await run(),/^UNKNOWN/);assert.equal(await page.evaluate(()=>JSON.stringify(window.live.layout)),old);
  rows.push({case:fault+' preserves edited layout',passed:true});
 }
 await page.selectOption('#fault','');await page.selectOption('#method','scratch');await page.selectOption('#budget','10000');await page.fill('#seed',String(slow.seed));
 const [started]=await Promise.all([page.waitForResponse(r=>r.url().endsWith('/start')),page.click('#runLive')]);
 const id=(await started.json()).id;let active=false;
 for(let i=0;i<100;i++){const state=await (await page.request.get('http://127.0.0.1:8769/job/'+id)).json();if(state.active){active=true;break}if(state.done)break;await new Promise(r=>setTimeout(r,1));}assert.equal(active,true);
 assert.equal(await page.locator('#family').isDisabled(),true);assert.equal(await page.locator('#busy').isVisible(),true);
 const [interrupted]=await Promise.all([page.waitForResponse(r=>r.url().endsWith('/interrupt')),page.evaluate(()=>document.getElementById('resumeLive').click())]);
 const interruption=await interrupted.json();assert.equal(interruption.was_running,true);
 await page.waitForFunction(()=>!window.live.running,{},{timeout:120000});rows.push({case:'actual process interrupted then restarted via page',interruption,text:await page.locator('#liveState').textContent()});
 await page.check('#withWitness');await page.selectOption('#method','extend');await page.selectOption('#budget','1000');await run();
 const preserved=await page.evaluate(()=>JSON.stringify(window.live.layout));
 await page.route('**/start',route=>route.fulfill({status:503,contentType:'application/json',body:JSON.stringify({error:'injected transport rejection'})}));
 assert.match(await run(),/实验任务错误/);assert.equal(await page.evaluate(()=>JSON.stringify(window.live.layout)),preserved);rows.push({case:'request failure unlocks and preserves layout',passed:true});await page.unroute('**/start');
 await page.fill('#seed','1.5');assert.match(await run(),/种子须为/);assert.equal(await page.evaluate(()=>JSON.stringify(window.live.layout)),preserved);rows.push({case:'invalid seed leaves layout usable',passed:true});
 await page.fill('#seed','2');await run();
 const legacy=require('./legacy-room-inputs.json').controlled;
 const input={family:'controlled',config:'act1',method:'extend',budget:0,seed:1,withWitness:true,pool:legacy.pool,layout:legacy.witnesses[0]};
 const legacyJob=await (await page.request.post('http://127.0.0.1:8769/start',{data:input})).json();let legacyResult;
 for(let i=0;i<500;i++){const j=await (await page.request.get('http://127.0.0.1:8769/job/'+legacyJob.id)).json();if(j.done){legacyResult=j.result;break}await new Promise(r=>setTimeout(r,10));}
 assert.equal(legacyResult.status,'UNKNOWN');assert.deepEqual(legacyResult.state.pool,input.pool);assert.deepEqual(legacyResult.state.layout,input.layout);rows.push({case:'old given pool rejected without replacement',passed:true});
 const missing=await page.request.post('http://127.0.0.1:8769/start',{data:{...input,pool:null}});assert.equal(missing.status(),400);rows.push({case:'stale client missing original pool rejected',passed:true});
 await page.screenshot({path:__dirname+'/live-preview.png',fullPage:true});
 const desktop=await page.evaluate(()=>({overflow:document.documentElement.scrollWidth>innerWidth,nodes:document.querySelectorAll('.node').length}));
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:__dirname+'/live-preview-mobile.png',fullPage:true});
 const mobile=await page.evaluate(()=>({overflow:document.documentElement.scrollWidth>innerWidth}));
 assert.deepEqual(errors,[]);assert.equal(desktop.overflow,false);assert.equal(mobile.overflow,false);
 fs.writeFileSync(__dirname+'/live-browser-results.json',JSON.stringify({rows,errors,desktop,mobile},null,2));console.log(JSON.stringify({cases:rows.length,errors,desktop,mobile}));await browser.close();
})().catch(async e=>{console.error(e);if(activeBrowser)await activeBrowser.close();process.exitCode=1});
