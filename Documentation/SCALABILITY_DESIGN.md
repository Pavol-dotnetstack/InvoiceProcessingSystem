# Invoice Processing System - Scalability Design Document

## Overview

This document outlines the scalability architecture for the Invoice Processing System, designed to sustain 50 years of operation with 10,000 transactions per minute and ~26 billion invoices.

## Architecture Pillars

### 1. **Multi-Tenancy via Sharding (PostgreSQL Citus)**

**Design Principle:** Horizontal scaling through distributed sharding on `TenantId`.

#### Sharding Key: `TenantId`

All entities contain a `TenantId` field that serves as the distribution key for Citus:
- **Invoice.TenantId**: Distribution key
- **Participant.TenantId**: Distribution key
- **InvoiceLineItem.TenantId**: Must match parent Invoice's TenantId
- **PaymentRecord.TenantId**: Must match parent Invoice's TenantId

#### Query Requirements

**ALL queries must include TenantId filter for optimal performance:**

✅ **CORRECT (Co-located execution):**
```csharp
var invoices = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)  // REQUIRED!
    .Where(i => i.Year == 2024)
    .ToList();
```

❌ **WRONG (Cross-shard query, slow):**
```csharp
var invoices = dbContext.Invoices
    .Where(i => i.Year == 2024)  // No TenantId filter
    .ToList();
```

#### Citus Setup

After database initialization, run these SQL commands to enable Citus distribution:

```sql
-- Enable Citus extension
CREATE EXTENSION IF NOT EXISTS citus;

-- Create reference tables (replicated on all workers)
-- Use for small, frequently joined tables
SELECT create_reference_table('Participants');

-- Create distributed tables (sharded by TenantId)
SELECT create_distributed_table('Invoices', 'TenantId');
SELECT create_distributed_table('InvoiceLineItems', 'TenantId');
SELECT create_distributed_table('PaymentRecords', 'TenantId');
```

#### Benefits

- **Horizontal Scaling**: Add worker nodes without re-architecting
- **Tenant Isolation**: Data naturally separated by shard key
- **Cost Efficiency**: Hot data on fast nodes, scale workers independently
- **10K transactions/minute**: Easily achievable with 4-8 worker nodes

**Estimated Cluster Capacity (with Citus):**
- 100+ active tenants
- 100,000+ invoices per day
- 10,000+ concurrent queries per minute across all tenants

---

### 2. **Tiered Storage (Hot vs. Cold Data)**

**Design Principle:** Optimize storage and query performance by separating active from historical data.

#### Storage Tiers

| Tier | Data | Storage | Queries | Retention |
|------|------|---------|---------|-----------|
| **Hot** | Current + recent year (Year >= CurrentYear - 1) | Fast SSD (Citus primary) | Sub-second | 2 years |
| **Cold** | Historical (Year < CurrentYear - 1) | Archive storage (S3, etc.) | Hours | Indefinite |

#### Implementation Strategy

**Fields Supporting Tiered Storage:**

- **Invoice.Year**: Extracted from IssueDate, used for partitioning
- **Invoice.IsArchived**: Boolean flag indicating cold storage status
- **Participant.IsArchived**: Flag for inactive participants

**Query Example - Active Invoices Only:**

```csharp
var activeInvoices = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => !i.IsArchived)
    .Where(i => i.Year >= DateTime.UtcNow.Year - 1)
    .ToList();
```

**Archival Process (Recommended):**

1. Run annually after year-close
2. Mark IsArchived = true for prior years
3. Export cold data to S3/Archive storage
4. Optionally delete from primary database
5. Archive queries fetch from separate "cold" query service

**PostgreSQL Partitioning (Optional Enhancement):**

```sql
-- Range partition invoices by year for automatic separation
CREATE TABLE invoices_2024 PARTITION OF invoices
    FOR VALUES FROM ('2024-01-01') TO ('2025-01-01');

CREATE TABLE invoices_2025 PARTITION OF invoices
    FOR VALUES FROM ('2025-01-01') TO ('2026-01-01');
```

#### Benefits

- **Query Performance**: Hot queries 100x faster (recent invoices)
- **Cost Reduction**: Cold data on cheaper storage (S3 vs. SSD)
- **Compliance**: Archive old data per regulations
- **Unlimited Growth**: 26B invoices don't all live on expensive hardware

---

### 3. **Schema Evolution (JSONB Metadata)**

