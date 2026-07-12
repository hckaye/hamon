namespace Hamon.Layout;

/// <summary>
/// Layout solver for Flexbox subset (equivalent to Yoga lite / Flutter Flex).
/// The size of each node is determined by a single pass with constraints down/size up + grow/shrink distribution on the main axis.
/// Determine the absolute rectangle.
/// </summary>
public static class FlexLayoutEngine
{
    /// <summary>Lay out the route under constraints, and<see cref="LayoutNode.Bounds"/>Determine using absolute coordinates.</summary>
    public static void Layout(LayoutNode root, BoxConstraints constraints)
    {
        Measure(root, constraints);
        Arrange(root, 0f, 0f);
    }

    /// <summary>Measure a single node (subtree). </summary>
    public static Size MeasureNode(LayoutNode node, BoxConstraints constraints) => Measure(node, constraints);

    /// <summary>
    /// Relayout only the subtree (targeted relayout).<paramref name="node"/>Remeasure with the same constraints as last time,
    /// Reposition from the current absolute position.
    /// </summary>
    public static void RelayoutSubtree(LayoutNode node)
    {
        Measure(node, node.LastConstraints);
        Arrange(node, node.Bounds.X, node.Bounds.Y);
    }

    private static void Arrange(LayoutNode node, float x, float y)
    {
        node.Bounds = new Rect(x, y, node.Size.Width, node.Size.Height);
        IReadOnlyList<LayoutNode> children = node.Children;
        for (int i = 0; i < children.Count; i++)
        {
            LayoutNode child = children[i];
            Arrange(child, x + child.OffsetX, y + child.OffsetY);
        }
    }

    private static Size Measure(LayoutNode node, BoxConstraints constraints)
    {
        node.LastConstraints = constraints; // 部分木再レイアウト（RelayoutSubtree）のために保持

        if (node.Virtual is not null)
        {
            node.Size = node.Virtual.Measure(node, constraints);
            return node.Size;
        }

        float? fixedW = node.Style.Width.Resolve(constraints.MaxWidth);
        float? fixedH = node.Style.Height.Resolve(constraints.MaxHeight);

        if (node.Style.Kind == LayoutKind.Stack)
        {
            node.Size = MeasureStack(node, constraints, fixedW, fixedH);
            return node.Size;
        }

        if (node.Style.Kind == LayoutKind.Scroll)
        {
            node.Size = MeasureScroll(node, constraints, fixedW, fixedH);
            return node.Size;
        }

        if (node.Style.Kind == LayoutKind.Box)
        {
            node.Size = MeasureBox(node, constraints, fixedW, fixedH);
            return node.Size;
        }

        if (node.Style.Kind == LayoutKind.Wrap)
        {
            node.Size = MeasureWrap(node, constraints, fixedW, fixedH);
            return node.Size;
        }

        if (node.Children.Count == 0)
        {
            Size content = Size.Zero;
            if (node.Measure is not null)
            {
                var measureConstraints = new BoxConstraints(0, fixedW ?? constraints.MaxWidth, 0, fixedH ?? constraints.MaxHeight);
                content = node.Measure(measureConstraints);
            }

            float w = fixedW ?? content.Width;
            float h = fixedH ?? content.Height;
            Size sized = ApplyMinMax(node.Style, new Size(w, h), constraints);
            node.Size = constraints.Constrain(sized);
            return node.Size;
        }

        node.Size = MeasureFlex(node, constraints, fixedW, fixedH);
        return node.Size;
    }

    /// <summary>
    /// Scroll viewport.
    /// on the main axis<see cref="LayoutNode.ScrollOffset"/>Place it with a minus offset by the amount (extra portions are clipped on the drawing side).
    /// </summary>
    private static Size MeasureScroll(LayoutNode node, BoxConstraints constraints, float? fixedW, float? fixedH)
    {
        bool vertical = node.Style.Direction == Axis.Vertical;
        float vpW = fixedW ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : 0f);
        float vpH = fixedH ?? (float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight : 0f);
        Size viewport = constraints.Constrain(new Size(vpW, vpH));

