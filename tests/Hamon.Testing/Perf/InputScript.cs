using Hamon.Layout;
using Hamon.Widgets;

namespace Hamon.Testing.Perf;

/// <summary>
/// A script that emulates operations on widgets frame by frame (<see cref="PerfHarness"/>pass to
/// <c>Action&lt;HamonRoot,int&gt;</c>(assemble).
/// Reproduce with the actual gesture route (HamonRoot.Dispatch*).
/// </summary>
public static class InputScript
{
    private const float Dt = 0.016f;

    /// <summary>Apply multiple scripts to each frame in order.</summary>
    public static Action<HamonRoot, int> Combine(params Action<HamonRoot, int>[] scripts) =>
        (host, f) =>
        {
            foreach (Action<HamonRoot, int> s in scripts)
            {
                s(host, f);
            }
        };

    /// <summary>
    /// D&D drag: Press at startFrame → move beyond slop in next frame (=<b>Start dragging</b>= Spike inspection target frame)
    /// → Continue with small movements → Finally release. <c>startFrame+1</c>。
    /// </summary>
    public static Action<HamonRoot, int> PointerDrag(Vec2 from, Vec2 to, int startFrame, int holdFrames) =>
        (host, f) =>
        {
            float t = f * Dt;
            if (f == startFrame)
            {
                host.DispatchPointer(new PointerEvent(from, PointerPhase.Down, t));
            }
            else if (f == startFrame + 1)
            {
                host.DispatchPointer(new PointerEvent(to, PointerPhase.Move, t)); // slop 超え＝ドラッグ開始
            }
            else if (f > startFrame + 1 && f <= startFrame + holdFrames)
            {
                var jitter = new Vec2(to.X + (f % 2), to.Y + ((f + 1) % 2));
                host.DispatchPointer(new PointerEvent(jitter, PointerPhase.Move, t));
            }
            else if (f == startFrame + holdFrames + 1)
            {
                host.DispatchPointer(new PointerEvent(to, PointerPhase.Up, t));
            }
        };

    /// <summary>Scroll: Gives wheel scroll by delta every frame from startFrame to frames (including fling trigger).</summary>
    public static Action<HamonRoot, int> Scroll(Vec2 at, float deltaPerFrame, int startFrame, int frames) =>
        (host, f) =>
        {
            if (f >= startFrame && f < startFrame + frames)
            {
                host.DispatchScroll(at, deltaPerFrame);
            }
        };

    /// <summary>
    /// Drag scroll by touch: Press at startFrame → move upward by dyPerFrame every frame (=scroll) →
    /// Release it to trigger a fling. <c>startFrame+1</c>, fling start = immediately after release.
    /// </summary>
    public static Action<HamonRoot, int> DragScroll(Vec2 at, float dyPerFrame, int startFrame, int frames) =>
        (host, f) =>
        {
            float t = f * Dt;
            if (f == startFrame)
            {
                host.DispatchPointer(new PointerEvent(at, PointerPhase.Down, t));
            }
            else if (f > startFrame && f < startFrame + frames)
            {
                var p = new Vec2(at.X, at.Y - (dyPerFrame * (f - startFrame)));
                host.DispatchPointer(new PointerEvent(p, PointerPhase.Move, t));
            }
            else if (f == startFrame + frames)
            {
                var p = new Vec2(at.X, at.Y - (dyPerFrame * frames));
                host.DispatchPointer(new PointerEvent(p, PointerPhase.Up, t)); // 解放＝フリング
            }
        };

    /// <summary>Keep the mouse hover moving within the screen (hover state update load).</summary>
    public static Action<HamonRoot, int> HoverSweep(int width, int height) =>
        (host, f) => host.DispatchHover(new Vec2(f % width, (f * 3) % height));

    /// <summary>Single tap (press and release) once in the specified frame.</summary>
    public static Action<HamonRoot, int> Tap(Vec2 at, int frame) =>
        (host, f) =>
        {
            float t = f * Dt;
            if (f == frame)
            {
                host.DispatchPointer(new PointerEvent(at, PointerPhase.Down, t));
                host.DispatchPointer(new PointerEvent(at, PointerPhase.Up, t + 0.01f));
            }
        };

    /// <summary>Do nothing (for steady frame baseline).</summary>
    public static Action<HamonRoot, int> None => (_, _) => { };
}
