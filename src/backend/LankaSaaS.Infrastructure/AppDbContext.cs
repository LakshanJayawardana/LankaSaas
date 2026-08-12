using LankaSaaS.Application;
using LankaSaaS.Domain;
using Microsoft.EntityFrameworkCore;

namespace LankaSaaS.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenant) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>(); public DbSet<User> Users => Set<User>(); public DbSet<LoginEvent> LoginEvents => Set<LoginEvent>(); public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>(); public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>(); public DbSet<Customer> Customers => Set<Customer>(); public DbSet<Product> Products => Set<Product>(); public DbSet<Expense> Expenses => Set<Expense>(); public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>(); public DbSet<Invoice> Invoices => Set<Invoice>(); public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>().HasIndex(x=>x.Email).IsUnique(); b.Entity<User>().HasIndex(x=>x.Email).IsUnique();
        b.Entity<User>().HasQueryFilter(x=>tenant.IsAuthenticated && x.TenantId==tenant.TenantId);
        b.Entity<LoginEvent>().HasQueryFilter(x=>tenant.IsAuthenticated && x.TenantId==tenant.TenantId); b.Entity<LoginEvent>().HasIndex(x=>new{x.TenantId,x.CreatedAt}); b.Entity<LoginEvent>().HasIndex(x=>x.UserId);
        b.Entity<PaymentOrder>().HasQueryFilter(x=>tenant.IsAuthenticated&&x.TenantId==tenant.TenantId); b.Entity<PaymentOrder>().HasIndex(x=>x.OrderId).IsUnique();
        b.Entity<PaymentTransaction>().HasQueryFilter(x=>tenant.IsAuthenticated&&x.TenantId==tenant.TenantId); b.Entity<PaymentTransaction>().HasIndex(x=>x.ProviderPaymentId).IsUnique(); b.Entity<PaymentTransaction>().HasIndex(x=>x.PaymentOrderId);
        b.Entity<Customer>().HasQueryFilter(x=>tenant.IsAuthenticated && x.TenantId==tenant.TenantId);
        b.Entity<Product>().HasQueryFilter(x=>tenant.IsAuthenticated && x.TenantId==tenant.TenantId); b.Entity<Product>().HasIndex(x=>new{x.TenantId,x.SKU}).IsUnique();
        b.Entity<Expense>().HasQueryFilter(x=>tenant.IsAuthenticated && x.TenantId==tenant.TenantId);
        b.Entity<RefreshToken>().HasQueryFilter(x=>tenant.IsAuthenticated && x.TenantId==tenant.TenantId);
        b.Entity<Invoice>().HasQueryFilter(x=>tenant.IsAuthenticated && x.TenantId==tenant.TenantId); b.Entity<Invoice>().HasIndex(x=>new{x.TenantId,x.InvoiceNumber}).IsUnique();
        b.Entity<Invoice>().HasMany(x=>x.Items).WithOne().HasForeignKey(x=>x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<InvoiceItem>().HasQueryFilter(x=>tenant.IsAuthenticated && x.TenantId==tenant.TenantId);
        foreach(var p in b.Model.GetEntityTypes().SelectMany(e=>e.GetProperties()).Where(p=>p.ClrType==typeof(decimal)))
        {
            p.SetPrecision(18);
            p.SetScale(2);
        }
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct=default)
    {
        foreach(var e in ChangeTracker.Entries<ITenantOwned>().Where(e=>e.State==EntityState.Added)) { if(tenant.IsAuthenticated) e.Entity.TenantId=tenant.TenantId; else if(e.Entity.TenantId==Guid.Empty) throw new UnauthorizedAccessException(); }
        foreach(var e in ChangeTracker.Entries<Entity>().Where(e=>e.State==EntityState.Modified)) e.Entity.UpdatedAt=DateTimeOffset.UtcNow;
        return base.SaveChangesAsync(ct);
    }
}
