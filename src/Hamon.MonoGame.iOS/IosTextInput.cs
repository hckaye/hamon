using System.Linq;
using Foundation;
using Hamon.Layout;
using Hamon.Widgets;
using UIKit;

namespace Hamon.MonoGame.iOS;

/// <summary>
/// iOS text input/IME backend (<see cref="ITextInput"/>). <see cref="IUIKeyInput"/>view
/// Set it to first responder to display the soft keyboard and enter the final character.<see cref="HamonRoot.DispatchText"/>, delete
/// <see cref="HamonRoot.DispatchEditKey"/>flow to
/// Keyboard height from notification<see cref="HamonRoot.SoftKeyboardHeight"/>reflected in
///
/// Integration: Generate this type and<c>host.TextInput = iosTextInput;</c>(After starting the game/in the UI thread).
/// If you need inline display during conversion (marked text)<c>IUITextInput</c>Extensible (optional) with a set of implementations.
/// </summary>
public sealed class IosTextInput : ITextInput
{
    private readonly HamonRoot _host;
    private readonly KeyInputView _view;
    private NSObject? _willShow;
    private NSObject? _willHide;

    public IosTextInput(HamonRoot host)
    {
        _host = host;
        _view = new KeyInputView(host) { Hidden = true };
        UIApplication.SharedApplication.InvokeOnMainThread(() =>
        {
            // 非推奨の KeyWindow/Windows を避け、接続中シーンから key window を取得（iOS 13+ マルチシーン対応）。
            UIWindow[] windows = UIApplication.SharedApplication.ConnectedScenes
                .OfType<UIWindowScene>()
                .SelectMany(s => s.Windows)
                .ToArray();
            UIWindow? window = windows.FirstOrDefault(w => w.IsKeyWindow) ?? windows.FirstOrDefault();
            window?.AddSubview(_view);

            _willShow = UIKeyboard.Notifications.ObserveWillShow((_, e) =>
                _host.SoftKeyboardHeight = (float)UIKeyboard.FrameEndFromNotification(e.Notification).Height);
            _willHide = UIKeyboard.Notifications.ObserveWillHide((_, _) => _host.SoftKeyboardHeight = 0f);
        });
    }

    public void Start() => UIApplication.SharedApplication.InvokeOnMainThread(() => _view.BecomeFirstResponder());

    public void Stop() => UIApplication.SharedApplication.InvokeOnMainThread(() => _view.ResignFirstResponder());

    // iOS は候補 UI を OS キーボードが出すため、キャレット矩形の指定は不要。
    public void SetCaretRect(Rect caret)
    {
    }

    /// <summary>Invisible key input view that streams confirmed characters/deletes to the Hamon core.</summary>
    private sealed class KeyInputView : UIView, IUIKeyInput
    {
        private readonly HamonRoot _host;

        public KeyInputView(HamonRoot host) => _host = host;

        public override bool CanBecomeFirstResponder => true;

        public bool HasText => true;

        public void InsertText(string text)
        {
            foreach (char c in text)
            {
                if (c == '\n')
                {
                    _host.DispatchEditKey(TextEditKey.Enter);
                }
                else
                {
                    _host.DispatchText(c); // 確定文字（IME 確定済みを含む）
                }
            }
        }

        public void DeleteBackward() => _host.DispatchEditKey(TextEditKey.Backspace);
    }
}
