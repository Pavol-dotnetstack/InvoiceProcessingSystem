namespace InvoiceSystem.Domain.Entities;

public class Invoice
{
    public Guid Id { get; private set; }

    #region Sharding & Multi-Tenancy
    /// <summary>
    /// TenantId: Distribution key for PostgreSQL Citus sharding.
    /// All queries must filter by TenantId for efficient distributed execution.
    /// This is the PRIMARY sharding key for horizontal scaling.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Year of invoice issuance - used for range partitioning between hot (current/recent) 
    /// and cold (historical) storage tiers. Derived from IssueDate but stored for 
    /// query optimization and partitioning strategies.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// IsArchived: Flag for tiered storage implementation. Hot data (current/recent years) 
    /// stays in primary storage; cold data (older years) can be archived to cost-effective 
    /// storage or partitioned separately. Applications should query IsArchived status 
    /// based on retention policies.
    /// </summary>
    public bool IsArchived { get; set; } = false;
    #endregion Sharding & Multi-Tenancy

    #region Invoice Details
    /// <summary>
    /// A unique identifier for the invoice, which is crucial for tracking and referencing the invoice in a B2B context. 
    /// This can be generated using a specific format (e.g., INV-2024-0001) to ensure uniqueness and to provide information 
    /// about the invoice, such as the year of issuance.
    /// </summary>
    public string InvoiceNumber { get; private set; } = string.Empty;

    /// <summary>
    /// The current status of the invoice, which can be used to track the payment process and manage outstanding invoices.
    /// Common statuses include "Pending," "Paid," "Overdue," and "Cancelled." 
    /// This field is crucial for financial management and reporting in a B2B context, as it helps identify which invoices 
    /// require attention and follow-up actions.
    /// Invoice State Machine: Draft → Issued → PartiallyPaid → Paid → Overdue → Voided
    /// </summary>
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    /// <summary>
    /// Pro Forma, Interim Invoice, Final Invoice, Credit Note, Debit Note, etc. - helps categorize the invoice type 
    /// for better processing and reporting.
    /// </summary>
    public InvoiceType InvoiceType { get; set; } = InvoiceType.Standard;

    /// <summary>
    /// JSONB metadata column for flexible schema evolution without database migrations.
    /// Store additional custom fields, audit trails, compliance data, regional-specific information, 
    /// or future extensions here. EF Core will serialize/deserialize as JSON.
    /// This supports 50-year evolution without schema lock-in.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
    #endregion Invoice Details

    #region Participants
    /// <summary>
    /// Supplier, Vendor, Service Provider, etc. - helps identify the party responsible for issuing the invoice 
    /// and can be used for filtering and reporting.
    /// </summary>
    public required Participant Sender { get; set; }

    /// <summary>
    /// Client, Customer, Buyer, etc. - helps identify the party responsible for paying the invoice 
    /// and can be used for filtering and reporting.
    /// Client accepts services or goods from the sender and is obligated to pay the invoice amount by the due date. 
    /// This field is crucial for tracking outstanding payments and managing customer relationships.
    /// </summary>
    public required Participant Receiver { get; set; }
    #endregion Participants

    #region Dates
    /// <summary>
    /// The date when the invoice was issued. This is important for tracking the age of the invoice, 
    /// calculating due dates, and managing cash flow in a B2B context. It also helps in generating reports 
    /// and analyzing payment patterns over time.
    /// </summary>
    public DateTime IssueDate { get; set; }
    
    /// <summary>
    /// Payment Terms: Instead of paying upfront, B2B partners often work on credit, paying at a later date 
    /// (e.g., 30, 60, or 90 days)
    /// </summary>
    public int PaymentTermsDays { get; set; }

    /// <summary>
    /// The date by which the payment for the invoice is due. This is calculated based on the issue date 
    /// and the payment delay, and it is crucial for managing cash flow and ensuring timely payments in a B2B context.
    /// </summary>
    public DateTime DueDate => IssueDate.AddDays(PaymentTermsDays);

    /// <summary>
    /// Timestamp for creating the entity. Useful for audit trails and tiered storage decisions.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp for last modification. Useful for change data capture and audit trails.
    /// </summary>
    public DateTime ModifiedAt { get; set; }
    #endregion Dates

    #region Financial Summary
    /// <summary>
    /// Description of the services provided or goods sold, 
    /// which can be used for better understanding the invoice details and for reporting purposes.
    /// </summary>
    private readonly List<InvoiceLineItem> _lineItems = new();
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<PaymentRecord> _payments = new();
    public IReadOnlyCollection<PaymentRecord> Payments => _payments.AsReadOnly();

    /// <summary>
    /// The total amount to be paid for the invoice, which is crucial for financial tracking and reporting. 
    /// In a B2B context, this amount may be subject to taxes, discounts, or other adjustments based on 
    /// the agreement between the sender and receiver.
    /// </summary>
    public decimal TotalAmount => _lineItems.Sum(item => item.Total);
    public decimal AmountPaid => _payments.Sum(p => p.Amount);
    public decimal BalanceRemaining => TotalAmount - AmountPaid;
    #endregion Financial Summary

    #region Constructors
    public Invoice(Guid tenantId, Participant sender, Participant receiver, string invoiceNumber, int paymentDelay)
    {
        TenantId = tenantId;
        Sender = sender;
        Receiver = receiver;
        InvoiceNumber = invoiceNumber;
        PaymentTermsDays = paymentDelay;
        IssueDate = DateTime.UtcNow;
        Year = IssueDate.Year;
        CreatedAt = DateTime.UtcNow;
        ModifiedAt = DateTime.UtcNow;
        _lineItems = new List<InvoiceLineItem>();
        _payments = new List<PaymentRecord>();
    }
    #endregion Constructors

    public void AddLineItem(string description, int quantity, decimal unitPrice)
    {
        if (Status != InvoiceStatus.Draft) 
            throw new InvalidOperationException("Cannot modify an invoice that is not in draft status.");
        
        _lineItems.Add(new InvoiceLineItem(description, quantity, unitPrice, Id));
        ModifiedAt = DateTime.UtcNow;
    }

    public void AddPayment(decimal amount, DateTime datePaid, string currency, string paymentMethod, string reference)
    {
        if (Status == InvoiceStatus.Draft) 
            throw new InvalidOperationException("Cannot pay a draft invoice.");
            
        _payments.Add(new PaymentRecord(datePaid, amount, currency, paymentMethod, reference, Id));
        ModifiedAt = DateTime.UtcNow;
        UpdateStatusBasedOnBalance();
    }

    private void UpdateStatusBasedOnBalance()
    {
        if (BalanceRemaining <= 0) Status = InvoiceStatus.Paid;
        else if (AmountPaid > 0) Status = InvoiceStatus.PartiallyPaid;
    }
}
