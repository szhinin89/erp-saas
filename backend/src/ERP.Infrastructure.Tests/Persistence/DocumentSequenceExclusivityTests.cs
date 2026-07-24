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
    private static readonly HashSet<string> AllowedCaptureAndIncrementCallers =
        new(StringComparer.OrdinalIgnoreCase);

    // SEQ-GATE-02: solo la entidad puede mutar su propio CurrentSeq.
    private static readonly HashSet<string> AllowedCurrentSeqMutators =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "src/ERP.Domain/Modules/Company/Entities/DocumentSequence.cs",
        };

    // SEQ-GATE-03: solo el repositorio puede emitir SQL de escritura sobre la tabla.
    private static readonly HashSet<string> AllowedRawSqlWriters =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "src/ERP.Infrastructure/Persistence/Repositories/DocumentSequenceRepository.cs",
        };

    // SEQ-GATE-04: .GetForUpdateAsync() solo está autorizado en la definición de
    // interfaz y en la implementación del repositorio; ningún handler de Application
    // puede invocarlo (implica transacción manual — patrón reemplazado por CaptureNextAsync).
    private static readonly HashSet<string> AllowedGetForUpdateCallers =
        new(StringComparer.OrdinalIgnoreCase)
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
            allowedPaths: AllowedCaptureAndIncrementCallers);

        violations.Should().BeEmpty(
            "CaptureAndIncrement() está reservado para la entidad de dominio. " +
            "Todo código que necesite el siguiente número debe invocar " +
            "IDocumentSequenceRepository.CaptureNextAsync() — el único punto de entrada " +
            "autorizado para asignar numeración documental.");
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
            "CurrentSeq =",   // asignación directa (incluye CurrentSeq = 1, CurrentSeq = value)
            "CurrentSeq++",   // post-incremento
            "CurrentSeq +=",  // incremento compuesto
            "CurrentSeq--",   // post-decremento
            "CurrentSeq -=",  // decremento compuesto
        };

        var violations = ScanForAnyPattern(mutationPatterns, AllowedCurrentSeqMutators);

        violations.Should().BeEmpty(
            "CurrentSeq es el estado interno de la secuencia. " +
            "Solo DocumentSequence puede mutarlo a través de CaptureAndIncrement(). " +
            "Toda otra modificación introduce inconsistencias y posibles duplicados.");
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

        violations.Should().BeEmpty(
            "Solo DocumentSequenceRepository puede ejecutar SQL de escritura sobre " +
            "document_sequence. El advisory lock y la transacción explícita que garantizan " +
            "unicidad solo operan correctamente dentro del repositorio autorizado.");
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
            allowedPaths: AllowedGetForUpdateCallers);

        violations.Should().BeEmpty(
            "GetForUpdateAsync() requiere que el caller gestione su propia transacción " +
            "y bloqueo — patrón obsoleto reemplazado por CaptureNextAsync(). " +
            "Los handlers deben llamar únicamente a CaptureNextAsync().");
    }

    // ── Infraestructura de scan ───────────────────────────────────────────────

    private static List<string> ScanForPattern(
        string pattern,
        HashSet<string> allowedPaths)
        => ScanForAnyPattern(new[] { pattern }, allowedPaths);

    private static List<string> ScanForAnyPattern(
        IEnumerable<string> patterns,
        HashSet<string> allowedPaths)
    {
        var backendRoot = ResolveBackendRoot();
        var patternArray = patterns.ToArray();
        var violations   = new List<string>();

        foreach (var file in Directory.EnumerateFiles(backendRoot, "*.cs", SearchOption.AllDirectories))
        {
            // Excluir archivos generados / no productivos.
            if (file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;
            if (file.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(backendRoot, file).Replace('\\', '/');
            if (allowedPaths.Contains(relative))
                continue;

            var text = File.ReadAllText(file);
            if (patternArray.Any(p => text.Contains(p, StringComparison.Ordinal)))
                violations.Add(relative);
        }

        return violations;
    }

    private static string ResolveBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ERP.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, "src", "ERP.API")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz backend (ERP.sln / src/ERP.API).");
    }
}
