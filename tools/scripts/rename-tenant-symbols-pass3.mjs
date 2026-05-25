#!/usr/bin/env node
/** Third pass: remaining Tenant symbols (excludes EF migrations). */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const SKIP = /[\\/](Migrations|node_modules)([\\/]|$)/;

const REPLACEMENTS = [
  ['loadTenantConfig', 'loadSubscriberConfig'],
  ['tenantUsers', 'subscriberUsers'],
  ['setTenantUsers', 'setSubscriberUsers'],
  ['GetTenantsWithMovementsAsync', 'GetSubscribersWithMovementsAsync'],
  ['RecalcularTenantAsync', 'RecalcularSubscriberAsync'],
  ['"Tenants API"', '"Subscribers API"'],
  ['activeTenants', 'activeSubscribers'],
  ['suspendedTenants', 'suspendedSubscribers'],
  ['graceTenants', 'graceSubscribers'],
  ['Sin usuarios tenant', 'Sin usuarios suscriptor'],
  ['public Guid? TenantId { get; init; }', 'public Guid? SubscriberId { get; init; }'],
  ['b.TenantId', 'b.SubscriberId'],
  ['TenantId = subscriberId', 'SubscriberId = subscriberId'],
  ['TenantId      = tenantId', 'SubscriberId      = subscriberId'],
  ['/// <summary>Tenant that raised the event', '/// <summary>Subscriber that raised the event'],
  ['public Guid? TenantId { get; private set; }', 'public Guid? SubscriberId { get; private set; }'],
  ['x.TenantId', 'x.SubscriberId'],
  ['message.TenantId', 'message.SubscriberId'],
  ['Tenant={TenantId}', 'Subscriber={SubscriberId}'],
  ['// Tenant-scoped analytics', '// Subscriber-scoped analytics'],
  ['Adds CorrelationId and optional TenantId', 'Adds CorrelationId and optional SubscriberId'],
  ['Subscriber/tenant that owns', 'Subscriber that owns'],
  ['userType: \'Tenant\'', 'userType: \'Subscriber\''],
];

function walk(dir, acc = []) {
  if (!fs.existsSync(dir) || SKIP.test(dir)) return acc;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    if (SKIP.test(full)) continue;
    const st = fs.statSync(full);
    if (st.isDirectory()) walk(full, acc);
    else if (/\.(cs|ts|tsx|js|mjs)$/.test(name)) acc.push(full);
  }
  return acc;
}

const roots = [
  path.join(ROOT, 'backend/src'),
  path.join(ROOT, 'frontend/src'),
  path.join(ROOT, 'qa-engine'),
];

let changed = 0;
for (const root of roots) {
  for (const file of walk(root)) {
    let text = fs.readFileSync(file, 'utf8');
    let next = text;
    for (const [from, to] of REPLACEMENTS) next = next.split(from).join(to);
    if (next !== text) {
      fs.writeFileSync(file, next, 'utf8');
      changed++;
      console.log(path.relative(ROOT, file));
    }
  }
}
console.log(`Updated ${changed} files.`);
