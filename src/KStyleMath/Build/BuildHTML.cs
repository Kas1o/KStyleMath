namespace KStyleMath;

/// <summary>
/// This file does the main work of building a domTree structure from a parse
/// tree. The entry point is the <c>Build</c> function, which takes a parse tree.
/// Then, the BuildExpression, BuildGroup, and various groupBuilders functions
/// are called, to produce a final HTML tree.
/// </summary>
public static class BuildHTML
{
    // Binary atoms (first class `mbin`) change into ordinary atoms (`mord`)
    // depending on their surroundings. See TeXbook pg. 442-446, Rules 5 and 6,
    // and the text before Rule 19.
    private static readonly HashSet<string> BinLeftCanceller = new(StringComparer.Ordinal)
    {
        "leftmost", "mbin", "mopen", "mrel", "mop", "mpunct",
    };

    private static readonly HashSet<string> BinRightCanceller = new(StringComparer.Ordinal)
    {
        "rightmost", "mrel", "mclose", "mpunct",
    };

    private static readonly Dictionary<string, Style> StyleMap = new(StringComparer.Ordinal)
    {
        ["display"] = Style.DISPLAY,
        ["text"] = Style.TEXT,
        ["script"] = Style.SCRIPT,
        ["scriptscript"] = Style.SCRIPTSCRIPT,
    };

    private static readonly HashSet<string> DomEnum = new(StringComparer.Ordinal)
    {
        "mord", "mop", "mbin", "mrel", "mopen", "mclose", "mpunct", "minner",
    };

    private sealed class TraversePrev
    {
        public HtmlDomNode Node;
        public Action<HtmlDomNode> InsertAfter;
    }

    // Spacing relationships for display and text styles (spacingData.ts).
    private static readonly Dictionary<string, Dictionary<string, Measurement>> Spacings =
        new(StringComparer.Ordinal)
        {
            ["mord"] = new(StringComparer.Ordinal)
            {
                ["mop"] = new Measurement(3, "mu"),
                ["mbin"] = new Measurement(4, "mu"),
                ["mrel"] = new Measurement(5, "mu"),
                ["minner"] = new Measurement(3, "mu"),
            },
            ["mop"] = new(StringComparer.Ordinal)
            {
                ["mord"] = new Measurement(3, "mu"),
                ["mop"] = new Measurement(3, "mu"),
                ["mrel"] = new Measurement(5, "mu"),
                ["minner"] = new Measurement(3, "mu"),
            },
            ["mbin"] = new(StringComparer.Ordinal)
            {
                ["mord"] = new Measurement(4, "mu"),
                ["mop"] = new Measurement(4, "mu"),
                ["mopen"] = new Measurement(4, "mu"),
                ["minner"] = new Measurement(4, "mu"),
            },
            ["mrel"] = new(StringComparer.Ordinal)
            {
                ["mord"] = new Measurement(5, "mu"),
                ["mop"] = new Measurement(5, "mu"),
                ["mopen"] = new Measurement(5, "mu"),
                ["minner"] = new Measurement(5, "mu"),
            },
            ["mopen"] = new(StringComparer.Ordinal),
            ["mclose"] = new(StringComparer.Ordinal)
            {
                ["mop"] = new Measurement(3, "mu"),
                ["mbin"] = new Measurement(4, "mu"),
                ["mrel"] = new Measurement(5, "mu"),
                ["minner"] = new Measurement(3, "mu"),
            },
            ["mpunct"] = new(StringComparer.Ordinal)
            {
                ["mord"] = new Measurement(3, "mu"),
                ["mop"] = new Measurement(3, "mu"),
                ["mrel"] = new Measurement(5, "mu"),
                ["mopen"] = new Measurement(3, "mu"),
                ["mclose"] = new Measurement(3, "mu"),
                ["mpunct"] = new Measurement(3, "mu"),
                ["minner"] = new Measurement(3, "mu"),
            },
            ["minner"] = new(StringComparer.Ordinal)
            {
                ["mord"] = new Measurement(3, "mu"),
                ["mop"] = new Measurement(3, "mu"),
                ["mbin"] = new Measurement(4, "mu"),
                ["mrel"] = new Measurement(5, "mu"),
                ["mopen"] = new Measurement(3, "mu"),
                ["mpunct"] = new Measurement(3, "mu"),
                ["minner"] = new Measurement(3, "mu"),
            },
        };

