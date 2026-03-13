namespace InvoiceSystem.Domain.Entities;

public enum InvoiceStatus
{
    Draft,
    Issued,
    PartiallyPaid,
    Paid,
    Overdue,
    Voided
}