using System.Text;
using ClaudeLog.Data.Services;
using LogLevel = ClaudeLog.Data.Models.LogLevel;

namespace ClaudeLog.Web.Api;

public static class CoreDataEndpoints
{
    public static void MapCoreDataEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/export/core", ExportCoreData);
        app.MapPost("/api/import/core", ImportCoreData)
            .DisableAntiforgery();
    }

    private static async Task<IResult> ExportCoreData(
        CoreDataTransferService coreDataTransferService,
        DiagnosticsService diagnosticsService,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = await coreDataTransferService.ExportJsonAsync(cancellationToken);
            var bytes = Encoding.UTF8.GetBytes(json);
            var fileName = $"ClaudeLog-core-export-{DateTime.Now:yyyyMMdd-HHmmss}.json";
            return Results.File(bytes, "application/json", fileName);
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.ExportCoreData", ex.Message, LogLevel.Error, ex.StackTrace ?? "");
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> ImportCoreData(
        IFormFile? file,
        CoreDataTransferService coreDataTransferService,
        DiagnosticsService diagnosticsService,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "A non-empty import file is required." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var summary = await coreDataTransferService.ImportAsync(stream, cancellationToken);
            return Results.Ok(summary);
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.ImportCoreData", ex.Message, LogLevel.Error, ex.StackTrace ?? "");
            return Results.Problem(ex.Message);
        }
    }
}
