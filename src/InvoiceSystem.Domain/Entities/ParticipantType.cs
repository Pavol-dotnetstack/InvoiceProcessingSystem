namespace InvoiceSystem.Domain.Entities;

public enum ParticipantType
{
    /// <summary>
    /// B2C (Business to Consumer) - Represents an individual person participating in the invoice system, such as a freelancer or sole proprietor.
    /// </summary>
    Individual,
    
    /// <summary>
    /// B2B (Business to Business) - Represents a company or organization participating in the invoice system.
    /// </summary>
    Company
}