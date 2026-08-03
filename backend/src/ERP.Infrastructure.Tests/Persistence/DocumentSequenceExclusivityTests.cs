using FluentAssertions;

namespace ERP.Infrastructure.Tests.Persistence;

/// <summary>
/// Gate de arquitectura — secuencias documentales.
///
/// REGLA ABSOLUTA: la única operación autorizada para asignar numeración documental
/// es <c>IDocumentSequenceRepository.CaptureNextAsync()</c>.
/// Ningún módulo puede incrementar, calcular ni modificar secuencias directamente.
///
/// Cuatro guards CI-bloqueantes:
///   SEQ-GATE-01  .CaptureAndIncrement() no es llamado fuera de la entidad de dominio.
///   SEQ-GATE-02  CurrentSeq solo se muta en DocumentSequence.cs.
///   SEQ-GATE-03  SQL raw de escritura sobre document_sequence solo en el repositorio.
///   SEQ-GATE-04  .GetForUpdateAsync() no es invocado desde capa Application.
/// </summary>
public sealed class DocumentSequenceExclusivityTests
{
    // ── Archivos autorizados por guard ────────────────────────────────────────

    // SEQ-GATE-01: nadie debería llamar .CaptureAndIncrement() — la definición
    // no tiene punto prefijo, así que el patrón "." captura solo llamadas externas.
    // Lista vacía: cero callers autorizados.
    // P0-02 §7.1bis: PurchaseReturnSequence is an independent sequence,
    // not ERP DocumentSequence. Exact-path exclusion prevents a name-collision
    // false positive without permitting other callers or mutators.
    private static readonly HashSet<string> AllowedCaptureAndIncrementCallers = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "src/ERP.Infrastructure/Persistence/Repositories/Purchases/PurchaseReturnSequenceRepository.cs",
    };

    // SEQ-GATE-02: solo la entidad puede mutar su propio CurrentSeq.
    // P0-02 §7.1bis: PurchaseReturnSequence is an independent sequence,
    // not ERP DocumentSequence. Exact-path exclusion prevents a name-collision
    // false positive without permitting other callers or mutators.
    private static readonly HashSet<string> AllowedCurrentSeqMutators = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "src/ERP.Domain/Modules/Company/Entities/DocumentSequence.cs",
        "src/ERP.Domain/Modules/Purchases/Entities/PurchaseReturnSequence.cs",
    };

    // SEQ-GATE-03: solo el repositorio puede emitir SQL de escritura sobre la tabla.
    private static readonly HashSet<string> AllowedRawSqlWriters = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "src/ERP.Infrastructure/Persistence/Repositories/DocumentSequenceRepository.cs",
    };

    // SEQ-GATE-04: .GetForUpdateAsync() solo está autorizado en la definición de
    // interfaz y en la implementación del repositorio; ningún handler de Application
    // puede invocarlo (implica transacción manual — patrón reemplazado por CaptureNextAsync).
    private static readonly HashSet<string> AllowedGetForUpdateCallers = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "src/ERP.Domain/Modules/Company/Interfaces/IDocumentSequenceRepository.cs",
        "src/ERP.Infrastructure/Persistence/Repositories/DocumentSequenceRepository.cs",
    };

    // ── SEQ-GATE-01 ───────────────────────────────────────────────────────────

    /// <summary>
    /// Ningún archivo de producción puede llamar directamente a .CaptureAndIncrement().
    /// La asignación de secuencias debe pasar exclusivamente por CaptureNextAsync().
    /// Llamar a CaptureAndIncrement() desde un handler saltea el advisory lock y
    /// la transacción dedicada, lo que produce duplicados bajo concurrencia.
    /// </summary>
    [Fact]
    public void SEQ_GATE_01_CaptureAndIncrement_is_never_called_outside_domain_entity()
    {
        var violations = ScanForPattern(
            pattern: ".CaptureAndIncrement(",
            allowedPaths: AllowedCaptureAndIncrementCallers
        );

        violations
            .Should()
            .BeEmpty(
                "CaptureAndIncrement() está reservado para la entidad de dominio. "
                    + "Todo código que necesite el siguiente número debe invocar "
                    + "IDocumentSequenceRepository.CaptureNextAsync() — el único punto de entrada "
                    + "autorizado para asignar numeración documental."
            );
    }

    // ── SEQ-GATE-01: mecanismo de detección (aislado, sin tocar el árbol real) ─

    /// <summary>
    /// P1-06 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — causa raíz del falso positivo: una mención
    /// puramente textual dentro de un comentario XML doc (idéntica a la que hoy vive en
    /// JournalEntrySequence.cs:9, referenciando DocumentSequence.CaptureAndIncrement() como
    /// comparación de diseño) no debe disparar el gate.
    /// </summary>
    [Fact]
    public void StripComments_elimina_una_mencion_textual_dentro_de_un_comentario_XML_doc()
    {
        const string source = """
            namespace ERP.Domain.Modules.Accounting.Entities;

            /// <summary>
            /// A diferencia de <c>DocumentSequence.CaptureAndIncrement()</c> (que se persiste en
            /// una transacción propia), este método hace otra cosa.
            /// </summary>
            public sealed class JournalEntrySequence
            {
                public void ReserveNextNumber() { }
            }
            """;

        var stripped = StripComments(source);

        stripped.Should().NotContain("CaptureAndIncrement(");
    }

    /// <summary>
    /// Una invocación real (fuera de comentario) sigue siendo detectada — el mecanismo no debe
    /// convertirse en una forma de ocultar violaciones reales.
    /// </summary>
    [Fact]
    public void StripComments_preserva_una_invocacion_real_de_codigo_ejecutable()
    {
        const string source = """
            namespace ERP.Application.Modules.Purchases.UseCases;

            public sealed class SomeHandler
            {
                public void Handle(DocumentSequence sequence)
                {
                    // Esto SÍ es una violación real, no un comentario que la mencione.
                    sequence.CaptureAndIncrement();
                }
            }
            """;

        var stripped = StripComments(source);

        stripped.Should().Contain(".CaptureAndIncrement(");
    }

    /// <summary>
    /// El comentario de línea previo (// Esto SÍ es...) no debe sobrevivir el stripping —
    /// confirma que solo la línea de código real, no el comentario que la precede, es lo que
    /// queda detectable.
    /// </summary>
    [Fact]
    public void StripComments_elimina_comentarios_de_linea_que_preceden_codigo_real()
    {
        const string source = """
            // sequence.CaptureAndIncrement(); — esto está comentado, no debe detectarse.
            var x = 1;
            """;

        var stripped = StripComments(source);

        stripped.Should().NotContain("CaptureAndIncrement(");
    }

    /// <summary>
    /// El contenido de literales de cadena debe preservarse intacto — SEQ-GATE-03 busca SQL de
    /// escritura que vive dentro de un string literal en el repositorio autorizado; si el
    /// stripping también vaciara los strings, ese gate dejaría de poder detectar nada.
    /// </summary>
    [Fact]
    public void StripComments_preserva_el_contenido_de_literales_de_cadena()
    {
        const string source = """
            // comentario que debe desaparecer
            var sql = "INSERT INTO document_sequence (tenant_id) VALUES (@p0)";
            """;

        var stripped = StripComments(source);

        stripped.Should().NotContain("comentario que debe desaparecer");
        stripped.Should().Contain("INSERT INTO document_sequence");
    }

    // ── SEQ-GATE-02 ───────────────────────────────────────────────────────────

    /// <summary>
    /// Solo DocumentSequence.cs puede mutar CurrentSeq.
    /// Cualquier otro archivo que asigne, incremente o decremente CurrentSeq rompe
    /// el invariante de dominio y permite generar secuenciales fuera del flujo atómico.
    /// </summary>
    [Fact]
    public void SEQ_GATE_02_CurrentSeq_is_only_mutated_inside_DocumentSequence_entity()
    {
        // Captura cualquier forma de escritura directa sobre la propiedad.
        var mutationPatterns = new[]
        {
            "CurrentSeq =", // asignación directa (incluye CurrentSeq = 1, CurrentSeq = value)
            "CurrentSeq++", // post-incremento
            "CurrentSeq +=", // incremento compuesto
            "CurrentSeq--", // post-decremento
            "CurrentSeq -=", // decremento compuesto
        };

        var violations = ScanForAnyPattern(mutationPatterns, AllowedCurrentSeqMutators);

        violations
            .Should()
            .BeEmpty(
                "CurrentSeq es el estado interno de la secuencia. "
                    + "Solo DocumentSequence puede mutarlo a través de CaptureAndIncrement(). "
                    + "Toda otra modificación introduce inconsistencias y posibles duplicados."
            );
    }

    // ── SEQ-GATE-03 ───────────────────────────────────────────────────────────

    /// <summary>
    /// Solo DocumentSequenceRepository puede emitir SQL de escritura sobre document_sequence.
    /// Un INSERT o UPDATE directo desde otro componente saltea el advisory lock
    /// y puede producir duplicados o violar la restricción UNIQUE compuesta.
    /// </summary>
    [Fact]
    public void SEQ_GATE_03_raw_sql_writes_on_document_sequence_table_only_in_repository()
    {
        // Patrones de escritura SQL (case-insensitive según el scan).
        // Las migraciones ya están excluidas del scan por la lógica de ScanForAnyPattern.
        var sqlWritePatterns = new[]
        {
            "INSERT INTO document_sequence",
            "UPDATE document_sequence",
        };

        var violations = ScanForAnyPattern(sqlWritePatterns, AllowedRawSqlWriters);

        violations
            .Should()
            .BeEmpty(
                "Solo DocumentSequenceRepository puede ejecutar SQL de escritura sobre "
                    + "document_sequence. El advisory lock y la transacción explícita que garantizan "
                    + "unicidad solo operan correctamente dentro del repositorio autorizado."
            );
    }

    // ── SEQ-GATE-04 ───────────────────────────────────────────────────────────

    /// <summary>
    /// Ningún handler de Application puede invocar .GetForUpdateAsync().
    /// Este método requiere una transacción activa externa y era el patrón anterior
    /// (pre-CaptureNextAsync) que producía race conditions. Está permitido solo en la
    /// interfaz (definición) y el repositorio (implementación).
    /// </summary>
    [Fact]
    public void SEQ_GATE_04_GetForUpdateAsync_is_not_called_from_Application_handlers()
    {
        var violations = ScanForPattern(
            pattern: ".GetForUpdateAsync(",
            allowedPaths: AllowedGetForUpdateCallers
        );

        violations
            .Should()
            .BeEmpty(
                "GetForUpdateAsync() requiere que el caller gestione su propia transacción "
                    + "y bloqueo — patrón obsoleto reemplazado por CaptureNextAsync(). "
                    + "Los handlers deben llamar únicamente a CaptureNextAsync()."
            );
    }

    // ── Infraestructura de scan ───────────────────────────────────────────────

    private static List<string> ScanForPattern(string pattern, HashSet<string> allowedPaths) =>
        ScanForAnyPattern(new[] { pattern }, allowedPaths);

    private static List<string> ScanForAnyPattern(
        IEnumerable<string> patterns,
        HashSet<string> allowedPaths
    )
    {
        var backendRoot = ResolveBackendRoot();
        var patternArray = patterns.ToArray();
        var violations = new List<string>();

        foreach (
            var file in Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories)
        )
        {
            // Excluir archivos generados / no productivos.
            if (
                file.Contains(
                    $"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;
            if (
                file.Contains(
                    $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;
            if (
                file.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;
            if (file.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(backendRoot, file).Replace('\\', '/');
            if (allowedPaths.Contains(relative))
                continue;

            var text = StripComments(File.ReadAllText(file));
            if (patternArray.Any(p => text.Contains(p, StringComparison.Ordinal)))
                violations.Add(relative);
        }

        return violations;
    }

    /// <summary>
    /// Quita comentarios de línea (<c>//</c>, incluido <c>///</c> XML doc) y de bloque
    /// (<c>/* */</c>) de código fuente C#, preservando intacto el contenido de literales de
    /// cadena (<c>"..."</c>, incluidas interpoladas <c>$"..."</c>) y verbatim (<c>@"..."</c>)
    /// y de literales char (<c>'...'</c>).
    ///
    /// Corrige el falso positivo de SEQ-GATE-01: antes, el scan de texto plano no distinguía
    /// una mención textual dentro de un comentario/XML doc (p. ej. un <c>&lt;c&gt;</c> que solo
    /// documenta el patrón de diseño) de una invocación real de código — cualquier archivo que
    /// mencionara el patrón buscado en un comentario disparaba el gate igual que una violación
    /// real. Preservar los literales de cadena es intencional: SEQ-GATE-03 busca SQL de
    /// escritura (p. ej. <c>"INSERT INTO document_sequence ..."</c>) que vive precisamente
    /// dentro de un string en el repositorio autorizado — stripearlos rompería ese gate.
    /// </summary>
    private static string StripComments(string source)
    {
        var sb = new System.Text.StringBuilder(source.Length);
        var i = 0;
        var n = source.Length;

        while (i < n)
        {
            var c = source[i];

            // Comentario de línea: // ... (incluye /// XML doc, mismo prefijo).
            if (c == '/' && i + 1 < n && source[i + 1] == '/')
            {
                while (i < n && source[i] != '\n')
                    i++;
                continue;
            }

            // Comentario de bloque: /* ... */
            if (c == '/' && i + 1 < n && source[i + 1] == '*')
            {
                i += 2;
                while (i < n && !(i + 1 < n && source[i] == '*' && source[i + 1] == '/'))
                {
                    if (source[i] == '\n')
                        sb.Append('\n');
                    i++;
                }
                i = Math.Min(i + 2, n);
                continue;
            }

            // Verbatim string: @"..." ("" es comilla escapada, sin escape con backslash).
            if (c == '@' && i + 1 < n && source[i + 1] == '"')
            {
                sb.Append(c).Append('"');
                i += 2;
                while (i < n)
                {
                    if (source[i] == '"' && i + 1 < n && source[i + 1] == '"')
                    {
                        sb.Append("\"\"");
                        i += 2;
                        continue;
                    }
                    if (source[i] == '"')
                    {
                        sb.Append('"');
                        i++;
                        break;
                    }
                    sb.Append(source[i]);
                    i++;
                }
                continue;
            }

            // String regular / interpolada: "..." / $"..."
            if (c == '"')
            {
                sb.Append('"');
                i++;
                while (i < n)
                {
                    if (source[i] == '\\' && i + 1 < n)
                    {
                        sb.Append(source[i]).Append(source[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (source[i] == '"')
                    {
                        sb.Append('"');
                        i++;
                        break;
                    }
                    sb.Append(source[i]);
                    i++;
                }
                continue;
            }

            // Char literal: '...'
            if (c == '\'')
            {
                sb.Append('\'');
                i++;
                while (i < n)
                {
                    if (source[i] == '\\' && i + 1 < n)
                    {
                        sb.Append(source[i]).Append(source[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (source[i] == '\'')
                    {
                        sb.Append('\'');
                        i++;
                        break;
                    }
                    sb.Append(source[i]);
                    i++;
                }
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static string ResolveBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (
                File.Exists(Path.Combine(dir.FullName, "ERP.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "src", "ERP.API"))
            )
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "No se encontró la raíz backend (ERP.sln / src/ERP.API)."
        );
    }
}