    // Spacing relationships for script and scriptscript styles.
    private static readonly Dictionary<string, Dictionary<string, Measurement>> TightSpacings =
        new(StringComparer.Ordinal)
        {
            ["mord"] = new(StringComparer.Ordinal)
            {
                ["mop"] = new Measurement(3, "mu"),
            },
            ["mop"] = new(StringComparer.Ordinal)
            {
                ["mord"] = new Measurement(3, "mu"),
                ["mop"] = new Measurement(3, "mu"),
            },
            ["mbin"] = new(StringComparer.Ordinal),
            ["mrel"] = new(StringComparer.Ordinal),
            ["mopen"] = new(StringComparer.Ordinal),
            ["mclose"] = new(StringComparer.Ordinal)
            {
                ["mop"] = new Measurement(3, "mu"),
            },
            ["mpunct"] = new(StringComparer.Ordinal),
            ["minner"] = new(StringComparer.Ordinal)
            {
                ["mop"] = new Measurement(3, "mu"),
            },
        };

    private static bool IsRealGroup(object isRealGroup)
    {
        if (isRealGroup is bool b) return b;
        if (isRealGroup is string s) return s.Length > 0;
        return isRealGroup != null;
    }

    /// <summary>
    /// Take a list of nodes, build them in order, and return a list of the built
    /// nodes. documentFragments are flattened into their contents, so the
    /// returned list contains no fragments. `isRealGroup` is true if `expression`
    /// is a real group (no atoms will be added on either side), as opposed to
    /// a partial group (e.g. one created by \color). `surrounding` is an array
    /// consisting type of nodes that will be added to the left and right.
    /// </summary>
    public static List<HtmlDomNode> BuildExpression(
        List<ParseNode> expression,
        Options options,
        object isRealGroup,
        string[] surrounding = null)
    {
        // Parse expressions into `groups`.
        var groups = new List<VirtualNode>();
        for (int i = 0; i < expression.Count; i++)
        {
            var output = BuildGroup(expression[i], options);
            if (output is DocumentFragment fragment)
            {
                var children = fragment.Children;
                groups.AddRange(children);
            }
            else
            {
                groups.Add(output);
            }
        }

        // Combine consecutive domTree.symbolNodes into a single symbolNode.
        BuildCommon.TryCombineChars(groups);

        // If `expression` is a partial group, let the parent handle spacings
        // to avoid processing groups multiple times.
        if (!IsRealGroup(isRealGroup))
        {
            return groups.Cast<HtmlDomNode>().ToList();
        }

        Options glueOptions = options;
        if (expression.Count == 1)
        {
            var node = expression[0];
            if (node.Type == "sizing")
            {
                glueOptions = options.HavingSize((int)node.Num("size"));
            }
            else if (node.Type == "styling")
            {
                var styleName = node.Str("style");
                glueOptions = options.HavingStyle(
                    styleName != null && StyleMap.TryGetValue(styleName, out var mappedStyle)
                        ? mappedStyle
                        : Style.TEXT);
            }
        }

        // Dummy spans for determining spacings between surrounding atoms.
        // If `expression` has no atoms on the left or right, class "leftmost"
        // or "rightmost", respectively, is used to indicate it.
        string surroundingLeft = surrounding != null && surrounding.Length > 0 ? surrounding[0] : null;
        string surroundingRight = surrounding != null && surrounding.Length > 1 ? surrounding[1] : null;
        var dummyPrev = BuildCommon.MakeSpan(
            new List<string> { !string.IsNullOrEmpty(surroundingLeft) ? surroundingLeft : "leftmost" },
            new List<VirtualNode>(), options);
        var dummyNext = BuildCommon.MakeSpan(
            new List<string> { !string.IsNullOrEmpty(surroundingRight) ? surroundingRight : "rightmost" },
            new List<VirtualNode>(), options);

        // TODO: These code assumes that a node's math class is the first element
        // of its `classes` array. A later cleanup should ensure this, for
        // instance by changing the signature of `makeSpan`.

        // Before determining what spaces to insert, perform bin cancellation.
        // Binary operators change to ordinary symbols in some contexts.
        bool isRoot = isRealGroup is string str && str == "root";
        TraverseNonSpaceNodes(groups, (node, prev) =>
        {
            var prevType = FirstClass(prev);
            var type = FirstClass(node);
            if (prevType == "mbin" && BinRightCanceller.Contains(type))
            {
                if (prev.Classes.Count > 0) prev.Classes[0] = "mord";
            }
            else if (type == "mbin" && BinLeftCanceller.Contains(prevType))
            {
                if (node.Classes.Count > 0) node.Classes[0] = "mord";
            }
            return null;
        }, new TraversePrev { Node = dummyPrev }, dummyNext, isRoot);

        TraverseNonSpaceNodes(groups, (node, prev) =>
        {
            var prevType = GetTypeOfDomTree(prev);
            var type = GetTypeOfDomTree(node);

            // 'mtight' indicates that the node is script or scriptscript style.
            Measurement space = null;
            if (prevType != null && type != null)
            {
                if (node.HasClass("mtight"))
                {
                    if (TightSpacings.TryGetValue(prevType, out var row)
                        && row.TryGetValue(type, out var tightSpace))
                        space = tightSpace;
                }
                else
                {
                    if (Spacings.TryGetValue(prevType, out var normalRow)
                        && normalRow.TryGetValue(type, out var normalSpace))
                        space = normalSpace;
                }
            }

            if (space != null)
            {
                // Insert glue (spacing) after the `prev`.
                return BuildCommon.MakeGlue(space, glueOptions);
            }
            return null;
        }, new TraversePrev { Node = dummyPrev }, dummyNext, isRoot);

        return groups.Cast<HtmlDomNode>().ToList();
    }

