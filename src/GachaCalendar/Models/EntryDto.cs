namespace GachaCalendar.Models;

public sealed class EntryDto
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public string Icon { get; set; } = "";
	public string Source { get; set; } = "";
	public string EventSource { get; set; } = "";
	public List<SourceOption> AltSources { get; set; } = new();
	public List<SourceOption> EventAltSources { get; set; } = new();
	public bool Custom { get; set; }
	public bool DefaultHidden { get; set; }
	public bool Hidden { get; set; }
	public bool Shown { get; set; }
	public bool Removed { get; set; }
	public bool Visible { get; set; }
}

public sealed class SourceOption
{
	public string Label { get; set; } = "";
	public string Value { get; set; } = "";
	public override string ToString() => Label;
}

public sealed class RefreshOption
{
	public string Label { get; set; } = "";
	public int Minutes { get; set; }
	public override string ToString() => Label;
}
