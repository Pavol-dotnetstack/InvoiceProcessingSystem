namespace InvoiceSystem.Domain.Entities;

public struct Address
{
    public Address(string street, string city, string state, string postalCode, string country)
    {
        Street = street;
        City = city;
        State = state;
        PostalCode = postalCode;
        Country = country;
    }
    public Address(string street, string city, string postalCode, string country)
    {
        Street = street;
        City = city;
        PostalCode = postalCode;
        Country = country;
    }

    public required string Street { get; set; }
    public required string City { get; set; }
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; }
    public required string Country { get; set; }
}