using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KStyleMath;

/// <summary>
/// A parse-tree node.  KaTeX's parse nodes are plain JavaScript objects, so to
/// keep the port faithful we store the same dynamically typed properties in a
/// dictionary instead of inventing dozens of CLR classes.
/// </summary>
public sealed class ParseNode : ITokenLoc
{
    public string Type;
    public string Mode;
    private Dictionary<string, object> _props = new(StringComparer.Ordinal);

    public SourceLocation Loc
    {
        get => Get<SourceLocation>("loc");
        set => this["loc"] = value;
    }

    public ParseNode(string type, string mode)
    {
        Type = type;
        Mode = mode;
    }

    public object this[string key]
    {
        get => _props.TryGetValue(key, out var v) ? v : null;
        set
        {
            if (value == null) _props.Remove(key);
            else _props[key] = value;
        }
    }

    public bool Has(string key) => _props.ContainsKey(key);
    public void Remove(string key) => _props.Remove(key);
    public IEnumerable<string> Keys => _props.Keys;

    public T Get<T>(string key)
    {
        return _props.TryGetValue(key, out var v) && v is T t ? t : default;
    }

    public string Str(string key)
    {
        var v = this[key];
        return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
    }

    public double Num(string key)
    {
        var v = this[key];
        if (v == null) return 0;
        return v is double d ? d : Convert.ToDouble(v, CultureInfo.InvariantCulture);
    }

    public bool Bool(string key)
    {
        var v = this[key];
        return v is bool b && b;
    }

    public ParseNode Node(string key) => this[key] as ParseNode;

    public List<ParseNode> List(string key)
    {
        if (this[key] is List<ParseNode> l) return l;
        return null;
    }

    public List<List<ParseNode>> ListList(string key)
    {
        if (this[key] is List<List<ParseNode>> l) return l;
        return null;
    }

    public Dictionary<string, object> Dict(string key) => this[key] as Dictionary<string, object>;

    public ParseNode CloneShallow()
    {
        var n = new ParseNode(Type, Mode) { _props = new Dictionary<string, object>(_props, StringComparer.Ordinal) };
        return n;
    }

    public override string ToString() => Type;
}

public static class ParseNodeExtensions
{
    public static ParseNode With(this ParseNode node, string key, object value)
    {
        node[key] = value;
        return node;
    }

    public static T Prop<T>(this ParseNode node, string key) => node.Get<T>(key);
}

/// <summary>Positional information for error reporting.</summary>
public sealed class SourceLocation
{
    public Lexer Lexer;
    public int Start;
    public int End;

    public SourceLocation(Lexer lexer, int start, int end)
    {
        Lexer = lexer;
        Start = start;
        End = end;
    }

    public static SourceLocation Range(ITokenLoc first, ITokenLoc second = null)
    {
        if (second == null) return first?.Loc;
        if (first?.Loc == null || second.Loc == null || first.Loc.Lexer != second.Loc.Lexer)
            return null;
        return new SourceLocation(first.Loc.Lexer, first.Loc.Start, second.Loc.End);
    }
}

public interface ITokenLoc
{
    SourceLocation Loc { get; }
}

public sealed class Token : ITokenLoc
{
    public string Text;
    public SourceLocation Loc { get; set; }
    public bool? Noexpand;
    public bool? TreatAsRelax;

    public Token(string text, SourceLocation loc = null)
    {
        Text = text;
        Loc = loc;
    }

    public Token Range(Token endToken, string text)
        => new Token(text, SourceLocation.Range(this, endToken));

    public override string ToString() => Text;
}

public class ParseError : Exception
{
    public int? Position;
    public int? Length;
    public string RawMessage;

    public ParseError(string message, object token = null) : base(BuildMessage(message, token, out var pos, out var len))
    {
        Position = pos;
        Length = len;
        RawMessage = message;
    }

    private static string BuildMessage(string message, object token, out int? start, out int? end)
    {
        string error = "KaTeX parse error: " + message;
        start = null;
        end = null;

        SourceLocation loc = token switch
        {
            Token t => t.Loc,
            ParseNode n => n.Loc,
            SourceLocation l => l,
            _ => null,
        };
        if (loc != null && loc.Start <= loc.End)
        {
            string input = loc.Lexer?.Input ?? "";
            start = loc.Start;
            end = loc.End;
            if (start == input.Length)
                error += " at end of input: ";
            else
                error += " at position " + (start + 1) + ": ";

            string underlined = "";
            foreach (var ch in input.Substring(start.Value, end.Value - start.Value))
                underlined += ch + "\u0332";

            string left;
            if (start > 15) left = "…" + input.Substring(start.Value - 15, 15);
            else left = input.Substring(0, start.Value);
            string right;
            if (end + 15 < input.Length) right = input.Substring(end.Value, 15) + "…";
            else right = input.Substring(end.Value);
            error += left + underlined + right;
        }
        return error;
    }

