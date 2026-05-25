#!/usr/bin/env node
/** Second pass: method/API symbols Tenant → Subscriber (excludes EF migrations). */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const SKIP = /[\\/](Migrations|node_modules|rename-tenant)([\\/]|$)/;

const REPLACEMENTS = [
  ['PlatformQueryReason.SubscriberScopedExplicit', 'PlatformQueryReason.SubscriberScopedExplicit'],
  ['PlatformQueryReason.CrossSubscriberSystem', 'PlatformQueryReason.CrossSubscriberSystem'],
  ['SubscriberScopedExplicit', 'SubscriberScopedExplicit'],
  ['CrossSubscriberSystem', 'CrossSubscriberSystem'],
  ['FromSubscriber(', 'FromSubscriber('],
  ['GetAllBySubscriberAsync', 'GetAllBySubscriberAsync'],
  ['GetProfilesBySubscriberAsync', 'GetProfilesBySubscriberAsync'],
  ['SubscriberAllowsPermissionAsync', 'SubscriberAllowsPermissionAsync'],
  ['GetBlockingReasonIfAtSubscriberCompanyUserMembershipUserCapAsync', 'GetBlockingReasonIfAtSubscriberCompanyUserMembershipUserCapAsync'],
  ['ListSubscriberUsers', 'ListSubscriberUsers'],
  ['loadSubscriberUsers', 'loadSubscriberUsers'],
  ['getSubscriberUsers', 'getSubscriberUsers'],
  ['loadSubscriberResolved', 'loadSubscriberResolved'],
  ['exportSubscribersCsv', 'exportSubscribersCsv'],
  ['filteredSubscribersForTable', 'filteredSubscribersForTable'],
  ['filteredSubscribers', 'filteredSubscribers'],
  ['Development:SeedDemoSubscriber', 'Development:SeedDemoSubscriber'],
  ['isSubscriberAdminRole', 'isSubscriberAdminRole'],
  ['isSubscriberAdmin', 'isSubscriberAdmin'],
  ['SubscriberModuleKey', 'SubscriberModuleKey'],
  ['maxActiveSubscribers', 'maxActiveSubscribers'],
  ['subscriberACompanyId', 'subscriberACompanyId'],
  ['subscriberBCompanyId', 'subscriberBCompanyId'],
  ['SEED_MANIFEST.subscriberA', 'SEED_MANIFEST.subscriberA'],
  ['SEED_MANIFEST.subscriberB', 'SEED_MANIFEST.subscriberB'],
  ['manifest.subscriberA', 'manifest.subscriberA'],
  ['manifest.subscriberB', 'manifest.subscriberB'],
  ['entityType: "Subscriber"', 'entityType: "Subscriber"'],
  ['<c>Subscriber</c>', '<c>Subscriber</c>'],
  ['/** Subscriber Admin', '/** Subscriber Admin'],
  ['Subscriber B QA', 'Subscriber B QA'],
  ['SubscriberB', 'SubscriberB'],
  ['subscriber-b-qa', 'subscriber-b-qa'],
  ['CHECK_NAME = \'backend-subscriber-rules\'', 'CHECK_NAME = \'backend-subscriber-rules\''],
  ['backend-subscriber-rules', 'backend-subscriber-rules'],
  ['B-subscriber-warn', 'B-subscriber-warn'],
  ['B-subscriber', 'B-subscriber'],
  ['rules.backend.subscriber', 'rules.backend.subscriber'],
];

function walk(dir, acc = []) {
  if (!fs.existsSync(dir) || SKIP.test(dir)) return acc;
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    if (SKIP.test(full)) continue;
    const st = fs.statSync(full);
    if (st.isDirectory()) walk(full, acc);
    else if (/\.(cs|ts|tsx|js|mjs|json|md|mdc)$/.test(name)) acc.push(full);
  }
  return acc;
}

const roots = [
  path.join(ROOT, 'backend/src'),
  path.join(ROOT, 'backend/src/ERP.Architecture.Tests'),
  path.join(ROOT, 'frontend/src'),
  path.join(ROOT, 'tools'),
  path.join(ROOT, 'qa-engine'),
  path.join(ROOT, 'scripts'),
  path.join(ROOT, 'AI-RULES'),
  path.join(ROOT, '.cursor/rules'),
  path.join(ROOT, 'docs/platform'),
];

let changed = 0;
for (const root of roots) {
  if (!fs.existsSync(root)) continue;
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
