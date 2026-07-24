// check-i18n-keys.cjs — detects t() calls whose keys are absent from es.json
const fs   = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const esDict = JSON.parse(fs.readFileSync('frontend/src/i18n/locales/es.json', 'utf8'));
const esKeys = new Set(Object.keys(esDict));

// Grep all t('...') calls from source files
const raw = execSync(
  'grep -rh "t(\'[^\']*\')" frontend/src --include=*.tsx --include=*.ts',
  { encoding: 'utf8', maxBuffer: 10 * 1024 * 1024 }
);

// Extract the key from every t('key') occurrence
const keyRegex = /t\('([^']+)'\)/g;
const allKeys = new Set();
let m;
while ((m = keyRegex.exec(raw)) !== null) {
  const k = m[1].trim();
  if (k.length > 3 && !k.includes('{') && !k.includes(' ')) allKeys.add(k);
}

const missing = [...allKeys].filter(k => !esKeys.has(k));

console.log('Unique t() keys scanned : ' + allKeys.size);
console.log('Missing from es.json    : ' + missing.length);
console.log('');

// Group by module prefix
const groups = {};
missing.sort().forEach(function(k) {
  const parts = k.split('.');
  const prefix = parts.slice(0, 2).join('.');
  if (!groups[prefix]) groups[prefix] = [];
  groups[prefix].push(k);
});

Object.keys(groups).sort().forEach(function(g) {
  console.log('[' + g + ']');
  groups[g].forEach(function(k) { console.log('  ' + k); });
});
