using ClaudeLog.Data.Models;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data.Repositories;

public class SectionRepository
{
    private readonly DbContext _dbContext;

    private const string InsertSectionQuery = @"
        INSERT INTO dbo.Sections (SectionId, Tool, CreatedAt)
        VALUES (@SectionId, @Tool, @CreatedAt)";

    private const string GetSectionsQuery = @"
        SELECT s.SectionId, s.Tool, s.CreatedAt, COUNT(c.Id) as Count
        FROM dbo.Sections s
        LEFT JOIN dbo.Conversations c ON s.SectionId = c.SectionId
        WHERE s.CreatedAt >= DATEADD(DAY, -@Days, SYSDATETIME())
        GROUP BY s.SectionId, s.Tool, s.CreatedAt
        ORDER BY s.CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

    public SectionRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateSectionResponse> CreateAsync(CreateSectionRequest request)
    {
        var sectionId = string.IsNullOrWhiteSpace(request.SectionId)
            ? Guid.NewGuid()
            : Guid.Parse(request.SectionId);

        var createdAt = request.CreatedAt ?? DateTime.Now;

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(InsertSectionQuery, conn);
        cmd.Parameters.AddWithValue("@SectionId", sectionId);
        cmd.Parameters.AddWithValue("@Tool", request.Tool);
        cmd.Parameters.AddWithValue("@CreatedAt", createdAt);

        await cmd.ExecuteNonQueryAsync();

        return new CreateSectionResponse(sectionId.ToString());
    }

    public async Task<List<Section>> GetSectionsAsync(int days = 30, int page = 1, int pageSize = 50)
    {
        var sections = new List<Section>();

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(GetSectionsQuery, conn);
        cmd.Parameters.AddWithValue("@Days", days);
        cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sections.Add(new Section(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.GetInt32(3)
            ));
        }

        return sections;
    }
}
