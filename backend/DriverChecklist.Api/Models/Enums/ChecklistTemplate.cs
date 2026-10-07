namespace DriverChecklist.Api.Models.Enums;

/// <summary>
/// De fysiska checklistemallar (Excel-filer) som kan genereras.
/// Type3_AlsLut finns med för framtida utökning (ALS/LUT) men saknar
/// ännu en mallfil i MVP:n - se <see cref="Services.TemplateResolver"/>.
/// </summary>
public enum ChecklistTemplate
{
    Type1_PixPaxSasBdp,
    Type2_SvsAkd,
    Type3_AlsLut
}
