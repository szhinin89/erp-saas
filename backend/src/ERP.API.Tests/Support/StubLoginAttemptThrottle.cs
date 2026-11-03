using ERP.Application.Common.Interfaces;

namespace ERP.API.Tests.Support;

/// <summary>SEC-A01 — admisión de login fija para tests de controller (sin Redis ni límites reales).</summary>
public sealed class StubLoginAttemptThrottle(
    LoginAdmissionStatus status = LoginAdmissionStatus.Admitted
) : ILoginAttemptThrottle
{
    public static StubLoginAttemptThrottle AdmitAll => new();

    public int Begun { get; private set; }
    public int Succeeded { get; private set; }
    public string? LastIdentifier { get; private set; }
    public string? LastClientIp { get; private set; }

    public Task<ILoginAttempt> BeginAsync(
        string identifier,
        string? clientIp,
        CancellationToken cancellationToken
    )
    {
        Begun++;
        LastIdentifier = identifier;
        LastClientIp = clientIp;
        return Task.FromResult<ILoginAttempt>(new Attempt(this, status));
    }

    private sealed class Attempt(StubLoginAttemptThrottle owner, LoginAdmissionStatus status)
        : ILoginAttempt
    {
        public LoginAdmissionStatus Status => status;

        public Task MarkSucceededAsync()
        {
            owner.Succeeded++;
            return Task.CompletedTask;
        }

        public void Dispose() { }
    }
}
