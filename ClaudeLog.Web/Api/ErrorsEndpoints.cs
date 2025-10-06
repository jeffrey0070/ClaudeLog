using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

namespace ClaudeLog.Web.Api;

public static class ErrorsEndpoints
{
    public static void MapErrorsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/errors", LogError);
    }

    private static async Task<IResult> LogError(
        LogErrorRequest request,
        ErrorRepository repository)
    {
        try
        {
            var response = await repository.LogErrorAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
