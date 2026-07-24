#!/usr/bin/env node
/**
 * Extrae endpoints HTTP referenciados en frontend/src → API_USAGE_GRAPH.json
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '../..');
const SCAN_ROOT = path.join(REPO_ROOT, 'frontend/src');
const OUT_PATH = path.join(REPO_ROOT, 'docs/future-platform/API_USAGE_GRAPH.json');

const LEGACY_PATTERNS = [
  /\/api\/superadmin\//,
  /superadmin-login/,
  /\/api\/admin\/iam\/superadmin/,
];

const API_LITERAL = /(['"`])(\/api\/[^'"`\s$]+)\1/g;
const PLATFORM_API_REF = /PLATFORM_API\.(\w+)/g;
const PLATFORM_SUBSCRIBERS = /PLATFORM_SUBSCRIBERS_API/g;

function walk(dir, out = []) {
  for (const name of fs.readdirSync(dir)) {
    const p = path.join(dir, name);
    const st = fs.statSync(p);
    if (st.isDirectory()) walk(p, out);
    else if (/\.(ts|tsx)$/.test(name)) out.push(p);
  }
  return out;
}

/** @type {Map<string, { endpoint: string, files: Set<string>, legacy: boolean }>} */
const endpoints = new Map();

function addEndpoint(endpoint, file, legacy = false) {
  const key = endpoint;
  if (!endpoints.has(key)) {
    endpoints.set(key, { endpoint, files: new Set(), legacy });
  }
  const row = endpoints.get(key);
  row.files.add(file);
  if (legacy) row.legacy = true;
}

for (const file of walk(SCAN_ROOT)) {
  const rel = path.relative(REPO_ROOT, file).replace(/\\/g, '/');
  const text = fs.readFileSync(file, 'utf8');

  for (const match of text.matchAll(API_LITERAL)) {
    const ep = match[2];
    const legacy = LEGACY_PATTERNS.some((p) => p.test(ep));
    addEndpoint(ep, rel, legacy);
  }

  if (PLATFORM_SUBSCRIBERS.test(text)) {
    addEndpoint('/api/platform/subscribers', rel);
  }

  for (const match of text.matchAll(PLATFORM_API_REF)) {
    addEndpoint(`/api/platform/${match[1].replace(/([A-Z])/g, '-$1').toLowerCase()}`, rel);
  }
}

const platform = [];
const runtime = [];
const legacy = [];
const publicApi = [];

for (const row of endpoints.values()) {
  const entry = {
    endpoint: row.endpoint,
    files: [...row.files].sort(),
    legacy: row.legacy,
  };
  if (row.legacy) legacy.push(entry);
  else if (row.endpoint.startsWith('/api/platform/')) platform.push(entry);
  else if (row.endpoint.startsWith('/api/public/')) publicApi.push(entry);
  else runtime.push(entry);
}

const report = {
  generatedAt: new Date().toISOString(),
  summary: {
    total: endpoints.size,
    platform: platform.length,
    runtime: runtime.length,
    public: publicApi.length,
    legacyViolations: legacy.length,
  },
  legacyViolations: legacy,
  platformEndpoints: platform.sort((a, b) => a.endpoint.localeCompare(b.endpoint)),
  runtimeEndpoints: runtime.sort((a, b) => a.endpoint.localeCompare(b.endpoint)),
  publicEndpoints: publicApi.sort((a, b) => a.endpoint.localeCompare(b.endpoint)),
};

fs.mkdirSync(path.dirname(OUT_PATH), { recursive: true });
fs.writeFileSync(OUT_PATH, `${JSON.stringify(report, null, 2)}\n`);

if (legacy.length > 0) {
  console.error(`API usage graph: ${legacy.length} legacy endpoint reference(s) in frontend`);
  process.exit(1);
}

console.log(`Wrote ${path.relative(REPO_ROOT, OUT_PATH)} (${report.summary.total} endpoints)`);
