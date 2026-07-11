namespace ObserwayLabelFlow.App.Data;

public sealed class UserSettingsRecord
{
    public int Id { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }
}
