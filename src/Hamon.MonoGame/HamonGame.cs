using Hamon.Widgets;
using Microsoft.Xna.Framework;

namespace Hamon.MonoGame;

/// <summary>
/// A minimal host for building UI apps with only Hamon (derived from <see cref="Game"/>). It wires up
/// <see cref="HamonApp"/> and the update/draw loop internally, so you can just pass the root widget and
/// call <c>Run()</c> to get started.
/// </summary>
/// <remarks>
/// <para>Minimal example: <c>using var game = new HamonGame(new MyRoot()); game.Run();</c></para>
/// <para>With options: <c>new HamonGame(() =&gt; new MyRoot(), new HamonAppOptions { Theme = HamonTheme.Dark })</c></para>
/// <para>To determine the root widget dynamically, subclass and override <see cref="BuildRoot"/> instead.</para>
/// <para>If no font is specified, one is resolved automatically (see <see cref="HamonFont"/>): this works
/// out of the box for European languages, while Japanese support requires specifying a font via
/// <see cref="HamonAppOptions.Font"/>, <c>HAMON_FONT</c>, etc.</para>
/// </remarks>
public class HamonGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly HamonAppOptions _options;
    private readonly Func<Widget>? _root;

    /// <summary>Constructs the host by passing a root widget builder.</summary>
    public HamonGame(Func<Widget> root, HamonAppOptions? options = null)
        : this(options) => _root = root;

    /// <summary>Constructed by passing a fixed root widget.</summary>
    public HamonGame(Widget root, HamonAppOptions? options = null)
        : this(options) => _root = () => root;

    /// <summary>For subclasses that supply the root by overriding <see cref="BuildRoot"/>.</summary>
    protected HamonGame(HamonAppOptions? options = null)
    {
        _graphics = new GraphicsDeviceManager(this);
        _options = options ?? new HamonAppOptions();
        // HamonGame は Hamon 主体のホストなので、使い勝手の良い既定を土台に敷く（カーソル表示・リサイズ可・実 dt 駆動・VSync）。
        IsMouseVisible = true;
        IsFixedTimeStep = false;
        Window.AllowUserResizing = true;
        _graphics.SynchronizeWithVerticalRetrace = true;
        // 指定（非null）のオプションだけを土台に上書き。device 生成前なので applyChanges:false（ちらつき回避）。
        HamonApp.ApplyWindow(this, Window, _graphics, _options, applyChanges: false);
    }

    /// <summary>The pre-wired <see cref="HamonApp"/> (valid from <see cref="Game.LoadContent"/> onward).</summary>
    public HamonApp App { get; private set; } = null!;

    /// <summary>Provides a root widget. </summary>
    protected virtual Func<Widget>? BuildRoot() => _root;

    protected override void LoadContent()
    {
        App = new HamonApp(this, BuildRoot(), _options);
        base.LoadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        App.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        App.Draw(gameTime);
        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            App?.Dispose();
        }

        base.Dispose(disposing);
    }
}
