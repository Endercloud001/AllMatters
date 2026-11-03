const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/Endercloud/.codex/skills/blcaptain-style-skill/node_modules/playwright-core');
const fs=require('node:fs'),assert=require('node:assert/strict');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||'C:/Users/Endercloud/AppData/Local/ms-playwright/chromium-1228/chrome-win64/chrome.exe'});
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
 await page.selectOption('#fault','');await page.selectOption('#method','scratch');await page.selectOption('#budget','10000');await page.fill('#seed','12');
 await page.click('#runLive');await page.waitForFunction(()=>window.live.running);
 assert.equal(await page.locator('#family').isDisabled(),true);assert.equal(await page.locator('#busy').isVisible(),true);
 await page.waitForTimeout(200);
 await page.screenshot({path:__dirname+'/live-waiting.png',fullPage:true});
 const [interrupted]=await Promise.all([page.waitForResponse(r=>r.url().endsWith('/interrupt')),page.click('#resumeLive')]);
 const interruption=await interrupted.json();assert.equal(interruption.was_running,true);
 await page.waitForFunction(()=>!window.live.running,{},{timeout:120000});rows.push({case:'actual process interrupted then restarted via page',interruption,text:await page.locator('#liveState').textContent()});
 await page.check('#withWitness');await page.selectOption('#method','extend');await page.selectOption('#budget','1000');await run();
 const preserved=await page.evaluate(()=>JSON.stringify(window.live.layout));
 await page.route('**/start',route=>route.fulfill({status:503,contentType:'application/json',body:JSON.stringify({error:'injected transport rejection'})}));
 assert.match(await run(),/实验任务错误/);assert.equal(await page.evaluate(()=>JSON.stringify(window.live.layout)),preserved);rows.push({case:'request failure unlocks and preserves layout',passed:true});await page.unroute('**/start');
 await page.fill('#seed','1.5');assert.match(await run(),/种子须为/);assert.equal(await page.evaluate(()=>JSON.stringify(window.live.layout)),preserved);rows.push({case:'invalid seed leaves layout usable',passed:true});
 await page.fill('#seed','2');await run();
 await page.screenshot({path:__dirname+'/live-preview.png',fullPage:true});
 const desktop=await page.evaluate(()=>({overflow:document.documentElement.scrollWidth>innerWidth,nodes:document.querySelectorAll('.node').length}));
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:__dirname+'/live-preview-mobile.png',fullPage:true});
 const mobile=await page.evaluate(()=>({overflow:document.documentElement.scrollWidth>innerWidth}));
 assert.deepEqual(errors,[]);assert.equal(desktop.overflow,false);assert.equal(mobile.overflow,false);
 fs.writeFileSync(__dirname+'/live-browser-results.json',JSON.stringify({rows,errors,desktop,mobile},null,2));console.log(JSON.stringify({cases:rows.length,errors,desktop,mobile}));await browser.close();
})().catch(e=>{console.error(e);process.exitCode=1});
