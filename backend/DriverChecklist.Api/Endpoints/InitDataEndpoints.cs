using DriverChecklist.Api.Services;

namespace DriverChecklist.Api.Endpoints;

/// <summary>Endpoints för att hämta förvald masterdata (chaufförer, fordon, produkter).</summary>
public static class InitDataEndpoints
{
    public static void MapInitDataEndpoints(this WebApplication app)
    {
        app.MapGet("/api/init-data", (IMasterDataService masterDataService, ILogger<MasterDataService> logger) =>
        {
            try
            {
                return Results.Ok(masterDataService.GetInitData());
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException
                or FormatException or ArgumentException or System.Xml.XmlException)
            {
                logger.LogError(exception, "Kunde inte läsa fordonsregistret.");
                var detail = exception is InvalidDataException
                    ? exception.Message
                    : "Kontrollera MasterData:Path och att Excel-filen finns, är läsbar och är en giltig .xlsx-fil.";
                return Results.Problem(
                    title: "Kunde inte läsa fordonsregistret",
                    detail: detail,
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }
}
