namespace LankaSaaS.Domain;

public abstract class Entity { public Guid Id { get; set; } = Guid.NewGuid(); public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow; }
public interface ITenantOwned { Guid TenantId { get; set; } }
public sealed class Tenant : Entity { public required string Name { get; set; } public required string BusinessName { get; set; } public required string Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } public string? TaxRegistrationNumber { get; set; } public string InvoicePrefix { get; set; } = "INV"; public int NextInvoiceNumber { get; set; } = 1; public int DefaultPaymentTermsDays { get; set; } = 14; public decimal DefaultTaxRate { get; set; } public string? InvoiceFooter { get; set; } public string? PaymentInstructions { get; set; } public string? LogoUrl { get; set; } public string SubscriptionPlan { get; set; } = SubscriptionPlans.Trial; public string SubscriptionStatus { get; set; } = SubscriptionStatuses.Trialing; public int UserLimit { get; set; } = 3; public DateTimeOffset? TrialEndsAt { get; set; } = DateTimeOffset.UtcNow.AddDays(14); public DateTimeOffset? SubscriptionEndsAt { get; set; } public DateTimeOffset? GraceEndsAt { get; set; } public string? PayHereSubscriptionId { get; set; } public DateTimeOffset? CancellationRequestedAt { get; set; } }
public sealed class User : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string FirstName { get; set; } public required string LastName { get; set; } public required string Email { get; set; } public required string PasswordHash { get; set; } public string Role { get; set; } = Roles.Staff; public bool IsActive { get; set; } = true; public long LoginCount { get; set; } public DateTimeOffset? LastLoginAt { get; set; } }
public sealed class LoginEvent : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid UserId { get; set; } }
public sealed class PaymentOrder : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string OrderId { get; set; } public required string Plan { get; set; } public decimal Amount { get; set; } public string Currency { get; set; } = "LKR"; public string Status { get; set; } = "Pending"; }
public sealed class PaymentTransaction : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid PaymentOrderId { get; set; } public required string ProviderPaymentId { get; set; } public decimal Amount { get; set; } public required string Currency { get; set; } public required string StatusCode { get; set; } public string? PaymentMethod { get; set; } }
public sealed class Customer : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Name { get; set; } public string? Phone { get; set; } public string? Email { get; set; } public string? Address { get; set; } }
public sealed class Product : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Name { get; set; } public required string SKU { get; set; } public string? Description { get; set; } public decimal SellingPrice { get; set; } public decimal CostPrice { get; set; } public int StockQuantity { get; set; } public bool IsActive { get; set; } = true; }
public sealed class Expense : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid? EventId { get; set; } public Guid? PurchaseOrderId { get; set; } public required string Description { get; set; } public decimal Amount { get; set; } public DateOnly ExpenseDate { get; set; } public required string Category { get; set; } }
public sealed class BusinessEvent : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid CustomerId { get; set; } public required string CustomerName { get; set; } public required string Name { get; set; } public required string Venue { get; set; } public DateTimeOffset StartsAt { get; set; } public DateTimeOffset EndsAt { get; set; } public string Status { get; set; } = EventStatuses.Planning; public decimal BudgetedRevenue { get; set; } public decimal BudgetedCost { get; set; } public string? Notes { get; set; } }
public sealed class LogisticsResource : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Name { get; set; } public required string Type { get; set; } public string? Identifier { get; set; } public int TotalQuantity { get; set; } = 1; public string Status { get; set; } = ResourceStatuses.Available; public string? Notes { get; set; } }
public sealed class EventResourceAllocation : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid EventId { get; set; } public Guid ResourceId { get; set; } public required string ResourceName { get; set; } public int Quantity { get; set; } public string Status { get; set; } = AllocationStatuses.Reserved; public int ReturnedQuantity { get; set; } public int DamagedQuantity { get; set; } public int MissingQuantity { get; set; } }
public sealed class EventChecklistItem : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid EventId { get; set; } public required string Description { get; set; } public bool IsCompleted { get; set; } public Guid? CompletedByUserId { get; set; } public DateTimeOffset? CompletedAt { get; set; } }
public sealed class Supplier : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Name { get; set; } public string? ContactName { get; set; } public string? Phone { get; set; } public string? Email { get; set; } public string? Address { get; set; } }
public sealed class PurchaseOrder : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid SupplierId { get; set; } public required string SupplierName { get; set; } public Guid? EventId { get; set; } public string Type { get; set; } = PurchaseOrderTypes.Purchase; public string Status { get; set; } = PurchaseOrderStatuses.Draft; public DateOnly OrderDate { get; set; } public DateOnly? RentalStartDate { get; set; } public DateOnly? RentalEndDate { get; set; } public decimal Total { get; set; } public string? Notes { get; set; } public List<PurchaseOrderItem> Items { get; set; }=[]; }
public sealed class PurchaseOrderItem : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid PurchaseOrderId { get; set; } public Guid? ResourceId { get; set; } public required string Description { get; set; } public decimal Quantity { get; set; } public decimal UnitCost { get; set; } public decimal LineTotal { get; set; } }
public sealed class SupplierPayment : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid PurchaseOrderId { get; set; } public decimal Amount { get; set; } public DateOnly PaymentDate { get; set; } public required string Method { get; set; } public string? Reference { get; set; } }
public sealed class EventQuotation : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid EventId { get; set; } public required string QuotationNumber { get; set; } public string Status { get; set; }=QuotationStatuses.Draft; public DateOnly IssueDate { get; set; } public DateOnly ValidUntil { get; set; } public decimal Total { get; set; } public decimal DepositRequired { get; set; } public string? Notes { get; set; } public List<EventQuotationItem> Items { get; set; }=[]; }
public sealed class EventQuotationItem : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid EventQuotationId { get; set; } public required string Description { get; set; } public decimal Quantity { get; set; } public decimal UnitPrice { get; set; } public decimal LineTotal { get; set; } }
public sealed class CustomerPayment : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid EventId { get; set; } public Guid InvoiceId { get; set; } public decimal Amount { get; set; } public DateOnly PaymentDate { get; set; } public required string Method { get; set; } public string? Reference { get; set; } public bool IsDeposit { get; set; } }
public sealed class RefreshToken : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid UserId { get; set; } public required string TokenHash { get; set; } public DateTimeOffset ExpiresAt { get; set; } public DateTimeOffset? RevokedAt { get; set; } }
public sealed class Invoice : Entity, ITenantOwned
{
    public Guid TenantId { get; set; }
    public Guid? EventId { get; set; }
    public Guid? QuotationId { get; set; }
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
public static class SubscriptionPlans { public const string Trial = "Trial"; public const string Starter = "Starter"; public const string Growth = "Growth"; public const string Business = "Business"; }
public static class SubscriptionStatuses { public const string Trialing = "Trialing"; public const string Active = "Active"; public const string PastDue = "PastDue"; public const string Cancelled = "Cancelled"; public const string Expired = "Expired"; }
public static class EventStatuses { public const string Planning="Planning"; public const string Confirmed="Confirmed"; public const string InProgress="InProgress"; public const string Completed="Completed"; public const string Cancelled="Cancelled"; }
public static class ResourceStatuses { public const string Available="Available"; public const string Maintenance="Maintenance"; public const string Retired="Retired"; }
public static class AllocationStatuses { public const string Reserved="Reserved"; public const string Dispatched="Dispatched"; public const string Returned="Returned"; public const string Cancelled="Cancelled"; }
public static class PurchaseOrderTypes { public const string Purchase="Purchase"; public const string Rental="Rental"; }
public static class PurchaseOrderStatuses { public const string Draft="Draft"; public const string Ordered="Ordered"; public const string Received="Received"; public const string Cancelled="Cancelled"; }
public static class QuotationStatuses { public const string Draft="Draft"; public const string Sent="Sent"; public const string Accepted="Accepted"; public const string Rejected="Rejected"; public const string Converted="Converted"; }
