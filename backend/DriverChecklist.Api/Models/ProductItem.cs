namespace DriverChecklist.Api.Models;

/// <summary>
/// En vald produkt som ska lastas, kopplad till en specifik utskriven checklista.
/// </summary>
public record ProductItem(string Name, string UnNumber);
