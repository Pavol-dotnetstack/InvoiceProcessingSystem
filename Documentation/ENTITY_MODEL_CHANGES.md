# Entity Model Changes: Quick Reference

## Summary of Changes

The Invoice Processing System domain model has been updated to support scalable, distributed processing with PostgreSQL Citus, tiered storage for 50-year retention, and schema evolution via JSONB metadata.

---

## Invoice Entity Changes

### New Required Fields

**Sharding Support:**
```csharp
public Guid TenantId { get; set; }  // Distribution key for Citus
```

**Tiered Storage Support:**
```csharp
public int Year { get; set; }  // Range: 1-50 (invoice year)
public bool IsArchived { get; set; }  // true = cold storage, false = hot
```

**Schema Evolution:**
```csharp
public Dictionary<string, object> Metadata { get; set; } = new();  // JSONB column
```

**Audit Trail:**
```csharp
public DateTime CreatedAt { get; set; }  // Timestamp of creation
public DateTime ModifiedAt { get; set; }  // Timestamp of last change
```

### Updated Constructor

**OLD:**
```csharp
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
```

**NEW:**
```csharp
public Invoice(Guid tenantId, Participant sender, Participant receiver, 
    string invoiceNumber, int paymentDelay)
{
    TenantId = tenantId;  // REQUIRED FIRST ARGUMENT
    Sender = sender;
    Receiver = receiver;
    InvoiceNumber = invoiceNumber;
    PaymentTermsDays = paymentDelay;
    IssueDate = DateTime.UtcNow;
    Year = IssueDate.Year;  // Auto-populated
    CreatedAt = DateTime.UtcNow;
    ModifiedAt = DateTime.UtcNow;
    _lineItems = new List<InvoiceLineItem>();
    _payments = new List<PaymentRecord>();
}
```

### Usage Migration

**OLD Code:**
```csharp
var invoice = new Invoice(sender, receiver, "INV-2024-001", 30);
dbContext.Invoices.Add(invoice);
```

**NEW Code:**
```csharp
var tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
var invoice = new Invoice(tenantId, sender, receiver, "INV-2024-001", 30);
dbContext.Invoices.Add(invoice);
```

### AddPayment() Method Change

The ModifiedAt timestamp is now automatically updated:

```csharp
invoice.AddPayment(
    amount: 1000.00m,
    datePaid: DateTime.UtcNow,
    currency: "USD",
    paymentMethod: "BankTransfer",
    reference: "Wire 12345"
);
// ModifiedAt is automatically set to DateTime.UtcNow
```

---

## Participant Entity Changes

### New Required Fields

**Sharding Support:**
```csharp
public Guid TenantId { get; set; }  // Distribution key for Citus
```

**Schema Evolution:**
```csharp
public Dictionary<string, object> Metadata { get; set; } = new();  // JSONB column
```

**Tiered Storage & Audit:**
```csharp
public bool IsArchived { get; set; } = false;  // Inactive participant flag
public DateTime CreatedAt { get; set; }
public DateTime ModifiedAt { get; set; }
```

### Constructor Requirements

**OLD:**
```csharp
var participant = new Participant
{
    Name = "Company ABC",
    Email = "contact@company.com",
    TaxId = "DE12345678",
    // ...
};
```

**NEW:**
```csharp
var tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");
var participant = new Participant
{
    TenantId = tenantId,  // REQUIRED
    Name = "Company ABC",
    Email = "contact@company.com",
    TaxId = "DE12345678",
    // ...
};
```

### Metadata Usage Example

```csharp
// Store regional compliance data
participant.Metadata = new Dictionary<string, object>
{
    ["country"] = "DE",
    ["vat_number"] = "DE12345678",
    ["eu_eori"] = "DE987654321",
    ["certified_reseller"] = true,
    ["credit_limit_eur"] = 100000m
};

// Query by metadata
var germanParticipants = dbContext.Participants
    .Where(p => p.TenantId == tenantId)
    .Where(p => (string)p.Metadata["country"] == "DE")
    .ToList();
```

---

## InvoiceLineItem Entity Changes

### New Fields

