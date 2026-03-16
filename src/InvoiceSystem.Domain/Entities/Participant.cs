namespace InvoiceSystem.Domain.Entities;

/// <summary>
/// Participant represents either a Sender (Supplier/Vendor) or Receiver (Customer/Client) in an invoice.
/// Multi-tenancy support: TenantId must always be set and used as a sharding key.
/// </summary>
public class Participant
{
    #region Identity & Multi-Tenancy
    public Guid Id { get; set; }

    /// <summary>
    /// TenantId: Distribution key for PostgreSQL Citus sharding.
    /// Participants must belong to a specific tenant and filter all queries by this.
    /// This ensures data isolation and enables efficient distributed execution.
    /// </summary>
    public Guid TenantId { get; set; }
    #endregion Identity & Multi-Tenancy

    #region Core Information
    public string Name { get; set; } = string.Empty;
    public Address Address { get; set; }
    public ParticipantType Type { get; set; }
    public string BankAccount { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;

    /// <summary>
    /// JSONB metadata for flexible schema evolution. Store Tax Ids in different regional formats,
    /// compliance certifications, custom attributes, or future extended data without migrations.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
    #endregion Core Information

    #region Audit
    /// <summary>
    /// Timestamp for creating the entity. Useful for audit trails.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp for last modification. Useful for change data capture.
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Status flag: true if participant is archived (no longer active).
    /// Useful for tiered storage and retention policies.
    /// </summary>
    public bool IsArchived { get; set; } = false;
    #endregion Audit

    public Participant()
    {
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
    }
}
