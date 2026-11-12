import assert from 'node:assert/strict';
import test from 'node:test';
import { execFileSync } from 'node:child_process';
import { findIgnoreQueryFilterHits, isTestProjectPath, measureController, stripCSharpComments, REPO_ROOT } from './backend-utils.mjs';
import { evaluateController } from '../check-backend-controller-thin.mjs';

for (const eol of ['\n', '\r\n']) {
  test(`Unicode conserva índices UTF-16, comentarios y saltos ${JSON.stringify(eol)}`, () => {
    const lines = [
      'var title = "📒 á 中";',
      '/// <summary>📒 documentación</summary>',
      '/* 📒 comentario */ var value = "😀";',
      '/// <remarks>otra línea</remarks>',
      '[ProducesResponseType(typeof(int), 200)]',
      'var query = db.Items.IgnoreQueryFilters();',
    ];
    const source = lines.join(eol);
    const masked = stripCSharpComments(source);
    assert.equal(masked.length, source.length);
    assert.deepEqual(masked.match(/\r|\n/g), source.match(/\r|\n/g));
    assert.equal(masked.split(eol)[0], lines[0]);
    assert.equal(masked.split(eol)[2], ' '.repeat('/* 📒 comentario */'.length) + ' var value = "😀";');
    assert.deepEqual(measureController(source), {
      lines: 6, xmlDocLines: 2, attributeLines: 1, effectiveLines: 3,
    });
    assert.deepEqual(findIgnoreQueryFilterHits('backend/src/Example.cs', source), [
      { file: 'backend/src/Example.cs', line: 6 },
    ]);
  });
}

test('AccountingController versionado reconoce todo el XML con LF y CRLF', () => {
  const source = execFileSync('git', [
    'show', 'HEAD:backend/src/ERP.API/Controllers/Accounting/AccountingController.cs',
  ], { cwd: REPO_ROOT, encoding: 'utf8' }).replace(/\r\n/g, '\n');
  const expected = {
    lines: source.split('\n').length,
    xmlDocLines: source.split('\n').filter((line) => line.trimStart().startsWith('///')).length,
    attributeLines: measureController(source).attributeLines,
  };
  expected.effectiveLines = expected.lines - expected.xmlDocLines - expected.attributeLines;
  assert.ok(source.includes('📒'));
  assert.deepEqual(measureController(source), expected);
  assert.deepEqual(measureController(source.replaceAll('\n', '\r\n')), expected);
});

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

// ── B-controller: el umbral cuenta líneas efectivas (sin documentación XML `///`) ─────────────────

const CONTROLLER_CFG = {
  maxLinesError: 400,
  maxLinesWarning: 150,
  forbiddenPatterns: ['ErpDbContext', 'FromSqlRaw', 'ExecuteSqlRaw', '\.Database\.', 'SaveChangesAsync'],
};

/** Controller sintético: `codeLines` líneas de código real y `docLines` líneas `///`. */
function syntheticController({ codeLines, docLines = 0, extra = [] }) {
  const lines = [];
  for (let i = 0; i < docLines; i++) lines.push(`    /// Documentación XML ${i}`);
  for (let i = 0; i < codeLines; i++) lines.push(`    public int Member${i} => ${i};`);
  lines.push(...extra);
  return lines.join('\n');
}

function evaluate(text, rel = 'backend/src/ERP.API/Controllers/ExampleController.cs') {
  return evaluateController({ rel, ...measureController(text), text }, CONTROLLER_CFG);
}

test('los comentarios XML (///) no incrementan el conteo efectivo', () => {
  const withoutDocs = measureController(syntheticController({ codeLines: 100 }));
  const withDocs = measureController(syntheticController({ codeLines: 100, docLines: 500 }));

  assert.equal(withDocs.xmlDocLines, 500);
  assert.equal(withDocs.lines, withoutDocs.lines + 500);
  assert.equal(withDocs.effectiveLines, withoutDocs.effectiveLines);
});

test('el código, los // ordinarios, otros atributos y los blancos siguen contando', () => {
  const text = [
    '/// <summary>doc</summary>',
    '// comentario ordinario',
    '[HttpGet("x")]',
    '[Authorize(Policy = "p")]',
    '',
    'public int Code => 1;',
  ].join('\n');

  const m = measureController(text);
  assert.equal(m.lines, 6);
  assert.equal(m.xmlDocLines, 1);
  assert.equal(m.attributeLines, 0);
  assert.equal(m.effectiveLines, 5);
});

test('una /// dentro de un literal de cadena no se excluye', () => {
  const text = ['var s = """', '/// no es documentación', '""";', 'public int A => 1;'].join('\n');

  assert.equal(measureController(text).xmlDocLines, 0);
});

