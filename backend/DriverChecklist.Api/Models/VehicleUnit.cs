namespace DriverChecklist.Api.Models;

/// <summary>
/// Representerar en fysisk enhet (dragbil eller släp) kopplad till en transport.
/// </summary>
/// <param name="RegNr">Registreringsnummer.</param>
/// <param name="IsNew">True om enheten matats in manuellt och inte finns i master-Excelen.</param>
public record VehicleUnit(string RegNr, bool IsNew);