**Sharding Support:**
```csharp
public Guid TenantId { get; set; }  // Must match parent Invoice.TenantId
```

**Audit:**
```csharp
public DateTime CreatedAt { get; set; }
```

### Constructor Changes

**OLD:**
```csharp
var lineItem = new InvoiceLineItem("Consulting Hours", 40, 150.00m, invoiceId);
```

**NEW:**
```csharp
// Option 1: Legacy constructor (TenantId defaults to Guid.Empty - you must set it)
var lineItem = new InvoiceLineItem("Consulting Hours", 40, 150.00m, invoiceId);
lineItem.TenantId = tenantId;  // Must be set manually

// Option 2: New constructor with TenantId
var lineItem = new InvoiceLineItem(tenantId, "Consulting Hours", 40, 150.00m, invoiceId);
```

### Recommended Usage

Always pass TenantId through the new constructor:

```csharp
var lineItem = new InvoiceLineItem(
    tenantId: tenantId,
    description: "Consulting Hours",
    quantity: 40,
    unitPrice: 150.00m,
    invoiceId: invoiceId
);
```

---

## PaymentRecord Entity Changes

### New Fields

**Sharding Support:**
```csharp
public Guid TenantId { get; set; }  // Must match parent Invoice.TenantId
```

**Audit:**
```csharp
public DateTime CreatedAt { get; set; }
```

### Constructor Changes

**OLD:**
```csharp
var payment = new PaymentRecord(
    datePaid: DateTime.UtcNow,
    amount: 5000.00m,
    currency: "USD",
    paymentMethod: "BankTransfer",
    reference: "Wire 12345",
    invoiceId: invoiceId
);
```

**NEW with TenantId:**
```csharp
var payment = new PaymentRecord(
    tenantId: tenantId,  // ADDED - must match Invoice.TenantId
    datePaid: DateTime.UtcNow,
    amount: 5000.00m,
    currency: "USD",
    paymentMethod: "BankTransfer",
    reference: "Wire 12345",
    invoiceId: invoiceId
);
```

### Important Constraint

**TenantId in PaymentRecord MUST match the parent Invoice's TenantId:**

```csharp
var tenantId = Guid.Parse("...");
var invoice = new Invoice(tenantId, sender, receiver, "INV-001", 30);

var payment = new PaymentRecord(
    tenantId: tenantId,  // MUST be same as invoice.TenantId
    datePaid: DateTime.UtcNow,
    amount: 1000m,
    currency: "USD",
    paymentMethod: "BankTransfer",
    reference: "Wire",
    invoiceId: invoice.Id
);
```

---

## Query Pattern Changes

### CRITICAL: Always Include TenantId Filter

**OLD (No longer optimal with Citus):**
```csharp
var invoices = dbContext.Invoices
    .Where(i => i.Year == 2024)
    .ToList();
```

**NEW (Citus-aware - much faster):**
```csharp
var tenantId = Guid.Parse("...");
var invoices = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.Year == 2024)
    .ToList();
```

### Query Examples

#### Get all invoices for a tenant (hot data only)

```csharp
var activeInvoices = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => !i.IsArchived)
    .OrderByDescending(i => i.IssueDate)
    .ToList();
```

#### Get overdue invoices

```csharp
var overdue = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.Status == InvoiceStatus.Overdue)
    .Where(i => i.DueDate < DateTime.UtcNow)
    .ToList();
```

#### Get invoices with archived data

```csharp
var historical = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.IsArchived)
    .Where(i => i.Year < DateTime.UtcNow.Year)
    .ToList();
```

#### Query by JSON metadata

```csharp
// Get all participants in a specific country
var germanSuppliers = dbContext.Participants
    .Where(p => p.TenantId == tenantId)
    .Where(p => (string)p.Metadata["country"] == "DE")
    .ToList();
```

---

## Database Schema Changes

### New Columns in `invoices` Table

