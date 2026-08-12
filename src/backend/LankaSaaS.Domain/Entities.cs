namespace LankaSaaS.Domain;

public abstract class Entity { public Guid Id { get; set; } = Guid.NewGuid(); public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow; }
public interface ITenantOwned { Guid TenantId { get; set; } }
public sealed class Tenant : Entity { public required string Name { get; set; } public required string BusinessName { get; set; } public required string Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } public string? TaxRegistrationNumber { get; set; } public string InvoicePrefix { get; set; } = "INV"; public int NextInvoiceNumber { get; set; } = 1; public int DefaultPaymentTermsDays { get; set; } = 14; public decimal DefaultTaxRate { get; set; } public string? InvoiceFooter { get; set; } public string? PaymentInstructions { get; set; } public string? LogoUrl { get; set; } }
public sealed class User : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string FirstName { get; set; } public required string LastName { get; set; } public required string Email { get; set; } public required string PasswordHash { get; set; } public string Role { get; set; } = Roles.Staff; public bool IsActive { get; set; } = true; public long LoginCount { get; set; } public DateTimeOffset? LastLoginAt { get; set; } }
public sealed class LoginEvent : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid UserId { get; set; } }
public sealed class Customer : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Name { get; set; } public string? Phone { get; set; } public string? Email { get; set; } public string? Address { get; set; } }
public sealed class Product : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Name { get; set; } public required string SKU { get; set; } public string? Description { get; set; } public decimal SellingPrice { get; set; } public decimal CostPrice { get; set; } public int StockQuantity { get; set; } public bool IsActive { get; set; } = true; }
public sealed class Expense : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Description { get; set; } public decimal Amount { get; set; } public DateOnly ExpenseDate { get; set; } public required string Category { get; set; } }
public sealed class RefreshToken : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid UserId { get; set; } public required string TokenHash { get; set; } public DateTimeOffset ExpiresAt { get; set; } public DateTimeOffset? RevokedAt { get; set; } }
public sealed class Invoice : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public required string InvoiceNumber { get; set; }
    public required string CustomerName { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public List<InvoiceItem> Items { get; set; } = [];
}
public sealed class InvoiceItem : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid? ProductId { get; set; }
    public required string Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineSubtotal { get; set; }
    public decimal LineTotal { get; set; }
}
public enum InvoiceStatus { Draft, Issued, Paid, Overdue, Cancelled }
public static class Roles { public const string Admin = "Admin"; public const string Staff = "Staff"; }
