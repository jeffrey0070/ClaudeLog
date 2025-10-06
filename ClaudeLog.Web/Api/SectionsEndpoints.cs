using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

namespace ClaudeLog.Web.Api;

public static class SectionsEndpoints
{
    public static void MapSectionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sections");

        group.MapPost("/", CreateSection);
        group.MapGet("/", GetSections);
        group.MapPatch("/{sectionId}/deleted", UpdateSectionDeleted);
    }

    private static async Task<IResult> CreateSection(
        CreateSectionRequest request,
        SectionRepository repository)
    {
        try
        {
            var response = await repository.CreateAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetSections(
        SectionRepository repository,
        int days = 30,
        int page = 1,
        int pageSize = 50,
        bool includeDeleted = false)
    {
        try
        {
            var sections = await repository.GetSectionsAsync(days, page, pageSize, includeDeleted);
            return Results.Ok(sections);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateSectionDeleted(
        Guid sectionId,
        UpdateSectionDeletedRequest request,
        SectionRepository repository)
    {
        try
        {
            await repository.UpdateDeletedAsync(sectionId, request.IsDeleted);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