test('un controller con más de 400 líneas efectivas sigue bloqueado aunque tenga mucha documentación XML', () => {
  const { violations } = evaluate(syntheticController({ codeLines: 401, docLines: 300 }));

  assert.equal(violations.length, 1);
  assert.equal(violations[0].rule, 'B-controller');
  assert.match(violations[0].message, /401 effective lines/);
  assert.match(violations[0].message, /max 400/);
});

test('el límite no se relaja: 400 líneas efectivas pasan y 401 fallan; la documentación no cuenta', () => {
  const atLimit = syntheticController({ codeLines: 400, docLines: 600 });
  const m = measureController(atLimit);
  assert.equal(m.effectiveLines, 400);
  assert.equal(evaluate(atLimit).violations.length, 0);

  const overLimit = syntheticController({ codeLines: 401, docLines: 600 });
  assert.equal(measureController(overLimit).effectiveLines, 401);
  assert.equal(evaluate(overLimit).violations.length, 1);
});

test('un controller con 401 líneas totales pero menos de 400 efectivas deja de fallar', () => {
  const { violations, warnings } = evaluate(syntheticController({ codeLines: 212, docLines: 189 }));

  assert.deepEqual(violations, []);
  assert.equal(warnings.length, 1); // sigue sobre el umbral de aviso (150) por líneas efectivas
});

test('el umbral de aviso también usa líneas efectivas', () => {
  assert.deepEqual(evaluate(syntheticController({ codeLines: 100, docLines: 400 })).warnings, []);
  assert.equal(evaluate(syntheticController({ codeLines: 200, docLines: 0 })).warnings.length, 1);
});

test('no se altera la detección de otras infracciones: patrones prohibidos en código siguen siendo error', () => {
  const text = syntheticController({
    codeLines: 10,
    docLines: 5,
    extra: ['    var n = await db.SaveChangesAsync(ct);', '    var d = ctx.ExecuteSqlRaw("x");'],
  });

  const rules = evaluate(text).violations.map((v) => v.message);
  assert.equal(rules.length, 2);
  assert.ok(rules.some((m) => m.includes('SaveChangesAsync')));
  assert.ok(rules.some((m) => m.includes('ExecuteSqlRaw')));
});

test('un patrón prohibido solo mencionado en documentación XML no es infracción (comportamiento previo)', () => {
  const text = syntheticController({ codeLines: 10, docLines: 0, extra: ['    /// usa SaveChangesAsync y ErpDbContext'] });

  assert.deepEqual(evaluate(text).violations, []);
});

// ── B-controller: [ProducesResponseType(...)] es metadato de documentación de la API ───────────────

const measure = (...lines) => measureController(lines.join('\n'));

test('[ProducesResponseType] de una línea no cuenta', () => {
  const m = measure(
    '[ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]',
    'public int A => 1;',
  );

  assert.equal(m.attributeLines, 1);
  assert.equal(m.effectiveLines, 1);
});

test('[ProducesResponseType] de varias líneas no cuenta ninguna de ellas', () => {
  const m = measure(
    '[ProducesResponseType(',
    '    typeof(ApiResponse<PagedResult<CashFundingRequestListItemDto>>),',
    '    StatusCodes.Status200OK',
    ')]',
    'public int A => 1;',
  );

  assert.equal(m.attributeLines, 4);
  assert.equal(m.effectiveLines, 1);
});

test('argumentos genéricos anidados, typeof(...) y coma dentro de <> no rompen la detección', () => {
  const m = measure(
    '[ProducesResponseType(typeof(ApiResponse<Dictionary<string, List<int[]>>>), 200)]',
    '[ProducesResponseType<ApiResponse<Dictionary<string, int>>>(StatusCodes.Status201Created)]',
    'public int A => 1;',
  );

  assert.equal(m.attributeLines, 2);
  assert.equal(m.effectiveLines, 1);
});

test('el texto [ProducesResponseType] en comentarios no se excluye ni confunde el escáner', () => {
  const m = measure(
    '// [ProducesResponseType(typeof(int), 200)]',
    '/* [ProducesResponseType(',
    '   typeof(int), 200)] */',
    'public int A => 1;',
  );

  assert.equal(m.attributeLines, 0);
  assert.equal(m.effectiveLines, 4);
});

test('el texto [ProducesResponseType] en cadenas no se excluye (también multilínea)', () => {
  const m = measure(
    'var a = "[ProducesResponseType(typeof(int), 200)]";',
    'var b = """',
    '[ProducesResponseType(',
    '    typeof(int), 200)]',
    '""";',
    'public int A => 1;',
  );

  assert.equal(m.attributeLines, 0);
  assert.equal(m.effectiveLines, 6);
});

