using DriverChecklist.Api.Models.Enums;

namespace DriverChecklist.Api.Models.MasterData;

/// <summary>
/// Statisk definition av en produkt i produktkatalogen: vilken familj den
/// tillhör, UN-nummer, fysisk utlastningsplats och vilken checklistemall
/// som ska användas. Se README.md avsnitt 2.1 för den fullständiga tabellen.
/// </summary>
public record ProductDefinition(
    string Code,
    string DisplayName,
    string Family,
    string UnNumber,
    string LoadingStationId,
    ChecklistTemplate Template
);
