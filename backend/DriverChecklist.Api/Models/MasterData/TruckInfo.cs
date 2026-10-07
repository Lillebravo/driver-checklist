namespace DriverChecklist.Api.Models.MasterData;

/// <summary>En dragbil och dess vanliga släpkombinationer enligt master-Excelen.</summary>
public record TruckInfo(
    string RegNr,
    string TankCode,
    string ApprovalExpiry,
    List<TrailerInfo> Trailers
);
