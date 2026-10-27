using System.ComponentModel.DataAnnotations;

namespace CuentasPorPagarAPI.Contracts;

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public sealed record LoginRequest(
    [param: Required] string UserName,
    [param: Required] string Password);

public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc, UserResponse User);
public sealed record UserResponse(Guid Id, string UserName, string Role, bool IsActive);

public sealed record CreateUserRequest(
    [param: Required, StringLength(80)] string UserName,
    [param: Required, MinLength(12)] string Password,
    [param: Required] string Role);

public sealed record ResetPasswordRequest([param: Required, MinLength(12)] string Password);
public sealed record UserStatusRequest(bool IsActive);

public sealed record SupplierRequest(
    [param: Required, StringLength(200)] string Name,
    [param: StringLength(30)] string? Nit,
    [param: StringLength(150)] string? ContactName,
    [param: EmailAddress, StringLength(200)] string? Email,
    [param: StringLength(40)] string? Phone,
    [param: StringLength(400)] string? Address);

public sealed record SupplierResponse(
    int Id, string Name, string? Nit, string? ContactName, string? Email,
    string? Phone, string? Address, bool IsActive);
public sealed record SupplierStatusRequest(bool IsActive);

public sealed record PaymentMethodRequest([param: Required, StringLength(80)] string Name, bool RequiresCheckDetails = false);
public sealed record PaymentMethodResponse(int Id, string Name, bool IsActive, bool RequiresCheckDetails);
public sealed record PaymentMethodStatusRequest(bool IsActive);

public sealed record InvoiceLineRequest(
    [param: Required, StringLength(300)] string Description,
    decimal Quantity,
    decimal UnitPrice);

public sealed record InvoiceRequest(
    int SupplierId,
    [param: Required, StringLength(30)] string Series,
    [param: Required, StringLength(50)] string Number,
    DateOnly IssueDate,
    DateOnly DueDate,
    [param: Required] List<InvoiceLineRequest> Lines);

public sealed record InvoiceLineResponse(int Id, string Description, decimal Quantity, decimal UnitPrice, decimal Subtotal);

public sealed record InvoiceResponse(
    int Id, int SupplierId, string SupplierName, string Series, string Number,
    DateOnly IssueDate, DateOnly DueDate, decimal Total, decimal Paid, decimal Balance,
    string Status, IReadOnlyList<InvoiceLineResponse> Lines, decimal Debit, decimal Credit);

public sealed record InvoiceListResponse(
    int Id, int SupplierId, string SupplierName, string Series, string Number,
    DateOnly IssueDate, DateOnly DueDate, decimal Total, decimal Paid, decimal Balance,
    string Status, bool IsOverdue, decimal Debit, decimal Credit);

public sealed record PaymentAllocationRequest(int InvoiceId, decimal Amount);
public sealed record CheckDetailRequest(
    [param: Required, StringLength(120)] string BankName,
    [param: Required, StringLength(60)] string CheckNumber,
    DateOnly? ClearedOn);
public sealed record CheckDetailResponse(string BankName, string CheckNumber, DateOnly? ClearedOn);

public sealed record PaymentRequest(
    int SupplierId,
    int PaymentMethodId,
    DateOnly Date,
    [param: StringLength(100)] string? Reference,
    decimal Amount,
    [param: Required] List<PaymentAllocationRequest> Allocations,
    CheckDetailRequest? Check);

public sealed record PaymentAllocationResponse(int InvoiceId, string InvoiceNumber, decimal Amount);

public sealed record PaymentResponse(
    int Id, int SupplierId, string SupplierName, int PaymentMethodId, string PaymentMethodName,
    DateOnly Date, string? Reference, decimal Amount, string Status,
    IReadOnlyList<PaymentAllocationResponse> Allocations, CheckDetailResponse? Check);

public sealed record PaymentListResponse(
    int Id, int SupplierId, string SupplierName, string PaymentMethodName,
    DateOnly Date, string? Reference, decimal Amount, string Status);

public sealed record AdjustmentAllocationRequest(int InvoiceId, decimal Amount);
public sealed record AdjustmentRequest(
    int SupplierId, string Kind,
    [param: Required, StringLength(30)] string Series,
    [param: Required, StringLength(50)] string Number,
    DateOnly Date,
    [param: Required, StringLength(300)] string Description,
    decimal Amount,
    [param: Required] List<AdjustmentAllocationRequest> Allocations);
public sealed record AdjustmentAllocationResponse(int InvoiceId, string InvoiceNumber, decimal Amount);
public sealed record AdjustmentResponse(
    int Id, int SupplierId, string SupplierName, string Kind, string Series, string Number,
    DateOnly Date, string Description, decimal Amount, string Status,
    IReadOnlyList<AdjustmentAllocationResponse> Allocations);
public sealed record AdjustmentListResponse(
    int Id, int SupplierId, string SupplierName, string Kind, string Series, string Number,
    DateOnly Date, decimal Amount, string Status);

public sealed record StatementMovement(DateOnly Date, string Type, int DocumentId, string Description,
    decimal Charge, decimal Credit, decimal RunningBalance);

public sealed record StatementResponse(int SupplierId, string SupplierName, DateOnly From, DateOnly To,
    decimal OpeningBalance, decimal ClosingBalance, IReadOnlyList<StatementMovement> Movements);

public sealed record DashboardResponse(int ActiveSuppliers, int PendingInvoices, int OverdueInvoices,
    decimal TotalOutstanding, decimal TotalOverdue);
public sealed record SupplierBalanceResponse(
    int SupplierId, string SupplierName, DateOnly AsOf, int PendingInvoices,
    decimal Outstanding, decimal Overdue);
