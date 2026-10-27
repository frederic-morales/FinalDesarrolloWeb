using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CuentasPorPagarAPI.Application;

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string title;
        switch (exception)
        {
            case ApiException api:
                status = api.StatusCode;
                title = api.Message;
                break;
            case DbUpdateException { InnerException: PostgresException pg } when pg.SqlState == PostgresErrorCodes.UniqueViolation:
                status = StatusCodes.Status409Conflict;
                title = "Ya existe un registro con estos datos únicos.";
                break;
            case DbUpdateException { InnerException: PostgresException pg } when pg.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure:
                status = StatusCodes.Status409Conflict;
                title = "La operación coincidió con otra actualización. Inténtelo de nuevo.";
                break;
            case PostgresException pg when pg.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure:
                status = StatusCodes.Status409Conflict;
                title = "La operación coincidió con otra actualización. Inténtelo de nuevo.";
                break;
            default:
                logger.LogError(exception, "Error no controlado en {Path}", context.Request.Path);
                status = StatusCodes.Status500InternalServerError;
                title = "Ocurrió un error interno.";
                break;
        }

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Instance = context.Request.Path
        }, cancellationToken);
        return true;
    }
}
