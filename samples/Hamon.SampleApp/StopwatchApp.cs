using Hamon.Layout;
using Hamon.Widgets;
using System.Globalization;
using System.Linq;

namespace Hamon.SampleApp;

/// <summary>
/// Standard sample: stopwatch.<b>ticker（<see cref="ITicker"/>) to advance your own clock every frame.</b>、
/// The elapsed time is<see cref="ValueNotifier{T}"/>+<see cref="Bind{T}"/>in<b>Only that display</b>update
/// (=no whole tree reconcile=Hamon's manner of frequent values). <c>UseState</c>array + virtualization list.
/// </summary>
public sealed class StopwatchApp : HookWidget
{
    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        Clock clock = hooks.UseMemo(() => new Clock());

        // マウント時に ticker 登録、アンマウントで解除（UseEffect のクリーンアップ）。
        hooks.UseEffect(
            () =>
            {
                context.Owner!.RegisterTicker(clock);
                return () => context.Owner!.UnregisterTicker(clock);
            });

        HookState<bool> running = hooks.UseState(false);
        HookState<float[]> laps = hooks.UseState(System.Array.Empty<float>());

        void Toggle()
        {
            running.Value = !running.Value;
            clock.Running = running.Value;
        }

        void Reset()
        {
            running.Value = false;
            clock.Running = false;
            clock.Elapsed.Value = 0f;
            laps.Value = System.Array.Empty<float>();
        }

        void Lap() => laps.Value = laps.Value.Append(clock.Elapsed.Value).ToArray();

        Widget Btn(string label, Color bg, Action onPressed) => new Button
        {
            Node = new FocusNode(),
            Background = bg,
            Radius = theme.Radius,
            Padding = EdgeInsets.Symmetric(24f, 14f),
            OnPressed = onPressed,
            Child = new Text(label) { FontSize = 16f, Color = theme.OnSurface },
        };

        float[] lapList = laps.Value;

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
                                // 経過時間：Bind が clock.Elapsed の通知でこの Text だけ作り直す（毎フレーム・全再構築なし）。
                                new Bind<float>
                                {
                                    Listenable = clock.Elapsed,
                                    Builder = e => new Text(Format(e)) { FontSize = 48f, Color = theme.OnSurface },
                                },
                                new Row
                                {
                                    MainAxisAlignment = MainAxisAlignment.SpaceBetween,
                                    Children = new Widget[]
                                    {
                                        Btn(running.Value ? "停止" : "開始", running.Value ? theme.Danger : theme.Primary, Toggle),
                                        Btn("ラップ", theme.SurfaceVariant, Lap),
                                        Btn("リセット", theme.SurfaceVariant, Reset),
                                    },
                                },
                                new Expanded
                                {
                                    Child = new Stack
                                    {
                                        Fit = StackFit.Expand,
                                        Background = theme.Surface,
                                        Radius = theme.Radius,
                                        Children = new Widget[]
                                        {
                                            new ListView
                                            {
                                                ItemCount = lapList.Length,
                                                EstimatedExtent = 36f,
                                                Builder = index => new Container
                                                {
                                                    Padding = EdgeInsets.Symmetric(16f, 9f),
                                                    Child = new Row
                                                    {
                                                        MainAxisAlignment = MainAxisAlignment.SpaceBetween,
                                                        Children = new Widget[]
                                                        {
                                                            new Text($"ラップ {index + 1}") { FontSize = 15f, Color = theme.OnSurfaceVariant },
                                                            new Text(Format(lapList[index])) { FontSize = 15f, Color = theme.OnSurface },
                                                        },
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
    }

    private static string Format(float seconds)
    {
        int total = (int)(seconds * 100f);
        int cs = total % 100;
        int s = total / 100 % 60;
        int m = total / 6000;
        return string.Create(CultureInfo.InvariantCulture, $"{m:00}:{s:00}.{cs:00}");
    }

    /// <summary>Own free clock (ITicker). </summary>
    private sealed class Clock : ITicker
    {
        public ValueNotifier<float> Elapsed { get; } = new(0f);

        public bool Running { get; set; }

        public bool Tick(float dtSeconds)
        {
            if (Running)
            {
                Elapsed.Value += dtSeconds;
            }

            return true; // 登録を維持
        }
    }
}
