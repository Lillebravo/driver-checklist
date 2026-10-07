namespace DriverChecklist.Api.Models.MasterData;

/// <summary>Ett släp som en dragbil brukar köra med, inklusive dess fack.</summary>
public record TrailerInfo(
    string RegNr,
    string ApprovalExpiry,
    List<CompartmentInfo> Compartments
);
