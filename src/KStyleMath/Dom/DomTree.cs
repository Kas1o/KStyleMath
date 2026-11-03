using System.Text;

namespace KStyleMath;

public abstract class VirtualNode
{
    public abstract string ToMarkup();

    /// <summary>Appends the markup to an existing builder to avoid intermediate strings.</summary>
    public virtual void AppendMarkup(StringBuilder sb) => sb.Append(ToMarkup());
}

public interface IMathDomNode
{
    string ToText();
}

/// <summary>Ordered CSS inline style, serialized like KaTeX's CssStyle records.</summary>
public sealed class CssStyle
{
    private readonly List<KeyValuePair<string, string>> _items = new();

    public int Count => _items.Count;
    public IEnumerable<string> Keys
    {
        get { foreach (var kv in _items) yield return kv.Key; }
    }

    public string this[string key]
    {
        get
        {
            foreach (var kv in _items) if (kv.Key == key) return kv.Value;
            return null;
        }
        set
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Key == key)
                {
                    if (value == null) _items.RemoveAt(i);
                    else _items[i] = new KeyValuePair<string, string>(key, value);
                    return;
                }
            }
            if (value != null) _items.Add(new KeyValuePair<string, string>(key, value));
        }
    }

    public bool ContainsKey(string key) => this[key] != null;

    public CssStyle Clone()
    {
        var s = new CssStyle();
        s._items.AddRange(_items);
        return s;
    }

    public string ToCssString()
    {
        if (_items.Count == 0) return "";
        var sb = new StringBuilder();
        foreach (var kv in _items)
            sb.Append(Utils.Hyphenate(kv.Key)).Append(':').Append(kv.Value).Append(';');
        return sb.ToString();
    }

    public string BackgroundColor { get => this["backgroundColor"]; set => this["backgroundColor"] = value; }
    public string BorderBottomWidth { get => this["borderBottomWidth"]; set => this["borderBottomWidth"] = value; }
    public string BorderColor { get => this["borderColor"]; set => this["borderColor"] = value; }
    public string BorderRightStyle { get => this["borderRightStyle"]; set => this["borderRightStyle"] = value; }
    public string BorderRightWidth { get => this["borderRightWidth"]; set => this["borderRightWidth"] = value; }
    public string BorderTopWidth { get => this["borderTopWidth"]; set => this["borderTopWidth"] = value; }
    public string BorderStyle { get => this["borderStyle"]; set => this["borderStyle"] = value; }
    public string BorderWidth { get => this["borderWidth"]; set => this["borderWidth"] = value; }
    public string Bottom { get => this["bottom"]; set => this["bottom"] = value; }
    public string Color { get => this["color"]; set => this["color"] = value; }
    public string Height { get => this["height"]; set => this["height"] = value; }
    public string Left { get => this["left"]; set => this["left"] = value; }
    public string Margin { get => this["margin"]; set => this["margin"] = value; }
    public string MarginLeft { get => this["marginLeft"]; set => this["marginLeft"] = value; }
    public string MarginRight { get => this["marginRight"]; set => this["marginRight"] = value; }
    public string MarginTop { get => this["marginTop"]; set => this["marginTop"] = value; }
    public string MinWidth { get => this["minWidth"]; set => this["minWidth"] = value; }
    public string PaddingLeft { get => this["paddingLeft"]; set => this["paddingLeft"] = value; }
    public string Position { get => this["position"]; set => this["position"] = value; }
    public string TextShadow { get => this["textShadow"]; set => this["textShadow"] = value; }
    public string Top { get => this["top"]; set => this["top"] = value; }
    public string Width { get => this["width"]; set => this["width"] = value; }
    public string VerticalAlign { get => this["verticalAlign"]; set => this["verticalAlign"] = value; }
}

public abstract class HtmlDomNode : VirtualNode
{
    public List<string> Classes = new();
    public double Height;
    public double Depth;
    public double MaxFontSize;
    public CssStyle Style = new();

    public bool HasClass(string className) => Classes.Contains(className);
}

public sealed class Span : HtmlDomNode
{
    public List<VirtualNode> Children = new();
    public double? Width;
    public double? Italic;

    public Span(List<string> classes = null, List<VirtualNode> children = null, Options options = null, CssStyle style = null)
    {
        Classes = classes ?? new List<string>();
        Children = children ?? new List<VirtualNode>();
        Style = style ?? new CssStyle();
        if (options != null)
        {
            if (options.Style.IsTight()) Classes.Add("mtight");
            var color = options.GetColor();
            if (color != null) Style.Color = color;
        }
    }

    public void SetAttribute(string attribute, string value) => Attributes[attribute] = value;
    public Dictionary<string, string> Attributes = new(StringComparer.Ordinal);

