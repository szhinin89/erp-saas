using ERP.Domain.Common;
// src/ERP.Domain/Common/BaseEntity.cs
namespace ERP.Domain.Common;
public abstract class BaseEntity {
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}