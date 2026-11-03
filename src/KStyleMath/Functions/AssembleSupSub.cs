namespace KStyleMath;

public static class AssembleSupSub
{
    public static Span Assemble(HtmlDomNode baseNode, ParseNode supGroup, ParseNode subGroup,
        Options options, Style style, double slant, double baseShift)
        => AssembleSupSubFunc(baseNode, supGroup, subGroup, options, style, slant, baseShift);

    public static Span AssembleSupSubFunc(HtmlDomNode baseNode, ParseNode supGroup, ParseNode subGroup,
        Options options, Style style, double slant, double baseShift)
    {
        baseNode = BuildCommon.MakeSpan(new List<string>(), new List<VirtualNode> { baseNode });
        bool subIsSingleCharacter = subGroup != null && Utils.IsCharacterBox(subGroup);
        (HtmlDomNode Elem, double Kern)? sub = null;
        (HtmlDomNode Elem, double Kern)? sup = null;

        if (supGroup != null)
        {
            var elem = BuildHTML.BuildGroup(supGroup, options.HavingStyle(style.Sup()), options);
            sup = (elem, Math.Max(options.FontMetrics().BigOpSpacing1,
                options.FontMetrics().BigOpSpacing3 - elem.Depth));
        }
        if (subGroup != null)
        {
            var elem = BuildHTML.BuildGroup(subGroup, options.HavingStyle(style.Sub()), options);
            sub = (elem, Math.Max(options.FontMetrics().BigOpSpacing2,
                options.FontMetrics().BigOpSpacing4 - elem.Height));
        }

        Span finalGroup;
        if (sup != null && sub != null)
        {
            double bottom = options.FontMetrics().BigOpSpacing5 +
                            sub.Value.Elem.Height + sub.Value.Elem.Depth +
                            sub.Value.Kern + baseNode.Depth + baseShift;
            finalGroup = BuildCommon.MakeVList(VListParam.Bottom(bottom, new List<object>
            {
                new VListKern { Size = options.FontMetrics().BigOpSpacing5 },
                new VListElem { Elem = sub.Value.Elem, MarginLeft = Units.MakeEm(-slant) },
                new VListKern { Size = sub.Value.Kern },
                new VListElem { Elem = baseNode },
                new VListKern { Size = sup.Value.Kern },
                new VListElem { Elem = sup.Value.Elem, MarginLeft = Units.MakeEm(slant) },
                new VListKern { Size = options.FontMetrics().BigOpSpacing5 },
            }), options);
        }
        else if (sub != null)
        {
            double top = baseNode.Height - baseShift;
            finalGroup = BuildCommon.MakeVList(VListParam.Top(top, new List<object>
            {
                new VListKern { Size = options.FontMetrics().BigOpSpacing5 },
                new VListElem { Elem = sub.Value.Elem, MarginLeft = Units.MakeEm(-slant) },
                new VListKern { Size = sub.Value.Kern },
                new VListElem { Elem = baseNode },
            }), options);
        }
        else if (sup != null)
        {
            double bottom = baseNode.Depth + baseShift;
            finalGroup = BuildCommon.MakeVList(VListParam.Bottom(bottom, new List<object>
            {
                new VListElem { Elem = baseNode },
                new VListKern { Size = sup.Value.Kern },
                new VListElem { Elem = sup.Value.Elem, MarginLeft = Units.MakeEm(slant) },
                new VListKern { Size = options.FontMetrics().BigOpSpacing5 },
            }), options);
        }
        else
        {
            return (Span)baseNode;
        }

        var parts = new List<VirtualNode> { finalGroup };
        if (sub != null && slant != 0 && !subIsSingleCharacter)
        {
            var spacer = BuildCommon.MakeSpan(new List<string> { "mspace" }, new List<VirtualNode>(), options);
            spacer.Style.MarginRight = Units.MakeEm(slant);
            parts.Insert(0, spacer);
        }
        return BuildCommon.MakeSpan(new List<string> { "mop", "op-limits" }, parts, options);
    }
}

