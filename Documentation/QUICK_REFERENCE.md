# Quick Reference: Key Changes & Patterns

## New Required Constructor Signatures

### Invoice (BREAKING CHANGE)

```csharp
// OLD ❌
new Invoice(sender, receiver, "INV-001", 30)

// NEW ✅
new Invoice(tenantId, sender, receiver, "INV-001", 30)
```

### Participant

```csharp
// Must set TenantId
var participant = new Participant
{
    TenantId = tenantId,  // REQUIRED
    Name = "Company ABC",
    Email = "contact@abc.com",
    // ... other fields
};
```

### InvoiceLineItem

```csharp
// Recommended: use new constructor with TenantId
var item = new InvoiceLineItem(
    tenantId: tenantId,
    description: "Consulting",
    quantity: 40,
    unitPrice: 150,
    invoiceId: invoice.Id
);
```

### PaymentRecord

```csharp
// Use new constructor with TenantId
var payment = new PaymentRecord(
    tenantId: tenantId,
    datePaid: DateTime.UtcNow,
    amount: 5000m,
    currency: "USD",
    paymentMethod: "BankTransfer",
    reference: "Wire 12345",
    invoiceId: invoice.Id
);
```

---

## Database Query Patterns

### ✅ CORRECT (Citus co-located execution)

```csharp
// Always include TenantId filter FIRST
var invoices = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)     // ← REQUIRED
    .Where(i => i.Year == 2024)
    .Where(i => i.Status != InvoiceStatus.Draft)
    .ToList();
```

### ❌ WRONG (Cross-shard, slow)

```csharp
// Missing TenantId filter = slow across all workers
var invoices = dbContext.Invoices
    .Where(i => i.Year == 2024)  // Searches all shards ❌
    .ToList();
```

---

## Common Query Examples

### Get All Active Invoices for a Tenant

```csharp
var tenantId = Guid.Parse("12345678-1234-1234-1234-123456789012");

var activeInvoices = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => !i.IsArchived)              // Hot data only
    .Where(i => i.Year >= DateTime.UtcNow.Year - 1)
    .OrderByDescending(i => i.IssueDate)
    .ToList();
```

### Get Overdue Invoices

```csharp
var overdue = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.Status == InvoiceStatus.Overdue)
    .Where(i => i.DueDate < DateTime.UtcNow)
    .ToList();
```

### Get Invoices with Specific Metadata

```csharp
// Query by JSONB metadata
var invoicesWithRegionalTax = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.Metadata.ContainsKey("regional_taxes"))
    .AsEnumerable()
    .Where(i => (double)((dynamic)i.Metadata["regional_taxes"]).de_vat > 0)
    .ToList();
```

### Get Participants by Country (from Metadata)

```csharp
var germanParticipants = dbContext.Participants
    .Where(p => p.TenantId == tenantId)
    .AsEnumerable()  // Client-side filtering for JSONB
    .Where(p => p.Metadata.TryGetValue("country", out var country) && 
                (string)country == "DE")
    .ToList();
```

### Archive Old Invoices

```csharp
var oldInvoices = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.Year < DateTime.UtcNow.Year - 1)
    .ToList();

foreach (var invoice in oldInvoices)
{
    invoice.IsArchived = true;
    // TODO: Export to S3/Archive storage
}

dbContext.SaveChanges();
```

### Get Invoice Summary with Payments

```csharp
var invoiceWithPayments = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.InvoiceNumber == "INV-2024-001")
    .Include(i => i.LineItems)
    .Include(i => i.Payments)
    .FirstOrDefault();

if (invoiceWithPayments != null)
{
    Console.WriteLine($"Total: {invoiceWithPayments.TotalAmount}");
    Console.WriteLine($"Paid: {invoiceWithPayments.AmountPaid}");
    Console.WriteLine($"Due: {invoiceWithPayments.BalanceRemaining}");
}
```

---

## New Entity Fields

### Invoice

| Field | Type | Purpose | Example |
|-------|------|---------|---------|
| TenantId | Guid | Shard key | `Guid.Parse("...")` |
| Year | int | Tiered storage | `2024` |
| IsArchived | bool | Cold storage flag | `false` |
| Metadata | Dict | Schema evolution | `{"taxes": {...}}` |
| CreatedAt | DateTime | Audit | Auto-set |
| ModifiedAt | DateTime | Audit | Auto-updated |

### Participant

| Field | Type | Purpose |
|-------|------|---------|
| TenantId | Guid | Shard key |
| Metadata | Dict | Regional variants, compliance |
| IsArchived | bool | Inactive flag |
| CreatedAt | DateTime | Audit |
| ModifiedAt | DateTime | Audit |

### InvoiceLineItem

| Field | Type | Purpose |
|-------|------|---------|
| TenantId | Guid | Must match Invoice.TenantId |
| CreatedAt | DateTime | Audit |

### PaymentRecord

| Field | Type | Purpose |
|-------|------|---------|
| TenantId | Guid | Must match Invoice.TenantId |

---

## Metadata Usage Examples

### Store Regional Tax Information

```csharp
invoice.Metadata["regional_taxes"] = new
{
    country = "DE",
    de_vat = 19.0,
    reverse_charge = true,
    vat_id = "DE123456789"
};
```

### Store ESG/Sustainability Data

```csharp
invoice.Metadata["esg_metrics"] = new
{
    carbon_footprint_kg = 12.5,
    sustainable_packaging = true,
    carbon_offset_applied = false,
    circular_economy_points = 85
};
```

