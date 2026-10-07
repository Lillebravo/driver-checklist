using DriverChecklist.Api.Services;

namespace DriverChecklist.Api.Endpoints;

/// <summary>Endpoints för att hämta förvald masterdata (chaufförer, fordon, produkter).</summary>
public static class InitDataEndpoints
{
    public static void MapInitDataEndpoints(this WebApplication app)
    {
        app.MapGet("/api/init-data", (IMasterDataService masterDataService) =>
            Results.Ok(masterDataService.GetInitData()));
    }
}
