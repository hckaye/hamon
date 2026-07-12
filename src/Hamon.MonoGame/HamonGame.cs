using Hamon.Widgets;
using Microsoft.Xna.Framework;

namespace Hamon.MonoGame;

/// <summary>
/// A minimal host for creating UI apps using only Hamon (<see cref="Game"/>derived). <see cref="HamonApp"/>・
/// Since we are wiring the update/draw loop internally, we can pass the root widget to<c>Run()</c>Just do it and stand up.
/// </summary>
/// <remarks>
/// <para>Minimal example:<c>using var game = new HamonGame(new MyRoot()); game.Run();</c></para>
/// <para>option:<c>new HamonGame(() =&gt; new MyRoot(), new HamonAppOptions { Theme = HamonTheme.Dark })</c></para>
/// <para>If you want to dynamically determine the route<see cref="BuildRoot"/>override (for subclasses).</para>
/// <para>If the font is not specified, it will be automatically resolved (<see cref="HamonFont"/>).
/// Startup without setting for European languages, and for Japanese<see cref="HamonAppOptions.Font"/>/<c>HAMON_FONT</c>Specified by etc.</para>
/// </remarks>
public class HamonGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly HamonAppOptions _options;
    private readonly Func<Widget>? _root;

    /// <summary>Build by passing the root Widget builder.</summary>
    public HamonGame(Func<Widget> root, HamonAppOptions? options = null)
        : this(options) => _root = root;

    /// <summary>Constructed by passing a fixed root widget.</summary>
    public HamonGame(Widget root, HamonAppOptions? options = null)
        : this(options) => _root = () => root;

    /// <summary>route<see cref="BuildRoot"/>For subclasses supplied with override of .</summary>
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

    /// <summary>pre-wired<see cref="HamonApp"/>（<see cref="Game.LoadContent"/>(valid thereafter).</summary>
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