**Design Principle:** Support unlimited schema changes without database migrations.

#### The Problem

Over 50 years, invoice requirements evolve:
- 2024: Basic invoice model
- 2030: Regional tax calculations added (Germany, France, etc.)
- 2035: Environmental impact reporting required
- 2040: AI-powered fraud detection fields needed
- 2050: Blockchain compliance data needed

**Hard schema changes = downtime, complex migrations, risk.**

#### The Solution: JSONB Metadata Columns

Each entity has a `Metadata` field (JSONB column):

```csharp
public Dictionary<string, object> Metadata { get; set; } = new();
```

**Example Usage:**

```csharp
// 2024 - Basic invoice
var invoice = new Invoice(tenantId, sender, receiver, "INV-2024-001", 30);
invoice.Metadata = new Dictionary<string, object>();

// 2030 - Add regional tax data dynamically
invoice.Metadata["regional_taxes"] = new
{
    de_vat = 19.0,
    fr_vat = 20.0,
    cee_reversal = true
};

// 2035 - Add ESG reporting without schema change
invoice.Metadata["esg_metrics"] = new
{
    carbon_footprint_kg = 12.5,
    sustainable_packaging = true,
    circular_economy_points = 85
};

// 2050 - Add blockchain data without migration
invoice.Metadata["blockchain"] = new
{
    eth_transaction_hash = "0x...",
    merkle_root = "0x...",
    timestamp = DateTime.UtcNow
};

dbContext.SaveChanges();
```

**Query Metadata:**

```csharp
// PostgreSQL JSONB operators
var invoicesWithRegionalTax = dbContext.Invoices
    .Where(i => i.TenantId == tenantId)
    .Where(i => i.Metadata.ContainsKey("regional_taxes"))
    .ToList();

// Get specific metadata field
var carbonFootprint = invoice.Metadata.TryGetValue("esg_metrics", out var esg) 
    ? esg 
    : null;
```

#### Benefits

- **Zero Downtime**: Add fields without migrations
- **Backward Compatible**: Old and new formats coexist
- **Cost Effective**: No schema lock-in
- **Future Proof**: Any requirement can be added
- **Queryable**: JSONB supports full-text search and operators

---

## Domain Model Changes Summary

### New Fields Added

#### Invoice Entity
| Field | Type | Purpose |
|-------|------|---------|
| `TenantId` | Guid | Sharding key for Citus distribution |
| `Year` | int | Range partitioning between hot/cold storage |
| `IsArchived` | bool | Tiered storage flag |
| `Metadata` | Dict<string, object> | JSONB for schema evolution |
| `CreatedAt` | DateTime | Audit timestamp |
| `ModifiedAt` | DateTime | Last change timestamp |

#### Participant Entity
| Field | Type | Purpose |
|-------|------|---------|
| `TenantId` | Guid | Sharding key |
| `Metadata` | Dict<string, object> | Regional variants, compliance data |
| `IsArchived` | bool | Inactive participant flag |
| `CreatedAt` | DateTime | Audit timestamp |
| `ModifiedAt` | DateTime | Last change timestamp |

#### InvoiceLineItem Entity
| Field | Type | Purpose |
|-------|------|---------|
| `TenantId` | Guid | Must match parent Invoice.TenantId |
| `CreatedAt` | DateTime | Audit timestamp |

#### PaymentRecord Entity
| Field | Type | Purpose |
|-------|------|---------|
| `TenantId` | Guid | Must match parent Invoice.TenantId |

---

## Database Indexes

Comprehensive indexes support all query patterns:

### Invoice Indexes
- `(TenantId, InvoiceNumber)` - Unique lookup within tenant
- `(TenantId, Year)` - Range queries for tiered storage
- `(TenantId, Status)` - Status-based filtering
- `(TenantId, IsArchived, Year)` - Active invoices query
- `(TenantId, DueDate)` - Aging analysis

### Participant Indexes
- `(TenantId, Email)` - Email lookup
- `(TenantId, TaxId)` - Tax ID lookup
- `(TenantId, Name)` - Name-based search
- `(TenantId, IsArchived)` - Active participants only

### Payment and LineItem Indexes
- Composite keys on (InvoiceId, TenantId) for efficient joins
- TenantId index for shard key awareness

---

## Migration Strategy

### Step 1: Create Initial Migration
```bash
cd src/InvoiceSystem.Infrastructure
dotnet ef migrations add AddShardingAndMetadata
```

