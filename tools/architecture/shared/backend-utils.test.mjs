import assert from 'node:assert/strict';
import test from 'node:test';
import { findIgnoreQueryFilterHits, isTestProjectPath } from './backend-utils.mjs';

test('IgnoreQueryFilters scanner ignores C# and XML documentation comments and preserves line numbers', () => {
  const source = [
    '/// Example: .IgnoreQueryFilters() is mentioned in XML docs.',
    '// .IgnoreQueryFilters() in a line comment.',
    '/* block comment starts',
    '   .IgnoreQueryFilters() inside block comment',
    '*/',
    'var query = db.Items.IgnoreQueryFilters();',
  ].join('\n');

  assert.deepEqual(
    findIgnoreQueryFilterHits('backend/src/ERP.Infrastructure/Example.cs', source),
    [{ file: 'backend/src/ERP.Infrastructure/Example.cs', line: 6 }],
  );
});

test('canonical PlatformQueryAccessor implementation is not reported as its own caller', () => {
  const source = [
    'internal static class PlatformQueryAccessor {',
    '  public static IQueryable<T> AsPlatformQuery<T>(this DbSet<T> set)',
    '    where T : class => set.IgnoreQueryFilters();',
    '}',
  ].join('\n');

  assert.deepEqual(
    findIgnoreQueryFilterHits(
      'backend/src/ERP.Infrastructure/Persistence/PlatformQueryAccessor.cs',
      source,
    ),
    [],
  );
});

test('additional direct use in PlatformQueryAccessor remains visible', () => {
  const source = [
    'where T : class => set.IgnoreQueryFilters();',
    'var unsafeQuery = db.Items.IgnoreQueryFilters();',
  ].join('\n');

  assert.deepEqual(
    findIgnoreQueryFilterHits(
      'backend/src/ERP.Infrastructure/Persistence/PlatformQueryAccessor.cs',
      source,
    ),
    [{ file: 'backend/src/ERP.Infrastructure/Persistence/PlatformQueryAccessor.cs', line: 2 }],
  );
});

test('test project exclusion is specific to this scanner', () => {
  assert.equal(isTestProjectPath('backend/src/ERP.Infrastructure.Tests/Persistence/Foo.cs'), true);
  assert.equal(isTestProjectPath('backend/src/ERP.API.Tests/Integration/Foo.cs'), true);
  assert.equal(isTestProjectPath('backend/src/ERP.Infrastructure/Persistence/Foo.cs'), false);
  assert.deepEqual(
    findIgnoreQueryFilterHits(
      'backend/src/ERP.Infrastructure.Tests/Persistence/Foo.cs',
      'db.Items.IgnoreQueryFilters();',
    ),
    [],
  );
});
