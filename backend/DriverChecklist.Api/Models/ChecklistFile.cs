namespace DriverChecklist.Api.Models;

/// <summary>Den färdigifyllda checklistan som binär ström, redo att skickas som nedladdning.</summary>
public record ChecklistFile(Stream Content, string FileName, string ContentType);
