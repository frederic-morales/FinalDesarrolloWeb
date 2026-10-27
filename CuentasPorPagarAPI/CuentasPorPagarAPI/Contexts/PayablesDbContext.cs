using CuentasPorPagarAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace CuentasPorPagarAPI.Contexts;

public sealed class PayablesDbContext(DbContextOptions<PayablesDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => x.UserName).IsUnique();
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Nit).HasMaxLength(30);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ContactName).HasMaxLength(150);
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.Phone).HasMaxLength(40);
            entity.Property(x => x.Address).HasMaxLength(400);
            entity.HasIndex(x => x.Nit).IsUnique().HasFilter("\"Nit\" IS NOT NULL");
            entity.HasIndex(x => x.Name);
        });

        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasData(
                new PaymentMethod { Id = 1, Name = "Efectivo" },
                new PaymentMethod { Id = 2, Name = "Transferencia" },
                new PaymentMethod { Id = 3, Name = "Cheque" });
        });

        modelBuilder.Entity<PurchaseInvoice>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Series).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Number).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Total).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => new { x.SupplierId, x.Series, x.Number }).IsUnique();
            entity.HasIndex(x => x.DueDate);
            entity.ToTable(t => t.HasCheckConstraint("CK_PurchaseInvoice_Total", "\"Total\" > 0"));
            entity.HasOne(x => x.Supplier).WithMany(x => x.Invoices).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Description).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 3);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.ToTable(t => t.HasCheckConstraint("CK_InvoiceLine_Values", "\"Quantity\" > 0 AND \"UnitPrice\" > 0 AND \"Subtotal\" > 0"));
            entity.HasOne(x => x.PurchaseInvoice).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reference).HasMaxLength(100);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.ToTable(t => t.HasCheckConstraint("CK_Payment_Amount", "\"Amount\" > 0"));
            entity.HasIndex(x => new { x.SupplierId, x.Date });
            entity.HasOne(x => x.Supplier).WithMany(x => x.Payments).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PaymentMethod).WithMany().HasForeignKey(x => x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RecordedByUser).WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PaymentAllocation>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.ToTable(t => t.HasCheckConstraint("CK_PaymentAllocation_Amount", "\"Amount\" > 0"));
            entity.HasIndex(x => new { x.PaymentId, x.PurchaseInvoiceId }).IsUnique();
            entity.HasIndex(x => x.PurchaseInvoiceId);
            entity.HasOne(x => x.Payment).WithMany(x => x.Allocations).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PurchaseInvoice).WithMany(x => x.Allocations).HasForeignKey(x => x.PurchaseInvoiceId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