    public override string ToString() => "ParseError: " + Message;
}

/// <summary>TeX style (display/text/script/scriptscript, cramped or not).</summary>
public sealed class Style
{
    public int Id;
    public int Size;
    public bool Cramped;

    public Style(int id, int size, bool cramped)
    {
        Id = id;
        Size = size;
        Cramped = cramped;
    }

    public Style Sup() => Styles[SupMap[Id]];
    public Style Sub() => Styles[SubMap[Id]];
    public Style FracNum() => Styles[FracNumMap[Id]];
    public Style FracDen() => Styles[FracDenMap[Id]];
    public Style Cramp() => Styles[CrampMap[Id]];
    public Style Text() => Styles[TextMap[Id]];
    public bool IsTight() => Size >= 2;

    private static readonly Style[] Styles =
    {
        new(0, 0, false), new(1, 0, true),
        new(2, 1, false), new(3, 1, true),
        new(4, 2, false), new(5, 2, true),
        new(6, 3, false), new(7, 3, true),
    };

    private static readonly int[] SupMap = {4, 5, 4, 5, 6, 7, 6, 7};
    private static readonly int[] SubMap = {5, 5, 5, 5, 7, 7, 7, 7};
    private static readonly int[] FracNumMap = {2, 3, 4, 5, 6, 7, 6, 7};
    private static readonly int[] FracDenMap = {3, 3, 5, 5, 7, 7, 7, 7};
    private static readonly int[] CrampMap = {1, 1, 3, 3, 5, 5, 7, 7};
    private static readonly int[] TextMap = {0, 1, 2, 3, 2, 3, 2, 3};

    public static readonly Style DISPLAY = Styles[0];
    public static readonly Style TEXT = Styles[2];
    public static readonly Style SCRIPT = Styles[4];
    public static readonly Style SCRIPTSCRIPT = Styles[6];
}

public static class JsNumber
{
    /// <summary>ECMAScript Number.prototype.toFixed.</summary>
    public static string ToFixed(double value, int digits)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";
        string s = value.ToString("F" + digits, CultureInfo.InvariantCulture);
        if (digits > 0)
        {
            s = s.TrimEnd('0').TrimEnd('.');
        }
        if (s == "-0") s = "0";
        return s;
    }
    /// <summary>ECMAScript Number::toString (shortest round-tripping representation).</summary>
    public static string ToString(double value)
    {
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";
        if (value == 0) return "0";
        if (value >= -1e15 && value <= 1e15 && value == Math.Truncate(value))
            return ((long)value).ToString(CultureInfo.InvariantCulture);

        string s = value.ToString("R", CultureInfo.InvariantCulture);
        bool neg = s.StartsWith("-");
        if (neg) s = s.Substring(1);
        int e = s.IndexOfAny(new[] {'E', 'e'});
        string digits;
        int exp10;
        if (e >= 0)
        {
            string mant = s.Substring(0, e);
            exp10 = int.Parse(s.Substring(e + 1), CultureInfo.InvariantCulture);
            int dot = mant.IndexOf('.');
            if (dot >= 0)
            {
                digits = mant.Remove(dot, 1);
                exp10 += dot - 1;
            }
            else
            {
                digits = mant;
                exp10 += mant.Length - 1;
            }
        }
        else
        {
            int dot = s.IndexOf('.');
            string ip = dot < 0 ? s : s.Substring(0, dot);
            string fp = dot < 0 ? "" : s.Substring(dot + 1);
            if (ip != "0")
            {
                digits = ip + fp;
                exp10 = ip.Length - 1;
            }
            else
            {
                int k = 0;
                while (k < fp.Length && fp[k] == '0') k++;
                if (k == fp.Length) return "0";
                digits = fp.Substring(k);
                exp10 = -(k + 1);
            }
        }

        // Remove trailing zeros but keep at least one digit.
        digits = digits.TrimEnd('0');
        if (digits.Length == 0) return "0";

        var sb = new StringBuilder();
        if (neg) sb.Append('-');
        if (exp10 >= -6 && exp10 < 21)
        {
            if (exp10 < 0)
            {
                sb.Append("0.");
                sb.Append('0', -exp10 - 1);
                sb.Append(digits);
            }
            else if (exp10 + 1 >= digits.Length)
            {
                sb.Append(digits);
                sb.Append('0', exp10 + 1 - digits.Length);
            }
            else
            {
                sb.Append(digits, 0, exp10 + 1);
                sb.Append('.');
                sb.Append(digits, exp10 + 1, digits.Length - exp10 - 1);
            }
        }
        else
        {
            sb.Append(digits[0]);
            if (digits.Length > 1)
            {
                sb.Append('.');
                sb.Append(digits, 1, digits.Length - 1);
            }
            sb.Append('e');
            sb.Append(exp10 >= 0 ? '+' : '-');
            sb.Append(Math.Abs(exp10).ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }
}