### Store Blockchain/Immutable Proof

```csharp
invoice.Metadata["blockchain"] = new
{
    chain = "ethereum",
    transaction_hash = "0xabc123...",
    block_number = 18500000,
    timestamp = DateTime.UtcNow
};
```

### Store Custom Compliance Data

```csharp
invoice.Metadata["compliance"] = new
{
    sarbanes_oxley = true,
    gdpr_approved = true,
    hipaa_compliant = false,
    pci_dss_version = 3.2
};
```

---

## Audit Fields

All invoice-related entities now have audit timestamps:

```csharp
var invoice = new Invoice(tenantId, sender, receiver, "INV-001", 30);

// Auto-set on creation
Console.WriteLine(invoice.CreatedAt);   // DateTime.UtcNow
Console.WriteLine(invoice.ModifiedAt);  // DateTime.UtcNow

// ModifiedAt auto-updates on every save
invoice.AddPayment(1000m, DateTime.UtcNow, "USD", "BankTransfer", "Wire");
dbContext.SaveChanges();

// ModifiedAt is now updated to current time
Console.WriteLine(invoice.ModifiedAt);  // DateTime.UtcNow (refreshed)
```

---

## Index Hints

Use these indexes in your queries for performance:

```csharp
// Leverages: (TenantId, InvoiceNumber)
var invoice = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.InvoiceNumber == "INV-2024-001")
    .FirstOrDefault();

// Leverages: (TenantId, Year)
var yearInvoices = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.Year == 2024)
    .ToList();

// Leverages: (TenantId, IsArchived, Year)
var active = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => !i.IsArchived)
    .Where(i => i.Year >= 2023)
    .ToList();

// Leverages: (TenantId, DueDate)
var overdue = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.DueDate < DateTime.UtcNow)
    .ToList();
```

---

## Migration Steps (Order Matters!)

```bash
# 1. Create migration
cd src/InvoiceSystem.Infrastructure
dotnet ef migrations add AddShardingAndMetadata

# 2. Apply to database
dotnet ef database update

# 3. Enable Citus (PostgreSQL CLI)
psql -U postgres -d invoicedb
CREATE EXTENSION IF NOT EXISTS citus;
SELECT create_distributed_table('invoices', 'tenant_id');
SELECT create_distributed_table('invoice_line_items', 'tenant_id');
SELECT create_distributed_table('payment_records', 'tenant_id');
SELECT create_reference_table('participants');
\q

# 4. Set default TenantId for existing data (if any)
UPDATE invoices SET tenant_id = '00000000-0000-0000-0000-000000000000'::uuid 
WHERE tenant_id IS NULL;

# 5. Update application code (all Invoice constructors)

# 6. Run tests with TenantId included

# 7. Load test: target 10,000 tx/minute
```

---

## Testing Checklist

- [ ] Unit test: `Invoice(tenantId, ...) creates invoice with TenantId`
- [ ] Unit test: `Participant with TenantId persists correctly`
- [ ] Unit test: `Metadata serialization/deserialization works`
- [ ] Integration test: Query with TenantId filter returns correct data
- [ ] Integration test: Query without TenantId filter (cross-shard) works
- [ ] Integration test: IsArchived filtering works
- [ ] Integration test: Year-based partitioning queries work
- [ ] Integration test: JSONB metadata queries work
- [ ] Load test: 10,000 invoices/minute throughput
- [ ] Backup/restore test: Full data recovery

---

## Common Mistakes

❌ **WRONG: Missing TenantId in Query**
```csharp
var all = dbContext.Invoices
    .Where(i => i.Year == 2024)  // No TenantId = cross-shard!
    .ToList();
```

✅ **CORRECT: TenantId First**
```csharp
var correct = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)  // Always first
    .Where(i => i.Year == 2024)
    .ToList();
```

---

❌ **WRONG: TenantId Mismatch in Related Entities**
```csharp
var payment = new PaymentRecord(
    tenantId: WRONG_TENANT_ID,  // ❌ Doesn't match invoice
    // ... other params
    invoiceId: invoice.Id
);
```

✅ **CORRECT: Match TenantIds**
```csharp
var payment = new PaymentRecord(
    tenantId: invoice.TenantId,  // ✅ Match parent
    // ... other params
    invoiceId: invoice.Id
);
```

---

## Performance Tips

1. **Always filter by TenantId first** - enables co-location on single worker
2. **Include Year in queries** - supports tiered storage strategy
3. **Use IsArchived filtering** - separates hot from cold data
4. **Batch inserts** - better throughput for 10K tx/min
5. **Use Metadata for flexibility** - avoid schema locks in JSONB
6. **Monitor shard distribution** - ensure even load balancing

---

## Troubleshooting

**Q: Why is my query slow?**
A: Missing TenantId filter = cross-shard query. Add `.Where(i => i.TenantId == tenantId)` first.

**Q: How do I query archived invoices?**
A: Use `.Where(i => i.IsArchived == true)` - they're in the same table.

**Q: Can I add new fields without migration?**
A: Yes! Use Metadata dictionary: `invoice.Metadata["new_field"] = value`

**Q: How is TenantId assigned?**
A: Application determines tenant context (auth, multi-tenancy pattern). Pass Guid to Invoice constructor.

**Q: What happens if TenantId is null?**
A: Database enforces NOT NULL - constructor won't allow it.

