namespace InvoiceSystem.Domain.Entities;

public class Participant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Address Address { get; set; }
    public ParticipantType Type { get; set; }
    public string BankAccount { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
}