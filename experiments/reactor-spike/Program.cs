using Microsoft.UI.Reactor;
using Microsoft.UI.Reactor.Core;
using Microsoft.UI.Reactor.Layout;
using Microsoft.UI.Xaml;
using static Microsoft.UI.Reactor.Factories;

ReactorApp.Run<ScheduleSpike>("Reactor Schedule Spike", width: 690, height: 720);

internal sealed record UrgentItem(string Game, string Kind, string Title, string When, string Color);
internal sealed record ScheduleRow(string Game, string Color, string Gacha, string GachaMeta, string Event, string EventMeta);

internal sealed class ScheduleSpike : Component
{
    private static readonly UrgentItem[] Urgent =
    {
        new("女神异闻录", "卡池", "汐见琴音", "还剩 7 时 38 分", "#EC4899"),
        new("女神异闻录", "活动", "2.5 周年时光庆典", "还剩 7 时 38 分", "#EC4899"),
        new("鸣潮", "卡池", "心、卜灵、桃祈", "还剩 13 时 38 分", "#7C5CFC"),
        new("物华弥新", "卡池", "凯尔特之书", "还剩 13 时 38 分", "#F59E0B"),
        new("物华弥新", "活动", "「经以山海」限时活动", "还剩 13 时 38 分", "#F59E0B"),
        new("赛马娘·日服", "活动", "レースイベント", "还剩 15 时 38 分", "#06B6D4"),
    };

    private static readonly ScheduleRow[] Rows =
    {
        new("原神", "#EF4444", "丝柯克、爱可菲", "10-13 18:00 → 11-03 14:59 · 还剩 12天18时", "「往冥府的安魂歌」", "9-23 10:00 → 11-03 14:59 · 还剩 12天18时"),
        new("崩坏：星穹铁道", "#EC4899", "千冶•刃、真珠", "10-21 12:00 → 11-11 06:00 · 还剩 20天9时", "「位面分裂」第二十六期", "10-12 04:00 → 10-25 03:59 · 还剩 3天8时"),
        new("绝区零", "#7C5CFC", "菲欧妮、维琳娜", "10-21 06:00 → 11-11 11:59 · 还剩 20天16时", "天使应援大作战", "9-9 06:00 → 11-30 03:59 · 还剩 39天8时"),
    };

    public override Element Render()
    {
        var (page, setPage) = UseState(0);
        var visible = Urgent.Skip(page * 4).Take(4).ToArray();
        var cards = visible.Select(item => UrgentRow(item)).ToArray();

        var header = FlexRow(
            TextBlock("24 小时内 3 条 · 7 天内 22 条").FontSize(12).Foreground(Theme.SecondaryText),
            TextBlock("10 月 21 日 · 周三").FontSize(11).Foreground(Theme.AccentText)
                .Padding(9, 2).Background(Theme.SubtleFill).CornerRadius(12)
        ) with { JustifyContent = FlexJustify.SpaceBetween, AlignItems = FlexAlign.Center };

        var urgentList = (FlexColumn(cards) with { RowGap = 2 }).Flex(grow: 1, basis: 0);
        var focus = Card(FlexColumn(
            TextBlock("女神异闻录：夜幕魅影").FontSize(14).SemiBold(),
            TextBlock("1 天后结束").FontSize(24).SemiBold(),
            TextBlock("「缘结之契」开启").FontSize(12.5).Foreground(Theme.SecondaryText).Margin(0, 4, 0, 0),
            TextBlock("09-24 04:00 ~ 10-22 03:59").FontSize(10).Foreground(Theme.TertiaryText),
            TextBlock("———").Foreground(Theme.DividerStroke).Margin(0, 8, 0, 6),
            TextBlock("鸣潮  角色活动唤取  还剩 13 时 38 分").FontSize(11).Foreground(Theme.SecondaryText),
            TextBlock("物华弥新  「经以山海」限时活动  还剩 13 时 38 分").FontSize(11).Foreground(Theme.SecondaryText).Margin(0, 4, 0, 0)
        )).Flex(grow: 0, basis: 270);

        var recent = (FlexRow(urgentList, focus) with { ColumnGap = 12 }).Margin(0, 10, 0, 10);

        var ruler = FlexRow(
            TextBlock("游戏").Width(150).FontSize(10).Foreground(Theme.TertiaryText),
            Ruler(),
            Ruler()
        );

        var tableHeader = FlexRow(
            TextBlock("游戏").Width(150).FontSize(10).Foreground(Theme.TertiaryText),
            TextBlock("◆ 卡池").Flex(grow: 1, basis: 0).FontSize(10).Foreground(Theme.AccentText),
            TextBlock("⚑ 活动").Flex(grow: 1, basis: 0).FontSize(10).Foreground(Theme.SystemCaution)
        ).Margin(0, 8, 0, 4);

        var rows = Rows.Select(row => TableRow(row)).ToArray();
        var expand = Button($"展开全部 18 条   {page + 1} / 2", () => setPage(page == 0 ? 1 : 0))
            .FontSize(11).Foreground(Theme.SecondaryText).HAlign(HorizontalAlignment.Center);

        var footer = (FlexRow(
            TextBlock("成功 23/23 新卡池未公布：雪松").FontSize(11).Foreground(Theme.TertiaryText).Flex(grow: 1, basis: 0),
            TextBlock("上次更新 1 小时前").FontSize(11).Foreground(Theme.TertiaryText),
            Button("刷新", () => { }).FontSize(11).Foreground(Theme.AccentText).Margin(8, 0, 0, 0)
        ) with { AlignItems = FlexAlign.Center }).Margin(0, 8, 0, 0);

        return Border(FlexColumn(
            header,
            recent,
            expand,
            tableHeader,
            ruler,
            (FlexColumn(rows) with { RowGap = 1 }),
            footer
        )).Background(Theme.SolidBackground).Padding(16).Flex(grow: 1);
    }

