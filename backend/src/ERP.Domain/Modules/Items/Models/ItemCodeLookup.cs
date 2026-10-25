namespace ERP.Domain.Modules.Items.Models;

/// <summary>Exact identity lookup; mirrors the existing unique keys, never changes them.</summary>
public sealed record ItemCodeLookup(string Kind, string Code, Guid? SupplierId = null, Guid? ExcludeId = null);

public sealed record ItemCodeOwner(Guid ItemId, string ItemName);
