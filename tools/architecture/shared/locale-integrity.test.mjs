import { test } from 'node:test';
import assert from 'node:assert/strict';
import { inspectLocale, compareLocaleKeys } from './locale-integrity.mjs';

test('detects duplicate keys before parsing, including escaped equivalents', () => {
  assert.deepEqual(inspectLocale('{"caja.a":"one","caja.\\u0061":"two"}').duplicates, ['caja.a']);
});
test('does not mistake quoted content for duplicate keys', () => {
  assert.deepEqual(inspectLocale(JSON.stringify({ 'caja.a': '"caja.a": "text"' })).duplicates, []);
});
for (const locale of ['en']) {
  test(`detects ES keys missing in ${locale} and orphan keys`, () => {
    assert.deepEqual(compareLocaleKeys({ 'caja.a': 'A' }, { 'caja.b': 'B' }), {
      missing: ['caja.a'], extra: ['caja.b'],
    });
  });
}
