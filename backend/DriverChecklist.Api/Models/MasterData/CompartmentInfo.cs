namespace DriverChecklist.Api.Models.MasterData;

/// <summary>
/// Ett fack (compartment) på en släpvagn, med egen tankkod och eget
/// provtryckningsdatum. Giltigheten beräknas i frontend (3 år för
/// tankbil/trailer, 2,5 år för tankcontainer).
/// </summary>
public record CompartmentInfo(
    int CompartmentNo,
    string TankCode,
    string LastTest,
    string TestType,
    bool IsTankContainer
);
