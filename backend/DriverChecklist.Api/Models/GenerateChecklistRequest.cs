using DriverChecklist.Api.Models.Enums;

namespace DriverChecklist.Api.Models;

/// <summary>
/// Begäran från frontend om att generera och fylla i en (1) checklista av en
/// specifik mall. Frontend grupperar valda produkter per (mall, station) och
/// skickar en begäran per grupp.
/// </summary>
public record GenerateChecklistRequest(
    ChecklistTemplate TemplateType,
    string OperatorName,
    string DriverName,
    DateTime DriverAdrExpiry,
    bool IsNewDriver,
    string? Akeri,
    VehicleUnit Truck,
    List<VehicleUnit> Trailers,
    List<TankSlot> TankSlots,
    List<ProductItem> SelectedProducts,
    AssistType AssistType,
    ChecklistPage? FirstPage = null
);
