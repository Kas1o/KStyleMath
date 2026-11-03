using System.Globalization;
using System.Text.RegularExpressions;

namespace KStyleMath;

// Data stored in the ParseNode associated with the environment.
public sealed class AlignSpec
{
    public string Type;
    public string Separator;
    public string Align;
    public double? Pregap;
    public double? Postgap;
}

/// <summary>Port of .katex-ref/src/environments/array.ts.</summary>
public static class ArrayFunctions
{
    private sealed class ArrayPayload
    {
        public bool? HskipBeforeAndAfter;
        public bool? AddJot;
        public List<AlignSpec> Cols;
        public double? Arraystretch;
        public string ColSeparationType;
        public bool? AutoTag;
        public bool? SingleRow;
        public bool? EmptySingleRow;
        public int? MaxNumCols;
        public bool? Leqno;
    }

    private sealed class Outrow
    {
        public List<HtmlDomNode> Cells;
        public double Height;
        public double Depth;
        public double Pos;
    }

    // Helper functions
    private static List<bool> GetHLines(Parser parser)
    {
        // Return an array. The array length = number of hlines.
        // Each element in the array tells if the line is dashed.
        var hlineInfo = new List<bool>();
        parser.ConsumeSpaces();
        var nxt = parser.Fetch().Text;
        if (nxt == "\\relax")
        { // \relax is an artifact of the \cr macro below
            parser.Consume();
            parser.ConsumeSpaces();
            nxt = parser.Fetch().Text;
        }
        while (nxt == "\\hline" || nxt == "\\hdashline")
        {
            parser.Consume();
            hlineInfo.Add(nxt == "\\hdashline");
            parser.ConsumeSpaces();
            nxt = parser.Fetch().Text;
        }
        return hlineInfo;
    }

    private static void ValidateAmsEnvironmentContext(EnvContext context)
    {
        var settings = context.Parser.Settings;
        if (!settings.DisplayMode)
        {
            throw new ParseError($"{{{context.EnvName}}} can be used only in" +
                " display mode.");
        }
    }

    private static readonly HashSet<string> GatherEnvironments = new(StringComparer.Ordinal)
        { "gather", "gather*" };

    // autoTag (an argument to parseArray) can be one of three values:
    // * undefined: Regular (not-top-level) array; no tags on each row
    // * true: Automatic equation numbering, overridable by \tag
    // * false: Tags allowed on each row, but no automatic numbering
    // This function *doesn't* work with the "split" environment name.
    private static bool? GetAutoTag(string name)
    {
        if (!name.Contains("ed"))
        {
            return !name.Contains("*");
        }
        return null;
        // return undefined;
    }

