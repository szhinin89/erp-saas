using ERP.Domain.Common;
namespace ERP.Domain.Common
{
    public abstract class BaseEntity
    {
        public int Id { get; set; }

        // Multi-Tenant (clave para SaaS)
        public int TenantId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
}