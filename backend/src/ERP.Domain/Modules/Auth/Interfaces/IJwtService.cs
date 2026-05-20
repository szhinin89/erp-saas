using ERP.Domain.Auth.Entities;

namespace ERP.Domain.Auth.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    string GenerateToken(User user, Guid subscriberIdOverride);
}