    private static string FirstClass(HtmlDomNode node)
        => node.Classes.Count > 0 ? node.Classes[0] : null;

    // Depth-first traverse non-space `nodes`, calling `callback` with the current and
    // previous node as arguments, optionally returning a node to insert after the
    // previous node. `prev` is an object with the previous node and `insertAfter`
    // function to insert after it. `next` is a node that will be added to the right.
    // Used for bin cancellation and inserting spacings.
    private static void TraverseNonSpaceNodes(
        List<VirtualNode> nodes,
        Func<HtmlDomNode, HtmlDomNode, HtmlDomNode> callback,
        TraversePrev prev,
        HtmlDomNode next,
        bool isRoot)
    {
        if (next != null)
        {
            // temporarily append the right node, if exists
            nodes.Add(next);
        }
        int i = 0;
        for (; i < nodes.Count; i++)
        {
            var node = (HtmlDomNode)nodes[i];
            var partialGroup = CheckPartialGroup(node);

            if (partialGroup != null)
            {
                // Recursive DFS
                TraverseNonSpaceNodes(GetPartialGroupChildren(partialGroup), callback, prev, null, isRoot);
                continue;
            }

            // Ignore explicit spaces (e.g., \;, \,) when determining what implicit
            // spacing should go between atoms of different classes
            bool nonspace = !node.HasClass("mspace");
            if (nonspace)
            {
                var result = callback(node, prev.Node);
                if (result != null)
                {
                    if (prev.InsertAfter != null)
                    {
                        prev.InsertAfter(result);
                    }
                    else
                    {
                        // insert at front
                        nodes.Insert(0, result);
                        i++;
                    }
                }
            }

            if (nonspace)
            {
                prev.Node = node;
            }
            else if (isRoot && node.HasClass("katex-newline"))
            {
                prev.Node = BuildCommon.MakeSpan(new List<string> { "leftmost" }); // treat like beginning of line
            }
            int index = i;
            prev.InsertAfter = n =>
            {
                nodes.Insert(index + 1, n);
                i++;
            };
        }

        if (next != null)
        {
            nodes.RemoveAt(nodes.Count - 1);
        }
    }

