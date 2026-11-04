import fs from 'node:fs';
import path from 'node:path';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';
import { createRequire } from 'node:module';
import { pathToFileURL } from 'node:url';
import { calculate, projectRoot } from './analyze-functional-v1.mjs';
const require=createRequire(path.join(projectRoot,'frontend/package.json'));
const { chromium }=require('@playwright/test');
const htmlPath=path.join(projectRoot,'docs/ProgressDashboard/index.html');
const html=fs.readFileSync(htmlPath,'utf8');
const manifest=JSON.parse(fs.readFileSync(path.join(projectRoot,'docs/ProgressDashboard/templates/dashboard-curated.json'),'utf8').replace(/^\uFEFF/,''));
const part=html.match(/<div id='view-architecture'>[\s\S]*?(?=<div class='groups-view')/)[0];
const shape=(part.match(/<[^>]+>/g)||[]).map(tag=>tag.replace(/ data-v1-(?:score|module)='[^']*'/g,'')).join('\n');
const css=[...html.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)].map(m=>m[1].replace(/\r\n/g,'\n')).join('\n');
const digest=t=>crypto.createHash('sha256').update(t).digest('hex');
assert.equal(digest(shape),manifest.protectedDiagram.structureSha256,'Diagram structure, colors, connections and handlers');
assert.equal(digest(css),manifest.protectedDiagram.cssSha256,'Protected layout and styles');
const expected=calculate();
assert.deepEqual(JSON.parse(fs.readFileSync(path.join(projectRoot,'docs/ProgressDashboard/data/functional-v1.json'),'utf8')),expected,'Data must match current sources');
const browser=await chromium.launch({headless:true});
try{
 const page=await browser.newPage({viewport:{width:1500,height:1000}});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto(pathToFileURL(htmlPath).href);
 assert.equal(await page.locator('#diagramnode-web .pct-badge').textContent(),expected.global.pct+'%');
 assert.equal(await page.locator('#diagramnode-core .pct-badge').textContent(),expected.core.pct+'%');
 assert.equal(await page.locator('#view-architecture .diagram-node').count(),11);
 await page.evaluate(()=>{showGroup('architecture');selectLayer('web');});
 const boxes=await page.locator('#view-architecture .diagram-node').evaluateAll(nodes=>nodes.map(n=>{const r=n.getBoundingClientRect(), origin=n.closest('.diagram-wrap').getBoundingClientRect();return {id:n.id,x:r.x-origin.x,y:r.y-origin.y,width:r.width,height:r.height};}));
 for(const [id,score] of Object.entries(expected.modules)){
  await page.evaluate(id=>{location.hash='#module='+id;},id);
  await page.waitForFunction(id=>breadcrumbStack.at(-1)?.id===id,id);
  const panel=page.locator('#levelContent');
  assert.equal(await panel.locator('.page-score-value').textContent(),score.pct+'%',id);
  assert.match(await panel.innerText(),new RegExp(score.approved+'/'+score.total+' aprobadas'),id);
  for(const feature of expected.features.filter(f=>f.scope==='v1'&&f.module===id)){
   assert.ok((await panel.innerText()).includes(feature.id),feature.id);
   if(feature.missing)assert.ok((await panel.innerText()).includes(feature.missing),feature.id+' missing detail');
  }
  assert.ok(await panel.locator('a[href^="../../"]').count(),id+' evidence links');
 }
 await page.evaluate(()=>selectLayer('core'));
 assert.equal(await page.locator('#levelContent .page-score-value').textContent(),expected.core.pct+'%');
 await page.evaluate(()=>popToIndex(0));
 await page.evaluate(()=>resetBreadcrumb());
 const after=await page.locator('#view-architecture .diagram-node').evaluateAll(nodes=>nodes.map(n=>{const r=n.getBoundingClientRect(), origin=n.closest('.diagram-wrap').getBoundingClientRect();return {id:n.id,x:r.x-origin.x,y:r.y-origin.y,width:r.width,height:r.height};}));
 assert.deepEqual(after,boxes,'Selecting modules preserves diagram positions');
 assert.deepEqual(errors,[],'No JavaScript errors');
 console.log('PASS: 21 module routes, ratios, evidence/missing detail, Web/Core, 11 nodes, protected CSS/structure, fixed diagram positions and breadcrumbs.');
}finally{await browser.close();}
