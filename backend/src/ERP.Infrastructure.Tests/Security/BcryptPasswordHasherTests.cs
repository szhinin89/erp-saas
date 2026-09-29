using System.Diagnostics;
using ERP.Infrastructure.Security;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Security;

/// <summary>
/// ZH-BACKEND-SECURITY-ERROR-FINAL-HARDENING-01 — la verificación simulada (usuario inexistente)
/// ejecuta el mismo trabajo BCrypt que una verificación real con contraseña incorrecta. Sin umbrales
/// de milisegundos: se compara la mediana de ambos caminos con márgenes amplios (orden de magnitud).
/// </summary>
public sealed class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();

    private static long MedianTicks(Action action, int runs = 5)
    {
        var samples = new long[runs];
        for (var i = 0; i < runs; i++)
        {
            var sw = Stopwatch.StartNew();
            action();
            samples[i] = sw.ElapsedTicks;
        }
        Array.Sort(samples);
        return samples[runs / 2];
    }

    [Fact]
    public void Verificacion_simulada_cuesta_lo_mismo_que_una_contrasena_incorrecta_real()
    {
        var realHash = _hasher.HashPassword("Correcta#2026");

        var wrongPassword = MedianTicks(() => _hasher.VerifyPassword("Incorrecta#1", realHash));
        var nonexistentUser = MedianTicks(() => _hasher.SimulatePasswordVerification("Incorrecta#1"));

        // Sin simulación el camino "usuario inexistente" era ~0 (ningún BCrypt): la relación sería ≪ 0,1.
        var ratio = (double)nonexistentUser / wrongPassword;
        ratio.Should().BeInRange(0.4, 2.5, $"verify={wrongPassword} simulate={nonexistentUser} ticks");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("contraseña-con-ñ-y-emoji-🔐")]
    public void Verificacion_simulada_nunca_lanza(string password)
    {
        var act = () => _hasher.SimulatePasswordVerification(password);

        act.Should().NotThrow();
    }

    [Fact]
    public void El_costo_de_produccion_es_12_y_se_usa_en_los_hashes_reales()
    {
        var hash = _hasher.HashPassword("Correcta#2026");

        hash.Should().MatchRegex(@"^\$2[ab]\$12\$");
        BCrypt.Net.BCrypt.PasswordNeedsRehash(hash, 12).Should().BeFalse();
    }

    // ── 01B: hash ficticio constante (ninguna generación BCrypt por request) ──

    [Fact]
    public void Hash_ficticio_usa_el_mismo_algoritmo_y_costo_que_un_hash_real()
    {
        var realHash = _hasher.HashPassword("Correcta#2026");

        BcryptPasswordHasher.WorkFactor.Should().Be(12);
        BcryptPasswordHasher.DummyVerificationHash.Should().MatchRegex(@"^\$2a\$12\$[./A-Za-z0-9]{53}$");
        BcryptPasswordHasher.DummyVerificationHash[..7].Should().Be(realHash[..7], "mismo algoritmo ($2a) y costo (12)");
        BCrypt.Net.BCrypt.PasswordNeedsRehash(BcryptPasswordHasher.DummyVerificationHash, BcryptPasswordHasher.WorkFactor)
            .Should().BeFalse();
    }

    [Fact]
    public void No_hay_generacion_BCrypt_por_request_el_hasher_no_tiene_estado_calculado()
    {
        // Sin campos (estáticos ni de instancia, Lazy o no) salvo constantes: SimulatePasswordVerification
        // solo puede verificar contra DummyVerificationHash — nunca HashPassword + Verify.
        var fields = typeof(BcryptPasswordHasher).GetFields(
            System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
        );

        fields.Where(f => !f.IsLiteral).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Correcta#2026")]
    [InlineData("admin")]
    [InlineData("123456")]
    public void Hash_ficticio_es_valido_para_BCrypt_pero_ninguna_contrasena_autentica(string password) =>
        // BCrypt.Verify directo: si el hash fuera inválido lanzaría (el adaptador lo convertiría en un
        // false inmediato, sin trabajo BCrypt). No lanza → verificación completa; y nunca autentica.
        BCrypt.Net.BCrypt.Verify(password, BcryptPasswordHasher.DummyVerificationHash).Should().BeFalse();
}
