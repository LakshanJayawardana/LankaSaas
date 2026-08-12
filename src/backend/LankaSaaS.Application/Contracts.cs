using System.ComponentModel.DataAnnotations;

namespace LankaSaaS.Application;

public record RegisterRequest([Required,MaxLength(120)] string BusinessName,[Required,EmailAddress] string Email,[Required,MinLength(8)] string Password,[Required,MaxLength(60)] string FirstName,[Required,MaxLength(60)] string LastName,string? Phone,string? Address);
public record LoginRequest([Required,EmailAddress] string Email,[Required] string Password);
public record RefreshRequest([Required] string RefreshToken);
public record AuthResponse(string AccessToken,string RefreshToken,DateTimeOffset ExpiresAt,UserDto User);
public record UserDto(Guid Id,string FirstName,string LastName,string Email,string Role);
public record CustomerRequest([Required,MaxLength(160)] string Name,string? Phone,[EmailAddress] string? Email,string? Address);
public record CustomerDto(Guid Id,string Name,string? Phone,string? Email,string? Address,DateTimeOffset CreatedAt);
public record ProductRequest([Required,MaxLength(160)] string Name,[Required,MaxLength(80)] string SKU,string? Description,[Range(0,double.MaxValue)] decimal SellingPrice,[Range(0,double.MaxValue)] decimal CostPrice,[Range(0,int.MaxValue)] int StockQuantity,bool IsActive=true);
public record ProductDto(Guid Id,string Name,string SKU,string? Description,decimal SellingPrice,decimal CostPrice,int StockQuantity,bool IsActive,DateTimeOffset CreatedAt);
public record ExpenseRequest([Required,MaxLength(300)] string Description,[Range(0.01,double.MaxValue)] decimal Amount,DateOnly ExpenseDate,[Required,MaxLength(80)] string Category);
public record ExpenseDto(Guid Id,string Description,decimal Amount,DateOnly ExpenseDate,string Category,DateTimeOffset CreatedAt);

public interface ITenantContext { Guid TenantId { get; } Guid UserId { get; } bool IsAuthenticated { get; } }
