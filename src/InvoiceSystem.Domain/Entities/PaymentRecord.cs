namespace InvoiceSystem.Domain.Entities;

public class PaymentRecord
{
    public Guid Id { get; private set; }

    /// <summary>
    /// TenantId: Distribution key for PostgreSQL Citus sharding.
    /// Must match the Invoice's TenantId for referential integrity in distributed model.
    /// </ summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The date when the payment was made. This is important for tracking the payment history 
    /// and for calculating the outstanding balance of the invoice in a B2B context.
    /// </summary>
    public DateTime DatePaid { get; set; }

    /// <summary>
    /// The amount paid in this transaction. This is crucial for managing cash flow and ensuring 
    /// that the correct amount is recorded against the invoice in a B2B context.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency code (e.g., USD, EUR, GBP) to specify the currency in which the invoice amount 
    /// is denominated. This is important for international transactions and for accurate financial 
    /// reporting, especially when dealing with multiple currencies in a B2B context.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// The method of payment used (e.g., Bank Transfer, Credit Card, PayPal, etc.). 
    /// This can be useful for reporting and for understanding customer preferences in a B2B context.
    /// </summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>
    /// A reference or note about the payment, which can be used for better tracking and 
    /// reconciliation of payments in a B2B context.
    /// </summary>
    public string Reference { get; set; } = string.Empty;

    /// <summary>
    /// The ID of the invoice to which this payment record is associated. 
    /// This is crucial for linking payments to their respective invoices and for managing 
    /// outstanding balances in a B2B context.
    /// </summary>
    public Guid InvoiceId { get; set; }

    /// <summary>
    /// Timestamp for creating the entity. Useful for audit trails.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    #region Constructors
    public PaymentRecord(DateTime datePaid, decimal amount, string currency, string paymentMethod, string reference, Guid invoiceId)
    {
        DatePaid = datePaid;
        Amount = amount;
        Currency = currency;
        PaymentMethod = paymentMethod;
        Reference = reference;
        InvoiceId = invoiceId;
        CreatedAt = DateTime.UtcNow;
        Id = Guid.NewGuid();
    }

    public PaymentRecord(Guid tenantId, DateTime datePaid, decimal amount, string currency, string paymentMethod, string reference, Guid invoiceId)
    {
        TenantId = tenantId;
        DatePaid = datePaid;
        Amount = amount;
        Currency = currency;
        PaymentMethod = paymentMethod;
        Reference = reference;
        InvoiceId = invoiceId;
        CreatedAt = DateTime.UtcNow;
        Id = Guid.NewGuid();
    }
    #endregion Constructors
}
