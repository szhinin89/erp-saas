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
    m[1].replaceAll('\\', '/'),
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

function maskRange(output, source, start, end) {
  for (let index = start; index < end; index++) {
    if (source[index] !== '\n' && source[index] !== '\r') output[index] = ' ';
  }
}

function quoteRunLength(source, start) {
  let count = 0;
  while (source[start + count] === '"') count++;
  return count;
}

function skipRawString(source, start, quoteCount) {
  for (let index = start + quoteCount; index < source.length;) {
    if (source[index] !== '"') {
      index++;
      continue;
    }
    const closingCount = quoteRunLength(source, index);
    if (closingCount >= quoteCount) return index + quoteCount;
    index += closingCount;
  }
  return source.length;
}

function skipVerbatimString(source, start) {
  for (let index = start + 1; index < source.length; index++) {
    if (source[index] !== '"') continue;
    if (source[index + 1] === '"') index++;
    else return index + 1;
  }
  return source.length;
}

function skipEscapedLiteral(source, start, quote) {
  for (let index = start + 1; index < source.length; index++) {
    if (source[index] === '\\') index++;
    else if (source[index] === quote) return index + 1;
  }
  return source.length;
}

function skipQuotedLiteral(source, start) {
  const quote = source[start];
  if (quote !== '"') return skipEscapedLiteral(source, start, quote);

  const quoteCount = quoteRunLength(source, start);
  if (quoteCount >= 3) return skipRawString(source, start, quoteCount);
  if (source[start - 1] === '@') return skipVerbatimString(source, start);
  return skipEscapedLiteral(source, start, quote);
}

function maskComment(source, output, start) {
  const lineComment = source[start + 1] === '/';
  let end = start + 2;
  if (lineComment) {
    while (end < source.length && source[end] !== '\n' && source[end] !== '\r') end++;
  } else {
    while (end < source.length - 1 && !(source[end] === '*' && source[end + 1] === '/')) end++;
    end = end < source.length - 1 ? end + 2 : source.length;
  }
  maskRange(output, source, start, end);
  return end;
}

/** Remove C# line/block/XML comments while preserving line numbers and string contents. */
export function stripCSharpComments(content) {
  const output = [...content];
  for (let index = 0; index < content.length;) {
    if (content[index] === '"' || content[index] === "'") {
      index = skipQuotedLiteral(content, index);
      continue;
    }
    if (content[index] === '/' && (content[index + 1] === '/' || content[index + 1] === '*')) {
      index = maskComment(content, output, index);
      continue;
    }
    index++;
  }
  return output.join('');
}

export function isTestProjectPath(relPath) {
  return relPath
    .replaceAll('\\', '/')
    .split('/')
    .some((segment) => /\.Tests(?:\.|$)/i.test(segment));
}

export function findIgnoreQueryFilterHits(relPath, content) {
  if (isTestProjectPath(relPath)) return [];

  const normalizedPath = relPath.replaceAll('\\', '/');
  const lines = stripCSharpComments(content).split(/\r?\n/);
  const hits = [];
  for (let index = 0; index < lines.length; index++) {
    const line = lines[index];
    if (!line.includes('.IgnoreQueryFilters(')) continue;

    const canonicalAccessorLine = normalizedPath === 'backend/src/ERP.Infrastructure/Persistence/PlatformQueryAccessor.cs'
      && /^\s*where T : class => set\.IgnoreQueryFilters\(\);\s*$/.test(line);
    if (!canonicalAccessorLine) hits.push({ file: normalizedPath, line: index + 1 });
  }
  return hits;
}

/** @param {string} dirRel */
export function scanIgnoreQueryFilters(dirRel, allowlist = []) {
  const abs = path.join(REPO_ROOT, dirRel);
  /** @type {{ file: string, line: number }[]} */
  const hits = [];
  const allow = new Set(allowlist.map((p) => p.replaceAll('\\', '/')));
  for (const file of walkFiles(abs, { extensions: ['.cs'] })) {
    const rel = toRepoRel(file);
    if (allow.has(rel) || isTestProjectPath(rel)) continue;
    hits.push(...findIgnoreQueryFilterHits(rel, readBackendText(rel)));
  }
  return hits;
}

export { REPO_ROOT, toRepoRel, walkFiles };
