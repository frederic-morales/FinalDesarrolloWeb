using System.Text;
using CuentasPorPagarAPI.Application;
using CuentasPorPagarAPI.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CuentasPorPagarAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/reports")]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardResponse>> Dashboard(CancellationToken cancellationToken) =>
        Ok(await reports.DashboardAsync(cancellationToken));

    [HttpGet("payables")]
    public async Task<ActionResult<PageResult<InvoiceListResponse>>> Payables(
        [FromQuery] DateOnly? asOf, [FromQuery] int? supplierId, [FromQuery] bool overdueOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await reports.PayablesAsync(asOf ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-6)),
            supplierId, overdueOnly, page, pageSize, cancellationToken));

    [HttpGet("suppliers/{supplierId:int}/statement")]
    public async Task<ActionResult<StatementResponse>> Statement(
        int supplierId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await reports.StatementAsync(supplierId, from, to, cancellationToken));

    [HttpGet("suppliers/{supplierId:int}/statement.csv")]
    public async Task<IActionResult> StatementCsv(
        int supplierId, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var statement = await reports.StatementAsync(supplierId, from, to, cancellationToken);
        var csv = new StringBuilder("Fecha,Tipo,Documento,Descripción,Cargo,Abono,Saldo\r\n");
        foreach (var row in statement.Movements)
        {
            csv.Append(row.Date.ToString("yyyy-MM-dd")).Append(',')
                .Append(Escape(row.Type)).Append(',').Append(row.DocumentId).Append(',')
                .Append(Escape(row.Description)).Append(',')
                .Append(row.Charge.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(row.Credit.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(row.RunningBalance.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
        }
        return File(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"estado-cuenta-{supplierId}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
