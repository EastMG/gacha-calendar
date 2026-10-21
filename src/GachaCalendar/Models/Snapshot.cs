using System.Text.Json;
using System.Text.Json.Serialization;

namespace GachaCalendar.Models;

/// <summary>
/// 快照文件（本仓库 <c>data/snapshot.json</c>）的根对象。
/// 由 <c>tools/refresh.mjs</c> 调用 gacha-calendar-core 生成；桌面端只读它，不参与抓取。
/// 结构说明见 docs/06-数据契约.md。
/// </summary>
public sealed class Snapshot
{
	public int SchemaVersion { get; set; }
	public long GeneratedAt { get; set; }
	public string? CoreVersion { get; set; }
	public ConfigJson Config { get; set; } = new();
	public List<GameMeta> Games { get; set; } = new();
	public ResultJson Result { get; set; } = new();
}

public sealed class ConfigJson
{
	public bool NewPanel { get; set; }
	public Dictionary<string, string> GameColors { get; set; } = new();
}

public sealed class GameMeta
{
	public string Id { get; set; } = "";
	public string Name { get; set; } = "";
	public string Icon { get; set; } = "";
	public bool Hidden { get; set; }
	public GameDefaults Defaults { get; set; } = new();
}

public sealed class GameDefaults
{
	public string Banner { get; set; } = "";
	public string Roles { get; set; } = "";
	public string BannerDates { get; set; } = "";
	public string Event { get; set; } = "";
	public string EventDates { get; set; } = "";
}

/// <summary>gacha-calendar-core 的 Result JSON（schemaVersion = 1）。</summary>
public sealed class ResultJson
{
	public int SchemaVersion { get; set; }
	public long RefreshedAt { get; set; }
	public Dictionary<string, int> ParserVersions { get; set; } = new();
	public Dictionary<string, GameRecord> Games { get; set; } = new();
}

/// <summary>
/// 单个条目的抓取结果。内容字段恒在（缺失为空串），状态字段恒在；
/// 三态语义：gachaFail/eventFail 为 null = 正常；kind=nomatch = 源站无当期；kind=down = 报错。
/// </summary>
public sealed class GameRecord
{
	public string Name { get; set; } = "";
	public string Banner { get; set; } = "";
	public string Roles { get; set; } = "";
	public string BannerDates { get; set; } = "";
	public string BannerDatesRaw { get; set; } = "";
	public string BannerHover { get; set; } = "";
	public string Tz { get; set; } = "";
	public string Event { get; set; } = "";
	public string EventDates { get; set; } = "";
	public string EventDatesRaw { get; set; } = "";
	public string EventHover { get; set; } = "";
	public SideFail? GachaFail { get; set; }
	public SideFail? EventFail { get; set; }
	public bool GachaStale { get; set; }
	public bool EventStale { get; set; }
	public bool Skipped { get; set; }
	public long OkAt { get; set; }
}

public sealed class SideFail
{
	public string Kind { get; set; } = "";
	public string? Reason { get; set; }
}

public static class Json
{
	public static readonly JsonSerializerOptions Read = new()
	{
		PropertyNameCaseInsensitive = true,
		AllowTrailingCommas = true,
		ReadCommentHandling = JsonCommentHandling.Skip,
	};

	public static readonly JsonSerializerOptions Write = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};
}
