using DriverChecklist.Api.Models.MasterData;

namespace DriverChecklist.Api.Services;

/// <summary>
/// Abstraktion för att hämta masterdata (chaufförer, fordon, produkter).
/// MasterDataService läser en lokal eller synkad Excel-fil read-only när
/// MasterData:Path är konfigurerad; annars används demodata.
/// </summary>
public interface IMasterDataService
{
    InitDataResponse GetInitData();
}
