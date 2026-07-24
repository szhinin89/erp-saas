#!/usr/bin/env node
/**
 * Extract frontend API endpoints and enforce allowlist + legacy denylist.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { REPO_ROOT, loadConfig, walkSourceFiles, matchesForbidden } from './platform-guard-lib.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const FRONTEND_SRC = path.join(REPO_ROOT, 'frontend/src');
const GRAPH_OUT = path.join(REPO_ROOT, 'docs/future-platform/API_USAGE_GRAPH.json');

const API_LITERAL = /(['"`])(\/api\/[^'"`\s$]+)\1/g;

/**
 * Strips a leading API version segment (e.g. /api/v1/auth -> /api/auth) so
 * allow/forbid prefixes stay version-agnostic across future /api/vN bumps.
 * @param {string} endpoint
 */
function stripApiVersion(endpoint) {
  return endpoint.replace(/^(\/api)\/v\d+(?=\/|$)/, '$1');
}

/**
 * @param {string} endpoint
 * @param {string[]} allowedPrefixes
 */
function endpointAllowed(endpoint, allowedPrefixes) {
  const trimmed = endpoint.endsWith('*') ? endpoint.slice(0, -1) : endpoint;
  const normalized = stripApiVersion(trimmed);
  return allowedPrefixes.some((prefix) => normalized === prefix || normalized.startsWith(`${prefix}/`));
}

/**
 * @param {string} endpoint
 * @param {string[]} forbiddenPrefixes
 */
function endpointForbidden(endpoint, forbiddenPrefixes) {
  const normalized = stripApiVersion(endpoint);
  return forbiddenPrefixes.some((prefix) => matchesForbidden(normalized, prefix));
}

export function runValidateApiEndpoints() {
  const config = loadConfig();
  const allowed = config.allowedApiPrefixes ?? [];
  const forbiddenPrefixes = config.forbiddenApiRoutePrefixes ?? ['/api/superadmin'];

  /** @type {Map<string, Set<string>>} */
  const endpoints = new Map();

  for (const file of walkSourceFiles(FRONTEND_SRC, ['.ts', '.tsx'], ['docs'])) {
    const rel = path.relative(REPO_ROOT, file).replace(/\\/g, '/');
    const text = fs.readFileSync(file, 'utf8');
    for (const match of text.matchAll(API_LITERAL)) {
      const ep = match[2];
      if (!endpoints.has(ep)) endpoints.set(ep, new Set());
      endpoints.get(ep).add(rel);
    }
  }

  /** @type {import('./platform-guard-lib.mjs').GuardViolation[]} */
  const violations = [];
  const detected = [];

  for (const [endpoint, files] of [...endpoints.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
    const fileList = [...files].sort();
    const legacy = endpointForbidden(endpoint, forbiddenPrefixes);
    const allowedOk = !legacy && endpointAllowed(endpoint, allowed);

    detected.push({
      endpoint,
      files: fileList,
      legacy,
      allowed: allowedOk,
    });

    if (legacy) {
      violations.push({
        check: 'api-endpoints',
        rule: 'forbidden-api-prefix',
        file: fileList[0],
        message: `Forbidden legacy API endpoint ${endpoint} (files: ${fileList.join(', ')})`,
      });
      continue;
    }

    if (!allowedOk) {
      violations.push({
        check: 'api-endpoints',
        rule: 'api-not-in-allowlist',
        file: fileList[0],
        message: `Endpoint ${endpoint} not in ALLOWED_API_PREFIXES (files: ${fileList.join(', ')})`,
      });
    }
  }

  const graph = {
    generatedAt: new Date().toISOString(),
    summary: {
      total: detected.length,
      allowed: detected.filter((d) => d.allowed).length,
      legacyViolations: detected.filter((d) => d.legacy).length,
      allowlistViolations: violations.filter((v) => v.rule === 'api-not-in-allowlist').length,
    },
    allowedPrefixes: allowed,
    forbiddenPrefixes,
    endpoints: detected,
  };

  fs.mkdirSync(path.dirname(GRAPH_OUT), { recursive: true });
  fs.writeFileSync(GRAPH_OUT, `${JSON.stringify(graph, null, 2)}\n`);

  return {
    name: 'api-endpoints',
    passed: violations.length === 0,
    violations,
    detail: `${detected.length} endpoints scanned`,
    endpoints: graph.summary,
  };
}

const isMain =
  process.argv[1] &&
  path.resolve(fileURLToPath(import.meta.url)) === path.resolve(process.argv[1]);

if (isMain) {
  const result = runValidateApiEndpoints();
  if (!result.passed) {
    console.error('API endpoint guard FAILED:\n');
    for (const v of result.violations) {
      console.error(`  [${v.rule}] ${v.file} — ${v.message}`);
    }
    process.exit(1);
  }
  console.log(`API endpoint guard OK (${result.detail})`);
}
