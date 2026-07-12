using Hamon.MonoGame;
using Hamon.Widgets;
using Xna = Microsoft.Xna.Framework;

namespace Hamon.SampleApp;

// Hamon カタログ・サンプル（MonoGame バックエンド）。
// ホーム一覧から各サンプル（電卓/ストップウォッチ/天気/ToDo/ギャラリー/設定）を push ナビゲーションで開き、
// Back/Esc/ゲームパッド B で戻る。配線（入力・描画・HiDPI・IME・フォント）はすべて HamonApp が面倒を見る。
public static class Program
{
    public static void Main()
    {
        using var game = new SampleAppGame();
        game.Run();
    }
}

/// <summary>Own<see cref="Xna.Game"/>An example of putting Hamon on. <see cref="HamonApp"/>delegate to.</summary>
public sealed class SampleAppGame : Xna.Game
{
    private HamonApp _app = null!;
    private SampleCatalog _catalog = null!;

    public SampleAppGame()
    {
        // GraphicsDeviceManager は必要（Game が GraphicsDevice を生成する）。ウィンドウ設定は HamonAppOptions で指定する。
        _ = new Xna.GraphicsDeviceManager(this);
    }

    protected override void LoadContent()
    {
        // GraphicsDevice が出来てから生成する（LoadContent 以降）。フォントは自動解決。
        // カタログは縦に長いのでポートレートの窓にする（既定 800x480 だと見切れる）。
        _app = new HamonApp(this, options: new HamonAppOptions
        {
            WindowWidth = 540,
            WindowHeight = 960,
            WindowTitle = "Hamon サンプル",
            MouseVisible = true,
            AllowResize = true,
            FixedTimeStep = false,
        });

        ITexture[] gallery = GalleryApp.CreateTextures(GraphicsDevice);
        _catalog = new SampleCatalog(_app.Ui, gallery);
        _app.SetRoot(() => _catalog.Root);

        // 「戻る」（Esc / パッド B）：オーバーレイが無くナビが pop 可能なときだけ前の画面へ。
        _app.OnBack = () =>
        {
            if (_app.Ui.OverlayCount == 0 && _catalog.Nav.CanPop)
            {
                _catalog.Nav.Pop();
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
