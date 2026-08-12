using System.ComponentModel.DataAnnotations;

namespace LankaSaaS.Application;

public record RegisterRequest([Required,MaxLength(120)] string BusinessName,[Required,EmailAddress] string Email,[Required,MinLength(8)] string Password,[Required,MaxLength(60)] string FirstName,[Required,MaxLength(60)] string LastName,string? Phone,string? Address);
public record LoginRequest([Required,EmailAddress] string Email,[Required] string Password);
public record AuthResponse(string AccessToken,DateTimeOffset ExpiresAt,UserDto User);
public record UserDto(Guid Id,string FirstName,string LastName,string Email,string Role);
public record TeamUserDto(Guid Id,string FirstName,string LastName,string Email,string Role,bool IsActive,DateTimeOffset CreatedAt);
public record CreateTeamUserRequest([Required,MaxLength(60)] string FirstName,[Required,MaxLength(60)] string LastName,[Required,EmailAddress] string Email,[Required,MinLength(8)] string Password,[Required] string Role);
public record UpdateTeamUserRequest([Required,MaxLength(60)] string FirstName,[Required,MaxLength(60)] string LastName,[Required] string Role,bool IsActive);
public record ResetUserPasswordRequest([Required,MinLength(8)] string NewPassword);
public record CustomerRequest([Required,MaxLength(160)] string Name,string? Phone,[EmailAddress] string? Email,string? Address);
public record CustomerDto(Guid Id,string Name,string? Phone,string? Email,string? Address,DateTimeOffset CreatedAt);
public record ProductRequest([Required,MaxLength(160)] string Name,[Required,MaxLength(80)] string SKU,string? Description,[Range(0,double.MaxValue)] decimal SellingPrice,[Range(0,double.MaxValue)] decimal CostPrice,[Range(0,int.MaxValue)] int StockQuantity,bool IsActive=true);
public record ProductDto(Guid Id,string Name,string SKU,string? Description,decimal SellingPrice,decimal CostPrice,int StockQuantity,bool IsActive,DateTimeOffset CreatedAt);
public record ExpenseRequest([Required,MaxLength(300)] string Description,[Range(0.01,double.MaxValue)] decimal Amount,DateOnly ExpenseDate,[Required,MaxLength(80)] string Category);
public record ExpenseDto(Guid Id,string Description,decimal Amount,DateOnly ExpenseDate,string Category,DateTimeOffset CreatedAt);
public record InvoiceItemRequest(Guid? ProductId,[Required,MaxLength(300)] string Description,[Range(0.01,999999)] decimal Quantity,[Range(0,double.MaxValue)] decimal UnitPrice,[Range(0,double.MaxValue)] decimal Discount,[Range(0,100)] decimal TaxRate);
public record InvoiceRequest(Guid CustomerId,DateOnly IssueDate,DateOnly DueDate,[MaxLength(1000)] string? Notes,[MinLength(1)] List<InvoiceItemRequest> Items);
public record InvoiceStatusRequest([Required] string Status);
public record InvoiceItemDto(Guid Id,Guid? ProductId,string Description,decimal Quantity,decimal UnitPrice,decimal Discount,decimal TaxRate,decimal LineSubtotal,decimal LineTotal);
public record InvoiceDto(Guid Id,string InvoiceNumber,Guid CustomerId,string CustomerName,DateOnly IssueDate,DateOnly DueDate,string Status,decimal Subtotal,decimal DiscountTotal,decimal TaxTotal,decimal Total,string? Notes,List<InvoiceItemDto> Items,DateTimeOffset CreatedAt);
public record InvoiceListDto(Guid Id,string InvoiceNumber,string CustomerName,DateOnly IssueDate,DateOnly DueDate,string Status,decimal Total);
public record DashboardDto(decimal TotalSales,decimal TotalExpenses,int Customers,int Products);
public record CompanySettingsDto(string BusinessName,string Email,string? Phone,string? Address,string? TaxRegistrationNumber,string InvoicePrefix,int NextInvoiceNumber,int DefaultPaymentTermsDays,decimal DefaultTaxRate,string? InvoiceFooter,string? PaymentInstructions,string? LogoUrl);
public record UpdateCompanySettingsRequest([Required,MaxLength(160)] string BusinessName,[Required,EmailAddress] string Email,[MaxLength(40)] string? Phone,[MaxLength(500)] string? Address,[MaxLength(80)] string? TaxRegistrationNumber,[Required,RegularExpression("^[A-Za-z0-9-]{1,12}$")] string InvoicePrefix,[Range(0,365)] int DefaultPaymentTermsDays,[Range(0,100)] decimal DefaultTaxRate,[MaxLength(500)] string? InvoiceFooter,[MaxLength(1000)] string? PaymentInstructions,[Url,MaxLength(500)] string? LogoUrl);
public record LoginActivityUserDto(Guid UserId,string Name,string Email,long TotalLogins,DateTimeOffset? LastLoginAt);
public record RecentLoginDto(Guid Id,Guid UserId,string Name,string Email,DateTimeOffset LoggedInAt);
public record LoginActivityDto(long TotalLogins,int LoginsLast30Days,int ActiveUsersLast30Days,List<LoginActivityUserDto> Users,List<RecentLoginDto> RecentLogins);

public interface ITenantContext { Guid TenantId { get; } Guid UserId { get; } bool IsAuthenticated { get; } }
