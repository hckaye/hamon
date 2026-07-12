using Hamon.MonoGame;
using Xna = Microsoft.Xna.Framework;

namespace Hamon.SampleApp2;

// Hamon リッチ・ショーケース（MonoGame バックエンド）。
// Scaffold＋下部ナビ／グラデ・影・チャート・回転／フォーム＋検証／DatePicker／Snackbar・Toast／Hero 遷移などを一通り見せる。
// ホスト配線（入力・描画・HiDPI・IME・フォント）は HamonApp に委譲する。
public static class Program
{
    public static void Main()
    {
        using var game = new ShowcaseGame();
        game.Run();
    }
}

/// <summary>An example of a pure UI app.<see cref="HamonApp"/>Just use it as is and replace only the route.</summary>
public sealed class ShowcaseGame : Xna.Game
{
    private HamonApp _app = null!;
    private Showcase _showcase = null!;

    public ShowcaseGame()
    {
        // ウィンドウ/タイムステップ等は HamonAppOptions で指定（既定で VSync=on・固定ステップ off＝アニメ滑らか）。
        _ = new Xna.GraphicsDeviceManager(this);
    }

    protected override void LoadContent()
    {
        _app = new HamonApp(this, options: new HamonAppOptions
        {
            WindowWidth = 960,
            WindowHeight = 640,
            WindowTitle = "Hamon ショーケース",
            MouseVisible = true,
            AllowResize = true,
            FixedTimeStep = false,
        });

        _showcase = new Showcase(_app.Ui);
        _app.SetRoot(() => _showcase.Root);

        // 「戻る」（Esc / パッド B）：オーバーレイが無ければ前の画面へ pop。
        _app.OnBack = () =>
        {
            if (_app.Ui.OverlayCount == 0 && _showcase.Nav.CanPop)
            {
                _showcase.Nav.Pop();
            }
        };
    }

    protected override void Update(Xna.GameTime gameTime) => _app.Update(gameTime);

    protected override void Draw(Xna.GameTime gameTime) => _app.Draw(gameTime);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _app?.Dispose();
        }

        base.Dispose(disposing);
    }
}
