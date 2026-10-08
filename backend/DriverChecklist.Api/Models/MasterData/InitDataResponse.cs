namespace DriverChecklist.Api.Models.MasterData;

/// <summary>
/// Svaret från <c>GET /api/init-data</c>: all förvald masterdata som
/// frontend behöver för autocomplete, fordonsval och produktlistan.
/// </summary>
public record InitDataResponse(
    string DefaultOperator,
    List<string> Operators,
    List<DriverInfo> Drivers,
    List<TruckInfo> Trucks,
    List<ProductDefinition> Products
);
