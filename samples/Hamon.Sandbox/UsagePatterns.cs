using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.Sandbox;

/// <summary>
/// A collection of samples of various ways to use Hamon.<b>useState / HookWidget is optional</b>So, from a "pure View" that does not use any state management,
/// How to use just drawing an external model, imperative<see cref="State{T}"/>, subscription type<see cref="ValueNotifier{T}"/>+<see cref="Bind{T}"/>、
/// and the hook (<see cref="HookWidget"/>), you can choose as many as you need.
/// All compilable actual code (also used as documentation).
///
/// Which pattern to use (light → high functionality):
/// <list type="number">
/// <item>No state/static =<see cref="StaticView"/>(HUD frame/label etc. It doesn't change once assembled).</item>
/// <item>Componentization (no hooks required) =<see cref="PriceTag"/>（<see cref="StatelessWidget"/>).</item>
/// <item>Just draw the external model (View = f(model)) =<see cref="WireExternalModelView"/>(When changing<c>host.Invalidate()</c>）。</item>
/// <item>Imperative small state =<see cref="WireCounterWithState"/>（<c>host.CreateState</c>= total reconstruction/naive).</item>
/// <item>Reflecting high-frequency values ​​without rebuilding =<see cref="HpBarBind"/>(Subscription type/partial reconstruction)/<see cref="HpBarCheap"/>(Reading when drawing = zero reconstruction).</item>
/// <item>I want to summarize local states =<see cref="HookCounter"/>(Hook. For screens with increasing states).</item>
/// </list>
///
/// Common drive:<c>host.SetRoot(build); /* every frame */ host.Update(size, dt); host.Render(painter);</c>
/// (MonoGame backend is<c>Program.cs</c>reference.
/// </summary>
public static class UsagePatterns
{
    // ──────────────────────────────────────────────────────────────────────
    // 1) 純View（状態なし）。build 関数がただのツリーを返すだけ。host.SetRoot(UsagePatterns.StaticView) で使う。
    // ──────────────────────────────────────────────────────────────────────
    public static Widget StaticView() => new Container
    {
        Color = new Color(20, 24, 30),
        Padding = EdgeInsets.All(20),
        Child = new Column
        {
            Spacing = 8f,
            Children = new Widget[]
            {
                new Text("Hamon 静的View") { FontSize = 22f, Color = Color.White },
                new Text("状態管理を一切使わない。組んだツリーをそのまま描くだけ。") { FontSize = 15f, Color = Color.SkyBlue },
            },
        },
    };

    // ──────────────────────────────────────────────────────────────────────
    // 3) 外部モデルを描くだけ（View = f(model)）。Hamon を「ビューだけ」として使う最も素朴な形。
    //    自分のモデルを保持し、build 内で読む。変えたら host.Invalidate() で次フレーム再構築。
    //    State<T> も hooks も使わない。
    // ──────────────────────────────────────────────────────────────────────
    public sealed class HudModel
    {
        public int Hp { get; set; } = 100;

        public int MaxHp { get; set; } = 100;

        public int Gold { get; set; }
    }

    public static void WireExternalModelView(HamonRoot host, HudModel model)
    {
        host.SetRoot(() => new Container
        {
            Color = new Color(18, 20, 26),
            Padding = EdgeInsets.All(16),
            Child = new Column
            {
                Spacing = 6f,
                Children = new Widget[]
                {
                    new Text($"HP {model.Hp}/{model.MaxHp}") { FontSize = 20f, Color = Color.White },
                    new Text($"Gold {model.Gold:N0}") { FontSize = 18f, Color = Color.Goldenrod },
                },
            },
        });
    }

    /// <summary>Call when changing external model: next<c>host.Update</c>(View = f(model)).</summary>
    public static void ApplyDamage(HamonRoot host, HudModel model, int amount)
    {
        model.Hp = System.Math.Max(0, model.Hp - amount);
        host.Invalidate();
    }

