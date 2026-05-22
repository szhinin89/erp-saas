import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const TOOL_DIR = path.dirname(fileURLToPath(import.meta.url));
export const ARCH_DIR = path.resolve(TOOL_DIR, '..');
export const REPO_ROOT = path.resolve(ARCH_DIR, '../..');

/** @param {string} dir */
export function walkFiles(dir, { extensions = ['.ts', '.tsx', '.css'], skipDirNames = ['node_modules', 'dist', '.git'] } = {}) {
  /** @type {string[]} */
  const out = [];
  if (!fs.existsSync(dir)) return out;

  /** @param {string} current */
  function walk(current) {
    for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (skipDirNames.includes(entry.name)) continue;
        walk(path.join(current, entry.name));
        continue;
      }
      const ext = path.extname(entry.name);
      if (extensions.length && !extensions.includes(ext)) continue;
      out.push(path.join(current, entry.name));
    }
  }

  walk(dir);
  return out;
}

/** @param {string} absPath */
export function toRepoRel(absPath) {
  return path.relative(REPO_ROOT, absPath).split(path.sep).join('/');
}

/** @param {string} relPath repo-relative posix */
export function readText(relPath) {
  return fs.readFileSync(path.join(REPO_ROOT, relPath), 'utf8');
}

/** @param {string} relPath */
export function lineCount(relPath) {
  const text = readText(relPath);
  if (!text) return 0;
  return text.split(/\r?\n/).length;
}

/** @param {string} filePath absolute */
export function loadJson(filePath) {
  return JSON.parse(fs.readFileSync(filePath, 'utf8'));
}

export function loadConfig(name) {
  return loadJson(path.join(ARCH_DIR, 'config', name));
}

export function loadGrandfather() {
  const p = path.join(ARCH_DIR, 'architecture-grandfather.json');
  return loadJson(p);
}

/** @param {string} relPath */
export function isGrandfathered(grandfather, key, relPath) {
  const list = grandfather[key];
  if (!Array.isArray(list)) return false;
  const norm = relPath.replace(/\\/g, '/');
  return list.some((item) => norm === item.replace(/\\/g, '/'));
}

/** Simple glob: ** and * supported against posix path */
export function matchGlob(glob, filePath) {
  const norm = filePath.replace(/\\/g, '/');
  const escaped = glob
    .replace(/[.+^${}()|[\]\\]/g, '\\$&')
    .replace(/\*\*/g, '{{GLOBSTAR}}')
    .replace(/\*/g, '[^/]*')
    .replace(/{{GLOBSTAR}}/g, '.*');
  return new RegExp(`^${escaped}$`).test(norm);
}
