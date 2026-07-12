using Hamon.Layout;
using Hamon.Widgets;
using System.Runtime.InteropServices;
using System.Text;

namespace Hamon.MonoGame;

/// <summary>
/// Desktop (DesktopGL=SDL2)<see cref="ITextInput"/>implementation.
/// Enabling IME (<c>SDL_StartTextInput</c>)・Candidate window position (<c>SDL_SetTextInputRect</c>）・
/// <b>Text being converted (<c>SDL_TEXTEDITING</c>) monitoring</b>(because MonoGame does not publish it)<c>SDL_AddEventWatch</c>).
/// Fixed characters are MonoGame's<c>Window.TextInput</c>Via (the app<see cref="HamonRoot.DispatchText"/>flow to).
/// In environments where SDL2 cannot be resolved/there are no symbols<b>Safely degenerate to no-op</b>(Confirm input will continue to work).
/// </summary>
public sealed class SdlTextInput : ITextInput, IDisposable
{
    // SDL2 のネイティブ名候補（OS により異なる。MonoGame が既にプロセスへロード済み）。
    private static readonly string[] LibNames = { "SDL2", "libSDL2-2.0.so.0", "libSDL2-2.0.0.dylib", "SDL2.dll", "libSDL2.dylib" };
    private const uint SdlTextEditing = 0x302; // SDL_TEXTEDITING

    private readonly Action<string, int> _onComposition; // (preedit, caret) → HamonRoot.DispatchComposition
    private readonly Native.EventFilter? _watch;          // GC に回収されないよう保持
    private readonly bool _ok;
    private volatile bool _disposed;

    static SdlTextInput()
    {
        // "SDL2" を、ロード可能な実体名へ解決する（MonoGame がロード済みなら多くの場合これで繋がる）。
        NativeLibrary.SetDllImportResolver(typeof(SdlTextInput).Assembly, (name, asm, path) =>
        {
            if (name != "SDL2")
            {
                return IntPtr.Zero;
            }

            foreach (string candidate in LibNames)
            {
                if (NativeLibrary.TryLoad(candidate, out IntPtr handle))
                {
                    return handle;
                }
            }

            return IntPtr.Zero;
        });
    }

    public SdlTextInput(Action<string, int> onComposition)
    {
        _onComposition = onComposition;
        try
        {
            _watch = OnSdlEvent;
            Native.SDL_AddEventWatch(_watch, IntPtr.Zero); // TEXTEDITING を横取り
            _ok = true;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            _ok = false; // SDL に届かない＝preedit/候補位置は諦め、確定入力のみ（縮退）
        }
    }

    public void Start()
    {
        if (_ok)
        {
            TryNative(static () => Native.SDL_StartTextInput());
        }
    }

    public void Stop()
    {
        if (_ok)
        {
            TryNative(static () => Native.SDL_StopTextInput());
        }
    }

    public void SetCaretRect(Rect caret)
    {
        if (!_ok)
        {
            return;
        }

        var rect = new Native.SDL_Rect
        {
            X = (int)caret.X,
            Y = (int)caret.Y,
            W = (int)System.MathF.Ceiling(caret.Width),
            H = (int)System.MathF.Ceiling(caret.Height),
        };
        TryNative(() => Native.SDL_SetTextInputRect(ref rect));
    }

    // SDL イベント監視コールバック（SDL_PumpEvents＝メインスレッドから呼ばれる）。TEXTEDITING だけ拾う。
    private int OnSdlEvent(IntPtr userdata, IntPtr evt)
    {
        if (_disposed)
        {
            return 0; // 破棄後に発火しても古いコールバックを呼ばない（DelEventWatch 競合の保険）
        }

        if ((uint)Marshal.ReadInt32(evt) == SdlTextEditing)
        {
            // SDL_TextEditingEvent: type(4) timestamp(4) windowID(4) text[32] start(int) length(int)
            byte[] buffer = new byte[32];
            Marshal.Copy(evt + 12, buffer, 0, 32);
            int len = Array.IndexOf(buffer, (byte)0);
            string text = Encoding.UTF8.GetString(buffer, 0, len < 0 ? 32 : len);
            int start = Marshal.ReadInt32(evt + 44);
            _onComposition(text, Math.Clamp(start, 0, text.Length)); // キャレットを preedit 範囲内へクランプ
        }

        return 0; // 監視は戻り値を無視する
    }

    /// <summary>Remove event monitoring and stop the IME (preventing watch failure during multiple generation/re-initialization = old callback firing).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_ok && _watch is not null)
        {
            TryNative(() => Native.SDL_DelEventWatch(_watch, IntPtr.Zero));
            TryNative(static () => Native.SDL_StopTextInput());
        }
    }

    private static void TryNative(Action call)
    {
        try
        {
            call();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // 縮退（呼べないだけ）。
        }
    }

    private static class Native
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int EventFilter(IntPtr userdata, IntPtr evt);

        [StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct SDL_Rect
        {
            public int X;
            public int Y;
            public int W;
            public int H;
        }

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_StartTextInput();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_StopTextInput();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_SetTextInputRect(ref SDL_Rect rect);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_AddEventWatch(EventFilter filter, IntPtr userdata);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        public static extern void SDL_DelEventWatch(EventFilter filter, IntPtr userdata);
    }
}