    // ──────────────────────────────────────────────────────────────────────
    // 4) State<T> でローカル状態（命令的・素朴）。値が変わると全ツリーが dirty → 次フレームで再構築。
    //    小さな画面なら最も書きやすい。HookWidget は不要。
    // ──────────────────────────────────────────────────────────────────────
    public static void WireCounterWithState(HamonRoot host)
    {
        State<int> count = host.CreateState(0);
        host.SetRoot(() => new Container
        {
            Color = new Color(20, 24, 30),
            Padding = EdgeInsets.All(20),
            Child = new Row
            {
                Spacing = 12f,
                CrossAxisAlignment = CrossAxisAlignment.Center,
                Children = new Widget[]
                {
                    new Text($"count = {count.Value}") { FontSize = 22f, Color = Color.White },
                    new Button
                    {
                        Autofocus = true,
                        Background = new Color(52, 60, 76),
                        Padding = EdgeInsets.Symmetric(14f, 10f),
                        OnPressed = () => count.Value++, // setter が host を dirty にする
                        Child = new Text("+1") { FontSize = 18f, Color = Color.White },
                    },
                },
            },
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // 5a) ValueNotifier + Bind（購読型・部分再構築）。高頻度に変わる値（HP等）を、全ツリー reconcile せず
    //     その部分だけ作り直す。値の見た目（テキスト/数）が変わるとき向き。
    // ──────────────────────────────────────────────────────────────────────
    public static Widget HpBarBind(ValueNotifier<int> hp) => new Bind<int>
    {
        Listenable = hp,
        Builder = value => new Text($"HP {value}") { FontSize = 20f, Color = value <= 20 ? Color.Red : Color.White },
    };

    // 5b) 描画時読み（再構築すら不要）。ValueGetter は paint 時に読まれる＝毎フレーム描く前提なら hp.Value を
    //     変えるだけでバーが伸縮する。reconcile も Bind も挟まない最安経路（レイアウトに影響しない値向け）。
    public static Widget HpBarCheap(ValueNotifier<float> hpRatio) => new ProgressBar
    {
        ValueGetter = () => hpRatio.Value, // 0..1
        Width = Dimension.Px(180),
        Height = 10f,
    };

    // ──────────────────────────────────────────────────────────────────────
    // 2) StatelessWidget で部品化（hooks 不要）。再利用可能な見た目をクラス化して合成する。
    // ──────────────────────────────────────────────────────────────────────
    public sealed class PriceTag : StatelessWidget
    {
        private readonly string _label;
        private readonly int _price;

        public PriceTag(string label, int price)
        {
            _label = label;
            _price = price;
        }

        public override Widget Build(BuildContext context) => new Row
        {
            Spacing = 8f,
            Children = new Widget[]
            {
                new Text(_label) { FontSize = 16f, Color = Color.White },
                new Text($"{_price:N0} G") { FontSize = 16f, Color = Color.Goldenrod },
            },
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    // 6) HookWidget（フック）。ローカル状態が増える画面向け。状態の宣言と利用が同じ場所に集まる。
    //    上の State<T> と同じカウンターをフックで。どちらでもよい＝好みと規模で選ぶ。
    // ──────────────────────────────────────────────────────────────────────
    public sealed class HookCounter : HookWidget
    {
        public override Widget Build(BuildContext context, Hooks hooks)
        {
            HookState<int> count = hooks.UseState(0);
            return new Row
            {
                Spacing = 12f,
                CrossAxisAlignment = CrossAxisAlignment.Center,
                Children = new Widget[]
                {
                    new Text($"count = {count.Value}") { FontSize = 22f, Color = Color.White },
                    new Button
                    {
                        Autofocus = true,
                        Background = new Color(52, 60, 76),
                        Padding = EdgeInsets.Symmetric(14f, 10f),
                        OnPressed = () => count.Value++, // フック state setter（この要素だけ再構築）
                        Child = new Text("+1") { FontSize = 18f, Color = Color.White },
                    },
                },
            };
        }
    }
}
