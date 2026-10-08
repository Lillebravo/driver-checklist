namespace DriverChecklist.Api.Models.Enums;

/// <summary>
/// Vem som utför/assisterar lastningen, motsvarar kryssrutorna vid
/// "Gäller endast självlastande chaufförer" (E16) och återanvänds för att
/// kryssa i rätt Operatör/Chaufför-ruta senare i dokumentet (se
/// <see cref="Services.ChecklistGeneratorService"/>).
/// <see cref="Unspecified"/> (standardvärdet) kryssar inte i någon ruta alls -
/// används för okända/manuellt inmatade chaufförer där vi inte kan anta något.
/// </summary>
public enum AssistType
{
    Unspecified,
    FullAssist,
    HalfAssist,
    SelfLoading
}

