using Hamon.Layout;
using System.Text;

namespace Hamon.Widgets;

/// <summary>
/// Diagnostic utilities during development (tree dump group).
/// Use from debug overlay/log/test.
/// </summary>
public static class HamonDebug
{
    /// <summary>Enumerate the type names of the Widget/Element tree with indentation.</summary>
    public static string DumpWidgetTree(Element? root)
    {
        var sb = new StringBuilder();
        DumpWidget(root, 0, sb);
        return sb.ToString();
    }

    private static void DumpWidget(Element? element, int depth, StringBuilder sb)
    {
        if (element is null)
        {
            return;
        }

        sb.Append(' ', depth * 2).Append(TypeName(element.Widget)).Append('\n');
        IReadOnlyList<Element> children = element.Children;
        for (int i = 0; i < children.Count; i++)
        {
            DumpWidget(children[i], depth + 1, sb);
        }
    }

    /// <summary>List the type name + fixed layout rectangle of each element with indentation (for identifying layout defects).</summary>
    public static string DumpLayout(Element? root)
    {
        var sb = new StringBuilder();
        DumpLayoutNode(root, 0, sb);
        return sb.ToString();
    }

    private static void DumpLayoutNode(Element? element, int depth, StringBuilder sb)
    {
        if (element is null)
        {
            return;
        }

        Rect b = element.LayoutNode.Bounds;
        sb.Append(' ', depth * 2)
          .Append(TypeName(element.Widget))
          .Append(" (").Append(R(b.X)).Append(',').Append(R(b.Y)).Append(' ').Append(R(b.Width)).Append('x').Append(R(b.Height)).Append(")\n");
        IReadOnlyList<Element> children = element.Children;
        for (int i = 0; i < children.Count; i++)
        {
            DumpLayoutNode(children[i], depth + 1, sb);
        }
    }

    /// <summary>
    /// Dumps the hit test path (foremost → root direction ancestor chain) at the specified coordinates.
    /// Used to identify the cause of "tap not working" (opaque element in the foreground, rectangle removed).
    /// </summary>
    public static string DumpHitTest(Element? root, Vec2 position)
    {
        var sb = new StringBuilder();
        sb.Append("HitTest @ (").Append(R(position.X)).Append(',').Append(R(position.Y)).Append(")\n");
        Element? hit = HitTest(root, position);
        if (hit is null)
        {
            sb.Append("  (no hit)\n");
            return sb.ToString();
        }

        // ヒット要素から祖先へ辿って経路を出す（手前が上）。
        int depth = 0;
        for (Element? e = hit; e is not null; e = e.Parent)
        {
            Rect b = e.LayoutNode.Bounds;
            sb.Append(' ', depth * 2).Append("> ").Append(TypeName(e.Widget))
              .Append(" (").Append(R(b.X)).Append(',').Append(R(b.Y)).Append(' ').Append(R(b.Width)).Append('x').Append(R(b.Height)).Append(')')
              .Append(e.WantsPointer ? " [pointer]" : string.Empty)
              .Append('\n');
            depth++;
        }

        return sb.ToString();
    }

    private static Element? HitTest(Element? element, Vec2 position)
    {
        if (element is null || !element.LayoutNode.Bounds.Contains(position.X, position.Y))
        {
            return null;
        }

        IReadOnlyList<Element> children = element.Children;
        for (int i = children.Count - 1; i >= 0; i--)
        {
            Element? hit = HitTest(children[i], position);
            if (hit is not null)
            {
                return hit;
            }
        }

        return element.WantsPointer ? element : null;
    }

    private static string TypeName(Widget widget) => widget.GetType().Name;

    private static int R(float v) => (int)MathF.Round(v);
}
