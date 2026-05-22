#!/usr/bin/env node
/**
 * QA Regression Bot — HTTP smoke checks for auth, permissions, and tenant isolation.
 * CI-only harness; does not modify application code.
 *
 * Env: QA_API_URL, QA_FRONTEND_URL, QA_EMAIL, QA_PASSWORD
 * Out: reports/regression.json, reports/regression.md
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '..');
const REPORTS_DIR = path.join(REPO_ROOT, 'reports');

const API_BASE = (process.env.QA_API_URL ?? 'http://localhost:5003').replace(/\/$/, '');
const FRONTEND_BASE = (process.env.QA_FRONTEND_URL ?? 'http://127.0.0.1:4173').replace(/\/$/, '');
const DEMO_EMAIL = process.env.QA_EMAIL ?? 'admin@erp.com';
const DEMO_PASSWORD = process.env.QA_PASSWORD ?? 'Admin123!';

const FOREIGN_COMPANY_ID = '00000000-0000-0000-0000-000000000099';

/** @type {Array<{ id: string; name: string; category: string; critical: boolean; status: 'passed' | 'failed'; message: string }>} */
const results = [];

function record(id, name, category, critical, passed, message = '') {
  results.push({
    id,
    name,
    category,
    critical,
    status: passed ? 'passed' : 'failed',
    message,
  });
}

async function http(method, url, options = {}) {
  const res = await fetch(url, {
    method,
    headers: options.headers,
    body: options.body ? JSON.stringify(options.body) : undefined,
  });
  let text = '';
  try {
    text = await res.text();
  } catch {
    text = '';
  }
  return { status: res.status, ok: res.ok, text };
}

function unwrapData(bodyText) {
  if (!bodyText) return null;
  try {
    const body = JSON.parse(bodyText);
    return body.data ?? body.Data ?? body.responseObject ?? body.ResponseObject ?? body;
  } catch {
    return null;
  }
}

async function runTests() {
  // --- infrastructure ---
  try {
    const health = await http('GET', `${API_BASE}/health/live`);
    record('infra.api.health', 'API /health/live', 'infra', true, health.ok, `status=${health.status}`);
  } catch (err) {
    record('infra.api.health', 'API /health/live', 'infra', true, false, String(err));
  }

  try {
    const front = await http('GET', `${FRONTEND_BASE}/`);
    record('infra.frontend.root', 'Frontend root reachable', 'infra', true, front.ok, `status=${front.status}`);
  } catch (err) {
    record('infra.frontend.root', 'Frontend root reachable', 'infra', true, false, String(err));
  }

  // --- auth / security ---
  try {
    const unauth = await http('GET', `${API_BASE}/api/admin/iam/me/permissions`);
    const blocked = unauth.status === 401 || unauth.status === 403;
    record(
      'auth.permissions.unauthenticated',
      'Permissions endpoint rejects unauthenticated',
      'auth',
      true,
      blocked,
      `status=${unauth.status}`,
    );
  } catch (err) {
    record('auth.permissions.unauthenticated', 'Permissions endpoint rejects unauthenticated', 'auth', true, false, String(err));
  }

  let sessionToken = '';
  try {
    const login = await http('POST', `${API_BASE}/api/auth/login`, {
      headers: { 'Content-Type': 'application/json' },
      body: { email: DEMO_EMAIL, password: DEMO_PASSWORD },
    });
    const data = unwrapData(login.text);
    sessionToken = data?.token ?? data?.Token ?? '';
    record(
      'auth.login.demo',
      'Demo tenant login',
      'auth',
      true,
      login.ok && Boolean(sessionToken),
      login.ok ? 'token received' : `status=${login.status}`,
    );
  } catch (err) {
    record('auth.login.demo', 'Demo tenant login', 'auth', true, false, String(err));
  }

  // --- permissions ---
  if (sessionToken) {
    try {
      const perms = await http('GET', `${API_BASE}/api/admin/iam/me/permissions`, {
        headers: { Authorization: `Bearer ${sessionToken}` },
      });
      const data = unwrapData(perms.text);
      const keys = data?.permissions ?? data?.Permissions ?? [];
      const hasKeys = Array.isArray(keys) && keys.length > 0;
      record(
        'permissions.me.snapshot',
        'Authenticated /me/permissions returns keys',
        'permissions',
        true,
        perms.ok && hasKeys,
        perms.ok ? `keys=${keys.length}` : `status=${perms.status}`,
      );
    } catch (err) {
      record('permissions.me.snapshot', 'Authenticated /me/permissions returns keys', 'permissions', true, false, String(err));
    }

    try {
      const activity = await http('GET', `${API_BASE}/api/admin/activity/my`, {
        headers: { Authorization: `Bearer ${sessionToken}` },
      });
      const allowed = activity.status !== 401;
      record(
        'permissions.activity.session',
        'Activity endpoint accepts session token',
        'permissions',
        true,
        allowed,
        `status=${activity.status}`,
      );
    } catch (err) {
      record('permissions.activity.session', 'Activity endpoint accepts session token', 'permissions', true, false, String(err));
    }
  } else {
    record('permissions.me.snapshot', 'Authenticated /me/permissions returns keys', 'permissions', true, false, 'skipped: no session');
    record('permissions.activity.session', 'Activity endpoint accepts session token', 'permissions', true, false, 'skipped: no session');
  }

  // --- tenant isolation ---
  if (sessionToken) {
    try {
      const foreignSwitch = await http('POST', `${API_BASE}/api/auth/switch-company`, {
        headers: { Authorization: `Bearer ${sessionToken}`, 'Content-Type': 'application/json' },
        body: { companyId: FOREIGN_COMPANY_ID },
      });
      const blocked = !foreignSwitch.ok && foreignSwitch.status >= 400;
      record(
        'tenant.switch.foreign',
        'Switch to foreign company blocked',
        'tenant',
        true,
        blocked,
        `status=${foreignSwitch.status}`,
      );
    } catch (err) {
      record('tenant.switch.foreign', 'Switch to foreign company blocked', 'tenant', true, false, String(err));
    }

    try {
      const foreignGet = await http('GET', `${API_BASE}/api/companies/${FOREIGN_COMPANY_ID}`, {
        headers: { Authorization: `Bearer ${sessionToken}` },
      });
      const blocked = !foreignGet.ok;
      record(
        'tenant.company.foreign',
        'Foreign company read blocked',
        'tenant',
        true,
        blocked,
        `status=${foreignGet.status}`,
      );
    } catch (err) {
      record('tenant.company.foreign', 'Foreign company read blocked', 'tenant', true, false, String(err));
    }
  } else {
    record('tenant.switch.foreign', 'Switch to foreign company blocked', 'tenant', true, false, 'skipped: no session');
    record('tenant.company.foreign', 'Foreign company read blocked', 'tenant', true, false, 'skipped: no session');
  }
}

