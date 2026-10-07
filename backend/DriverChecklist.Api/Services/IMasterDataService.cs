using DriverChecklist.Api.Models.MasterData;

namespace DriverChecklist.Api.Services;

/// <summary>
/// Abstraktion för att hämta masterdata (chaufförer, fordon, produkter).
/// Gör det möjligt att senare byta ut <see cref="MasterDataService"/> mot en
/// implementation som läser read-only direkt från den delade Teams/OneDrive-
/// Excelen, utan att ändra något i Endpoints-lagret.
/// </summary>
public interface IMasterDataService
{
    InitDataResponse GetInitData();
}