| Column | Type | Null | Default | Comment |
|--------|------|------|---------|---------|
| `tenant_id` | uuid | NO | - | Citus sharding key |
| `year` | int | NO | - | For range partitioning |
| `is_archived` | boolean | NO | false | Tiered storage flag |
| `metadata` | jsonb | NO | {} | JSONB for schema evolution |
| `created_at` | timestamp | NO | NOW() | Audit timestamp |
| `modified_at` | timestamp | NO | NOW() | Audit timestamp |

### New Columns in `participants` Table

| Column | Type | Null | Default | Comment |
|--------|------|------|---------|---------|
| `tenant_id` | uuid | NO | - | Citus sharding key |
| `metadata` | jsonb | NO | {} | JSONB for flexibility |
| `is_archived` | boolean | NO | false | Inactive flag |
| `created_at` | timestamp | NO | NOW() | Audit timestamp |
| `modified_at` | timestamp | NO | NOW() | Audit timestamp |

### New Columns in Related Tables

**invoice_line_items:**
- `tenant_id` (uuid, NO) - Must match parent Invoice

**payment_records:**
- `tenant_id` (uuid, NO) - Must match parent Invoice

### New Indexes

```sql
CREATE INDEX IX_Invoices_TenantId_InvoiceNumber ON invoices(tenant_id, invoice_number) UNIQUE;
CREATE INDEX IX_Invoices_TenantId_Year ON invoices(tenant_id, year);
CREATE INDEX IX_Invoices_TenantId_Status ON invoices(tenant_id, status);
CREATE INDEX IX_Invoices_TenantId_IsArchived_Year ON invoices(tenant_id, is_archived, year);
CREATE INDEX IX_Invoices_TenantId_DueDate ON invoices(tenant_id, due_date);

-- Similar for Participants and related tables
```

---

## Migration Steps

### 1. Update Existing Migrations

Add a new migration to apply the changes:

```bash
cd src/InvoiceSystem.Infrastructure
dotnet ef migrations add AddShardingAndMetadata
```

### 2. Apply to Database

```bash
dotnet ef database update
```

### 3. Populate TenantId (For Existing Data)

If migrating existing invoices, assign a default tenant:

```sql
UPDATE invoices 
SET tenant_id = '00000000-0000-0000-0000-000000000000'::uuid 
WHERE tenant_id IS NULL;

UPDATE participants 
SET tenant_id = '00000000-0000-0000-0000-000000000000'::uuid 
WHERE tenant_id IS NULL;
```

### 4. Update Application Code

- Replace all Invoice instantiations with `new Invoice(tenantId, ...)`
- Replace all Participant instantiations with `TenantId = tenantId, ...`
- Add TenantId to all database queries
- Update unit tests to include TenantId

### 5. Enable Citus (PostgreSQL Only)

```sql
CREATE EXTENSION IF NOT EXISTS citus;
SELECT create_distributed_table('invoices', 'tenant_id');
SELECT create_distributed_table('invoice_line_items', 'tenant_id');
SELECT create_distributed_table('payment_records', 'tenant_id');
SELECT create_reference_table('participants');
```

---

## Breaking Changes Summary

| Entity | Change | Impact | Action Required |
|--------|--------|--------|-----------------|
| Invoice | TenantId required | High | Add tenantId parameter to all constructors |
| Invoice | Constructor signature changed | High | Update all Invoice instantiations |
| Participant | TenantId required | High | Add tenantId to all instances |
| InvoiceLineItem | New optional TenantId | Medium | Recommend setting via new constructor |
| PaymentRecord | New optional TenantId | Medium | Recommend setting via new constructor |
| All | New audit timestamps | Low | Auto-populated, no action needed |
| All | Metadata fields | Low | Optional, use for future extensibility |

---

## Backward Compatibility

The changes are **NOT backward compatible** due to constructor signature changes. All existing code creating Invoice and Participant instances must be updated.

---

## Testing Checklist

- [ ] Unit tests for Invoice with TenantId
- [ ] Unit tests for Participant with TenantId
- [ ] Integration tests verifying TenantId isolation
- [ ] Query tests with TenantId filter
- [ ] JSONB metadata serialization/deserialization
- [ ] Tiered storage queries (IsArchived filtering)
- [ ] Database schema verification
- [ ] Migration rollback capability tested

