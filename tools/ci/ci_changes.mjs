import { execFileSync } from 'node:child_process';
import { appendFileSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

// Stage 1: activation requires owner approval and verified GitHub protections.
export const SELECTIVE_ENABLED = false;
const SHA = /^[a-f0-9]{40}(?:[a-f0-9]{24})?$/;
const ROOT_DOCS = new Set(['STATUS.md', 'FEATURES.md', 'README.md', 'CONTEXT.md',
  'CONTRIBUTING.md', 'ARCHITECTURE_GATES.md', 'ERP_CORE_FREEZE.md']);
const DOC_DIRS = ['docs/architecture/', 'docs/decisions/', 'docs/agent/', 'docs/QA/',
  'docs/evidence/', 'docs/ci/'];
const SOURCE_EXT = /\.(?:cs|ts|tsx|js|jsx|css|json|svg|png|jpg|jpeg|webp|ico)$/;
const SENSITIVE = /auth|identity|permission|security|tenant|scope|access|session|token|password|first.?run|setup|bootstrap|middleware|migration|seeding|configuration|dbcontext|queryfilter|program\.cs|startup\.cs/i;

export function classifyFiles(files) {
  const kinds = new Set();
  for (const file of files) {
    // Git paths are case-sensitive; malformed or unfamiliar paths fail closed.
    if (typeof file !== 'string' || !file || file.startsWith('/') || file.includes('\\') ||
        file.split('/').some(part => part === '..' || part === '.' || !part)) {
      kinds.add('full');
    } else if (ROOT_DOCS.has(file) ||
        (DOC_DIRS.some(dir => file.startsWith(dir)) && file.endsWith('.md'))) {
      kinds.add('documentation');
    } else if (SENSITIVE.test(file)) {
      kinds.add('full');
    } else if (file.startsWith('frontend/src/') && SOURCE_EXT.test(file)) {
      kinds.add('frontend');
    } else if (file.startsWith('frontend/public/') && /\.(?:svg|png|jpg|jpeg|webp|ico)$/.test(file)) {
      kinds.add('frontend');
    } else if (file.startsWith('backend/src/') && file.endsWith('.cs')) {
      kinds.add('backend');
    } else {
      kinds.add('full');
    }
  }
  if (!files.length) kinds.add('full');
  return [...kinds].sort();
}

export function createPlan({ files = [], eventName, ref = '', baseRef = '', sha,
  selectiveEnabled = SELECTIVE_ENABLED, publishRequested = false, forceFull = false,
  uncertain = false, reason = '', diffBase = '', diffHead = '' }) {
  const kinds = classifyFiles(files);
  const publicationBranch = (eventName === 'push' && ['refs/heads/main', 'refs/heads/develop'].includes(ref)) ||
    (eventName === 'pull_request' && baseRef === 'main');
  // Unknown inputs may affect packaging; known frontend/docs-only inputs do not publish API.
  const apiImpact = uncertain || kinds.includes('backend') ||
    files.some(file => !classifyFiles([file]).includes('documentation') &&
      !file.startsWith('frontend/') && !file.startsWith('.github/') &&
      !file.startsWith('tools/') && !file.startsWith('docs/'));
  const publish = eventName === 'workflow_dispatch'
    ? publishRequested === true && ['refs/heads/main', 'refs/heads/develop'].includes(ref)
    : publicationBranch && apiImpact;
  const proposed = uncertain || kinds.includes('full') ? 'full'
    : kinds.every(kind => kind === 'documentation') ? 'documentation'
      : kinds.includes('backend') && kinds.includes('frontend') ? 'mixed'
        : kinds.includes('backend') ? 'backend' : 'frontend';
  const full = !selectiveEnabled || forceFull || uncertain || publish ||
    !['push', 'pull_request'].includes(eventName) || ref.startsWith('refs/heads/release/') ||
    baseRef.startsWith('release/') || proposed === 'full';
  const profile = full ? 'full' : proposed;
  return { version: 1, sha, eventName, ref, baseRef, selectiveEnabled, proposed, profile,
    reason, uncertain, diffBase, diffHead, kinds, files, publish,
    architectureProfile: profile === 'documentation' ? 'documentation' : 'full',
    backend: ['full', 'backend', 'mixed'].includes(profile),
    frontend: ['full', 'frontend', 'mixed'].includes(profile), security: true };
}

function git(cwd, args) {
  return execFileSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 32 * 1024 * 1024,
    stdio: ['ignore', 'pipe', 'pipe'] });
}

