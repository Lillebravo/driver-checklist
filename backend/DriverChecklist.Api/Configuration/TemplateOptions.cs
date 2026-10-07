namespace DriverChecklist.Api.Configuration;

/// <summary>
/// Bindas från konfigurationssektionen "Templates" i appsettings.json / miljövariabler.
/// </summary>
public class TemplateOptions
{
    public const string SectionName = "Templates";

    /// <summary>
    /// Mapp där checklistemallarna (xlsx) ligger. I MVP:n pekar denna mot en
    /// lokal mapp (t.ex. Skrivbordet). Ska senare bytas ut mot en synkad
    /// Teams/OneDrive-mapp - se README.md avsnitt 7 (Säkerhet & Fillåsning).
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