    /**
     * Parse the body of the environment, with rows delimited by \\ and
     * columns delimited by &, and create a nested list in row-major order
     * with one group per cell.  If given an optional argument style
     * ("text", "display", etc.), then each cell is cast into that style.
     */
    private static ParseNode ParseArray(Parser parser, ArrayPayload payload, string style)
    {
        parser.Gullet.BeginGroup();
        if (payload.SingleRow != true)
        {
            // \cr is equivalent to \\ without the optional size argument (see below)
            // TODO: provide helpful error when \cr is used outside array environment
            parser.Gullet.Macros.Set("\\cr", "\\\\\\relax");
        }

        // Get current arraystretch if it's not set by the environment
        double arraystretch;
        if (!(payload.Arraystretch > 0))
        {
            var stretch = parser.Gullet.ExpandMacroAsText("\\arraystretch");
            if (stretch == null)
            {
                // Default \arraystretch from lttab.dtx
                arraystretch = 1;
            }
            else
            {
                if (!double.TryParse(stretch, NumberStyles.Float, CultureInfo.InvariantCulture, out arraystretch))
                    arraystretch = double.NaN;
                if (arraystretch == 0 || double.IsNaN(arraystretch) || arraystretch < 0)
                {
                    throw new ParseError($"Invalid \\arraystretch: {stretch}");
                }
            }
        }
        else
        {
            arraystretch = payload.Arraystretch.Value;
        }

        // Start group for first cell
        parser.Gullet.BeginGroup();

        var row = new List<ParseNode>();
        var body = new List<List<ParseNode>> { row };
        var rowGaps = new List<Measurement>();
        var hLinesBeforeRow = new List<List<bool>>();
        var tags = payload.AutoTag != null ? new List<object>() : null;

        // amsmath uses \global\@eqnswtrue and \global\@eqnswfalse to represent
        // whether this row should have an equation number.  Simulate this with
        // a \@eqnsw macro set to 1 or 0.
        void BeginRow()
        {
            if (payload.AutoTag == true)
            {
                parser.Gullet.Macros.Set("\\@eqnsw", "1", true);
            }
        }
        void EndRow()
        {
            if (tags != null)
            {
                if (IsTruthy(parser.Gullet.Macros.Get("\\df@tag")))
                {
                    tags.Add(parser.Subparse(new List<Token> { new Token("\\df@tag") }));
                    parser.Gullet.Macros.Set("\\df@tag", null, true);
                }
                else
                {
                    tags.Add(payload.AutoTag == true &&
                        parser.Gullet.Macros.Get("\\@eqnsw") as string == "1");
                }
            }
        }
        BeginRow();

        // Test for \hline at the top of the array.
        hLinesBeforeRow.Add(GetHLines(parser));

        while (true)
        {
            // Parse each cell in its own group (namespace)
            var cellBody = parser.ParseExpression(false, payload.SingleRow == true ? "\\end" : "\\\\");
            parser.Gullet.EndGroup();
            parser.Gullet.BeginGroup();
            var cell = new ParseNode("ordgroup", parser.Mode) { ["body"] = cellBody };
            if (!string.IsNullOrEmpty(style))
            {
                cell = new ParseNode("styling", parser.Mode)
                {
                    ["style"] = style,
                    ["resetFont"] = true,
                    ["body"] = new List<ParseNode> { cell },
                };
            }
            row.Add(cell);
            var next = parser.Fetch().Text;
            if (next == "&")
            {
                if (payload.MaxNumCols > 0 && row.Count == payload.MaxNumCols.Value)
                {
                    if (payload.SingleRow == true || !string.IsNullOrEmpty(payload.ColSeparationType))
                    {
                        // {equation} or {split}
                        throw new ParseError("Too many tab characters: &",
                                            parser.NextToken);
                    }
                    else
                    {
                        // {array} environment
                        StrictHelper.Handle(parser.Settings.Strict, "textEnv",
                            "Too few columns specified in the {array} column argument.",
                            null, true);
                    }
                }
                parser.Consume();
            }
            else if (next == "\\end")
            {
                EndRow();
                // Arrays terminate newlines with `\crcr` which consumes a `\cr` if
                // the last line is empty.  However, AMS environments keep the
                // empty row if it's the only one, has a manual tag, or is an
                // automatically numbered row explicitly ended with `\\`.
                // NOTE: Currently, `cell` is the last item added into `row`.
                if (row.Count == 1 && cell.Type == "styling" &&
                    cell.List("body").Count == 1 && cell.List("body")[0].Type == "ordgroup" &&
                    cell.List("body")[0].List("body").Count == 0 &&
                    (body.Count > 1 || payload.EmptySingleRow != true) &&
                    !IsTagArray(tags) &&
                    !(payload.AutoTag == true && IsLastTagTrue(tags)))
                {
                    body.RemoveAt(body.Count - 1);
                }
                if (hLinesBeforeRow.Count < body.Count + 1)
                {
                    hLinesBeforeRow.Add(new List<bool>());
                }
                break;
            }
            else if (next == "\\\\")
            {
                parser.Consume();
                ParseNode size = null;
                // \def\Let@{\let\\\math@cr}
                // \def\math@cr{...\math@cr@}
                // \def\math@cr@{\new@ifnextchar[\math@cr@@{\math@cr@@[\z@]}}
                // \def\math@cr@@[#1]{...\math@cr@@@...}
                // \def\math@cr@@@{\cr}
                if (parser.Gullet.Future().Text != " ")
                {
                    size = parser.ParseSizeGroup(true);
                }
                rowGaps.Add(size != null ? size.Get<Measurement>("value") : null);
                EndRow();

                // check for \hline(s) following the row separator
                hLinesBeforeRow.Add(GetHLines(parser));

                row = new List<ParseNode>();
                body.Add(row);
                BeginRow();
            }
            else
            {
                throw new ParseError("Expected & or \\\\ or \\cr or \\end",
                                     parser.NextToken);
            }
        }

        // End cell group
        parser.Gullet.EndGroup();
        // End array group defining \cr
        parser.Gullet.EndGroup();

        var result = new ParseNode("array", parser.Mode)
        {
            ["arraystretch"] = arraystretch,
            ["body"] = body,
            ["rowGaps"] = rowGaps,
            ["hLinesBeforeRow"] = hLinesBeforeRow,
        };
        if (payload.AddJot != null) result["addJot"] = payload.AddJot.Value;
        if (payload.Cols != null) result["cols"] = payload.Cols;
        if (payload.HskipBeforeAndAfter != null) result["hskipBeforeAndAfter"] = payload.HskipBeforeAndAfter.Value;
        if (payload.ColSeparationType != null) result["colSeparationType"] = payload.ColSeparationType;
        if (tags != null) result["tags"] = tags;
        if (payload.Leqno != null) result["leqno"] = payload.Leqno.Value;
        return result;
    }