export function detectChanges({ cwd = process.cwd(), eventName, event = {}, sha }) {
  try {
    if (!SHA.test(sha ?? '') || git(cwd, ['rev-parse', 'HEAD']).trim() !== sha) {
      throw new Error('Checkout does not match event SHA');
    }
    if (!['push', 'pull_request'].includes(eventName)) {
      return { files: [], uncertain: true, reason: 'Event requires full validation' };
    }
    const base = eventName === 'push' ? event.before : event.pull_request?.base?.sha;
    const head = eventName === 'push' ? sha : event.pull_request?.head?.sha;
    for (const commit of [base, head]) {
      if (!SHA.test(commit ?? '') || /^0+$/.test(commit)) throw new Error('Missing event history');
      git(cwd, ['cat-file', '-e', `${commit}^{commit}`]);
    }
    const diffBase = eventName === 'pull_request'
      ? git(cwd, ['merge-base', base, head]).trim() : base;
    if (!SHA.test(diffBase)) throw new Error('Missing merge base');
    // --no-renames exposes both old and new paths; -z preserves spaces/newlines.
    const names = git(cwd, ['diff', '--no-ext-diff', '--no-renames', '--name-only', '-z', diffBase, head, '--']);
    return { files: names.split('\0').filter(Boolean), diffBase, diffHead: head,
      uncertain: false, reason: 'Complete Git tree comparison' };
  } catch {
    return { files: [], uncertain: true, reason: 'History unavailable or invalid; full validation required' };
  }
}

export function evaluateGate(plan, needs, sha) {
  const errors = [];
  if (!plan || plan.version !== 1 || !SHA.test(sha ?? '') || plan.sha !== sha ||
      !['full', 'documentation', 'backend', 'frontend', 'mixed'].includes(plan.profile) ||
      typeof plan.selectiveEnabled !== 'boolean' || typeof plan.publish !== 'boolean' ||
      typeof plan.uncertain !== 'boolean' || !Array.isArray(plan.files) ||
      plan.files.some(file => typeof file !== 'string')) return ['Invalid or missing CI plan'];
  const expected = createPlan({ ...plan, forceFull: plan.profile === 'full',
    publishRequested: plan.publish });
  for (const key of ['profile', 'backend', 'frontend', 'security', 'architectureProfile', 'proposed', 'publish']) {
    if (plan[key] !== expected[key]) errors.push(`Invalid plan field: ${key}`);
  }
  if (!plan.selectiveEnabled && plan.profile !== 'full') errors.push('Selective CI is disabled');
  if ((plan.publish || plan.uncertain || plan.eventName === 'workflow_dispatch') && plan.profile !== 'full') {
    errors.push('Full validation required');
  }
  if (plan.architectureProfile !== (plan.profile === 'documentation' ? 'documentation' : 'full') ||
      plan.backend !== ['full', 'backend', 'mixed'].includes(plan.profile) ||
      plan.frontend !== ['full', 'frontend', 'mixed'].includes(plan.profile)) errors.push('Inconsistent profile');
  for (const [job, required] of Object.entries({ changes: true, architecture: true,
    backend: plan.backend, frontend: plan.frontend, security: true })) {
    const result = needs?.[job]?.result;
    if (required ? result !== 'success' : result !== 'skipped') {
      errors.push(`${job}: expected ${required ? 'success' : 'skipped'}, received ${result ?? 'missing'}`);
    }
  }
  return errors;
}

function main() {
  if (process.argv.includes('--gate')) {
    const plan = JSON.parse(process.env.CI_PLAN || 'null');
    const needs = JSON.parse(process.env.CI_NEEDS || '{}');
    const errors = evaluateGate(plan, needs, process.env.GITHUB_SHA);
    if (plan?.selectiveEnabled !== SELECTIVE_ENABLED) errors.push('Activation does not match versioned configuration');
    if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY,
      `## CI gate: ${errors.length ? 'FAIL' : 'PASS'}\n\n` +
      `SHA: ${process.env.GITHUB_SHA}\n\nProfile: ${plan?.profile ?? 'missing'}\n\n` +
      Object.entries(needs).map(([job, value]) => `- ${job}: ${value.result}\n`).join(''));
    if (errors.length) throw new Error(errors.join('\n'));
    console.log(`CI gate PASS: ${plan.profile}, SHA ${plan.sha}, publish=${plan.publish}`);
    return;
  }
  const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
  const eventName = process.env.GITHUB_EVENT_NAME;
  const detected = detectChanges({ eventName, event, sha: process.env.GITHUB_SHA });
  if (detected.diffBase) git(process.cwd(), ['diff', '--check', detected.diffBase, detected.diffHead, '--']);
  const plan = createPlan({ ...detected, eventName, ref: process.env.GITHUB_REF,
    baseRef: process.env.GITHUB_BASE_REF, sha: process.env.GITHUB_SHA,
    publishRequested: event.inputs?.publish_api === 'true' });
  if (process.env.GITHUB_OUTPUT) {
    for (const [key, value] of Object.entries({ plan: JSON.stringify(plan),
      backend: plan.backend, frontend: plan.frontend, publish: plan.publish,
      architecture_profile: plan.architectureProfile })) appendFileSync(process.env.GITHUB_OUTPUT, `${key}=${value}\n`);
  }
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY,
    `## CI plan\n\nSelective enabled: ${SELECTIVE_ENABLED}\n\nProposed: ${plan.proposed}; effective: ${plan.profile}` +
    `\n\nSHA: ${plan.sha}\n\nRange: ${plan.diffBase || 'unavailable'} → ${plan.diffHead || plan.sha}` +
    `\n\nBackend: ${plan.backend}; frontend: ${plan.frontend}; security: true; publish: ${plan.publish}` +
    `\n\n${plan.reason}\n\nChanged paths: ${plan.files.length}\n`);
  console.log(JSON.stringify(plan, null, 2));
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { main(); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