test('una cadena con corchetes o paréntesis dentro del atributo no corta el atributo antes de tiempo', () => {
  const m = measure(
    '[ProducesResponseType(typeof(int), 200, Description = "cierra ]) y [( raro")]',
    'public int A => 1;',
  );

  assert.equal(m.attributeLines, 1);
  assert.equal(m.effectiveLines, 1);
});

test('otros atributos siguen contando, también multilínea', () => {
  const m = measure(
    '[HttpPost("{id:guid}/close")]',
    '[Authorize(',
    '    Policy = "perm:x")]',
    '[ProducesResponseType(typeof(int), 200)]',
    'public int A => 1;',
  );

  assert.equal(m.attributeLines, 1);
  assert.equal(m.effectiveLines, 4);
});

test('un grupo que mezcla [ProducesResponseType] con otro atributo se conserva', () => {
  const m = measure(
    '[ProducesResponseType(typeof(int), 200), Authorize]',
    '[Authorize, ProducesResponseType(typeof(int), 200)]',
    'public int A => 1;',
  );

  assert.equal(m.attributeLines, 0);
  assert.equal(m.effectiveLines, 3);
});

test('código y atributo compartiendo línea: la línea se conserva', () => {
  const m = measure(
    '[ProducesResponseType(typeof(int), 200)] public int A => 1;',
    'public int B => 1; [ProducesResponseType(typeof(int), 200)]',
    '[ProducesResponseType(typeof(int), 200)] // nota',
    'public int C => 1; [ProducesResponseType(',
    '    typeof(int), 200)]',
  );

  // Las cuatro primeras mezclan atributo con código o comentario; en la quinta solo la
  // continuación `typeof(int), 200)]` es atributo puro.
  assert.equal(m.lines, 5);
  assert.equal(m.attributeLines, 1);
  assert.equal(m.effectiveLines, 4);
});

test('///, atributos de respuesta y código conviven y no se descuentan dos veces', () => {
  const m = measure(
    '/// <summary>doc</summary>',
    '/// <response code="200">ok</response>',
    '[ProducesResponseType(',
    '    /// comentario XML dentro del atributo',
    '    typeof(int), 200)]',
    'public int A => 1;',
  );

  assert.equal(m.xmlDocLines, 3);
  assert.equal(m.attributeLines, 2);
  assert.equal(m.effectiveLines, 1);
});

test('CRLF y LF dan el mismo conteo', () => {
  const lines = ['/// doc', '[ProducesResponseType(', '    typeof(int), 200)]', 'public int A => 1;'];
  const lf = measureController(lines.join('\n'));
  const crlf = measureController(lines.join('\r\n'));

  assert.deepEqual(crlf, lf);
  assert.equal(lf.effectiveLines, 1);
});

/** Controller sintético con un bloque de atributos de respuesta de varias líneas por miembro. */
function controllerWithAttributes({ members, attrLinesPerMember, extra = [] }) {
  const lines = [];
  for (let i = 0; i < members; i++) {
    lines.push('    [ProducesResponseType(');
    for (let a = 0; a < attrLinesPerMember - 2; a++) lines.push(`        typeof(ApiResponse<Dto${i}>),`);
    lines.push('        StatusCodes.Status200OK)]');
    lines.push(`    public int M${i} => ${i};`);
  }
  lines.push(...extra);
  return lines.join('\n');
}

test('los atributos de respuesta no penalizan: 400 efectivas cumplen aunque el total sea mucho mayor', () => {
  const text = controllerWithAttributes({ members: 200, attrLinesPerMember: 5 });
  assert.equal(measureController(text).effectiveLines, 200);
  assert.deepEqual(evaluate(text).violations, []);

  const atLimit = controllerWithAttributes({ members: 400, attrLinesPerMember: 3 });
  assert.equal(measureController(atLimit).effectiveLines, 400);
  assert.deepEqual(evaluate(atLimit).violations, []);
});

test('401 líneas efectivas siguen fallando aunque haya atributos de respuesta', () => {
  const text = controllerWithAttributes({ members: 401, attrLinesPerMember: 3 });

  assert.equal(measureController(text).effectiveLines, 401);
  const { violations } = evaluate(text);
  assert.equal(violations.length, 1);
  assert.match(violations[0].message, /401 effective lines/);
  assert.match(violations[0].message, /ProducesResponseType/);
});

test('un patrón prohibido sigue siendo error aunque el controller tenga atributos de respuesta', () => {
  const text = controllerWithAttributes({
    members: 3,
    attrLinesPerMember: 4,
    extra: ['    var n = await db.SaveChangesAsync(ct);'],
  });

  const { violations } = evaluate(text);
  assert.equal(violations.length, 1);
  assert.match(violations[0].message, /SaveChangesAsync/);
});
