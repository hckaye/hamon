using Hamon.MonoGame;
using Hamon.Widgets;
using Xna = Microsoft.Xna.Framework;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace Hamon.SampleApp3;

// Hamon ゲーム風 HUD（モック）。Hamon を「自分のゲームの上に重ねる HUD」として使う例。
// 9-slice パネル＋HP/MP ゲージ・画像スキンの仮想スティック・アクションスロット（クールダウン）・ドラッグ装備など。
// 入力/HiDPI/IME/フォントは HamonApp に委譲し、背景クリアだけ自前で行う（ClearBackground=false）。
public static class Program
{
    public static void Main()
    {
        using var game = new GameHudApp();
        game.Run();
    }
}

/// <summary>An example of overlaying a HUD on top of the game world. <see cref="HamonApp.Render"/>Put the HUD on it.</summary>
public sealed class GameHudApp : Xna.Game
{
    private HamonApp _app = null!;

    public GameHudApp()
    {
        // ウィンドウ等は HamonAppOptions で指定する。
        _ = new Xna.GraphicsDeviceManager(this);
    }

    protected override void LoadContent()
    {
        // ゲームは暗い画面が多いので、ダークテーマ（opt-in）にして背景クリアは自前にする。
        _app = new HamonApp(this, options: new HamonAppOptions
        {
            Theme = HamonTheme.Dark,
            ClearBackground = false,
            WindowWidth = 1280,
            WindowHeight = 720,
            WindowTitle = "Hamon ゲーム HUD",
            MouseVisible = true,
            AllowResize = true,
            FixedTimeStep = false,
        });

        GameSkins skins = GameSkins.Create(GraphicsDevice);
        var hud = new GameHud(_app.Ui, skins);
        _app.SetRoot(() => hud.Root);
    }

    protected override void Update(Xna.GameTime gameTime) => _app.Update(gameTime);

    protected override void Draw(Xna.GameTime gameTime)
    {
        GraphicsDevice.Clear(new XnaColor(18, 22, 30)); // ここで本来はゲーム世界を描く
        _app.Render();                                   // その上に Hamon の HUD を重ねる
        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _app?.Dispose();
        }

        base.Dispose(disposing);
    }
}
