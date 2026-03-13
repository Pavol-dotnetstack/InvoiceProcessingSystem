namespace InvoiceSystem.Domain.Entities;

public enum InvoiceType
{
    /// <summary>
    /// The most common type of invoice used for regular transactions. 
    /// It includes details about the goods or services provided, the amount due, and the payment terms. 
    /// Standard invoices are typically issued after the delivery of goods or completion of services and are used for billing purposes 
    /// in B2B transactions.
    /// </summary>
    Standard,

    /// <summary>
    /// A "negative" invoice used to correct a mistake. 
    /// Issued when a customer returns goods or if there was an overcharge on a previous bill.
    /// </summary>
    CreditNote,

    /// <summary>
    /// An upward adjustment to a previous invoice.
    /// Issued when additional charges need to be added after the original invoice was sent, such as for extra services or late fees.
    /// </summary>
    DebitNote,

    /// <summary>
    /// An "estimate" or "pre-invoice."
    ///  It is a non-binding document that outlines the expected costs for goods or services before they are delivered. 
    /// Proforma invoices are often used in B2B transactions to provide clients with an idea of the costs involved, 
    /// allowing them to make informed decisions before committing to a purchase. 
    /// They can also be used for customs purposes when shipping goods internationally.
    /// </summary>
    Proforma,

    /// <summary>
    /// Breaks down a large project into smaller payments. Used for long-term projects (like construction) to help with cash flow. 
    /// Sent for large projects to get paid in stages (progress payments).
    /// </summary>
    InterimInvoice,

    /// <summary>
    /// A final invoice that is issued after all goods or services have been delivered and all payments have been made. 
    /// It serves as a formal record of the completed transaction and can be used for accounting and tax purposes.
    /// </summary>
    FinalInvoice,
}