    // Check if given node is a partial group, i.e., does not affect spacing around.
    private static HtmlDomNode CheckPartialGroup(HtmlDomNode node)
    {
        if (node is DocumentFragment || node is Anchor
            || (node is Span && node.HasClass("enclosing")))
        {
            return node;
        }
        return null;
    }

    private static List<VirtualNode> GetPartialGroupChildren(HtmlDomNode node)
        => node switch
        {
            DocumentFragment f => f.Children,
            Anchor a => a.Children,
            Span s => s.Children,
            _ => new List<VirtualNode>(),
        };

    // Return the outermost node of a domTree.
    private static HtmlDomNode GetOutermostNode(HtmlDomNode node, string side)
    {
        var partialGroup = CheckPartialGroup(node);
        if (partialGroup != null)
        {
            var children = GetPartialGroupChildren(partialGroup);
            if (children.Count > 0)
            {
                if (side == "right")
                {
                    return GetOutermostNode((HtmlDomNode)children[children.Count - 1], "right");
                }
                else if (side == "left")
                {
                    return GetOutermostNode((HtmlDomNode)children[0], "left");
                }
            }
        }
        return node;
    }

    // Return math atom class (mclass) of a domTree.
    // If `side` is given, it will get the type of the outermost node at given side.
    public static string GetTypeOfDomTree(HtmlDomNode node, string side = null)
    {
        if (node == null)
        {
            return null;
        }
        if (!string.IsNullOrEmpty(side))
        {
            node = GetOutermostNode(node, side);
        }
        // This makes a lot of assumptions as to where the type of atom
        // appears.  We should do a better job of enforcing this.
        if (node.Classes.Count == 0) return null;
        var className = node.Classes[0];
        return DomEnum.Contains(className) ? className : null;
    }

    public static Span MakeNullDelimiter(Options options, List<string> classes)
    {
        var moreClasses = new List<string> { "nulldelimiter" };
        moreClasses.AddRange(options.BaseSizingClasses());
        return BuildCommon.MakeSpan(classes.Concat(moreClasses).ToList());
    }

    /// <summary>
    /// BuildGroup is the function that takes a group and calls the correct groupType
    /// function for it. It also handles the interaction of size and style changes
    /// between parents and children.
    /// </summary>
    public static HtmlDomNode BuildGroup(
        ParseNode group,
        Options options,
        Options baseOptions = null)
    {
        if (group == null)
        {
            return BuildCommon.MakeSpan();
        }

        if (Registries.HtmlGroupBuilders.TryGetValue(group.Type, out var builder) && builder != null)
        {
            HtmlDomNode groupNode = (HtmlDomNode)builder(group, options);

            // If the size changed between the parent and the current group, account
            // for that size difference.
            if (baseOptions != null && options.Size != baseOptions.Size)
            {
                groupNode = BuildCommon.MakeSpan(options.SizingClasses(baseOptions),
                    new List<VirtualNode> { groupNode }, options);

                double multiplier =
                    options.SizeMultiplier / baseOptions.SizeMultiplier;

                groupNode.Height *= multiplier;
                groupNode.Depth *= multiplier;
            }

            return groupNode;
        }
        else
        {
            throw new ParseError(
                "Got group of unknown type: '" + group.Type + "'");
        }
    }

    /// <summary>
    /// Combine an array of HTML DOM nodes (e.g., the output of `BuildExpression`)
    /// into an unbreakable HTML node of class .katex-base, with proper struts to
    /// guarantee correct vertical extent.  `Build` calls this repeatedly to
    /// make up the entire expression as a sequence of unbreakable units.
    /// </summary>
    private static Span BuildHTMLUnbreakable(List<HtmlDomNode> children, Options options)
    {
        // Compute height and depth of this chunk.
        var body = BuildCommon.MakeSpan(new List<string> { "katex-base" },
            children.Cast<VirtualNode>().ToList(), options);

        // Add strut, which ensures that the top of the HTML element falls at
        // the height of the expression, and the bottom of the HTML element
        // falls at the depth of the expression.
        var strut = BuildCommon.MakeSpan(new List<string> { "katex-strut" });
        strut.Style.Height = Units.MakeEm(body.Height + body.Depth);
        if (body.Depth != 0)
        {
            strut.Style.VerticalAlign = Units.MakeEm(-body.Depth);
        }
        body.Children.Insert(0, strut);

        return body;
    }

