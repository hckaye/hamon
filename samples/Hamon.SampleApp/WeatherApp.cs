using Hamon.Layout;
using Hamon.Widgets;
using System.Threading.Tasks;

namespace Hamon.SampleApp;

/// <summary>
/// Standard sample: Weather app.<b><c>UseAsync</c>Get asynchronously with</b>and outputs Loading/Ok/Fail.
/// (key = re-obtained if city changes). <c>Task.Delay</c>).
/// （<c>HamonRoot.Post</c>）。
/// </summary>
public sealed class WeatherApp : HookWidget
{
    private static readonly string[] Cities = { "東京", "大阪", "札幌" };

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        HookState<int> city = hooks.UseState(0);
        AsyncValue<Weather> weather = hooks.UseAsync(() => FetchAsync(city.Value), key: city.Value);

        Widget Tab(int i) => new Button
        {
            Node = new FocusNode(),
            Autofocus = i == 0,
            Background = city.Value == i ? theme.Primary : theme.SurfaceVariant,
            Radius = theme.Radius,
            Padding = EdgeInsets.Symmetric(28f, 12f),
            OnPressed = () => city.Value = i,
            Child = new Text(Cities[i]) { FontSize = 16f, Color = city.Value == i ? theme.OnPrimary : theme.OnSurface },
        };

        Widget body =
            weather.IsLoading ? new Text("読み込み中…") { FontSize = 20f, Color = theme.OnSurfaceVariant } :
            weather.HasError ? new Text($"エラー: {weather.Error?.Message}") { FontSize = 18f, Color = theme.Danger } :
            Card(theme, weather.Data!);

        return new Stack
        {
            Fit = StackFit.Expand,
            Background = theme.Background,
            Children = new Widget[]
            {
                new Positioned
                {
                    Left = Dimension.Px(0f),
                    Top = Dimension.Px(0f),
                    Right = Dimension.Px(0f),
                    Bottom = Dimension.Px(0f),
                    Child = new Container
                    {
                        Padding = EdgeInsets.All(20f),
                        Child = new Column
                        {
                            CrossAxisAlignment = CrossAxisAlignment.Stretch,
                            Spacing = 16f,
                            Children = new Widget[]
                            {
                                new Row
                                {
                                    MainAxisAlignment = MainAxisAlignment.SpaceBetween,
                                    Children = new Widget[] { Tab(0), Tab(1), Tab(2) },
                                },
                                body,
                            },
                        },
                    },
                },
            },
        };
    }

    private static Widget Card(HamonTheme theme, Weather w)
    {
        var rows = new Widget[w.Forecast.Length + 2];
        rows[0] = new Text(w.City) { FontSize = 24f, Color = theme.OnSurface };
        rows[1] = new Row
        {
            CrossAxisAlignment = CrossAxisAlignment.Center,
            Spacing = 12f,
            Children = new Widget[]
            {
                new Text($"{w.TempC}°") { FontSize = 56f, Color = theme.OnSurface },
                new Text(w.Condition) { FontSize = 20f, Color = theme.OnSurfaceVariant },
            },
        };
        for (int i = 0; i < w.Forecast.Length; i++)
        {
            (string day, int hi, int lo) = w.Forecast[i];
            rows[i + 2] = new Row
            {
                MainAxisAlignment = MainAxisAlignment.SpaceBetween,
                Children = new Widget[]
                {
                    new Text(day) { FontSize = 16f, Color = theme.OnSurfaceVariant },
                    new Text($"{hi}° / {lo}°") { FontSize = 16f, Color = theme.OnSurface },
                },
            };
        }

        return new Container
        {
            Color = theme.Surface,
            Radius = theme.Radius,
            Padding = EdgeInsets.All(20f),
            Child = new Column
            {
                CrossAxisAlignment = CrossAxisAlignment.Stretch,
                Spacing = 10f,
                Children = rows,
            },
        };
    }

    private static async Task<Weather> FetchAsync(int cityIndex)
    {
        await Task.Delay(600).ConfigureAwait(false); // ネットワークのモック
        string[] conditions = { "晴れ", "くもり", "雨" };
        int baseTemp = 12 + (cityIndex * 4);
        var forecast = new (string, int, int)[]
        {
            ("今日", baseTemp + 6, baseTemp - 2),
            ("明日", baseTemp + 4, baseTemp - 3),
            ("明後日", baseTemp + 7, baseTemp - 1),
        };
        return new Weather(Cities[cityIndex], baseTemp + 3, conditions[cityIndex % conditions.Length], forecast);
    }

    private sealed record Weather(string City, int TempC, string Condition, (string Day, int Hi, int Lo)[] Forecast);
}