    private static bool IsTruthy(object value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            double d => d != 0,
            _ => true,
        };
    }

    private static bool IsTagArray(List<object> tags)
        => tags != null && tags.Count > 0 && tags[^1] is List<ParseNode>;

    private static bool IsLastTagTrue(List<object> tags)
        => tags != null && tags.Count > 0 && tags[^1] is bool b && b;

    private static AlignSpec At(List<AlignSpec> list, int index)
        => index >= 0 && index < list.Count ? list[index] : null;

    // Decides on a style for cells in an array according to whether the given
    // environment name starts with the letter 'd'.
    private static string DCellStyle(string envName)
    {
        if (envName.Substring(0, 1) == "d")
        {
            return "display";
        }
        else
        {
            return "text";
        }
    }

    private static VirtualNode HtmlBuilder(ParseNode group, Options options)
    {
        int r;
        int c;
        var bodyList = group.ListList("body");
        var nr = bodyList.Count;
        var hLinesBeforeRow = group.Get<List<List<bool>>>("hLinesBeforeRow");
        var nc = 0;
        var body = new Outrow[nr];
        var hlines = new List<(double Pos, bool IsDashed)>();
        var ruleThickness = Math.Max(
            // From LaTeX \showthe\arrayrulewidth. Equals 0.04 em.
            options.FontMetrics().ArrayRuleWidth,
            options.MinRuleThickness // User override.
        );

        // Horizontal spacing
        var pt = 1 / options.FontMetrics().PtPerEm;
        var arraycolsep = 5 * pt; // default value, i.e. \arraycolsep in article.cls
        if (!string.IsNullOrEmpty(group.Str("colSeparationType")) && group.Str("colSeparationType") == "small")
        {
            // We're in a {smallmatrix}. Default column space is \thickspace,
            // i.e. 5/18em = 0.2778em, per amsmath.dtx for {smallmatrix}.
            // But that needs adjustment because LaTeX applies \scriptstyle to the
            // entire array, including the colspace, but this function applies
            // \scriptstyle only inside each element.
            var localMultiplier = options.HavingStyle(Style.SCRIPT).SizeMultiplier;
            arraycolsep = 0.2778 * (localMultiplier / options.SizeMultiplier);
        }

        // Vertical spacing
        var baselineskip = group.Str("colSeparationType") == "CD"
            ? Units.CalculateSize(new Measurement(3, "ex"), options)
            : 12 * pt; // see size10.clo
        // Default \jot from ltmath.dtx
        // TODO(edemaine): allow overriding \jot via \setlength (#687)
        var jot = 3 * pt;
        var arrayskip = group.Num("arraystretch") * baselineskip;
        var arstrutHeight = 0.7 * arrayskip; // \strutbox in ltfsstrc.dtx and
        var arstrutDepth = 0.3 * arrayskip;  // \@arstrutbox in lttab.dtx

        var totalHeight = 0.0;

        // Set a position for \hline(s) at the top of the array, if any.
        void SetHLinePos(List<bool> hlinesInGap)
        {
            for (var i = 0; i < hlinesInGap.Count; ++i)
            {
                if (i > 0)
                {
                    totalHeight += 0.25;
                }
                hlines.Add((totalHeight, hlinesInGap[i]));
            }
        }
        SetHLinePos(hLinesBeforeRow[0]);

        for (r = 0; r < bodyList.Count; ++r)
        {
            var inrow = bodyList[r];
            var height = arstrutHeight; // \@array adds an \@arstrut
            var depth = arstrutDepth;   // to each tow (via the template)

            if (nc < inrow.Count)
            {
                nc = inrow.Count;
            }

            var outrow = new Outrow
            {
                Cells = Enumerable.Repeat<HtmlDomNode>(null, inrow.Count).ToList(),
                Height = 0,
                Depth = 0,
                Pos = 0,
            };
            for (c = 0; c < inrow.Count; ++c)
            {
                var elt = BuildHTML.BuildGroup(inrow[c], options);
                if (depth < elt.Depth)
                {
                    depth = elt.Depth;
                }
                if (height < elt.Height)
                {
                    height = elt.Height;
                }
                outrow.Cells[c] = elt;
            }

            var rowGaps = group.Get<List<Measurement>>("rowGaps");
            var rowGap = r < rowGaps.Count ? rowGaps[r] : null;
            var gap = 0.0;
            if (rowGap != null)
            {
                gap = Units.CalculateSize(rowGap, options);
                if (gap > 0)
                { // \@argarraycr
                    gap += arstrutDepth;
                    if (depth < gap)
                    {
                        depth = gap; // \@xargarraycr
                    }
                    gap = 0;
                }
            }
            // In AMS multiline environments such as aligned and gathered, rows
            // correspond to lines that have additional \jot added between lines
            // via \openup.
            // We simulate this by adding \jot depth to each row except the last.
            if (group.Bool("addJot") && r < bodyList.Count - 1)
            {
                depth += jot;
            }

            outrow.Height = height;
            outrow.Depth = depth;
            totalHeight += height;
            outrow.Pos = totalHeight;
            totalHeight += depth + gap; // \@yargarraycr
            body[r] = outrow;

            // Set a position for \hline(s), if any.
            SetHLinePos(hLinesBeforeRow[r + 1]);
        }

        var offset = totalHeight / 2 + options.FontMetrics().AxisHeight;
        var colDescriptions = group.Get<List<AlignSpec>>("cols") ?? new List<AlignSpec>();
        var cols = new List<HtmlDomNode>();
        int colDescrNum;
        var tagSpans = new List<VListElem>();
        var groupTags = group.Get<List<object>>("tags");

        if (groupTags != null && groupTags.Any(tag => IsTruthy(tag)))
        {
            // An environment with manual tags and/or automatic equation numbers.
            // Create node(s), the latter of which trigger CSS counter increment.
            for (r = 0; r < nr; ++r)
            {
                var rw = body[r];
                var shift = rw.Pos - offset;
                var tag = groupTags[r];
                HtmlDomNode tagSpan;
                if (tag is bool tb && tb)
                {  // automatic numbering
                    tagSpan = BuildCommon.MakeSpan(new List<string> { "eqn-num" }, new List<VirtualNode>(), options);
                }
                else if (tag is bool)
                {
                    // \nonumber/\notag or starred environment
                    tagSpan = BuildCommon.MakeSpan(new List<string>(), new List<VirtualNode>(), options);
                }
                else
                {  // manual \tag
                    tagSpan = BuildCommon.MakeSpan(new List<string>(),
                        BuildHTML.BuildExpression((List<ParseNode>)tag, options, true).Cast<VirtualNode>().ToList(), options);
                }
                tagSpan.Depth = rw.Depth;
                tagSpan.Height = rw.Height;
                tagSpans.Add(new VListElem { Type = "elem", Elem = tagSpan, Shift = shift });
            }
        }

        for (c = 0, colDescrNum = 0;
             // Continue while either there are more columns or more column
             // descriptions, so trailing separators don't get lost.
             c < nc || colDescrNum < colDescriptions.Count;
             ++c, ++colDescrNum)
        {
            var colDescr = At(colDescriptions, colDescrNum);

            var firstSeparator = true;
            while (colDescr != null && colDescr.Type == "separator")
            {
                // If there is more than one separator in a row, add a space
                // between them.
                if (!firstSeparator)
                {
                    var colSepSpan = BuildCommon.MakeSpan(new List<string> { "arraycolsep" }, new List<VirtualNode>());
                    colSepSpan.Style.Width = Units.MakeEm(options.FontMetrics().DoubleRuleSep);
                    cols.Add(colSepSpan);
                }

                if (colDescr.Separator == "|" || colDescr.Separator == ":")
                {
                    var lineType = colDescr.Separator == "|" ? "solid" : "dashed";
                    var separator = BuildCommon.MakeSpan(new List<string> { "vertical-separator" }, new List<VirtualNode>(), options);
                    separator.Style.Height = Units.MakeEm(totalHeight);
                    separator.Style.BorderRightWidth = Units.MakeEm(ruleThickness);
                    separator.Style.BorderRightStyle = lineType;
                    separator.Style.Margin = $"0 {Units.MakeEm(-ruleThickness / 2)}";
                    var shift = totalHeight - offset;
                    if (shift != 0)
                    {
                        separator.Style.VerticalAlign = Units.MakeEm(-shift);
                    }

                    cols.Add(separator);
                }
                else
                {
                    throw new ParseError(
                        "Invalid separator type: " + colDescr.Separator);
                }

                colDescrNum++;
                colDescr = At(colDescriptions, colDescrNum);
                firstSeparator = false;
            }

            if (c >= nc)
            {
                continue;
            }

            double sepwidth;
            if (c > 0 || group.Bool("hskipBeforeAndAfter"))
            {
                sepwidth = colDescr?.Pregap ?? arraycolsep;
                if (sepwidth != 0)
                {
                    var colSepSpan = BuildCommon.MakeSpan(new List<string> { "arraycolsep" }, new List<VirtualNode>());
                    colSepSpan.Style.Width = Units.MakeEm(sepwidth);
                    cols.Add(colSepSpan);
                }
            }

            var colElems = new List<VListElem>();
            for (r = 0; r < nr; ++r)
            {
                var row = body[r];
                var elem = c < row.Cells.Count ? row.Cells[c] : null;
                if (elem == null)
                {
                    continue;
                }
                var shift = row.Pos - offset;
                elem.Depth = row.Depth;
                elem.Height = row.Height;
                colElems.Add(new VListElem { Type = "elem", Elem = elem, Shift = shift });
            }

            var colVList = BuildCommon.MakeVList(
                VListParam.IndividualShift(colElems), options);
            var colAlign = !string.IsNullOrEmpty(colDescr?.Align) ? colDescr.Align : "c";
            var colSpan = BuildCommon.MakeSpan(
                new List<string> { "col-align-" + colAlign },
                new List<VirtualNode> { colVList });
            cols.Add(colSpan);

            if (c < nc - 1 || group.Bool("hskipBeforeAndAfter"))
            {
                sepwidth = colDescr?.Postgap ?? arraycolsep;
                if (sepwidth != 0)
                {
                    var colSepSpan = BuildCommon.MakeSpan(new List<string> { "arraycolsep" }, new List<VirtualNode>());
                    colSepSpan.Style.Width = Units.MakeEm(sepwidth);
                    cols.Add(colSepSpan);
                }
            }
        }

        HtmlDomNode tableBody = BuildCommon.MakeSpan(
            new List<string> { "mtable" }, cols.Cast<VirtualNode>().ToList());

        // Add \hline(s), if any.
        if (hlines.Count > 0)
        {
            var line = BuildCommon.MakeLineSpan("katex-hline", options, ruleThickness);
            var dashes = BuildCommon.MakeLineSpan("katex-hdashline", options, ruleThickness);
            var vListElems = new List<VListElem>
            {
                new VListElem { Type = "elem", Elem = tableBody, Shift = 0 },
            };
            while (hlines.Count > 0)
            {
                var hline = hlines[^1];
                hlines.RemoveAt(hlines.Count - 1);
                var lineShift = hline.Pos - offset;
                if (hline.IsDashed)
                {
                    vListElems.Add(new VListElem { Type = "elem", Elem = dashes, Shift = lineShift });
                }
                else
                {
                    vListElems.Add(new VListElem { Type = "elem", Elem = line, Shift = lineShift });
                }
            }

            tableBody = BuildCommon.MakeVList(
                VListParam.IndividualShift(vListElems), options);
        }

        if (tagSpans.Count == 0)
        {
            return BuildCommon.MakeSpan(new List<string> { "mord" }, new List<VirtualNode> { tableBody }, options);
        }
        else
        {
            var eqnNumCol = BuildCommon.MakeVList(
                VListParam.IndividualShift(tagSpans), options);
            var tagCol = BuildCommon.MakeSpan(new List<string> { "katex-tag" }, new List<VirtualNode> { eqnNumCol }, options);
            return BuildCommon.MakeFragment(new List<VirtualNode> { tableBody, tagCol });
        }
    }

    private static readonly Dictionary<string, string> AlignMap = new(StringComparer.Ordinal)
    {
        ["c"] = "center ",
        ["l"] = "left ",
        ["r"] = "right ",
    };

    private static VirtualNode MathmlBuilder(ParseNode group, Options options)
    {
        var tbl = new List<VirtualNode>();
        var glue = new MathNode("mtd", new List<VirtualNode>(), new List<string> { "mtr-glue" });
        var tag = new MathNode("mtd", new List<VirtualNode>(), new List<string> { "mml-eqn-num" });
        var groupBody = group.ListList("body");
        var groupTags = group.Get<List<object>>("tags");
        for (var i = 0; i < groupBody.Count; i++)
        {
            var rw = groupBody[i];
            var row = new List<VirtualNode>();
            for (var j = 0; j < rw.Count; j++)
            {
                row.Add(new MathNode("mtd",
                    new List<VirtualNode> { BuildMathML.BuildGroup(rw[j], options) }));
            }
            if (groupTags != null && i < groupTags.Count && IsTruthy(groupTags[i]))
            {
                row.Insert(0, glue);
                row.Add(glue);
                if (group.Bool("leqno"))
                {
                    row.Insert(0, tag);
                }
                else
                {
                    row.Add(tag);
                }
            }
            tbl.Add(new MathNode("mtr", row));
        }
        var table = new MathNode("mtable", tbl);

        // Set column alignment, row spacing, column spacing, and
        // array lines by setting attributes on the table element.

        // Set the row spacing. In MathML, we specify a gap distance.
        // We do not use rowGap[] because MathML automatically increases
        // cell height with the height/depth of the element content.

        // LaTeX \arraystretch multiplies the row baseline-to-baseline distance.
        // We simulate this by adding (arraystretch - 1)em to the gap. This
        // does a reasonable job of adjusting arrays containing 1 em tall content.

        // The 0.16 and 0.09 values are found empirically. They produce an array
        // similar to LaTeX and in which content does not interfere with \hlines.
        var gap = (group.Num("arraystretch") == 0.5)
            ? 0.1  // {smallmatrix}, {subarray}
            : 0.16 + group.Num("arraystretch") - 1 + (group.Bool("addJot") ? 0.09 : 0);
        table.SetAttribute("rowspacing", Units.MakeEm(gap));

        // MathML table lines go only between cells.
        // To place a line on an edge we'll use <menclose>, if necessary.
        var menclose = "";
        var align = "";

        var groupCols = group.Get<List<AlignSpec>>("cols");
        if (groupCols != null && groupCols.Count > 0)
        {
            // Find column alignment, column spacing, and  vertical lines.
            var cols = groupCols;
            var columnLines = "";
            var prevTypeWasAlign = false;
            var iStart = 0;
            var iEnd = cols.Count;

            if (cols[0].Type == "separator")
            {
                menclose += "top ";
                iStart = 1;
            }
            if (cols[cols.Count - 1].Type == "separator")
            {
                menclose += "bottom ";
                iEnd -= 1;
            }

            for (var i = iStart; i < iEnd; i++)
            {
                var col = cols[i];
                if (col.Type == "align")
                {
                    align += AlignMap[col.Align];

                    if (prevTypeWasAlign)
                    {
                        columnLines += "none ";
                    }
                    prevTypeWasAlign = true;
                }
                else if (col.Type == "separator")
                {
                    // MathML accepts only single lines between cells.
                    // So we read only the first of consecutive separators.
                    if (prevTypeWasAlign)
                    {
                        columnLines += col.Separator == "|" ? "solid " : "dashed ";
                        prevTypeWasAlign = false;
                    }
                }
            }

            table.SetAttribute("columnalign", align.Trim());

            if (Regex.IsMatch(columnLines, "[sd]"))
            {
                table.SetAttribute("columnlines", columnLines.Trim());
            }
        }

        // Set column spacing.
        if (group.Str("colSeparationType") == "align")
        {
            var cols = groupCols ?? new List<AlignSpec>();
            var spacing = "";
            for (var i = 1; i < cols.Count; i++)
            {
                spacing += i % 2 != 0 ? "0em " : "1em ";
            }
            table.SetAttribute("columnspacing", spacing.Trim());
        }
        else if (group.Str("colSeparationType") == "alignat" ||
            group.Str("colSeparationType") == "gather")
        {
            table.SetAttribute("columnspacing", "0em");
        }
        else if (group.Str("colSeparationType") == "small")
        {
            table.SetAttribute("columnspacing", "0.2778em");
        }
        else if (group.Str("colSeparationType") == "CD")
        {
            table.SetAttribute("columnspacing", "0.5em");
        }
        else
        {
            table.SetAttribute("columnspacing", "1em");
        }

        // Address \hline and \hdashline
        var rowLines = "";
        var hlines = group.Get<List<List<bool>>>("hLinesBeforeRow");

        menclose += hlines[0].Count > 0 ? "left " : "";
        menclose += hlines[hlines.Count - 1].Count > 0 ? "right " : "";

        for (var i = 1; i < hlines.Count - 1; i++)
        {
            rowLines += (hlines[i].Count == 0)
              ? "none "
              // MathML accepts only a single line between rows. Read one element.
              : hlines[i][0] ? "dashed " : "solid ";
        }
        if (Regex.IsMatch(rowLines, "[sd]"))
        {
            table.SetAttribute("rowlines", rowLines.Trim());
        }

        if (menclose != "")
        {
            table = new MathNode("menclose", new List<VirtualNode> { table });
            table.SetAttribute("notation", menclose.Trim());
        }

        if (group.Num("arraystretch") > 0 && group.Num("arraystretch") < 1)
        {
            // A small array. Wrap in scriptstyle so row gap is not too large.
            table = new MathNode("mstyle", new List<VirtualNode> { table });
            table.SetAttribute("scriptlevel", "1");
        }

        return table;
    }

    // Convenience function for align, align*, aligned, alignat, alignat*, alignedat.
    private static ParseNode AlignedHandler(EnvContext context, List<ParseNode> args, List<ParseNode> optArgs)
    {
        if (!context.EnvName.Contains("ed"))
        {
            ValidateAmsEnvironmentContext(context);
        }
        var cols = new List<AlignSpec>();
        var isSplit = context.EnvName == "split";
        var res = ParseArray(context.Parser, new ArrayPayload
        {
            Cols = cols,
            AddJot = true,
            AutoTag = isSplit ? null : GetAutoTag(context.EnvName),
            EmptySingleRow = true,
            ColSeparationType = context.EnvName.Contains("at") ? "alignat" : "align",
            MaxNumCols = isSplit ? 2 : null,
            Leqno = context.Parser.Settings.Leqno,
        }, "display");

        // Determining number of columns.
        // 1. If the first argument is given, we use it as a number of columns,
        //    and makes sure that each row doesn't exceed that number.
        // 2. Otherwise, just count number of columns = maximum number
        //    of cells in each row ("aligned" mode -- isAligned will be true).
        //
        // At the same time, prepend empty group {} at beginning of every second
        // cell in each row (starting with second cell) so that operators become
        // binary.  This behavior is implemented in amsmath's \start@aligned.
        double numMaths = 0;
        var numCols = 0;
        var emptyGroup = new ParseNode("ordgroup", context.Mode) { ["body"] = new List<ParseNode>() };
        var firstArg = args.Count > 0 ? args[0] : null;
        if (firstArg != null && firstArg.Type == "ordgroup")
        {
            const string message = "Number of columns should be a positive integer";
            var numColumns = ParseNodeUtil.AssertCharacterGroup(firstArg, message);
            if (!Regex.IsMatch(numColumns, "^[0-9]+$"))
            {
                throw new ParseError(message, firstArg);
            }
            if (!double.TryParse(numColumns, NumberStyles.Float, CultureInfo.InvariantCulture, out var numColumnsValue) ||
                numColumnsValue < 1)
            {
                throw new ParseError(message, firstArg);
            }
            numMaths = numColumnsValue;
            numCols = (int)(numMaths * 2);
        }
        var isAligned = numCols == 0;
        foreach (var row in res.ListList("body"))
        {
            for (var i = 1; i < row.Count; i += 2)
            {
                // Modify ordgroup node within styling node
                var styling = ParseNodeUtil.AssertNodeType(row[i], "styling");
                var ordgroup = ParseNodeUtil.AssertNodeType(styling.List("body")[0], "ordgroup");
                ordgroup.List("body").Insert(0, emptyGroup);
            }
            if (!isAligned)
            { // Case 1
                var curMaths = row.Count / 2.0;
                if (numMaths < curMaths)
                {
                    throw new ParseError(
                        "Too many math in a row: " +
                        $"expected {JsNumber.ToString(numMaths)}, but got {JsNumber.ToString(curMaths)}",
                        row[0]);
                }
            }
            else if (numCols < row.Count)
            { // Case 2
                numCols = row.Count;
            }
        }

        // Adjusting alignment.
        // In aligned mode, we add one \qquad between columns;
        // otherwise we add nothing.
        for (var i = 0; i < numCols; ++i)
        {
            var align = "r";
            var pregap = 0.0;
            if (i % 2 == 1)
            {
                align = "l";
            }
            else if (i > 0 && isAligned)
            { // "aligned" mode.
                pregap = 1; // add one \quad
            }
            cols.Add(new AlignSpec
            {
                Type = "align",
                Align = align,
                Pregap = pregap,
                Postgap = 0,
            });
        }
        res["colSeparationType"] = isAligned ? "align" : "alignat";
        return res;
    }

    public static void Define()
    {
        // Arrays are part of LaTeX, defined in lttab.dtx so its documentation
        // is part of the source2e.pdf file of LaTeX2e source documentation.
        // {darray} is an {array} environment where cells are set in \displaystyle,
        // as defined in nccmath.sty.
        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string> { "array", "darray" },
            NumArgs = 1,
            Handler = (context, args, optArgs) =>
            {
                // Since no types are specified above, the two possibilities are
                // - The argument is wrapped in {} or [], in which case Parser's
                //   parseGroup() returns an "ordgroup" wrapping some symbol node.
                // - The argument is a bare symbol node.
                var symNode = ParseNodeUtil.CheckSymbolNodeType(args[0]);
                var colalign = symNode != null
                    ? new List<ParseNode> { args[0] }
                    : ParseNodeUtil.AssertNodeType(args[0], "ordgroup").List("body");
                var cols = new List<AlignSpec>();
                foreach (var nde in colalign)
                {
                    var node = ParseNodeUtil.AssertSymbolNodeType(nde);
                    var ca = node.Str("text");
                    if ("lcr".Contains(ca))
                    {
                        cols.Add(new AlignSpec
                        {
                            Type = "align",
                            Align = ca,
                        });
                    }
                    else if (ca == "|")
                    {
                        cols.Add(new AlignSpec
                        {
                            Type = "separator",
                            Separator = "|",
                        });
                    }
                    else if (ca == ":")
                    {
                        cols.Add(new AlignSpec
                        {
                            Type = "separator",
                            Separator = ":",
                        });
                    }
                    else
                    {
                        throw new ParseError("Unknown column alignment: " + ca, nde);
                    }
                }
                var res = new ArrayPayload
                {
                    Cols = cols,
                    HskipBeforeAndAfter = true, // \@preamble in lttab.dtx
                    MaxNumCols = cols.Count,
                };
                return ParseArray(context.Parser, res, DCellStyle(context.EnvName));
            },
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        // The matrix environments of amsmath builds on the array environment
        // of LaTeX, which is discussed above.
        // The mathtools package adds starred versions of the same environments.
        // These have an optional argument to choose left|center|right justification.
        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string>
            {
                "matrix",
                "pmatrix",
                "bmatrix",
                "Bmatrix",
                "vmatrix",
                "Vmatrix",
                "matrix*",
                "pmatrix*",
                "bmatrix*",
                "Bmatrix*",
                "vmatrix*",
                "Vmatrix*",
            },
            NumArgs = 0,
            Handler = (context, args, optArgs) =>
            {
                List<string> delimiters;
                switch (context.EnvName.Replace("*", ""))
                {
                    case "pmatrix": delimiters = new List<string> { "(", ")" }; break;
                    case "bmatrix": delimiters = new List<string> { "[", "]" }; break;
                    case "Bmatrix": delimiters = new List<string> { "\\{", "\\}" }; break;
                    case "vmatrix": delimiters = new List<string> { "|", "|" }; break;
                    case "Vmatrix": delimiters = new List<string> { "\\Vert", "\\Vert" }; break;
                    default: delimiters = null; break;
                }
                // \hskip -\arraycolsep in amsmath
                var colAlign = "c";
                var payload = new ArrayPayload
                {
                    HskipBeforeAndAfter = false,
                    Cols = new List<AlignSpec> { new AlignSpec { Type = "align", Align = colAlign } },
                };
                if (context.EnvName.Substring(context.EnvName.Length - 1) == "*")
                {
                    // It's one of the mathtools starred functions.
                    // Parse the optional alignment argument.
                    var parser = context.Parser;
                    parser.ConsumeSpaces();
                    if (parser.Fetch().Text == "[")
                    {
                        parser.Consume();
                        parser.ConsumeSpaces();
                        colAlign = parser.Fetch().Text;
                        if (!"lcr".Contains(colAlign))
                        {
                            throw new ParseError("Expected l or c or r", parser.NextToken);
                        }
                        parser.Consume();
                        parser.ConsumeSpaces();
                        parser.Expect("]");
                        parser.Consume();
                        payload.Cols = new List<AlignSpec> { new AlignSpec { Type = "align", Align = colAlign } };
                    }
                }
                var res = ParseArray(context.Parser, payload, DCellStyle(context.EnvName));
                // Populate cols with the correct number of column alignment specs.
                var numCols = Math.Max(0, res.ListList("body").Select(row => row.Count).DefaultIfEmpty(0).Max());
                var colSpec = new AlignSpec { Type = "align", Align = colAlign };
                res["cols"] = Enumerable.Repeat(colSpec, numCols).ToList();
                if (delimiters != null)
                {
                    return new ParseNode("leftright", context.Mode)
                    {
                        ["body"] = new List<ParseNode> { res },
                        ["left"] = delimiters[0],
                        ["right"] = delimiters[1],
                        ["rightColor"] = null, // \right uninfluenced by \color in array
                    };
                }
                return res;
            },
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string> { "smallmatrix" },
            NumArgs = 0,
            Handler = (context, args, optArgs) =>
            {
                var payload = new ArrayPayload { Arraystretch = 0.5 };
                var res = ParseArray(context.Parser, payload, "script");
                res["colSeparationType"] = "small";
                return res;
            },
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string> { "subarray" },
            NumArgs = 1,
            Handler = (context, args, optArgs) =>
            {
                // Parsing of {subarray} is similar to {array}
                var symNode = ParseNodeUtil.CheckSymbolNodeType(args[0]);
                var colalign = symNode != null
                    ? new List<ParseNode> { args[0] }
                    : ParseNodeUtil.AssertNodeType(args[0], "ordgroup").List("body");
                var cols = new List<AlignSpec>();
                foreach (var nde in colalign)
                {
                    var node = ParseNodeUtil.AssertSymbolNodeType(nde);
                    var ca = node.Str("text");
                    // {subarray} only recognizes "l" & "c"
                    if ("lc".Contains(ca))
                    {
                        cols.Add(new AlignSpec
                        {
                            Type = "align",
                            Align = ca,
                        });
                    }
                    else
                    {
                        throw new ParseError("Unknown column alignment: " + ca, nde);
                    }
                }
                if (cols.Count > 1)
                {
                    throw new ParseError("{subarray} can contain only one column");
                }
                var payload = new ArrayPayload
                {
                    Cols = cols,
                    HskipBeforeAndAfter = false,
                    Arraystretch = 0.5,
                };
                var res = ParseArray(context.Parser, payload, "script");
                if (res.ListList("body").Count > 0 && res.ListList("body")[0].Count > 1)
                {
                    throw new ParseError("{subarray} can contain only one column");
                }
                return res;
            },
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        // A cases environment (in amsmath.sty) is almost equivalent to
        // \def\arraystretch{1.2}%
        // \left\{\begin{array}{@{}l@{\quad}l@{}} … \end{array}\right.
        // {dcases} is a {cases} environment where cells are set in \displaystyle,
        // as defined in mathtools.sty.
        // {rcases} is another mathtools environment. It's brace is on the right side.
        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string>
            {
                "cases",
                "dcases",
                "rcases",
                "drcases",
            },
            NumArgs = 0,
            Handler = (context, args, optArgs) =>
            {
                var payload = new ArrayPayload
                {
                    Arraystretch = 1.2,
                    Cols = new List<AlignSpec>
                    {
                        new AlignSpec
                        {
                            Type = "align",
                            Align = "l",
                            Pregap = 0,
                            // TODO(kevinb) get the current style.
                            // For now we use the metrics for TEXT style which is what we were
                            // doing before.  Before attempting to get the current style we
                            // should look at TeX's behavior especially for \over and matrices.
                            Postgap = 1.0, /* 1em quad */
                        },
                        new AlignSpec
                        {
                            Type = "align",
                            Align = "l",
                            Pregap = 0,
                            Postgap = 0,
                        },
                    },
                };
                var res = ParseArray(context.Parser, payload, DCellStyle(context.EnvName));
                return new ParseNode("leftright", context.Mode)
                {
                    ["body"] = new List<ParseNode> { res },
                    ["left"] = context.EnvName.Contains("r") ? "." : "\\{",
                    ["right"] = context.EnvName.Contains("r") ? "\\}" : ".",
                    ["rightColor"] = null,
                };
            },
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        // In the align environment, one uses ampersands, &, to specify number of
        // columns in each row, and to locate spacing between each column.
        // align gets automatic numbering. align* and aligned do not.
        // The alignedat environment can be used in math mode.
        // Note that we assume \nomallineskiplimit to be zero,
        // so that \strut@ is the same as \strut.
        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string> { "align", "align*", "aligned", "split" },
            NumArgs = 0,
            Handler = AlignedHandler,
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        // A gathered environment is like an array environment with one centered
        // column, but where rows are considered lines so get \jot line spacing
        // and contents are set in \displaystyle.
        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string> { "gathered", "gather", "gather*" },
            NumArgs = 0,
            Handler = (context, args, optArgs) =>
            {
                if (GatherEnvironments.Contains(context.EnvName))
                {
                    ValidateAmsEnvironmentContext(context);
                }
                var res = new ArrayPayload
                {
                    Cols = new List<AlignSpec>
                    {
                        new AlignSpec
                        {
                            Type = "align",
                            Align = "c",
                        },
                    },
                    AddJot = true,
                    ColSeparationType = "gather",
                    AutoTag = GetAutoTag(context.EnvName),
                    EmptySingleRow = true,
                    Leqno = context.Parser.Settings.Leqno,
                };
                return ParseArray(context.Parser, res, "display");
            },
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        // alignat environment is like an align environment, but one must explicitly
        // specify maximum number of columns in each row, and can adjust spacing between
        // each columns.
        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string> { "alignat", "alignat*", "alignedat" },
            NumArgs = 1,
            Handler = AlignedHandler,
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string> { "equation", "equation*" },
            NumArgs = 0,
            Handler = (context, args, optArgs) =>
            {
                ValidateAmsEnvironmentContext(context);
                var res = new ArrayPayload
                {
                    AutoTag = GetAutoTag(context.EnvName),
                    EmptySingleRow = true,
                    SingleRow = true,
                    MaxNumCols = 1,
                    Leqno = context.Parser.Settings.Leqno,
                };
                return ParseArray(context.Parser, res, "display");
            },
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        Registries.DefineEnvironment(new EnvSpec
        {
            Type = "array",
            Names = new List<string> { "CD" },
            NumArgs = 0,
            Handler = (context, args, optArgs) =>
            {
                ValidateAmsEnvironmentContext(context);
                return CdFunctions.ParseCD(context.Parser);
            },
            HtmlBuilder = HtmlBuilder,
            MathmlBuilder = MathmlBuilder,
        });

        MacrosData.DefineMacro("\\nonumber", "\\gdef\\@eqnsw{0}");
        MacrosData.DefineMacro("\\notag", "\\nonumber");

        // Catch \hline outside array environment
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "text", // Doesn't matter what this is.
            Names = new List<string> { "\\hline", "\\hdashline" },
            NumArgs = 0,
            AllowedInText = true,
            AllowedInMath = true,

            Handler = (context, args, optArgs) =>
            {
                throw new ParseError(
                    $"{context.FuncName} valid only within array environment");
            },
        });
    }
}