        if (node.Children.Count > 0)
        {
            LayoutNode child = node.Children[0];
            BoxConstraints childConstraints = vertical
                ? new BoxConstraints(viewport.Width, viewport.Width, 0f, float.PositiveInfinity)
                : new BoxConstraints(0f, float.PositiveInfinity, viewport.Height, viewport.Height);
            Measure(child, childConstraints);

            float offset = node.ScrollOffset;
            child.OffsetX = vertical ? 0f : -offset;
            child.OffsetY = vertical ? -offset : 0f;
        }

        return viewport;
    }

    /// <summary>
    /// Single Child Box (Flutter<c>Container</c>/<c>Padding</c>/proxy equivalent).
    /// Pass it to the child (tight is transmitted as it is = child fills under Expanded/Stretch/Positioned).
    /// Pass that tight.
    /// </summary>
    // 制約 [min..max] 内で width/height == ratio を満たすサイズを決める。両軸 unbounded のときは null（比率を確定できない）。
    private static Size? ResolveAspect(float ratio, float maxW, float maxH, float minW, float minH)
    {
        float w, h;
        if (float.IsFinite(maxW))
        {
            w = maxW;
            h = w / ratio;
            if (float.IsFinite(maxH) && h > maxH)
            {
                h = maxH;
                w = h * ratio;
            }
        }
        else if (float.IsFinite(maxH))
        {
            h = maxH;
            w = h * ratio;
        }
        else
        {
            return null; // 両軸 unbounded＝サイズを確定できない
        }

        // 下限を満たす（比率より最小寸法を優先）。
        w = Math.Max(w, minW);
        h = Math.Max(h, minH);
        return new Size(w, h);
    }

    // 折り返しフロー（Wrap）。子を主軸へ並べ、主軸が尽きたら交差軸へ改行。子は loose 測定。
    private static Size MeasureWrap(LayoutNode node, BoxConstraints constraints, float? fixedW, float? fixedH)
    {
        bool horizontal = node.Style.Direction != Axis.Vertical;
        float spacing = node.Style.Spacing;       // 主軸間隔
        float runSpacing = node.Style.RunSpacing; // 行間
        IReadOnlyList<LayoutNode> children = node.Children;

        float maxMain = horizontal
            ? (fixedW ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : float.PositiveInfinity))
            : (fixedH ?? (float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight : float.PositiveInfinity));

        var loose = new BoxConstraints(0f, float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : float.PositiveInfinity, 0f, float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight : float.PositiveInfinity);

        float lineMain = 0f;     // 現在行の主軸使用量
        float lineCross = 0f;    // 現在行の交差軸最大
        float crossOffset = 0f;  // これまでの行の交差軸合計（＋行間）
        float maxLineMain = 0f;  // 全行で最大の主軸使用量
        int lineStart = 0;       // 現在行の先頭 index

        for (int i = 0; i < children.Count; i++)
        {
            Size s = Measure(children[i], loose);
            float childMain = horizontal ? s.Width : s.Height;
            float childCross = horizontal ? s.Height : s.Width;

            float add = lineMain == 0f ? childMain : lineMain + spacing + childMain;
            if (lineMain > 0f && add > maxMain)
            {
                // 改行：現在行を確定し、次行へ。
                PlaceWrapLine(children, lineStart, i, crossOffset, spacing, horizontal);
                maxLineMain = Math.Max(maxLineMain, lineMain);
                crossOffset += lineCross + runSpacing;
                lineMain = childMain;
                lineCross = childCross;
                lineStart = i;
            }
            else
            {
                lineMain = add;
                lineCross = Math.Max(lineCross, childCross);
            }
        }

        // 最終行を確定。
        PlaceWrapLine(children, lineStart, children.Count, crossOffset, spacing, horizontal);
        maxLineMain = Math.Max(maxLineMain, lineMain);
        float totalCross = crossOffset + lineCross;

        float w = fixedW ?? (horizontal ? maxLineMain : totalCross);
        float h = fixedH ?? (horizontal ? totalCross : maxLineMain);
        return constraints.Constrain(ApplyMinMax(node.Style, new Size(w, h), constraints));
    }

    // 1 行分の子を主軸方向に spacing を挟んで詰め、交差軸 crossOffset の位置へ配置する。
    private static void PlaceWrapLine(IReadOnlyList<LayoutNode> children, int start, int end, float crossOffset, float spacing, bool horizontal)
    {
        float main = 0f;
        for (int i = start; i < end; i++)
        {
            LayoutNode c = children[i];
            float childMain = horizontal ? c.Size.Width : c.Size.Height;
            if (horizontal)
            {
                c.OffsetX = main;
                c.OffsetY = crossOffset;
            }
            else
            {
                c.OffsetX = crossOffset;
                c.OffsetY = main;
            }

            main += childMain + spacing;
        }
    }

    private static Size MeasureBox(LayoutNode node, BoxConstraints constraints, float? fixedW, float? fixedH)
    {
        EdgeInsets pad = node.Style.Padding;
        float padW = pad.Horizontal;
        float padH = pad.Vertical;

        // 子へ渡す制約：固定があれば tight(fixed)、無ければ incoming を引き継ぎ、padding 分だけ deflate（min<=max を維持）。
        float minW = fixedW ?? constraints.MinWidth;
        float maxW = fixedW ?? constraints.MaxWidth;
        float minH = fixedH ?? constraints.MinHeight;
        float maxH = fixedH ?? constraints.MaxHeight;

        float innerMinW = Math.Max(0f, minW - padW);
        float innerMaxW = float.IsFinite(maxW) ? Math.Max(innerMinW, maxW - padW) : float.PositiveInfinity;
        float innerMinH = Math.Max(0f, minH - padH);
        float innerMaxH = float.IsFinite(maxH) ? Math.Max(innerMinH, maxH - padH) : float.PositiveInfinity;
        var inner = new BoxConstraints(innerMinW, innerMaxW, innerMinH, innerMaxH);

        // アスペクト比指定（固定寸法が無いとき）：制約内で w/h=ratio を満たすサイズに自分を決め、子はその tight で充填。
        if (node.Style.AspectRatio > 0f && fixedW is null && fixedH is null)
        {
            Size? ratioSize = ResolveAspect(node.Style.AspectRatio, maxW, maxH, minW, minH);
            if (ratioSize is Size rs)
            {
                IReadOnlyList<LayoutNode> aspectChildren = node.Children;
                if (aspectChildren.Count > 0)
                {
                    var childInner = BoxConstraints.Tight(new Size(Math.Max(0f, rs.Width - padW), Math.Max(0f, rs.Height - padH)));
                    Measure(aspectChildren[0], childInner);
                    aspectChildren[0].OffsetX = pad.Left;
                    aspectChildren[0].OffsetY = pad.Top;
                }

                return constraints.Constrain(rs);
            }
        }

        // 子整列あり（Align/Center/Container.alignment/Button）：子は loose（自然サイズ）で測り、ボックス内にアンカー配置。
        // 無し（proxy/padding）：子は inner（tight 引き継ぎ）で測り＝充填。ボックスサイズは固定 or 子＋padding を制約でクランプ
        //（＝Expanded/Stretch/固定の tight が来ればそのサイズに、loose なら子に縮む。充填したい Center/Align は Width/Height=100% を使う）。
        bool aligned = node.Style.ChildAlign is not null;
        BoxConstraints childConstraints = aligned ? new BoxConstraints(0f, innerMaxW, 0f, innerMaxH) : inner;

        IReadOnlyList<LayoutNode> children = node.Children;
        Size childSize = Size.Zero;
        if (children.Count > 0)
        {
            childSize = Measure(children[0], childConstraints);
        }
        else if (node.Measure is not null)
        {
            childSize = node.Measure(childConstraints);
        }

        float w = fixedW ?? (childSize.Width + padW);
        float h = fixedH ?? (childSize.Height + padH);
        Size boxSize = constraints.Constrain(ApplyMinMax(node.Style, new Size(w, h), constraints));

        if (children.Count > 0)
        {
            if (node.Style.ChildAlign is Alignment align)
            {
                AlignFractions(align, out float ax, out float ay);
                float innerW = Math.Max(0f, boxSize.Width - padW);
                float innerH = Math.Max(0f, boxSize.Height - padH);
                children[0].OffsetX = pad.Left + ((innerW - childSize.Width) * ax);
                children[0].OffsetY = pad.Top + ((innerH - childSize.Height) * ay);
            }
            else
            {
                children[0].OffsetX = pad.Left;
                children[0].OffsetY = pad.Top;
            }
        }

        return boxSize;
    }

    /// <summary>
    /// Stack arrangement (Stack).
    /// The non-locator is<see cref="Style.StackAlignment"/>Anchor, placer (<c>Positioned</c>) is determined as a rectangle by inset.
    /// </summary>
    private static Size MeasureStack(LayoutNode node, BoxConstraints constraints, float? fixedW, float? fixedH)
    {
        float availW = fixedW ?? (float.IsFinite(constraints.MaxWidth) ? constraints.MaxWidth : float.PositiveInfinity);
        float availH = fixedH ?? (float.IsFinite(constraints.MaxHeight) ? constraints.MaxHeight : float.PositiveInfinity);

        IReadOnlyList<LayoutNode> children = node.Children;
        int count = children.Count;

        // --- Phase 1: 非配置子を loose 測定し、最大サイズからノードサイズを得る ---
        float maxW = 0f;
        float maxH = 0f;
        for (int i = 0; i < count; i++)
        {
            LayoutNode child = children[i];
            if (child.Style.Positioned)
            {
                continue;
            }

            Size s = Measure(child, new BoxConstraints(0, availW, 0, availH));
            maxW = Math.Max(maxW, s.Width);
            maxH = Math.Max(maxH, s.Height);
        }

        float w = fixedW ?? maxW;
        float h = fixedH ?? maxH;
        Size nodeSize = constraints.Constrain(ApplyMinMax(node.Style, new Size(w, h), constraints));
        float boxW = nodeSize.Width;
        float boxH = nodeSize.Height;

        bool expand = node.Style.StackExpandChildren;
        AlignFractions(node.Style.StackAlignment, out float ax, out float ay);

        // --- Phase 2: 各子を配置（配置子はインセット、非配置子はアンカー/充填） ---
        for (int i = 0; i < count; i++)
        {
            LayoutNode child = children[i];
            if (child.Style.Positioned)
            {
                Size intrinsic = Measure(child, new BoxConstraints(0, boxW, 0, boxH));
                ResolveAxis(child.Style.Left.Resolve(boxW), child.Style.Right.Resolve(boxW), child.Style.Width.Resolve(boxW), boxW, intrinsic.Width, out float x, out float cw);
                ResolveAxis(child.Style.Top.Resolve(boxH), child.Style.Bottom.Resolve(boxH), child.Style.Height.Resolve(boxH), boxH, intrinsic.Height, out float y, out float ch);
                Measure(child, BoxConstraints.Tight(new Size(cw, ch)));
                child.OffsetX = x;
                child.OffsetY = y;
            }
            else
            {
                if (expand)
                {
                    Measure(child, BoxConstraints.Tight(nodeSize));
                }

                child.OffsetX = (boxW - child.Size.Width) * ax;
                child.OffsetY = (boxH - child.Size.Height) * ay;
            }
        }

        return nodeSize;
    }

    /// <summary>Solve the absolute configuration of one axis. </summary>
    private static void ResolveAxis(float? start, float? end, float? size, float extent, float intrinsic, out float offset, out float length)
    {
        if (start.HasValue && end.HasValue)
        {
            offset = start.Value;
            length = extent - start.Value - end.Value;
        }
        else
        {
            length = size ?? intrinsic;
            if (start.HasValue)
            {
                offset = start.Value;
            }
            else if (end.HasValue)
            {
                offset = extent - end.Value - length;
            }
            else
            {
                offset = 0f;
            }
        }

        length = Math.Max(0f, length);
    }

    private static void AlignFractions(Alignment alignment, out float ax, out float ay)
    {
        ax = alignment switch
        {
            Alignment.TopCenter or Alignment.Center or Alignment.BottomCenter => 0.5f,
            Alignment.TopRight or Alignment.CenterRight or Alignment.BottomRight => 1f,
            _ => 0f,
        };
        ay = alignment switch
        {
            Alignment.CenterLeft or Alignment.Center or Alignment.CenterRight => 0.5f,
            Alignment.BottomLeft or Alignment.BottomCenter or Alignment.BottomRight => 1f,
            _ => 0f,
        };
    }

    private static Size MeasureFlex(LayoutNode node, BoxConstraints constraints, float? fixedW, float? fixedH)
    {
        bool row = node.Style.Direction == Axis.Horizontal;
        EdgeInsets pad = node.Style.Padding;

        float padMain = row ? pad.Horizontal : pad.Vertical;
        float padCross = row ? pad.Vertical : pad.Horizontal;

        float? fixedMain = row ? fixedW : fixedH;
        float? fixedCross = row ? fixedH : fixedW;

        float maxMainConstraint = row ? constraints.MaxWidth : constraints.MaxHeight;
        float maxCrossConstraint = row ? constraints.MaxHeight : constraints.MaxWidth;

        // パディング控除で負になったら0にクランプ（内容が箱より大きいときは例外でなくオーバーフローさせる）。
        float availMain = fixedMain.HasValue
            ? Math.Max(0f, fixedMain.Value - padMain)
            : (float.IsFinite(maxMainConstraint) ? Math.Max(0f, maxMainConstraint - padMain) : float.PositiveInfinity);
        float availCross = fixedCross.HasValue
            ? Math.Max(0f, fixedCross.Value - padCross)
            : (float.IsFinite(maxCrossConstraint) ? Math.Max(0f, maxCrossConstraint - padCross) : float.PositiveInfinity);

        IReadOnlyList<LayoutNode> children = node.Children;
        int count = children.Count;
        float spacingTotal = count > 1 ? node.Style.Spacing * (count - 1) : 0f;

        // --- Phase 1: 各子の主軸ベースサイズ ---
        float totalBase = 0f;
        float totalGrow = 0f;
        float totalShrinkWeighted = 0f;
        for (int i = 0; i < count; i++)
        {
            LayoutNode child = children[i];
            Dimension childMainDim = row ? child.Style.Width : child.Style.Height;
            float marginMain = MainMargin(row, child.Style.Margin);

            float baseMain;
            float? fixedChildMain = childMainDim.Resolve(availMain);
            if (child.Style.FlexGrow > 0f)
            {
                // flex 子（Expanded/Spacer/Flexible）は intrinsic を基準にせず、余剰主軸を取り分として得る。
                baseMain = 0f;
            }
            else if (fixedChildMain.HasValue)
            {
                baseMain = fixedChildMain.Value;
            }
            else if (!child.Style.FlexBasis.IsAuto && child.Style.FlexBasis.Resolve(availMain) is float basis)
            {
                baseMain = basis;
            }
            else
            {
                BoxConstraints intrinsic = MakeConstraints(row, 0, float.PositiveInfinity, 0, availCross);
                Size measured = Measure(child, intrinsic);
                baseMain = MainOf(row, measured);
            }

            child.BaseMain = baseMain;
            totalBase += baseMain + marginMain;
            totalGrow += child.Style.FlexGrow;
            totalShrinkWeighted += child.Style.FlexShrink * baseMain;
        }

        // --- Phase 2: 余剰/不足の主軸を grow/shrink で分配（spacing を差し引く） ---
        float free = float.IsFinite(availMain) ? availMain - spacingTotal - totalBase : 0f;
        for (int i = 0; i < count; i++)
        {
            LayoutNode child = children[i];
            float main = child.BaseMain;
            if (free > 0f && totalGrow > 0f)
            {
                main += child.Style.FlexGrow / totalGrow * free;
            }
            else if (free < 0f && totalShrinkWeighted > 0f)
            {
                main += child.Style.FlexShrink * child.BaseMain / totalShrinkWeighted * free;
            }

            child.FinalMain = Math.Max(0f, ClampMain(row, child.Style, main, availMain));
        }

        // --- Phase 3: 交差軸サイズを決めて各子を最終レイアウト ---
        float crossContent = 0f;
        for (int i = 0; i < count; i++)
        {
            LayoutNode child = children[i];
            Dimension childCrossDim = row ? child.Style.Height : child.Style.Width;
            float marginCross = CrossMargin(row, child.Style.Margin);
            float crossAvail = float.IsFinite(availCross) ? availCross - marginCross : float.PositiveInfinity;

            bool stretch = node.Style.CrossAxisAlignment == CrossAxisAlignment.Stretch && childCrossDim.IsAuto && float.IsFinite(crossAvail);
            BoxConstraints childConstraints = stretch
                ? MakeConstraints(row, child.FinalMain, child.FinalMain, crossAvail, crossAvail)
                : MakeConstraints(row, child.FinalMain, child.FinalMain, 0, crossAvail);

            Size childSize = Measure(child, childConstraints);
            crossContent = Math.Max(crossContent, CrossOf(row, childSize) + marginCross);
        }

        // --- ノード自身のサイズ（ボーダーボックス） ---
        float mainContentExtent;
        if (fixedMain.HasValue)
        {
            mainContentExtent = fixedMain.Value - padMain;
        }
        else if (node.Style.MainAxisSize == MainAxisSize.Max && float.IsFinite(availMain))
        {
            mainContentExtent = availMain;
        }
        else
        {
            mainContentExtent = SumMainUsed(row, children) + spacingTotal;
        }

        float crossExtent = fixedCross.HasValue ? fixedCross.Value - padCross : crossContent;

        Size nodeSize = MakeSize(row, mainContentExtent + padMain, crossExtent + padCross);
        nodeSize = constraints.Constrain(ApplyMinMax(node.Style, nodeSize, constraints));

        // --- Phase 4: 主軸 justify ＋ 交差軸 align で各子を配置 ---
        float contentMainFinal = MainOf(row, nodeSize) - padMain;
        float contentCrossFinal = CrossOf(row, nodeSize) - padCross;
        PlaceChildren(node, row, pad, contentMainFinal, contentCrossFinal, spacingTotal);

        return nodeSize;
    }

    private static void PlaceChildren(LayoutNode node, bool row, EdgeInsets pad, float contentMain, float contentCross, float spacingTotal)
    {
        IReadOnlyList<LayoutNode> children = node.Children;
        int count = children.Count;

        float usedMain = SumMainUsed(row, children) + spacingTotal;
        float freeSpace = contentMain - usedMain;

        ComputeJustify(node.Style.MainAxisAlignment, freeSpace, count, out float leading, out float gap);

        float padMainLeading = row ? pad.Left : pad.Top;
        float padCrossLeading = row ? pad.Top : pad.Left;
        float spacing = node.Style.Spacing;

        float cursor = padMainLeading + leading;
        for (int i = 0; i < count; i++)
        {
            LayoutNode child = children[i];
            float marginMainStart = MainMarginStart(row, child.Style.Margin);
            float marginMainEnd = MainMarginEnd(row, child.Style.Margin);
            float marginCrossStart = CrossMarginStart(row, child.Style.Margin);
            float marginCross = CrossMargin(row, child.Style.Margin);

            float childCross = CrossOf(row, child.Size);
            float crossFree = contentCross - (childCross + marginCross);
            float crossOffset = node.Style.CrossAxisAlignment switch
            {
                CrossAxisAlignment.Center => crossFree / 2f,
                CrossAxisAlignment.End => crossFree,
                _ => 0f, // Start / Stretch
            };

            float mainPos = cursor + marginMainStart;
            float crossPos = padCrossLeading + crossOffset + marginCrossStart;

            child.OffsetX = row ? mainPos : crossPos;
            child.OffsetY = row ? crossPos : mainPos;

            cursor = mainPos + child.FinalMain + marginMainEnd + gap;
            if (i < count - 1)
            {
                cursor += spacing;
            }
        }
    }

    private static void ComputeJustify(MainAxisAlignment justify, float freeSpace, int count, out float leading, out float gap)
    {
        leading = 0f;
        gap = 0f;
        if (count == 0)
        {
            return;
        }

        switch (justify)
        {
            case MainAxisAlignment.Center:
                leading = freeSpace / 2f;
                break;
            case MainAxisAlignment.End:
                leading = freeSpace;
                break;
            case MainAxisAlignment.SpaceBetween:
                gap = count > 1 ? freeSpace / (count - 1) : 0f;
                break;
            case MainAxisAlignment.SpaceAround:
                gap = freeSpace / count;
                leading = gap / 2f;
                break;
            case MainAxisAlignment.SpaceEvenly:
                gap = freeSpace / (count + 1);
                leading = gap;
                break;
            default: // Start
                break;
        }
    }

    // --- 主軸/交差軸ヘルパ（direction で写像） ---

    private static float MainOf(bool row, Size s) => row ? s.Width : s.Height;
    private static float CrossOf(bool row, Size s) => row ? s.Height : s.Width;
    private static Size MakeSize(bool row, float main, float cross) => row ? new Size(main, cross) : new Size(cross, main);

    private static BoxConstraints MakeConstraints(bool row, float mainMin, float mainMax, float crossMin, float crossMax) =>
        row
            ? new BoxConstraints(mainMin, mainMax, crossMin, crossMax)
            : new BoxConstraints(crossMin, crossMax, mainMin, mainMax);

    private static float MainMargin(bool row, EdgeInsets m) => row ? m.Horizontal : m.Vertical;
    private static float CrossMargin(bool row, EdgeInsets m) => row ? m.Vertical : m.Horizontal;
    private static float MainMarginStart(bool row, EdgeInsets m) => row ? m.Left : m.Top;
    private static float MainMarginEnd(bool row, EdgeInsets m) => row ? m.Right : m.Bottom;
    private static float CrossMarginStart(bool row, EdgeInsets m) => row ? m.Top : m.Left;

    private static float SumMainUsed(bool row, IReadOnlyList<LayoutNode> children)
    {
        float sum = 0f;
        for (int i = 0; i < children.Count; i++)
        {
            sum += children[i].FinalMain + MainMargin(row, children[i].Style.Margin);
        }

        return sum;
    }

    private static float ClampMain(bool row, Style style, float value, float availMain)
    {
        Dimension min = row ? style.MinWidth : style.MinHeight;
        Dimension max = row ? style.MaxWidth : style.MaxHeight;
        if (min.Resolve(availMain) is float lo)
        {
            value = Math.Max(value, lo);
        }

        if (max.Resolve(availMain) is float hi)
        {
            value = Math.Min(value, hi);
        }

        return value;
    }

    private static Size ApplyMinMax(Style style, Size size, BoxConstraints constraints)
    {
        float w = size.Width;
        float h = size.Height;
        if (style.MinWidth.Resolve(constraints.MaxWidth) is float minW)
        {
            w = Math.Max(w, minW);
        }

        if (style.MaxWidth.Resolve(constraints.MaxWidth) is float maxW)
        {
            w = Math.Min(w, maxW);
        }

        if (style.MinHeight.Resolve(constraints.MaxHeight) is float minH)
        {
            h = Math.Max(h, minH);
        }

        if (style.MaxHeight.Resolve(constraints.MaxHeight) is float maxH)
        {
            h = Math.Min(h, maxH);
        }

        return new Size(w, h);
    }
}
