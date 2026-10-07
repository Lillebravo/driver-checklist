using DriverChecklist.Api.Data;
using DriverChecklist.Api.Models.MasterData;

namespace DriverChecklist.Api.Services;

/// <summary>
/// MVP-implementation som serverar hårdkodad masterdata från <see cref="InitialDataStore"/>.
/// </summary>
public class MasterDataService : IMasterDataService
{
    public InitDataResponse GetInitData() => InitialDataStore.GetData();
}
