using Microsoft.EntityFrameworkCore;
using InvoiceSystem.Domain.Entities;

namespace InvoiceSystem.Infrastructure.Persistence;

/// <summary>
/// ApplicationDbContext: EF Core DbContext for PostgreSQL with Citus (distributed SQL) support.
/// 
/// ARCHITECTURE:
/// - Multi-Tenancy: TenantId is the primary sharding key for Citus distribution
/// - Tiered Storage: Year and IsArchived columns enable hot/cold data separation
/// - Schema Evolution: JSONB metadata columns allow flexible schema without migrations
/// - Scalability: Designed for 50 years, 26B invoices, 10K transactions/minute
/// 
/// CITUS CONFIGURATION:
/// After initial creation, execute these SQL commands to enable distribution:
///   -- Enable Citus extension
///   CREATE EXTENSION IF NOT EXISTS citus;
///   
///   -- Create reference tables (small, replicated on all workers)
///   SELECT create_reference_table('Participants');
///   
///   -- Create distributed tables (partitioned by TenantId)
///   SELECT create_distributed_table('Invoices', 'TenantId');
///   SELECT create_distributed_table('InvoiceLineItems', 'TenantId');
///   SELECT create_distributed_table('PaymentRecords', 'TenantId');
/// 
/// QUERY PATTERNS:
/// All queries MUST include TenantId filter for proper shard routing:
///   var invoices = dbContext.Invoices
///       .Where(i => i.TenantId == tenantId) // REQUIRED for Citus!
///       .Where(i => i.Year >= 2023)
///       .ToList();
/// 
/// PERFORMANCE FEATURES:
/// - Composite indexes on (TenantId, Year) for range queries
/// - Composite indexes on (TenantId, InvoiceNumber) for lookups
/// - Indexes on foreign key columns for efficient joins
/// - Partial indexes on IsArchived for active data filtering
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    #region DbSets
    public DbSet<Invoice> Invoices { get; set; } = null!;
    public DbSet<Participant> Participants { get; set; } = null!;
    public DbSet<InvoiceLineItem> InvoiceLineItems { get; set; } = null!;
    public DbSet<PaymentRecord> PaymentRecords { get; set; } = null!;
    #endregion DbSets

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        #region Invoice Configuration
        modelBuilder.Entity<Invoice>(entity =>
        {
            // Primary Key
            entity.HasKey(e => e.Id);

            // Sharding Key for Citus
            entity.Property(e => e.TenantId)
                .IsRequired()
                .HasComment("Distribution key for PostgreSQL Citus sharding");

            // Year for tiered storage (hot/cold partitioning)
            entity.Property(e => e.Year)
                .IsRequired()
                .HasComment("Year of invoice issuance - used for range partitioning between hot and cold storage");

            // IsArchived for tiered storage queries
            entity.Property(e => e.IsArchived)
                .HasDefaultValue(false)
                .HasComment("Tiered storage flag: cold (archived) vs hot (current) data");

            // JSONB Metadata for schema flexibility
            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb")
                .IsRequired()
                .HasComment("JSONB column for flexible schema evolution without migrations");

            // Timestamps for audit trails
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasComment("Entity creation timestamp");

            entity.Property(e => e.ModifiedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasComment("Entity last modification timestamp");

            // Invoice-specific properties
            entity.Property(e => e.InvoiceNumber)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("Unique invoice identifier (e.g., INV-2024-0001)");

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>() // Store enum as string in DB for readability
                .HasComment("Invoice status: Draft, Issued, PartiallyPaid, Paid, Overdue, Voided");

            entity.Property(e => e.InvoiceType)
                .IsRequired()
                .HasConversion<string>()
                .HasComment("Invoice type: Standard, ProForma, Interim, Final, CreditNote, DebitNote");

            entity.Property(e => e.IssueDate)
                .IsRequired()
                .HasColumnType("timestamp with time zone");

            entity.Property(e => e.PaymentTermsDays)
                .IsRequired()
                .HasDefaultValue(30)
                .HasComment("Payment terms in days (e.g., Net 30, Net 60, Net 90)");

            // Relationships
            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey("SenderId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            entity.HasOne(e => e.Receiver)
                .WithMany()
                .HasForeignKey("ReceiverId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            // Collection navigation
            entity.HasMany<InvoiceLineItem>()
                .WithOne()
                .HasForeignKey(li => li.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany<PaymentRecord>()
                .WithOne()
                .HasForeignKey(pr => pr.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);

            #region Indexes for Citus Sharding and Performance
            // Composite index: (TenantId, InvoiceNumber) - for unique lookups within tenant
            entity.HasIndex(e => new { e.TenantId, e.InvoiceNumber })
                .IsUnique()
                .HasDatabaseName("IX_Invoices_TenantId_InvoiceNumber");

            // Composite index: (TenantId, Year) - for range queries (hot/cold partitioning)
            entity.HasIndex(e => new { e.TenantId, e.Year })
                .HasDatabaseName("IX_Invoices_TenantId_Year");

            // Composite index: (TenantId, Status) - for status-based queries
            entity.HasIndex(e => new { e.TenantId, e.Status })
                .HasDatabaseName("IX_Invoices_TenantId_Status");

            // Composite index: (TenantId, IsArchived, Year) - for active data queries
            entity.HasIndex(e => new { e.TenantId, e.IsArchived, e.Year })
                .HasDatabaseName("IX_Invoices_TenantId_IsArchived_Year");

            // Index on DueDate for aging analysis
            entity.HasIndex(e => new { e.TenantId, e.DueDate })
                .HasDatabaseName("IX_Invoices_TenantId_DueDate");

            // Index on foreign keys for efficient joins
            entity.HasIndex("SenderId")
                .HasDatabaseName("IX_Invoices_SenderId");

            entity.HasIndex("ReceiverId")
                .HasDatabaseName("IX_Invoices_ReceiverId");
            #endregion Indexes
        });
        #endregion Invoice Configuration

        #region Participant Configuration
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Sharding Key
            entity.Property(e => e.TenantId)
                .IsRequired()
                .HasComment("Distribution key for PostgreSQL Citus sharding");

            // Core properties
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(255)
                .HasComment("Participant name (Supplier, Customer, etc.)");

            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasComment("Email address");

            entity.Property(e => e.TaxId)
                .HasMaxLength(100)
                .HasComment("Tax ID / VAT Number");

            entity.Property(e => e.BankAccount)
                .HasMaxLength(100)
                .HasComment("Bank account number");

            entity.Property(e => e.Type)
                .IsRequired()
                .HasConversion<string>()
                .HasComment("Participant type: Supplier, Customer, etc.");

            // Address stored as JSON (struct value type flattened to columns)
            // Address properties are automatically flattened to separate columns with Address_ prefix
            // This is handled by default EF Core behavior for value types

            // Metadata for regional variants and custom attributes
            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb")
                .IsRequired()
                .HasComment("JSONB for flexible regional/custom participant data");

            // Audit timestamps
            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasComment("Entity creation timestamp");

            entity.Property(e => e.ModifiedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasComment("Entity last modification timestamp");

            entity.Property(e => e.IsArchived)
                .HasDefaultValue(false)
                .HasComment("Archive flag: true if participant is no longer active");

            #region Indexes for Participant
            // Composite index: (TenantId, Email) - for lookups
            entity.HasIndex(e => new { e.TenantId, e.Email })
                .HasDatabaseName("IX_Participants_TenantId_Email");

            // Composite index: (TenantId, TaxId) - for tax identification
            entity.HasIndex(e => new { e.TenantId, e.TaxId })
                .HasDatabaseName("IX_Participants_TenantId_TaxId");

            // Composite index: (TenantId, Name) - for search/filter by name
            entity.HasIndex(e => new { e.TenantId, e.Name })
                .HasDatabaseName("IX_Participants_TenantId_Name");

            // Filtered index: active participants only
            entity.HasIndex(e => new { e.TenantId, e.IsArchived })
                .HasDatabaseName("IX_Participants_TenantId_IsArchived");
            #endregion Indexes
        });
        #endregion Participant Configuration

        #region InvoiceLineItem Configuration
        modelBuilder.Entity<InvoiceLineItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Sharding Key
            entity.Property(e => e.TenantId)
                .IsRequired()
                .HasComment("Distribution key for PostgreSQL Citus sharding");

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(500)
                .HasComment("Line item description");

            entity.Property(e => e.Quantity)
                .IsRequired()
                .HasComment("Item quantity");

            entity.Property(e => e.UnitPrice)
                .IsRequired()
                .HasPrecision(18, 4)
                .HasComment("Unit price in invoice currency");

            entity.Property(e => e.InvoiceId)
                .IsRequired()
                .HasComment("Foreign key to invoice");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasComment("Entity creation timestamp");

            #region Indexes for InvoiceLineItem
            // Composite index: (InvoiceId, TenantId) for efficient item lookups
            entity.HasIndex(e => new { e.InvoiceId, e.TenantId })
                .HasDatabaseName("IX_InvoiceLineItems_InvoiceId_TenantId");

            // Index on TenantId (Citus shard key)
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_InvoiceLineItems_TenantId");
            #endregion Indexes
        });
        #endregion InvoiceLineItem Configuration

        #region PaymentRecord Configuration
        modelBuilder.Entity<PaymentRecord>(entity =>
        {
            entity.HasKey(e => e.Id);

            // Sharding Key
            entity.Property(e => e.TenantId)
                .IsRequired()
                .HasComment("Distribution key for PostgreSQL Citus sharding");

            entity.Property(e => e.DatePaid)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasComment("Date payment was received");

            entity.Property(e => e.Amount)
                .IsRequired()
                .HasPrecision(18, 4)
                .HasComment("Payment amount in invoice currency");

            entity.Property(e => e.Currency)
                .IsRequired()
                .HasMaxLength(3)
                .HasComment("ISO 4217 currency code (e.g., USD, EUR, GBP)");

            entity.Property(e => e.PaymentMethod)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("Payment method: BankTransfer, CreditCard, Check, PayPal, etc.");

            entity.Property(e => e.Reference)
                .HasMaxLength(255)
                .HasComment("Payment reference, transaction ID, check number, etc.");

            entity.Property(e => e.InvoiceId)
                .IsRequired()
                .HasComment("Foreign key to invoice");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasComment("Entity creation timestamp");

            #region Indexes for PaymentRecord
            // Composite index: (InvoiceId, TenantId) for invoice payment lookup
            entity.HasIndex(e => new { e.InvoiceId, e.TenantId })
                .HasDatabaseName("IX_PaymentRecords_InvoiceId_TenantId");

            // Composite index: (TenantId, DatePaid) for payment history range queries
            entity.HasIndex(e => new { e.TenantId, e.DatePaid })
                .HasDatabaseName("IX_PaymentRecords_TenantId_DatePaid");

            // Index on TenantId (Citus shard key)
            entity.HasIndex(e => e.TenantId)
                .HasDatabaseName("IX_PaymentRecords_TenantId");
            #endregion Indexes
        });
        #endregion PaymentRecord Configuration

        #region Global Configuration
        // Configure JSON serialization for EF Core 7+
        if (Database.IsNpgsql())
        {
            modelBuilder.HasPostgresExtension("uuid-ossp"); // For generating UUIDs in PostgreSQL
        }
        #endregion Global Configuration
    }

    /// <summary>
    /// Override SaveChangesAsync to automatically update ModifiedAt timestamps on Invoice entities.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Override SaveChanges to automatically update ModifiedAt timestamps.
    /// </summary>
    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.Entity is Invoice && e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.Entity is Invoice invoice)
            {
                invoice.ModifiedAt = DateTime.UtcNow;
            }
        }
    }
}
