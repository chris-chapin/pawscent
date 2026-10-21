namespace Pawscent.Core.Profiles;

/// <summary>A profile is a workspace: one sub-folder under the profiles root.</summary>
public sealed record Profile(string Name, DateTimeOffset CreatedUtc, int SchemaVersion)
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>Full path of the profile's folder. Not serialized.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string FolderPath { get; init; } = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public string CreatedDisplay => $"Created {CreatedUtc.LocalDateTime:g}";
}
