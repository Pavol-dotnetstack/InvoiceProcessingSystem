namespace InvoiceSystem.Domain.Entities;

public class Invoice
{
    public Guid Id { get; private set; }

    #region Invoice Details
    /// <summary>
    /// A unique identifier for the invoice, which is crucial for tracking and referencing the invoice in a B2B context. This can be generated using a specific format (e.g., INV-2024-0001) to ensure uniqueness and to provide information about the invoice, such as the year of issuance.
    /// </summary>
    public string InvoiceNumber { get; private set; } = string.Empty;

    /// <summary>
    /// The current status of the invoice, which can be used to track the payment process and manage outstanding invoices. 
    /// Common statuses include "Pending," "Paid," "Overdue," and "Cancelled." 
    /// This field is crucial for financial management and reporting in a B2B context, as it helps identify which invoices require attention and follow-up actions.
    /// Invoice State Machine: Draft → Issued → PartiallyPaid → Paid → Overdue → Voided
    /// </summary>
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    /// <summary>
    /// Pro Forma, Interim Invoice, Final Invoice, Credit Note, Debit Note, etc. - helps categorize the invoice type for better processing and reporting.
    /// </summary>
    public InvoiceType InvoiceType { get; set; } = InvoiceType.Standard;
    #endregion Invoice Details

    #region Participants
    /// <summary>
    /// Supplier, Vendor, Service Provider, etc. - helps identify the party responsible for issuing the invoice and can be used for filtering and reporting.
    /// </summary>
    public required Participant Sender { get; set; }

    /// <summary>
    /// Client, Customer, Buyer, etc. - helps identify the party responsible for paying the invoice and can be used for filtering and reporting.
    /// Client accepts services or goods from the sender and is obligated to pay the invoice amount by the due date. This field is crucial for tracking outstanding payments and managing customer relationships.
    /// </summary>
    public required Participant Receiver { get; set; }
    #endregion Participants

    #region Dates
    /// <summary>
    /// The date when the invoice was issued. This is important for tracking the age of the invoice, calculating due dates, and managing cash flow in a B2B context. It also helps in generating reports and analyzing payment patterns over time.
    /// </summary>
    public DateTime IssueDate { get; set; }
    
    /// <summary>
    /// Payment Terms: Instead of paying upfront, B2B partners often work on credit, paying at a later date (e.g., 30, 60, or 90 days)
    /// </summary>
    public int PaymentTermsDays { get; set; }

    /// <summary>
    /// The date by which the payment for the invoice is due. This is calculated based on the issue date and the payment delay, and it is crucial for managing cash flow and ensuring timely payments in a B2B context.
    /// </summary>
    public DateTime DueDate => IssueDate.AddDays(PaymentTermsDays);
    #endregion Dates

    /// <summary>
    /// Description of the services provided or goods sold, 
    /// which can be used for better understanding the invoice details and for reporting purposes.
    /// </summary>
    private readonly List<InvoiceLineItem> _lineItems = new();
    public IReadOnlyCollection<InvoiceLineItem> LineItems => _lineItems.AsReadOnly();

    private readonly List<PaymentRecord> _payments = new();
    public IReadOnlyCollection<PaymentRecord> Payments => _payments.AsReadOnly();


    /// <summary>
    /// The total amount to be paid for the invoice, which is crucial for financial tracking and reporting. In a B2B context, this amount may be subject to taxes, discounts, or other adjustments based on the agreement between the sender and receiver.
    /// </summary>
    public decimal TotalAmount => _lineItems.Sum(item => item.Total);
    public decimal AmountPaid => _payments.Sum(p => p.Amount);
    public decimal BalanceRemaining => TotalAmount - AmountPaid;

    #region Constructors
    public Invoice(Participant sender, Participant receiver, string invoiceNumber, int paymentDelay)
    {
        Sender = sender;
        Receiver = receiver;
        InvoiceNumber = invoiceNumber;
        PaymentTermsDays = paymentDelay;
        IssueDate = DateTime.UtcNow;
        _lineItems = new List<InvoiceLineItem>();
        _payments = new List<PaymentRecord>();
    }
    #endregion Constructors

    public void AddLineItem(string description, int quantity, decimal unitPrice)
    {
        if (Status != InvoiceStatus.Draft) 
            throw new InvalidOperationException("Cannot modify an invoice that is not in draft status.");
            
        _lineItems.Add(new InvoiceLineItem(description, quantity, unitPrice, Id));
    }

    public void AddPayment(decimal amount, DateTime datePaid, string currency, string paymentMethod, string reference)
    {
        if (Status == InvoiceStatus.Draft) 
            throw new InvalidOperationException("Cannot pay a draft invoice.");
            
        _payments.Add(new PaymentRecord(datePaid, amount, currency, paymentMethod, reference, Id));
        
        UpdateStatusBasedOnBalance();
    }

    private void UpdateStatusBasedOnBalance()
    {
        if (BalanceRemaining <= 0) Status = InvoiceStatus.Paid;
        else if (AmountPaid > 0) Status = InvoiceStatus.PartiallyPaid;
    }
}
