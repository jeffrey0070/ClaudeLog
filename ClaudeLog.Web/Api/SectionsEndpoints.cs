using ClaudeLog.Web.Api.Dtos;
using ClaudeLog.Web.Data;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Web.Api;

public static class SectionsEndpoints
{
    public static void MapSectionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sections");

        group.MapPost("/", CreateSection);
        group.MapGet("/", GetSections);
    }

    private static async Task<IResult> CreateSection(
        CreateSectionRequest request,
        Db db)
    {
        try
        {
            var sectionId = string.IsNullOrWhiteSpace(request.SectionId)
                ? Guid.NewGuid()
                : Guid.Parse(request.SectionId);

            var createdAt = request.CreatedAt ?? DateTime.Now;

            using var conn = db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(Queries.InsertSection, conn);
            cmd.Parameters.AddWithValue("@SectionId", sectionId);
            cmd.Parameters.AddWithValue("@Tool", request.Tool);
            cmd.Parameters.AddWithValue("@CreatedAt", createdAt);

            await cmd.ExecuteNonQueryAsync();

            return Results.Ok(new CreateSectionResponse(sectionId.ToString()));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetSections(
        Db db,
        int days = 30,
        int page = 1,
        int pageSize = 50)
    {
        try
        {
            var sections = new List<SectionDto>();

            using var conn = db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(Queries.GetSections, conn);
            cmd.Parameters.AddWithValue("@Days", days);
            cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                sections.Add(new SectionDto(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    reader.GetDateTime(2),
                    reader.GetInt32(3)
                ));
            }

            return Results.Ok(sections);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
