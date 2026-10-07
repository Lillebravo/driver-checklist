namespace DriverChecklist.Api.Models;

/// <summary>
/// En beräknad tankplats (Tank 1-4) på checklistan, härledd från dragbilens
/// tankkod samt släpens fack i rätt ordning.
/// </summary>
public record TankSlot(
    string TankCode,
    string InspectionType,
    string LastInspectionMonthYear,
    string ExpiryFormatted,
    bool IsExpired
);