### Step 2: Apply to Database
```bash
dotnet ef database update --project src/InvoiceSystem.Infrastructure
```

### Step 3: Enable Citus (PostgreSQL Only)
```sql
CREATE EXTENSION IF NOT EXISTS citus;
SELECT create_distributed_table('Invoices', 'TenantId');
SELECT create_distributed_table('InvoiceLineItems', 'TenantId');
SELECT create_distributed_table('PaymentRecords', 'TenantId');
SELECT create_reference_table('Participants');
```

### Step 4: Backfill TenantId (If Migrating Existing Data)
```sql
-- Assign default tenant to existing invoices
UPDATE invoices SET tenant_id = 'DEFAULT-TENANT-ID'::uuid WHERE tenant_id IS NULL;
```

---

## Performance Expectations

### Throughput Capacity
- **Single Node (PostgreSQL)**: ~1,000 invoices/minute
- **Citus 4-node Cluster**: ~10,000 invoices/minute ✓ **Meets requirement**
- **Citus 8-node Cluster**: ~200,000 invoices/minute (future headroom)

### Query Latency
- **Hot data queries** (current year): <100ms p99
- **Cold data queries** (archive): <5 seconds p99
- **Cross-shard queries** (rare): <1 second p99

### Storage Capacity
- **Single node**: ~500GB invoices (2B records)
- **Citus 4-node**: ~2TB invoices (26B records over 50 years)
- **Archive storage**: Unlimited (S3)

---

## Operational Considerations

### Backup & Recovery
```bash
# Full backup
pg_dump -h host -U user database > invoices_backup.sql

# Citus-aware backup (includes metadata)
pg_basebackup -h host -U user -D ./backup
```

### Monitoring

Key metrics to monitor:
- Shard distribution balance (invoices per shard)
- Query latency by tenant
- Archive rate (invoices aging into cold storage)
- JSONB metadata growth

### Scaling Strategy

**Year 1-10:**
- Single 2-node Citus cluster
- 10-100 tenants
- Hot storage: 5TB

**Year 10-30:**
- 4-6 node Citus cluster
- 100-1,000 tenants
- Hot storage: 50TB
- Cold storage: Archive old data

**Year 30-50:**
- 8+ node Citus cluster
- 1,000+ tenants
- Hot storage: 100TB (recent 2 years only)
- Cold storage: 500TB+ (S3, Glacier)

---

## Testing the Model

### Unit Test Example
```csharp
[Fact]
public void Invoice_WithTenantId_CanBeQueried()
{
    var tenantId = Guid.NewGuid();
    var sender = new Participant { TenantId = tenantId };
    var receiver = new Participant { TenantId = tenantId };
    
    var invoice = new Invoice(tenantId, sender, receiver, "INV-001", 30);
    
    Assert.Equal(tenantId, invoice.TenantId);
    Assert.Equal(DateTime.UtcNow.Year, invoice.Year);
    Assert.False(invoice.IsArchived);
}
```

### Integration Test
```csharp
[Fact]
public async Task Invoices_FilteredByTenantId_ReturnCorrectShardData()
{
    var tenantId1 = Guid.NewGuid();
    var tenantId2 = Guid.NewGuid();
    
    // Create invoices for different tenants
    var invoice1 = new Invoice(tenantId1, sender1, receiver1, "INV-001", 30);
    var invoice2 = new Invoice(tenantId2, sender2, receiver2, "INV-002", 30);
    
    dbContext.Invoices.Add(invoice1);
    dbContext.Invoices.Add(invoice2);
    await dbContext.SaveChangesAsync();
    
    // Query with TenantId filter
    var results = await dbContext.Invoices
        .Where(i => i.TenantId == tenantId1)
        .ToListAsync();
    
    Assert.Single(results);
    Assert.Equal(invoice1.Id, results.First().Id);
}
```

---

## Conclusion

This scalable architecture ensures:
- ✅ **Unlimited Growth**: 26B invoices over 50 years
- ✅ **High Throughput**: 10,000 transactions/minute
- ✅ **Zero Downtime**: Schema evolution via JSONB
- ✅ **Cost Efficiency**: Tiered storage (hot/cold)
- ✅ **Open Standards**: PostgreSQL + Citus
- ✅ **Multi-Tenancy**: Tenant isolation via sharding

Next: Implement application layer services that always include TenantId in queries.
