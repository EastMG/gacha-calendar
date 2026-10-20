using System.Text.RegularExpressions;
using GachaCalendar.Models;

namespace GachaCalendar.ViewModels;

public sealed record RowData(
	string Name,
	string BadgeText,
	string BadgeKind,
	bool HasBadge,
	string GachaText,
	string GachaTip,
	string GachaDateText,
	string GachaDateTip,
	string EventText,
	string EventTip,
	string EventDateText,
	string EventDateTip,
	DateTime? ActiveEnd,
	string ActiveKind);

/// <summary>
/// 展示层纯函数：把 core 的 Result JSON 翻译成"这一行显示什么"。
/// 口径对齐 <c>gacha-calendar-extension/src/view-helpers.js</c>（同一份产品语言）。
/// </summary>
public static class RowBuilder
{
	private static readonly string[] BaRoleIds = { "ba-cn", "ba-global", "ba-jp" };

	public static RowData Build(GameMeta game, GameRecord? rec, DateTime now)
	{
		rec ??= new GameRecord();
		var gachaText = !string.IsNullOrWhiteSpace(rec.Roles)
			? CleanRoleNames(rec.Roles, game.Id)
			: (!string.IsNullOrWhiteSpace(rec.Banner) ? rec.Banner : "—");
		var eventText = !string.IsNullOrWhiteSpace(rec.Event) ? rec.Event : "—";

		var gachaTip = WithFailNote(BuildGachaTip(rec), "gacha", rec.GachaFail, rec.GachaStale);
		var gachaDateTip = WithFailNote(RawOrDisplay(rec.BannerDatesRaw, rec.BannerDates), "gacha", rec.GachaFail, rec.GachaStale);
		var eventTip = WithFailNote(BuildEventTip(rec), "event", rec.EventFail, rec.EventStale);
		var eventDateTip = WithFailNote(RawOrDisplay(rec.EventDatesRaw, rec.EventDates), "event", rec.EventFail, rec.EventStale);

		var (badgeText, badgeKind, hasBadge) = BuildBadge(rec);

		var bannerWindow = ParseRange(rec.BannerDates, now);
		var eventWindow = ParseRange(rec.EventDates, now);
		DateTime? activeEnd = null;
		var activeKind = "";
		if (bannerWindow is { } bw && now >= bw.Start && now <= bw.End)
		{
			activeEnd = bw.End;
			activeKind = "卡池";
		}
		if (eventWindow is { } ew && now >= ew.Start && now <= ew.End && (activeEnd == null || ew.End < activeEnd))
		{
			activeEnd = ew.End;
			activeKind = "活动";
		}

		return new RowData(
			game.Name,
			badgeText,
			badgeKind,
			hasBadge,
			gachaText,
			gachaTip,
			DisplayTimeCell(rec.BannerDates, now),
			gachaDateTip,
			eventText,
			eventTip,
			DisplayTimeCell(rec.EventDates, now),
			eventDateTip,
			activeEnd,
			activeKind);
	}

	// ---------------------------------------------------------------- 时间

	public static (DateTime Start, DateTime End)? ParseRange(string? raw, DateTime now)
	{
		if (string.IsNullOrWhiteSpace(raw)) return null;
		var parts = raw.Split('~');
		if (parts.Length < 2) return null;
		var a = ParsePoint(parts[0].Trim(), now);
		var b = ParsePoint(parts[1].Trim(), now);
		if (a == null || b == null) return null;

		var start = a.Value;
		var end = b.Value;
		// 跨年：结束早于开始 → 结束算下一年
		if (end < start) end = end.AddYears(1);
		// 一月初看到"12-30 ~ 01-05"时，开始是去年的 12-30
		if (start > now && end < now) start = start.AddYears(-1);
		return (start, end);
	}

	private static DateTime? ParsePoint(string text, DateTime now)
	{
		var m = Regex.Match(text, @"^(\d{1,2})-(\d{1,2})(?:\s+(\d{1,2}):(\d{2}))?$");
		if (!m.Success) return null;
		var mo = int.Parse(m.Groups[1].Value);
		var d = int.Parse(m.Groups[2].Value);
		if (mo is < 1 or > 12 || d is < 1 or > 31) return null;
		var h = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0;
		var mi = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 0;
		try { return new DateTime(now.Year, mo, d, h, mi, 0); }
		catch { return null; }
	}

	public static string FormatRemaining(DateTime target, DateTime now)
	{
		var diff = target - now;
		if (diff < TimeSpan.Zero) return "";
		var days = (int)Math.Floor(diff.TotalDays);
		var hours = diff.Hours;
		var mins = diff.Minutes;
		return $"{days}天{hours}小时{mins}分钟";
	}

