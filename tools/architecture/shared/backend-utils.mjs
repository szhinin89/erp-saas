import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, toRepoRel, walkFiles } from './fs-utils.mjs';
import { stripComments as stripCsComments } from './rule-utils.mjs';

/** @param {string} relPath repo-relative */
export function readBackendText(relPath) {
  return fs.readFileSync(path.join(REPO_ROOT, relPath), 'utf8');
}

/** @param {string} csprojRel */
export function parseCsproj(csprojRel) {
  const text = readBackendText(csprojRel);
  const projectRefs = [...text.matchAll(/ProjectReference\s+Include="([^"]+)"/g)].map((m) =>
    m[1].replace(/\\/g, '/'),
  );
  const packages = [...text.matchAll(/PackageReference\s+Include="([^"]+)"/g)].map((m) => m[1]);
  return { projectRefs, packages, text };
}

/** @param {string} dirRel e.g. backend/src/ERP.Domain */
export function scanCsUsings(dirRel, { forbiddenPatterns = [] } = {}) {
  const abs = path.join(REPO_ROOT, dirRel);
  /** @type {{ file: string, line: number, using: string, pattern: string }[]} */
  const hits = [];
  for (const file of walkFiles(abs, { extensions: ['.cs'] })) {
    const rel = toRepoRel(file);
    const lines = readBackendText(rel).split(/\r?\n/);
    for (let i = 0; i < lines.length; i++) {
      const line = lines[i].trim();
      if (!line.startsWith('using ')) continue;
      for (const pattern of forbiddenPatterns) {
        const re = pattern.startsWith('/') ? new RegExp(pattern.slice(1, -1)) : new RegExp(pattern);
        if (re.test(line)) {
          hits.push({ file: rel, line: i + 1, using: line, pattern: String(pattern) });
        }
      }
    }
  }
  return hits;
}

/** @param {string} controllersDirRel */
export function scanControllers(controllersDirRel) {
  const abs = path.join(REPO_ROOT, controllersDirRel);
  return walkFiles(abs, { extensions: ['.cs'] }).map((f) => ({
    rel: toRepoRel(f),
    lines: readBackendText(toRepoRel(f)).split(/\r?\n/).length,
    text: readBackendText(toRepoRel(f)),
  }));
}

/** @param {string} content */
export function findBackendPatternLines(content, patterns) {
  const stripped = stripCsComments(content);
  const lines = stripped.split(/\r?\n/);
  /** @type {{ line: number, snippet: string, pattern: string }[]} */
  const hits = [];
  for (let i = 0; i < lines.length; i++) {
    for (const pattern of patterns) {
      const re = typeof pattern === 'string' ? new RegExp(pattern) : pattern;
      if (re.test(lines[i])) {
        hits.push({ line: i + 1, snippet: lines[i].trim(), pattern: re.source });
      }
    }
  }
  return hits;
}

/** @param {string} dirRel */
export function scanIgnoreQueryFilters(dirRel, allowlist = []) {
  const abs = path.join(REPO_ROOT, dirRel);
  /** @type {{ file: string, line: number }[]} */
  const hits = [];
  const allow = new Set(allowlist.map((p) => p.replace(/\\/g, '/')));
  for (const file of walkFiles(abs, { extensions: ['.cs'] })) {
    const rel = toRepoRel(file);
    if (allow.has(rel)) continue;
    const lines = readBackendText(rel).split(/\r?\n/);
    for (let i = 0; i < lines.length; i++) {
      if (lines[i].includes('.IgnoreQueryFilters(')) {
        hits.push({ file: rel, line: i + 1 });
      }
    }
  }
  return hits;
}

export { REPO_ROOT, toRepoRel, walkFiles };