    public override string ToMarkup() => DomTreeUtil.ToMarkup(this, "span");
    public override void AppendMarkup(StringBuilder sb) => DomTreeUtil.AppendMarkup(this, "span", sb);
}

public sealed class Anchor : HtmlDomNode
{
    public List<VirtualNode> Children = new();
    public Dictionary<string, string> Attributes = new(StringComparer.Ordinal);

    public Anchor(string href, List<string> classes, List<VirtualNode> children, Options options)
    {
        Classes = classes ?? new List<string>();
        Children = children ?? new List<VirtualNode>();
        if (options != null)
        {
            if (options.Style.IsTight()) Classes.Add("mtight");
            var color = options.GetColor();
            if (color != null) Style.Color = color;
        }
        SetAttribute("href", href);
    }

    public void SetAttribute(string attribute, string value) => Attributes[attribute] = value;
    public override string ToMarkup() => DomTreeUtil.ToMarkup(this, "a");
    public override void AppendMarkup(StringBuilder sb) => DomTreeUtil.AppendMarkup(this, "a", sb);
}

public sealed class Img : HtmlDomNode
{
    public string Src;
    public string Alt;

    public Img(string src, string alt, CssStyle style)
    {
        Alt = alt;
        Src = src;
        Classes = new List<string> { "mord" };
        Style = style ?? new CssStyle();
    }

    public override string ToMarkup()
    {
        var sb = new StringBuilder();
        AppendMarkup(sb);
        return sb.ToString();
    }

    public override void AppendMarkup(StringBuilder sb)
    {
        sb.Append("<img src=\"").Append(Utils.Escape(Src)).Append("\" alt=\"").Append(Utils.Escape(Alt)).Append('"');
        var styles = Style.ToCssString();
        if (styles.Length > 0) sb.Append(" style=\"").Append(Utils.Escape(styles)).Append('"');
        sb.Append("'/>");
    }
}

public sealed class SymbolNode : HtmlDomNode
{
    public string Text;
    public double Italic;
    public double Skew;
    public double Width;

    private static readonly Dictionary<string, string> ICombinations = new()
    {
        ["î"] = "\u0131\u0302",
        ["ï"] = "\u0131\u0308",
        ["í"] = "\u0131\u0301",
        ["ì"] = "\u0131\u0300",
    };

    public SymbolNode(string text, double height = 0, double depth = 0, double italic = 0,
        double skew = 0, double width = 0, List<string> classes = null, CssStyle style = null)
    {
        Text = text;
        Height = height;
        Depth = depth;
        Italic = italic;
        Skew = skew;
        Width = width;
        Classes = classes ?? new List<string>();
        Style = style ?? new CssStyle();
        MaxFontSize = 0;

        var script = UnicodeScripts.ScriptFromCodepoint(text.Length > 0 ? text[0] : '\0');
        if (script != null) Classes.Add(script + "_fallback");

        if (text.Length == 1 && ICombinations.TryGetValue(text, out var replacement))
            Text = replacement;
    }

    public override string ToMarkup()
    {
        if (Classes.Count == 0 && Italic <= 0 && Style.Count == 0)
            return Utils.Escape(Text);
        var sb = new StringBuilder();
        AppendMarkup(sb);
        return sb.ToString();
    }

    public override void AppendMarkup(StringBuilder sb)
    {
        if (Classes.Count == 0 && Italic <= 0 && Style.Count == 0)
        {
            sb.Append(Utils.Escape(Text));
            return;
        }

        bool needsSpan = false;
        sb.Append("<span");
        if (Classes.Count > 0)
        {
            needsSpan = true;
            sb.Append(" class=\"").Append(Utils.Escape(DomTreeUtil.CreateClass(Classes))).Append('"');
        }
        string styles = "";
        if (Italic > 0) styles += "margin-right:" + Units.MakeEm(Italic) + ";";
        styles += Style.ToCssString();
        if (styles.Length > 0)
        {
            needsSpan = true;
            sb.Append(" style=\"").Append(Utils.Escape(styles)).Append('"');
        }
        if (needsSpan) sb.Append('>').Append(Utils.Escape(Text)).Append("</span>");
        else sb.Append(Utils.Escape(Text));
    }
}

public sealed class SvgNode : VirtualNode
{
    public List<VirtualNode> Children;
    public Dictionary<string, string> Attributes;