	/// <summary>任务栏常驻条用的短倒计时：只保留最有信息量的一档。</summary>
	public static string FormatShortRemaining(TimeSpan diff)
	{
		if (diff <= TimeSpan.Zero) return "已结束";
		if (diff.TotalDays >= 1) return $"{(int)Math.Floor(diff.TotalDays)}天";
		if (diff.TotalHours >= 1) return $"{(int)Math.Floor(diff.TotalHours)}时";
		return $"{Math.Max(1, (int)Math.Floor(diff.TotalMinutes))}分";
	}

	public static string DisplayTimeCell(string? raw, DateTime now)
	{
		var window = ParseRange(raw, now);
		if (window == null) return raw ?? "";
		var (start, end) = window.Value;
		if (now < start)
		{
			var t = FormatRemaining(start, now);
			return t.Length == 0 ? raw ?? "" : $"还有 {t} 开始";
		}
		if (now > end) return "已结束";
		var remain = FormatRemaining(end, now);
		return remain.Length == 0 ? raw ?? "" : $"还剩 {remain}";
	}

	// ---------------------------------------------------------------- 文案

	public static string CleanRoleNames(string? roles, string gameId)
	{
		if (string.IsNullOrWhiteSpace(roles)) return "";
		var isBa = BaRoleIds.Contains(gameId);
		var list = new List<string>();
		foreach (var raw in Regex.Split(roles, @"[、,，]"))
		{
			var s = Regex.Replace(raw, @"[「」【】]", "");
			if (isBa) s = s.Replace('（', '(').Replace('）', ')');
			else s = Regex.Replace(s, @"[（(][^）)]*[）)]", "");
			s = s.Trim();
			if (s.Length == 0) continue;

			var m1 = Regex.Match(s, @"^([\u4e00-\u9fff]{2,10})·(.+)$");
			if (m1.Success && m1.Groups[2].Value.Trim().Length > 0)
			{
				s = m1.Groups[2].Value.Trim();
			}
			else
			{
				var m2 = Regex.Match(s, @"^([\u4e00-\u9fff]{4})•(.+)$");
				if (m2.Success) s = m2.Groups[2].Value.Trim();
			}
			if (s.Length > 0) list.Add(s);
		}
		return string.Join("、", list);
	}

	private static string BuildGachaTip(GameRecord rec)
	{
		var head = !string.IsNullOrWhiteSpace(rec.Roles) ? $"{rec.Banner}：{rec.Roles}" : rec.Banner;
		if (!string.IsNullOrWhiteSpace(rec.BannerDates))
			head = string.IsNullOrEmpty(head) ? RawOrDisplay(rec.BannerDatesRaw, rec.BannerDates) : head + "\n" + RawOrDisplay(rec.BannerDatesRaw, rec.BannerDates);
		return head;
	}

	private static string BuildEventTip(GameRecord rec)
	{
		if (string.IsNullOrWhiteSpace(rec.Event)) return RawOrDisplay(rec.EventDatesRaw, rec.EventDates);
		if (string.IsNullOrWhiteSpace(rec.EventDates)) return rec.Event;
		return rec.Event + "\n" + RawOrDisplay(rec.EventDatesRaw, rec.EventDates);
	}

	private static string RawOrDisplay(string raw, string display) => !string.IsNullOrWhiteSpace(raw) ? raw : display;

	private static (string Text, string Kind, bool Has) BuildBadge(GameRecord rec)
	{
		var parts = new List<(string Kind, string Text)>();
		var g = NormalizeFail(rec.GachaFail);
		var e = NormalizeFail(rec.EventFail);
		if (g != null) parts.Add((g.Kind, SideFailText("gacha", g)));
		if (e != null) parts.Add((e.Kind, SideFailText("event", e)));

		var down = parts.FirstOrDefault(p => p.Kind == "down");
		if (down != default) return (down.Text, "err", true);
		if (parts.Count > 0) return (parts[0].Text, "warn", true);
		if (rec.GachaStale || rec.EventStale) return ("沿用旧数据", "stale", true);
		return ("", "", false);
	}

	public static SideFail? NormalizeFail(SideFail? fail)
	{
		if (fail == null) return null;
		if (fail.Kind == "down")
		{
			if (string.IsNullOrWhiteSpace(fail.Reason)) fail.Reason = "抓取异常";
			return fail;
		}
		return fail.Kind == "nomatch" ? fail : null;
	}

	public static string SideFailText(string side, SideFail fail)
	{
		var label = side == "gacha" ? "卡池" : "活动";
		if (fail.Kind == "down") return $"{label}失败：{fail.Reason ?? "抓取异常"}";
		return side == "gacha" ? "新卡池未公布" : "新活动未公布";
	}

	public static string WithFailNote(string? baseText, string side, SideFail? fail, bool stale)
	{
		var f = NormalizeFail(fail);
		var text = f == null ? "" : SideFailText(side, f);
		var note = text.Length == 0 ? "" : $"（{text}）";
		if (note.Length == 0) return baseText ?? "";
		return stale && !string.IsNullOrEmpty(baseText) ? baseText + "\n" + note : note;
	}
}
