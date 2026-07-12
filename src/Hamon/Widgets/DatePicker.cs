using Hamon.Layout;

namespace Hamon.Widgets;

/// <summary>
/// A calendar-based date picker (equivalent to Flutter's <c>CalendarDatePicker</c>). Tapping a date invokes
/// <see cref="OnDateSelected"/>; the currently displayed month is held internally via <c>UseState</c>.
/// </summary>
public sealed class CalendarDatePicker : HookWidget
{
    public DateTime? SelectedDate { get; init; }

    /// <summary>
    /// The month to display initially. Falls back to <see cref="SelectedDate"/> if unspecified; if neither is
    /// set, a fixed default date is used, so callers should pass something like <see cref="DateTime.Today"/>.
    /// </summary>
    public DateTime? InitialMonth { get; init; }

    public Action<DateTime>? OnDateSelected { get; init; }

    private static readonly string[] Weekdays = { "日", "月", "火", "水", "木", "金", "土" };

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        DateTime seed = InitialMonth ?? SelectedDate ?? new DateTime(2000, 1, 1);
        HookState<DateTime> month = hooks.UseState(new DateTime(seed.Year, seed.Month, 1));
        DateTime first = month.Value;

        Widget header = new Row
        {
            CrossAxisAlignment = CrossAxisAlignment.Center,
            Children = new Widget[]
            {
                ArrowButton(theme, "‹", () => month.Value = first.AddMonths(-1)),
                new Expanded
                {
                    Child = new Align
                    {
                        Alignment = Alignment.Center,
                        Child = new Text($"{first.Year}年 {first.Month}月") { FontSize = theme.TextTitle, Color = theme.OnSurface },
                    },
                },
                ArrowButton(theme, "›", () => month.Value = first.AddMonths(1)),
            },
        };

        var weekdayCells = new Widget[7];
        for (int i = 0; i < 7; i++)
        {
            Color c = i == 0 ? theme.Danger : i == 6 ? theme.Primary : theme.OnSurfaceVariant;
            weekdayCells[i] = new Expanded { Child = new Align { Alignment = Alignment.Center, Child = new Text(Weekdays[i]) { FontSize = theme.TextCaption, Color = c } } };
        }

        Widget weekdayRow = new Row { Children = weekdayCells };

        int leading = (int)first.DayOfWeek; // 日=0
        int days = DateTime.DaysInMonth(first.Year, first.Month);
        var weekRows = new List<Widget>(6);
        int day = 1;
        for (int row = 0; row < 6 && day <= days; row++)
        {
            var cells = new Widget[7];
            for (int col = 0; col < 7; col++)
            {
                int cellIndex = (row * 7) + col;
                if (cellIndex < leading || day > days)
                {
                    cells[col] = new Expanded { Child = new SizedBox { Height = Dimension.Px(40f) } };
                }
                else
                {
                    var date = new DateTime(first.Year, first.Month, day);
                    bool selected = SelectedDate is DateTime sd && sd.Date == date.Date;
                    cells[col] = new Expanded { Child = DayCell(theme, day, selected, () => OnDateSelected?.Invoke(date)) };
                    day++;
                }
            }

            weekRows.Add(new Row { Children = cells });
        }

        var column = new List<Widget>(8) { header, new SizedBox { Height = Dimension.Px(theme.SpacingS) }, weekdayRow };
        column.AddRange(weekRows);

        return new Container
        {
            Width = Dimension.Px(320f),
            Padding = EdgeInsets.All(theme.SpacingS),
            Child = new Column { CrossAxisAlignment = CrossAxisAlignment.Stretch, MainAxisSize = MainAxisSize.Min, Spacing = 2f, Children = column },
        };
    }

    private static Widget ArrowButton(HamonTheme theme, string glyph, Action onPressed) => new Button
    {
        Node = new FocusNode(),
        Background = new Color(0, 0, 0, 0),
        Radius = theme.Radius,
        Padding = EdgeInsets.Symmetric(theme.SpacingM, theme.SpacingS),
        OnPressed = onPressed,
        Child = new Text(glyph) { FontSize = theme.TextTitle, Color = theme.OnSurface },
    };

    private static Widget DayCell(HamonTheme theme, int day, bool selected, Action onPressed) => new Button
    {
        Node = new FocusNode(),
        Background = selected ? theme.Primary : new Color(0, 0, 0, 0),
        Radius = 20f,
        Padding = EdgeInsets.Symmetric(theme.SpacingS, theme.SpacingS),
        OnPressed = onPressed,
        Child = new Text(day.ToString()) { FontSize = theme.TextBody, Color = selected ? theme.OnPrimary : theme.OnSurface },
    };
}

/// <summary>Extension methods on <see cref="HamonRoot"/> for displaying a date selection dialog.</summary>
public static class DatePickers
{
    /// <summary>
    /// Opens the calendar dialog and, once a date is selected, calls <paramref name="onPicked"/> and closes the
    /// dialog (equivalent to Flutter's <c>showDatePicker</c>). <paramref name="initial"/> is the month to display
    /// first, and <paramref name="selected"/> is the day to highlight.
    /// </summary>
    public static OverlayEntry ShowDatePicker(this HamonRoot root, DateTime initial, Action<DateTime> onPicked, DateTime? selected = null)
    {
        return root.ShowDialog(close => new Card
        {
            Elevation = 6f,
            Padding = EdgeInsets.All(root.Theme.SpacingS),
            Child = new CalendarDatePicker
            {
                InitialMonth = initial,
                SelectedDate = selected,
                OnDateSelected = d =>
                {
                    onPicked(d);
                    close();
                },
            },
        });
    }
}
