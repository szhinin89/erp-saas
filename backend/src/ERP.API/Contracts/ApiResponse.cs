namespace ERP.API.Contracts;

public record ApiResponse<T>(
    bool Success,
    string Message,
    T ResponseObject
);

