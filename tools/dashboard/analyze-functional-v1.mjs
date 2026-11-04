import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';

export const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const sourceFile = 'docs/ProgressDashboard/data/functional-v1-source.json';
export function calculate(root = projectRoot, source = JSON.parse(fs.readFileSync(path.join(root, sourceFile), 'utf8').replace(/^\uFEFF/, '')), readFile = file => fs.readFileSync(file, 'utf8')) {
  const inputs = new Map();
  function read(file) {
    if (!inputs.has(file)) {
      const resolved = path.resolve(root, file);
      if (!resolved.startsWith(path.resolve(root) + path.sep)) throw new Error(`Invalid evidence path: ${file}`);
      inputs.set(file, readFile(resolved).replace(/^\uFEFF/, '').replace(/\r\n/g, '\n'));
    }
    return inputs.get(file);
  }
  function evidence(ref, required = true) {
    if (!ref?.path || !ref.contains) throw new Error('Evidence requires path and literal quotation');
    let text = read(ref.path);
    let line = 1;
    if (ref.section) {
      const headings = [...text.matchAll(/^## (.+)$/gm)];
      const matches = headings.filter(h => h[1].startsWith(ref.section));
      if (matches.length !== 1) throw new Error(`Expected unique section ${ref.path}: ${ref.section}`);
      const start = matches[0].index;
      const next = headings.find(h => h.index > start)?.index ?? text.length;
      line = text.slice(0, start).split('\n').length;
      text = text.slice(start, next);
    }
    const at = text.indexOf(ref.contains);
    if (at < 0 && required) throw new Error(`Evidence no longer matches ${ref.path}: ${ref.contains}`);
    return { ...ref, found: at >= 0, line: at < 0 ? line : line + text.slice(0, at).split('\n').length - 1 };
  }
  for (const file of ['FEATURES.md', 'STATUS.md', 'docs/agent/WORK_QUEUE.md']) read(file);
  read(sourceFile);
  const catalogRows = read('FEATURES.md').split('\n').filter(line => /^\| /.test(line))
    .map(line => line.split('|')[1].trim()).filter(label => !['Dominio','Feature'].includes(label));
  if (new Set(source.catalogCoverage.map(row => row.label)).size !== source.catalogCoverage.length ||
      catalogRows.length !== source.catalogCoverage.length ||
      catalogRows.some(label => !source.catalogCoverage.some(row => row.label === label))) {
    throw new Error('FEATURES catalog changed: reconcile V1 inventory before calculating');
  }
  const ids = new Set(), capabilities = new Set();
  const features = source.features.map(feature => {
    if (!feature.id || ids.has(feature.id)) throw new Error(`Duplicate/missing feature id: ${feature.id}`);
    ids.add(feature.id);
    if (!feature.name || !feature.module || !feature.missing) throw new Error(`Incomplete feature: ${feature.id}`);
    const key = feature.module + '|' + feature.name.normalize('NFKC').toLowerCase();
    if (capabilities.has(key)) throw new Error(`Duplicate capability: ${key}`);
    capabilities.add(key);
    if (!['v1','future','production'].includes(feature.scope)) throw new Error(`Invalid scope: ${feature.id}`);
    const implementation = evidence(feature.implementation);
    const approval = feature.approval ? evidence(feature.approval, false) : null;
    const blocker = feature.blocker ? evidence(feature.blocker, false) : null;
    if (approval?.found && feature.implemented === false) throw new Error(`Approval without implementation: ${feature.id}`);
    const state = feature.scope !== 'v1' ? feature.scope : approval?.found ? 'approved' : blocker?.found ? 'blocked' : feature.implemented === false ? 'planned' : 'validation_pending';
    return { id:feature.id, module:feature.module, name:feature.name, scope:feature.scope, state, implemented:feature.implemented !== false, missing:state === 'approved' ? '' : feature.missing, evidence:[implementation, ...(approval ? [approval] : []), ...(blocker?.found ? [blocker] : [])] };
  });
  function aggregate(list) {
    const total = list.length;
    if (!total) throw new Error('Empty V1 denominator');
    const approved = list.filter(f => f.state === 'approved').length;
    return { approved, total, pct:Math.round(approved / total * 10000) / 100, ids:list.map(f => f.id) };
  }
  const v1 = features.filter(f => f.scope === 'v1');
  const modules = Object.fromEntries([...new Set(v1.map(f => f.module))].map(id => [id, aggregate(v1.filter(f => f.module === id))]));
  if (source.catalogCoverage.some(row => !modules[row.module])) throw new Error('Catalog row without V1 owner');
  if (source.coreModules.some(id => !modules[id]) || new Set(source.coreModules).size !== source.coreModules.length) throw new Error('Invalid Core membership');
  return { schemaVersion:1, formula:'approved / unique planned V1 capabilities * 100; equal weight; rounded to 2 decimals only for display', global:aggregate(v1), core:aggregate(v1.filter(f => source.coreModules.includes(f.module))), modules, moduleTitles:source.moduleTitles, features, inputs:Object.fromEntries([...inputs].map(([file,text]) => [file,crypto.createHash('sha256').update(text).digest('hex')])) };
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const result = calculate();
  fs.writeFileSync(path.join(projectRoot, 'docs/ProgressDashboard/data/functional-v1.json'), JSON.stringify(result, null, 2) + '\n');
  console.log(`V1: ${result.global.approved}/${result.global.total} = ${result.global.pct}%; Core: ${result.core.approved}/${result.core.total} = ${result.core.pct}%`);
}
