// Local file QA; no social/login browser session or game process.
const fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
(async()=>{
 const {chromium}=require(process.env.PROTOTYPE_PLAYWRIGHT||'C:/Users/Endercloud/.codex/skills/blcaptain-style-skill/node_modules/playwright-core');
 const browser=await chromium.launch({executablePath:process.env.PROTOTYPE_CHROME||'C:/Program Files/Google/Chrome/Application/chrome.exe',headless:true});
 const page=await browser.newPage({viewport:{width:1500,height:1000}});const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(pathToFileURL(path.join(__dirname,'prototype.html')).href);
 const records=[];
 const count=await page.locator('#tabs button').count();
 for(let i=0;i<count;i++){
  await page.locator('#tabs button').nth(i).click();
  const steps=await page.evaluate(i=>scenarios[i][2].length,i);
  for(let j=0;j<steps;j++)await page.getByRole('button',{name:'执行下一步',exact:true}).click();
  records.push(await page.evaluate(i=>({name:scenarios[i][0],ok:scenarios[i][3](state),phase:state.phase,steps:cursor}),i));
 }
 await page.locator('#tabs button').first().click();await page.getByRole('button',{name:'拿 甲 A',exact:true}).click();
 const held=await page.evaluate(()=>state.held.甲.block);await page.getByRole('button',{name:'模拟恢复（非原生保存）',exact:true}).click();
 const cleared=await page.evaluate(()=>Object.keys(state.held).length===0);
 await page.screenshot({path:path.join(__dirname,'browser-desktop.png'),fullPage:true});
 await page.setViewportSize({width:800,height:900});
 const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 await page.screenshot({path:path.join(__dirname,'browser-compact.png'),fullPage:true});
 const result={browser:await browser.version(),records,errors,held,cleared,compactOverflow:overflow};
 fs.writeFileSync(path.join(__dirname,'browser-results.json'),JSON.stringify(result,null,2));
 await browser.close();console.log(JSON.stringify(result));process.exitCode=errors.length||records.some(r=>!r.ok)||!cleared||overflow?1:0;
})().catch(e=>{console.error(e);process.exitCode=1;});
