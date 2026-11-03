using System.Text;

namespace KStyleMath;

public sealed class MathNode : VirtualNode, IMathDomNode
{
    public string Type;
    public List<KeyValuePair<string, string>> Attributes = new();
    public List<VirtualNode> Children;
    public List<string> Classes;

    public MathNode(string type, IEnumerable<VirtualNode> children, List<string> classes = null)
        : this(type, children?.ToList(), classes)
    {
    }

    public MathNode(string type, List<VirtualNode> children = null, List<string> classes = null)
    {
        Type = type;
        Children = children ?? new List<VirtualNode>();
        Classes = classes ?? new List<string>();
    }

    public void SetAttribute(string name, string value)
    {
        for (int i = 0; i < Attributes.Count; i++)
        {
            if (Attributes[i].Key == name)
            {
                Attributes[i] = new KeyValuePair<string, string>(name, value);
                return;
            }
        }
        Attributes.Add(new KeyValuePair<string, string>(name, value));
    }

    public string GetAttribute(string name)
    {
        foreach (var kv in Attributes) if (kv.Key == name) return kv.Value;
        return null;
    }

    public override string ToMarkup()
    {
        var sb = new StringBuilder();
        AppendMarkup(sb);
        return sb.ToString();
    }

    public override void AppendMarkup(StringBuilder markup)
    {
        markup.Append('<').Append(Type);
        foreach (var kv in Attributes)
            markup.Append(' ').Append(kv.Key).Append("=\"").Append(Utils.Escape(kv.Value)).Append('"');
        if (Classes.Count > 0)
            markup.Append(" class =\"").Append(Utils.Escape(DomTreeUtil.CreateClass(Classes))).Append('"');
        markup.Append('>');
        foreach (var child in Children) child.AppendMarkup(markup);
        markup.Append("</").Append(Type).Append('>');
    }

    public string ToText()
    {
        var sb = new StringBuilder();
        foreach (var child in Children)
        {
            if (child is MathTextNode t) sb.Append(t.ToText());
            else if (child is IMathDomNode m) sb.Append(m.ToText());
        }
        return sb.ToString();
    }
}

public sealed class MathTextNode : VirtualNode, IMathDomNode
{
    public string Text;
    public MathTextNode(string text) => Text = text;
    public override string ToMarkup() => Utils.Escape(Text);
    public override void AppendMarkup(StringBuilder sb) => sb.Append(Utils.Escape(Text));
    public string ToText() => Text;
}

public sealed class SpaceNode : VirtualNode, IMathDomNode
{
    public double Width;
    public string Character;

    public SpaceNode(double width)
    {
        Width = width;
        if (width >= 0.05555 && width <= 0.05556) Character = "\u200a";
        else if (width >= 0.1666 && width <= 0.1667) Character = "\u2009";
        else if (width >= 0.2222 && width <= 0.2223) Character = "\u2005";
        else if (width >= 0.2777 && width <= 0.2778) Character = "\u2005\u200a";
        else if (width >= -0.05556 && width <= -0.05555) Character = "\u200a\u2063";
        else if (width >= -0.1667 && width <= -0.1666) Character = "\u2009\u2063";
        else if (width >= -0.2223 && width <= -0.2222) Character = "\u205f\u2063";
        else if (width >= -0.2778 && width <= -0.2777) Character = "\u2005\u2063";
        else Character = null;
    }

    public override string ToMarkup()
        => Character != null
            ? $"<mtext>{Character}</mtext>"
            : $"<mspace width=\"{Units.MakeEm(Width)}\"/>";

    public override void AppendMarkup(StringBuilder sb)
    {
        if (Character != null) sb.Append("<mtext>").Append(Character).Append("</mtext>");
        else sb.Append("<mspace width=\"").Append(Units.MakeEm(Width)).Append("\"/>");
    }

    public string ToText() => Character ?? " ";
}

public static class MathMLTree
{
    public static DocumentFragment NewDocumentFragment(List<VirtualNode> children)
        => new DocumentFragment(children);
}

