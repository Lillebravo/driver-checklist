using DriverChecklist.Api.Models.MasterData;

namespace DriverChecklist.Api.Data;

/// <summary>
/// Hårdkodad förvald masterdata för MVP:n (chaufförer, dragbilar, släp).
/// I en senare version ska detta ersättas av en tjänst som läser samma
/// data read-only från den delade master-Excelen på Teams/OneDrive -
/// se <see cref="Services.IMasterDataService"/> för abstraktionen som
/// gör det bytet möjligt utan att ändra API-kontraktet.
/// </summary>
public static class InitialDataStore
{
    public static InitDataResponse GetData() => new(
        DefaultOperator: "Vakt",
        Operators: new List<string> { "Jerry", "Joel", "André" },
        Drivers: new List<DriverInfo>
        {
            new("Tony Blaffert", "2027-02-01"),
            new("Håkan Tyrander", "2030-03-31"),
        },
        Trucks: new List<TruckInfo>
        {
            new(
                RegNr: "JAF 58E",
                TankCode: "ADR",
                ApprovalExpiry: "2027-03-13",
                Trailers: new List<TrailerInfo>
                {
                    new(
                        RegNr: "CSU 474",
                        ApprovalExpiry: "2027-07-01",
                        // Fack 1 & 3: L4BH, Fack 2: L4DH, Trycktest: 2025-06 P (nästa blir L)
                        Compartments: new List<CompartmentInfo>
                        {
                            new(1, "L4BH", "2025-06", "P", IsTankContainer: false),
                            new(2, "L4DH", "2025-06", "P", IsTankContainer: false),
                            new(3, "L4BH", "2025-06", "P", IsTankContainer: false),
                        }),
                    new(
                        RegNr: "WMJ 180",
                        ApprovalExpiry: "2027-09-17",
                        Compartments: new List<CompartmentInfo>
                        {
                            new(1, "L4BH", "2024-09", "P", IsTankContainer: false),
                        }),
                    new(
                        RegNr: "EGN 351",
                        ApprovalExpiry: "2027-05-31",
                        Compartments: new List<CompartmentInfo>
                        {
                            new(1, "L4BH", "2024-05", "P", IsTankContainer: false),
                        }),
                    new(
                        RegNr: "DMA 570",
                        ApprovalExpiry: "2027-05-05",
                        Compartments: new List<CompartmentInfo>
                        {
                            new(1, "L4BH", "2024-06", "P", IsTankContainer: false),
                        }),
                }),
        },
        Products: ProductCatalog.Products.ToList()
    );
}