    private static Element Ruler() =>
        HStack(2, Enumerable.Range(19, 14).Select(n =>
            TextBlock(n.ToString()).Width(14).FontSize(8).Foreground(Theme.TertiaryText).HAlign(HorizontalAlignment.Center)
        ).ToArray()).Flex(grow: 1, basis: 0);

    private static Element UrgentRow(UrgentItem item) =>
        (FlexRow(
            Border(TextBlock(item.Game[..1]).FontSize(10).Foreground(Theme.PrimaryText))
                .Width(26).Height(26).CornerRadius(6).Background(item.Color),
            TextBlock(item.Game).Width(82).FontSize(12).Foreground(Theme.SecondaryText)
                .TextTrimming(TextTrimming.CharacterEllipsis),
            TextBlock(item.Kind).FontSize(10)
                .Foreground(item.Kind == "活动" ? Theme.SystemCaution : Theme.AccentText).Margin(0, 0, 4, 0),
            TextBlock(item.Title).FontSize(13).SemiBold().Flex(grow: 1, basis: 0)
                .TextTrimming(TextTrimming.CharacterEllipsis),
            TextBlock(item.When).FontSize(12).Foreground(Theme.SystemCritical)
        ) with { AlignItems = FlexAlign.Center, ColumnGap = 8 }).Padding(6, 4);

    private static Element TableRow(ScheduleRow row) =>
        (FlexRow(
            (FlexRow(
                Border(TextBlock(row.Game[..1]).FontSize(14).Foreground(Theme.PrimaryText))
                    .Width(40).Height(40).CornerRadius(10).Background(row.Color),
                TextBlock(row.Game).FontSize(13).SemiBold().Margin(10, 0, 0, 0)
            ) with { AlignItems = FlexAlign.Center }).Width(150),
            FlexColumn(
                TextBlock(row.Gacha).FontSize(13),
                TextBlock(row.GachaMeta).FontSize(10).Foreground(Theme.TertiaryText),
                Bars(row.Color)
            ).Flex(grow: 1, basis: 0).Margin(12, 0, 8, 0),
            FlexColumn(
                TextBlock(row.Event).FontSize(13),
                TextBlock(row.EventMeta).FontSize(10).Foreground(Theme.TertiaryText),
                Bars("#F9A8D4")
            ).Flex(grow: 1, basis: 0).Margin(12, 0, 8, 0)
        ) with { AlignItems = FlexAlign.Center }).Padding(0, 8)
            .BorderBrush(Theme.DividerStroke).BorderThickness(0, 0, 0, 1);

    private static Element Bars(string color) =>
        HStack(1, Enumerable.Range(0, 14).Select(_ =>
            Border(TextBlock(" ")).Width(10).Height(9).CornerRadius(1).Background(color)
        ).ToArray()).Margin(0, 4, 0, 0);
}