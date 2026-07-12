using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.MonoGame.Android;

/// <summary>
/// Android text input/IME backend (<see cref="ITextInput"/>).
/// <see cref="HamonInputConnection"/>（<see cref="HamonRoot"/>game view<c>OnCreateInputConnection</c>returns) from
/// Confirmed characters/Converting/Deleting<see cref="HamonRoot.DispatchText"/>/<see cref="HamonRoot.DispatchComposition"/>/<see cref="HamonRoot.DispatchEditKey"/>flow to
/// The conversion candidate UI is displayed by the OS keyboard (normal mobile UX). <see cref="HamonRoot.SoftKeyboardHeight"/>Reflect on.
///
/// Integration procedure: (1) Game<c>View</c>(MonoGame's<c>AndroidGameView</c>etc.) in<c>OnCheckIsTextEditor()=&gt;true</c>and
/// <c>OnCreateInputConnection</c>override<see cref="CreateInputConnection"/>return.
/// <c>host.TextInput = androidTextInput;</c>.
/// keyboard height<see cref="HamonRoot.SoftKeyboardHeight"/>Set to (<see cref="AttachKeyboardHeightWatcher"/>auxiliary).
/// Actual device verification premise (API-compliant reference implementation).
/// </summary>
public sealed class AndroidTextInput : ITextInput
{
    private readonly HamonRoot _host;
    private readonly Activity _activity;
    private readonly View _view;

    public AndroidTextInput(HamonRoot host, Activity activity, View view)
    {
        _host = host;
        _activity = activity;
        _view = view;
    }

    private InputMethodManager? Imm => _activity.GetSystemService(Context.InputMethodService) as InputMethodManager;

    public void Start() => _activity.RunOnUiThread(() =>
    {
        _view.Focusable = true;
        _view.FocusableInTouchMode = true;
        _view.RequestFocus();
        Imm?.ShowSoftInput(_view, ShowFlags.Implicit);
    });

    public void Stop() => _activity.RunOnUiThread(() =>
        Imm?.HideSoftInputFromWindow(_view.WindowToken, HideSoftInputFlags.None));

    // Android は候補 UI を OS キーボードが出すため、キャレット矩形の指定は不要（必要ならビューのスクロール等に利用）。
    public void SetCaretRect(Rect caret)
    {
    }

    /// <summary>game view<c>OnCreateInputConnection</c>Generate an InputConnection to return from.</summary>
    public IInputConnection CreateInputConnection(EditorInfo outAttrs)
    {
        outAttrs.InputType = InputTypes.ClassText | InputTypes.TextFlagNoSuggestions;
        outAttrs.ImeOptions = ImeFlags.NoExtractUi;
        return new HamonInputConnection(_host, _view);
    }

    /// <summary>Estimate the soft keyboard height from the visible height of the root view<see cref="HamonRoot.SoftKeyboardHeight"/>reflected in</summary>
    public void AttachKeyboardHeightWatcher(View root) => root.ViewTreeObserver!.GlobalLayout += (_, _) =>
    {
        var visible = new global::Android.Graphics.Rect();
        root.GetWindowVisibleDisplayFrame(visible);
        int covered = root.RootView!.Height - visible.Bottom;
        _host.SoftKeyboardHeight = covered > root.RootView.Height * 0.15f ? covered : 0f; // 15%超で「キーボードあり」
    };

    /// <summary>InputConnection that bridges commit/transform/delete to Hamon core.</summary>
    private sealed class HamonInputConnection : BaseInputConnection
    {
        private readonly HamonRoot _host;

        public HamonInputConnection(HamonRoot host, View targetView)
            : base(targetView, fullEditor: false) => _host = host;

        public override bool CommitText(Java.Lang.ICharSequence? text, int newCursorPosition)
        {
            _host.DispatchComposition(string.Empty, 0); // 確定＝変換中を消す
            if (text is not null)
            {
                foreach (char c in text.ToString())
                {
                    _host.DispatchText(c);
                }
            }

            return true;
        }

        public override bool SetComposingText(Java.Lang.ICharSequence? text, int newCursorPosition)
        {
            string s = text?.ToString() ?? string.Empty;
            _host.DispatchComposition(s, s.Length); // 変換中（preedit）
            return true;
        }

        public override bool FinishComposingText()
        {
            _host.DispatchComposition(string.Empty, 0);
            return true;
        }

        public override bool DeleteSurroundingText(int beforeLength, int afterLength)
        {
            for (int i = 0; i < beforeLength; i++)
            {
                _host.DispatchEditKey(TextEditKey.Backspace);
            }

            for (int i = 0; i < afterLength; i++)
            {
                _host.DispatchEditKey(TextEditKey.Delete);
            }

            return true;
        }

        public override bool SendKeyEvent(KeyEvent? e)
        {
            if (e is { Action: KeyEventActions.Down })
            {
                switch (e.KeyCode)
                {
                    case Keycode.Del: _host.DispatchEditKey(TextEditKey.Backspace); return true;
                    case Keycode.ForwardDel: _host.DispatchEditKey(TextEditKey.Delete); return true;
                    case Keycode.Enter: _host.DispatchEditKey(TextEditKey.Enter); return true;
                    case Keycode.DpadLeft: _host.DispatchEditKey(TextEditKey.Left); return true;
                    case Keycode.DpadRight: _host.DispatchEditKey(TextEditKey.Right); return true;
                }
            }

            return base.SendKeyEvent(e);
        }
    }
}