function writeReports() {
  fs.mkdirSync(REPORTS_DIR, { recursive: true });

  const passed = results.filter((r) => r.status === 'passed').length;
  const failed = results.filter((r) => r.status === 'failed').length;
  const criticalFailures = results.filter((r) => r.critical && r.status === 'failed').length;

  const payload = {
    timestamp: new Date().toISOString(),
    environment: {
      api: API_BASE,
      frontend: FRONTEND_BASE,
    },
    summary: {
      total: results.length,
      passed,
      failed,
      criticalFailures,
    },
    tests: results,
  };

  fs.writeFileSync(path.join(REPORTS_DIR, 'regression.json'), `${JSON.stringify(payload, null, 2)}\n`, 'utf8');

  const lines = [
    '# QA Regression Report',
    '',
    `**Generated:** ${payload.timestamp}`,
    '',
    `| Metric | Value |`,
    `|--------|-------|`,
    `| Total | ${payload.summary.total} |`,
    `| Passed | ${payload.summary.passed} |`,
    `| Failed | ${payload.summary.failed} |`,
    `| Critical failures | ${payload.summary.criticalFailures} |`,
    '',
    '## Results',
    '',
    '| ID | Category | Status | Message |',
    '|----|----------|--------|---------|',
  ];

  for (const t of results) {
    const icon = t.status === 'passed' ? 'PASS' : 'FAIL';
    lines.push(`| ${t.id} | ${t.category} | ${icon} | ${t.message.replace(/\|/g, '\\|')} |`);
  }

  lines.push('');
  fs.writeFileSync(path.join(REPORTS_DIR, 'regression.md'), `${lines.join('\n')}\n`, 'utf8');

  return criticalFailures;
}

async function main() {
  console.log(`QA Regression Bot — API ${API_BASE}, frontend ${FRONTEND_BASE}`);
  await runTests();
  const criticalFailures = writeReports();
  console.log(`Report written to reports/regression.json (${results.length} tests, ${criticalFailures} critical failures)`);

  if (criticalFailures > 0) {
    console.error('Critical regression failures detected.');
    process.exit(1);
  }

  const anyFailed = results.some((r) => r.status === 'failed');
  if (anyFailed) {
    console.error('Non-critical test failures detected.');
    process.exit(1);
  }

  console.log('All regression checks passed.');
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