    public SvgNode(List<VirtualNode> children = null, Dictionary<string, string> attributes = null)
    {
        Children = children ?? new List<VirtualNode>();
        Attributes = attributes ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public override string ToMarkup()
    {
        var sb = new StringBuilder();
        AppendMarkup(sb);
        return sb.ToString();
    }

    public override void AppendMarkup(StringBuilder sb)
    {
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\"");
        foreach (var kv in Attributes)
            sb.Append(' ').Append(kv.Key).Append("=\"").Append(Utils.Escape(kv.Value)).Append('"');
        sb.Append('>');
        foreach (var child in Children) child.AppendMarkup(sb);
        sb.Append("</svg>");
    }
}

public sealed class PathNode : VirtualNode
{
    public string PathName;
    public string Alternate;

    public PathNode(string pathName, string alternate = null)
    {
        PathName = pathName;
        Alternate = alternate;
    }

    public override string ToMarkup()
        => Alternate != null
            ? $"<path d=\"{Utils.Escape(Alternate)}\"/>"
            : $"<path d=\"{Utils.Escape(SvgGeometry.Path[PathName])}\"/>";

    public override void AppendMarkup(StringBuilder sb)
    {
        sb.Append("<path d=\"").Append(Utils.Escape(Alternate ?? SvgGeometry.Path[PathName])).Append("\"/>");
    }
}

public sealed class LineNode : VirtualNode
{
    public Dictionary<string, string> Attributes;

    public LineNode(Dictionary<string, string> attributes = null)
    {
        Attributes = attributes ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public override string ToMarkup()
    {
        var sb = new StringBuilder();
        AppendMarkup(sb);
        return sb.ToString();
    }

    public override void AppendMarkup(StringBuilder sb)
    {
        sb.Append("<line");
        foreach (var kv in Attributes)
            sb.Append(' ').Append(kv.Key).Append("=\"").Append(Utils.Escape(kv.Value)).Append('"');
        sb.Append("/>");
    }
}

/// <summary>A document fragment; has both HTML and MathML node behaviour.</summary>
public sealed class DocumentFragment : HtmlDomNode, IMathDomNode
{
    public List<VirtualNode> Children;

    public DocumentFragment(List<VirtualNode> children)
    {
        Children = children ?? new List<VirtualNode>();
    }

    public override string ToMarkup()
    {
        var sb = new StringBuilder();
        AppendMarkup(sb);
        return sb.ToString();
    }

    public override void AppendMarkup(StringBuilder sb)
    {
        foreach (var child in Children) child.AppendMarkup(sb);
    }

    public string ToText()
    {
        var sb = new StringBuilder();
        foreach (var child in Children)
        {
            if (child is IMathDomNode m) sb.Append(m.ToText());
            else throw new InvalidOperationException("Expected MathDomNode with toText");
        }
        return sb.ToString();
    }
}

public static class DomTreeUtil
{
    public static string CreateClass(List<string> classes)
    {
        if (classes.Count == 0) return "";
        if (classes.Count == 1) return classes[0] ?? "";
        var sb = new StringBuilder();
        foreach (var c in classes)
        {
            if (string.IsNullOrEmpty(c)) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static readonly System.Text.RegularExpressions.Regex InvalidAttributeName =
        new("[\\s\"'>/=\\x00-\\x1f]", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static string ToMarkup(HtmlDomNode node, string tagName)
    {
        var sb = new StringBuilder();
        AppendMarkup(node, tagName, sb);
        return sb.ToString();
    }

    public static void AppendMarkup(HtmlDomNode node, string tagName, StringBuilder markup)
    {
        Dictionary<string, string> attributes = null;
        List<VirtualNode> children = null;
        if (node is Span span)
        {
            attributes = span.Attributes;
            children = span.Children;
        }
        else if (node is Anchor anchor)
        {
            attributes = anchor.Attributes;
            children = anchor.Children;
        }

        markup.Append('<').Append(tagName);
        if (node.Classes.Count > 0)
            markup.Append(" class=\"").Append(Utils.Escape(CreateClass(node.Classes))).Append('"');
        var styles = node.Style.ToCssString();
        if (styles.Length > 0)
            markup.Append(" style=\"").Append(Utils.Escape(styles)).Append('"');
        if (attributes != null)
        {
            foreach (var kv in attributes)
            {
                if (InvalidAttributeName.IsMatch(kv.Key))
                    throw new ParseError($"Invalid attribute name '{kv.Key}'");
                markup.Append(' ').Append(kv.Key).Append("=\"").Append(Utils.Escape(kv.Value)).Append('"');
            }
        }
        markup.Append('>');
        if (children != null)
            foreach (var child in children) child.AppendMarkup(markup);
        markup.Append("</").Append(tagName).Append('>');
    }

    public static bool HasHtmlDomChildren(HtmlDomNode node)
        => node is Span || node is Anchor || node is DocumentFragment;
}