    /// <summary>
    /// Take an entire parse tree, and build it into an appropriate set of HTML
    /// nodes.
    /// </summary>
    public static Span Build(List<ParseNode> tree, Options options)
    {
        // Strip off outer tag wrapper for processing below.
        List<ParseNode> tag = null;
        if (tree.Count == 1 && tree[0].Type == "tag")
        {
            tag = tree[0].List("tag");
            tree = tree[0].List("body");
        }

        // Build the expression contained in the tree
        var expression = BuildExpression(tree, options, "root");

        HtmlDomNode eqnNum = null;
        if (expression.Count == 2 && expression[1].HasClass("katex-tag"))
        {
            // An environment with automatic equation numbers, e.g. {gather}.
            eqnNum = expression[expression.Count - 1];
            expression.RemoveAt(expression.Count - 1);
        }

        var children = new List<HtmlDomNode>();

        // Create one base node for each chunk between potential line breaks.
        // The TeXBook [p.173] says "A formula will be broken only after a
        // relation symbol like $=$ or $<$ or $\rightarrow$, or after a binary
        // operation symbol like $+$ or $-$ or $\times$, where the relation or
        // binary operation is on the ``outer level'' of the formula (i.e., not
        // enclosed in {...} and not part of an \over construction)."

        var parts = new List<HtmlDomNode>();
        for (int i = 0; i < expression.Count; i++)
        {
            parts.Add(expression[i]);
            if (expression[i].HasClass("mbin") ||
                expression[i].HasClass("mrel") ||
                expression[i].HasClass("allowbreak"))
            {
                // Put any post-operator glue on same line as operator.
                // Watch for \nobreak along the way, and stop at \newline.
                bool nobreak = false;
                while (i < expression.Count - 1 &&
                       expression[i + 1].HasClass("mspace") &&
                       !expression[i + 1].HasClass("katex-newline"))
                {
                    i++;
                    parts.Add(expression[i]);
                    if (expression[i].HasClass("nobreak"))
                    {
                        nobreak = true;
                    }
                }
                // Don't allow break if \nobreak among the post-operator glue.
                if (!nobreak)
                {
                    children.Add(BuildHTMLUnbreakable(parts, options));
                    parts = new List<HtmlDomNode>();
                }
            }
            else if (expression[i].HasClass("katex-newline"))
            {
                // Write the line except the newline
                parts.RemoveAt(parts.Count - 1);
                if (parts.Count > 0)
                {
                    children.Add(BuildHTMLUnbreakable(parts, options));
                    parts = new List<HtmlDomNode>();
                }
                // Put the newline at the top level
                children.Add(expression[i]);
            }
        }
        if (parts.Count > 0)
        {
            children.Add(BuildHTMLUnbreakable(parts, options));
        }

        // Now, if there was a tag, build it too and append it as a final child.
        Span tagChild = null;
        if (tag != null)
        {
            tagChild = BuildHTMLUnbreakable(
                BuildExpression(tag, options, true),
                options);
            tagChild.Classes = new List<string> { "katex-tag" };
            children.Add(tagChild);
        }
        else if (eqnNum != null)
        {
            children.Add(eqnNum);
        }

        var htmlNode = BuildCommon.MakeSpan(new List<string> { "katex-html" },
            children.Cast<VirtualNode>().ToList());
        htmlNode.SetAttribute("aria-hidden", "true");

        // Adjust the strut of the tag to be the maximum height of all children
        // (the height of the enclosing htmlNode) for proper vertical alignment.
        if (tagChild != null)
        {
            var strut = (HtmlDomNode)tagChild.Children[0];
            strut.Style.Height = Units.MakeEm(htmlNode.Height + htmlNode.Depth);
            if (htmlNode.Depth != 0)
            {
                strut.Style.VerticalAlign = Units.MakeEm(-htmlNode.Depth);
            }
        }

        return htmlNode;
    }
}

