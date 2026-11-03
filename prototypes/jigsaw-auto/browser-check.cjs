// Local evidence capture; override module/executable paths on other machines.
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||'C:/Users/Endercloud/.codex/skills/blcaptain-style-skill/node_modules/playwright-core');
const fs=require('node:fs');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:process.env.CHROME_PATH||'C:/Users/Endercloud/AppData/Local/ms-playwright/chromium-1228/chrome-win64/chrome.exe'});
 const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+(__dirname+'/review.html').replaceAll('\\','/'));
 let cases=0;
 for(const family of ['existing-demo','controlled'])for(const config of ['act1','act2','act3','act3-double'])for(const method of ['witness','extend','scratch'])for(const budget of ['0','1000','10000']){
  for(const [id,value]of [['family',family],['config',config],['method',method],['budget',budget]])await page.selectOption('#'+id,value);
  await page.click('#nextRecord');cases++;
 }
 await page.selectOption('#method','extend');await page.screenshot({path:__dirname+'/preview.png',fullPage:true});
 const desktop=await page.evaluate(()=>({nodes:document.querySelectorAll('.node').length,overflow:document.documentElement.scrollWidth>innerWidth,evidence:document.getElementById('evidence').textContent}));
 await page.setViewportSize({width:390,height:844});await page.screenshot({path:__dirname+'/preview-mobile.png',fullPage:true});
 const mobile=await page.evaluate(()=>({overflow:document.documentElement.scrollWidth>innerWidth}));
 const result={cases,errors,desktop,mobile};fs.writeFileSync(__dirname+'/browser-results.json',JSON.stringify(result,null,2));console.log(JSON.stringify(result));await browser.close();
 if(errors.length)process.exitCode=1;
})().catch(e=>{console.error(e);process.exitCode=1});
