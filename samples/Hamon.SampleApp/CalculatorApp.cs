using Hamon.Layout;
using Hamon.Widgets;
using System.Globalization;

namespace Hamon.SampleApp;

/// <summary>
/// Standard sample: Calculator. <b>Gamepad/keyboard directional focus</b>(The focus node is
/// <c>UseFocusNode</c>Persistent = retains selected position even if reconstructed by keystroke). <c>UseState</c>unchanging
/// <see cref="Calc"/>.
/// </summary>
public sealed class CalculatorApp : HookWidget
{
    private static readonly string[] Cells =
    {
        "7", "8", "9", "/",
        "4", "5", "6", "*",
        "1", "2", "3", "-",
        "0", ".", "C", "+",
    };

    public override Widget Build(BuildContext context, Hooks hooks)
    {
        HamonTheme theme = context.Theme;
        HookState<Calc> state = hooks.UseState(Calc.Initial);

        var nodes = new FocusNode[Cells.Length + 1]; // +1 = イコール
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = hooks.UseFocusNode();
        }

        void Press(string key) => state.Value = Calc.Apply(state.Value, key);

        // Expanded で各キーを等幅に（Button は tight 制約で充填する＝Flutter と同じ）。行は Stretch で等高。
        Widget Cell(int index, string label, Color? bg = null) => new Expanded
        {
            Child = new Button
            {
                Node = nodes[index],
                Autofocus = index == 0,
                Background = bg ?? theme.SurfaceVariant,
                Radius = theme.Radius,
                Padding = EdgeInsets.Symmetric(0f, 22f),
                OnPressed = () => Press(label),
                Child = new Text(label) { FontSize = 24f, Color = theme.OnSurface },
            },
        };

        Widget GridRow(int row) => new Row
        {
            CrossAxisAlignment = CrossAxisAlignment.Stretch,
            Spacing = 8f,
            Children = new Widget[]
            {
                Cell((row * 4) + 0, Cells[(row * 4) + 0]),
                Cell((row * 4) + 1, Cells[(row * 4) + 1]),
                Cell((row * 4) + 2, Cells[(row * 4) + 2]),
                Cell((row * 4) + 3, Cells[(row * 4) + 3]),
            },
        };

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
                        Padding = EdgeInsets.All(16f),
                        Child = new Column
                        {
                            CrossAxisAlignment = CrossAxisAlignment.Stretch,
                            Spacing = 8f,
                            Children = new Widget[]
                            {
                                // 表示（右寄せ＝Row + Expanded スペーサ）
                                new Container
                                {
                                    Color = theme.Surface,
                                    Radius = theme.Radius,
                                    Padding = EdgeInsets.Symmetric(16f, 20f),
                                    Child = new Row
                                    {
                                        Children = new Widget[]
                                        {
                                            new Expanded { Child = new SizedBox() },
                                            new Text(state.Value.Display) { FontSize = 36f, Color = theme.OnSurface },
                                        },
                                    },
                                },
                                GridRow(0),
                                GridRow(1),
                                GridRow(2),
                                GridRow(3),
                                new Row
                                {
                                    CrossAxisAlignment = CrossAxisAlignment.Stretch,
                                    Children = new Widget[] { Cell(Cells.Length, "=", theme.Primary) },
                                },
                            },
                        },
                    },
                },
            },
        };
    }

    /// <summary>Calculator state for sequential operations (unchanged).</summary>
    private sealed record Calc(string Display, double Acc, string? Op, bool Fresh)
    {
        public static Calc Initial => new("0", 0d, null, true);

        public static Calc Apply(Calc c, string key) => key switch
        {
            "C" => Initial,
            "=" => Evaluate(c),
            "+" or "-" or "*" or "/" => Operate(c, key),
            "." => Dot(c),
            _ => Digit(c, key),
        };

        private static Calc Digit(Calc c, string d)
        {
            string disp = c.Fresh || c.Display == "0" ? d : c.Display + d;
            return c with { Display = disp, Fresh = false };
        }

        private static Calc Dot(Calc c)
        {
            if (c.Fresh)
            {
                return c with { Display = "0.", Fresh = false };
            }

            return c.Display.Contains('.') ? c : c with { Display = c.Display + "." };
        }

        private static Calc Operate(Calc c, string op)
        {
            double cur = Parse(c.Display);
            double acc = c.Op is null ? cur : Compute(c.Acc, c.Op, cur);
            return c with { Acc = acc, Op = op, Fresh = true, Display = Format(acc) };
        }

        private static Calc Evaluate(Calc c)
        {
            if (c.Op is null)
            {
                return c;
            }

            double acc = Compute(c.Acc, c.Op, Parse(c.Display));
            return c with { Acc = acc, Op = null, Fresh = true, Display = Format(acc) };
        }

        private static double Compute(double a, string op, double b) => op switch
        {
            "+" => a + b,
            "-" => a - b,
            "*" => a * b,
            "/" => b == 0d ? double.NaN : a / b,
            _ => b,
        };

        private static double Parse(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0d;

        private static string Format(double v) =>
            double.IsNaN(v) || double.IsInfinity(v) ? "Error" : v.ToString("0.##########", CultureInfo.InvariantCulture);
    }
}
