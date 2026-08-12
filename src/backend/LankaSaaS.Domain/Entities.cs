namespace LankaSaaS.Domain;

public abstract class Entity { public Guid Id { get; set; } = Guid.NewGuid(); public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow; }
public interface ITenantOwned { Guid TenantId { get; set; } }
public sealed class Tenant : Entity { public required string Name { get; set; } public required string BusinessName { get; set; } public required string Email { get; set; } public string? Phone { get; set; } public string? Address { get; set; } }
public sealed class User : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string FirstName { get; set; } public required string LastName { get; set; } public required string Email { get; set; } public required string PasswordHash { get; set; } public string Role { get; set; } = Roles.Staff; public bool IsActive { get; set; } = true; }
public sealed class Customer : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Name { get; set; } public string? Phone { get; set; } public string? Email { get; set; } public string? Address { get; set; } }
public sealed class Product : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Name { get; set; } public required string SKU { get; set; } public string? Description { get; set; } public decimal SellingPrice { get; set; } public decimal CostPrice { get; set; } public int StockQuantity { get; set; } public bool IsActive { get; set; } = true; }
public sealed class Expense : Entity, ITenantOwned { public Guid TenantId { get; set; } public required string Description { get; set; } public decimal Amount { get; set; } public DateOnly ExpenseDate { get; set; } public required string Category { get; set; } }
public sealed class RefreshToken : Entity, ITenantOwned { public Guid TenantId { get; set; } public Guid UserId { get; set; } public required string TokenHash { get; set; } public DateTimeOffset ExpiresAt { get; set; } public DateTimeOffset? RevokedAt { get; set; } }
public static class Roles { public const string Admin = "Admin"; public const string Staff = "Staff"; }
