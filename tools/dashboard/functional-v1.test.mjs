import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { calculate, projectRoot } from './analyze-functional-v1.mjs';
const source=()=>JSON.parse(fs.readFileSync(path.join(projectRoot,'docs/ProgressDashboard/data/functional-v1-source.json'),'utf8'));
test('unique global denominator, exact module union and Core membership',()=>{
 const d=calculate();
 assert.deepEqual([d.global.approved,d.global.total,d.global.pct],[65,70,92.86]);
 assert.deepEqual([d.core.approved,d.core.total,d.core.pct],[40,43,93.02]);
 assert.equal(new Set(d.global.ids).size,d.global.total);
 assert.equal(Object.values(d.modules).reduce((n,m)=>n+m.total,0),d.global.total);
 assert.equal(Object.values(d.modules).reduce((n,m)=>n+m.approved,0),d.global.approved);
 assert.equal(d.modules.Caja.approved,3); // R3 scope, no invented global closure.
 assert.equal(d.modules.Accounting.approved,8); // Additional focal evidence closes the scopes not covered by R4.
 assert.equal(d.features.find(f=>f.id==='ElectronicDocuments-04').state,'blocked');
 assert.equal(d.features.find(f=>f.id==='InitialLoad-05').state,'planned');
 for(const f of d.features.filter(f=>f.state==='approved'))assert.ok(f.evidence.some(e=>e.found));
});
test('future/production never affect denominator or approved totals',()=>{
 const s=source();s.features=s.features.filter(f=>f.scope==='v1');
 assert.deepEqual(calculate(projectRoot,s).global,calculate().global);
});
test('new explicit STATUS acceptance recalculates; implementation alone does not',()=>{
 const s=source();
 s.features.find(f=>f.id==='Dashboard-01').approval.contains='[TEST-APPROVED:Dashboard-01]';
 const before=calculate(projectRoot,s);
 const after=calculate(projectRoot,s,file=>fs.readFileSync(file,'utf8')+(path.basename(file)==='STATUS.md'?'\nAprobación sintética del fixture [TEST-APPROVED:Dashboard-01]\n':''));
 assert.equal(before.modules.Dashboard.approved,0);
 assert.equal(after.modules.Dashboard.approved,1);
 assert.equal(after.global.approved,before.global.approved+1);
 assert.equal(after.global.total,70);
 assert.notEqual(before.inputs['STATUS.md'],after.inputs['STATUS.md']);
});
test('withdrawn approval stops contributing without removing denominator',()=>{
 const s=source();s.features.find(f=>f.id==='Purchases-01').approval.contains='[V1-APPROVED:withdrawn]';
 const d=calculate(projectRoot,s);assert.equal(d.global.approved,calculate().global.approved-1);assert.equal(d.global.total,70);
 assert.equal(d.features.find(f=>f.id==='Purchases-01').state,'validation_pending');
});
test('duplicate ids, duplicate capabilities, missing evidence and ambiguous sections fail closed',()=>{
 for(const mutate of [
  s=>s.features.push({...s.features[0]}),
  s=>s.features.push({...s.features[0],id:'other'}),
  s=>s.features[0].implementation.contains='quotation absent from official source',
  s=>s.features[0].implementation.path='../outside',
  s=>s.features[0].implementation={path:'STATUS.md',section:'SRI-TOGGLE-',contains:'SRI'},
  s=>s.coreModules.push('Sales'),
  s=>s.features.find(f=>f.id==='Purchases-01').implemented=false
 ]){const s=source();mutate(s);assert.throws(()=>calculate(projectRoot,s));}
});
test('unreconciled catalog additions fail instead of silently shrinking scope',()=>{
 assert.throws(()=>calculate(projectRoot,source(),file=>fs.readFileSync(file,'utf8')+(path.basename(file)==='FEATURES.md'?'\n| New capability | /new | Scope to reconcile |\n':'')),/catalog changed/);
});
