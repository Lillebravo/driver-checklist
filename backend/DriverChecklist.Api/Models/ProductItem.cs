namespace DriverChecklist.Api.Models;

/// <summary>
/// En vald produkt som ska lastas, kopplad till en specifik utskriven checklista.
/// <paramref name="Family"/> (t.ex. "PIX", "BDP", "SVS") används för att
/// kryssa i rätt produkt-kryssruta i mallen, se
/// <see cref="Services.ChecklistGeneratorService"/>.
/// </summary>
public record ProductItem(string Name, string UnNumber, string Family);
