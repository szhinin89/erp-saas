/** @param {string} content */
export function stripComments(content) {
  return content
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/\/\/.*$/gm, '');
}

/** @param {string} content */
export function extractImportSources(content) {
  const sources = [];
  const re = /(?:import|export)\s+(?:type\s+)?(?:[\w*{}\s,]+from\s+)?['"]([^'"]+)['"]/g;
  let m;
  while ((m = re.exec(content)) !== null) {
    sources.push({ source: m[1], index: m.index });
  }
  return sources;
}

/** @param {string} source */
export function countRelativeDepth(source) {
  if (!source.startsWith('.')) return 0;
  const parts = source.split('/');
  return parts.filter((p) => p === '..').length;
}

/** @param {string} content */
export function findHookUsage(content) {
  const hits = [];
  const stripped = stripComments(content);
  const hookImport = /import\s*\{([^}]+)\}\s*from\s*['"]react['"]/g;
  let m;
  while ((m = hookImport.exec(stripped)) !== null) {
    for (const name of m[1].split(',')) {
      const n = name.trim().split(/\s+as\s+/)[0];
      if (/^use[A-Z]/.test(n)) hits.push({ name: n, kind: 'import' });
    }
  }
  const callRe = /\b(use[A-Z][A-Za-z0-9_]*)\s*\(/g;
  while ((m = callRe.exec(stripped)) !== null) {
    hits.push({ name: m[1], kind: 'call' });
  }
  return hits;
}

/** @param {string} relPath modules/foo/bar.tsx */
export function moduleFromPath(relPath) {
  const m = relPath.replace(/\\/g, '/').match(/^frontend\/src\/modules\/([^/]+)/);
  return m ? m[1] : null;
}

/** @param {string} importSource */
export function moduleFromImport(importSource, fromFileRel) {
  if (importSource.includes('/modules/')) {
    const m = importSource.match(/\/modules\/([^/]+)/);
    if (m) return m[1];
  }
  if (importSource.startsWith('.')) {
    const posix = fromFileRel.replace(/\\/g, '/');
    const dir = posix.slice(0, posix.lastIndexOf('/'));
    const resolved = resolveRelative(dir, importSource);
    return moduleFromPath(resolved);
  }
  return null;
}

/** @param {string} baseDir @param {string} relImport */
function resolveRelative(baseDir, relImport) {
  const stack = baseDir.split('/').filter(Boolean);
  for (const part of relImport.split('/')) {
    if (part === '.' || part === '') continue;
    if (part === '..') stack.pop();
    else stack.push(part);
  }
  return stack.join('/');
}

/** @param {string} content @param {RegExp[]} patterns */
export function findPatternViolations(content, patterns) {
  const stripped = stripComments(content);
  /** @type {{ pattern: string, line: number, snippet: string }[]} */
  const hits = [];
  const lines = stripped.split(/\r?\n/);
  for (let i = 0; i < lines.length; i++) {
    for (const pattern of patterns) {
      if (pattern.test(lines[i])) {
        hits.push({ pattern: pattern.source, line: i + 1, snippet: lines[i].trim() });
      }
    }
  }
  return hits;
}
