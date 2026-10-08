namespace DriverChecklist.Api.Models;

/// <summary>Editable values on the first page of a checklist.</summary>
public sealed record ChecklistPage(
    DateTimeOffset? Timestamp,
    string SapNumber,
    string LoadingAmount,
    string ContainerNumber,
    List<string> UnNumbers,
    List<ChecklistRowValue> Rows,
    List<ChecklistRoleValue> Roles,
    List<string> CompartmentVolumes
);

/// <summary>Checks and an additional comment for one template row.</summary>
public sealed record ChecklistRowValue(int Row, bool Tt, bool Tc, bool Rc, string Comment);

/// <summary>Selected role labels for one section header.</summary>
public sealed record ChecklistRoleValue(int Row, List<string> Selected);

/// <summary>First-page layout read directly from the selected Excel template.</summary>
public sealed record ChecklistPageResponse(
    List<ChecklistRowResponse> Rows, List<ChecklistSectionResponse> Sections, List<string> UnNumbers, List<string> AssistOptions);

/// <summary>A question and its enabled check columns.</summary>
public sealed record ChecklistRowResponse(int Row, string Question, string Instruction, string CommentInstruction, List<bool> Enabled);

/// <summary>A section title and its role checkbox labels.</summary>
public sealed record ChecklistSectionResponse(int Row, string Title, List<string> Roles);
