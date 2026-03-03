namespace InvoiceSystem.Domain.Entities;

public class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime IssueDate { get; set; }
    public string Status { get; set; } = "Pending";
    public string TaxID { get; set; } = string.Empty;
}
