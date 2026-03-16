namespace InvoiceSystem.Domain.Entities;

public class InvoiceLineItem
{
    public Guid Id { get; private set; }

    /// <summary>
    /// TenantId: Distribution key for PostgreSQL Citus sharding.
    /// Must match the parent Invoice's TenantId for referential integrity.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// A description of the product or service being invoiced. 
    /// This is important for providing clarity on what is being billed and can be used for reporting
    /// and analysis in a B2B context.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The quantity of the product or service being invoiced. 
    /// This is crucial for calculating the total amount for the line item and for managing inventory in a B2B context.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// The unit price of the product or service being invoiced. 
    /// This is important for calculating the total amount for the line item and for managing pricing strategies in a B2B context.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// The total amount for this line item, calculated as Quantity * UnitPrice.
    /// This is crucial for managing cash flow and ensuring accurate billing in a B2B context.
    /// </summary>
    public decimal Total => Quantity * UnitPrice;

    /// <summary>
    /// The ID of the invoice to which this line item belongs. 
    /// This is crucial for linking line items to their respective invoices and for managing invoice details in a B2B context.
    /// </summary>
    public Guid InvoiceId { get; set; }

    /// <summary>
    /// Timestamp for creating the entity. Useful for audit trails.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    #region Constructors
    public InvoiceLineItem(string description, int quantity, decimal unitPrice, Guid invoiceId)
    {
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        InvoiceId = invoiceId;
        CreatedAt = DateTime.UtcNow;
        Id = Guid.NewGuid();
    }

    public InvoiceLineItem(Guid tenantId, string description, int quantity, decimal unitPrice, Guid invoiceId)
    {
        TenantId = tenantId;
        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        InvoiceId = invoiceId;
        CreatedAt = DateTime.UtcNow;
        Id = Guid.NewGuid();
    }
    #endregion Constructors
}
