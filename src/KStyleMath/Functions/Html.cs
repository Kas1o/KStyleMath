using System.Text.RegularExpressions;

namespace KStyleMath;

public static class HtmlFunctions
{
    private static bool StrictTruthy(object strict)
    {
        if (strict == null) return false;
        if (strict is bool b) return b;
        if (strict is StrictMode m) return m != StrictMode.False;
        if (strict is string s) return s.Length > 0;
        return true;
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "html",
            Names = new List<string> { "\\htmlClass", "\\htmlId", "\\htmlStyle", "\\htmlData" },
            NumArgs = 2,
            ArgTypes = new List<string> { "raw", "original" },
            AllowedInText = true,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var funcName = context.FuncName;
                var token = context.Token;
                var value = ParseNodeUtil.AssertNodeType(args[0], "raw").Str("string");
                var body = args[1];

                if (StrictTruthy(parser.Settings.Strict))
                {
                    StrictHelper.Handle(parser.Settings.Strict,
                        "htmlExtension",
                        "HTML extension is disabled on strict mode",
                        token, true);
                }

                Dictionary<string, object> trustContext;
                var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

                switch (funcName)
                {
                    case "\\htmlClass":
                        attributes["class"] = value;
                        trustContext = new Dictionary<string, object>
                        {
                            ["command"] = "\\htmlClass",
                            ["class"] = value,
                        };
                        break;
                    case "\\htmlId":
                        attributes["id"] = value;
                        trustContext = new Dictionary<string, object>
                        {
                            ["command"] = "\\htmlId",
                            ["id"] = value,
                        };
                        break;
                    case "\\htmlStyle":
                        attributes["style"] = value;
                        trustContext = new Dictionary<string, object>
                        {
                            ["command"] = "\\htmlStyle",
                            ["style"] = value,
                        };
                        break;
                    case "\\htmlData":
                    {
                        // `{,}` escapes a literal comma. Braces are used rather than a
                        // backslash because `\,` is a macro (a thin space) that expands
                        // away before this raw argument is ever read.
                        const string ESCAPED_COMMA = "{,}";
                        var data = new List<string>();
                        var current = "";
                        for (int i = 0; i < value.Length; i++)
                        {
                            if (value.Substring(i).StartsWith(ESCAPED_COMMA, StringComparison.Ordinal))
                            {
                                current += ",";
                                i += ESCAPED_COMMA.Length - 1;
                            }
                            else if (value[i] == ',')
                            {
                                data.Add(current);
                                current = "";
                            }
                            else
                            {
                                current += value[i];
                            }
                        }
                        data.Add(current);
                        for (int i = 0; i < data.Count; i++)
                        {
                            var item = data[i];
                            var firstEquals = item.IndexOf('=');
                            if (firstEquals < 0)
                            {
                                throw new ParseError($"\\htmlData key/value '{item}'" +
                                    " missing equals sign");
                            }
                            var key = item.Substring(0, firstEquals);
                            var dataValue = item.Substring(firstEquals + 1);
                            attributes["data-" + key.Trim()] = dataValue;
                        }

                        trustContext = new Dictionary<string, object>
                        {
                            ["command"] = "\\htmlData",
                            ["attributes"] = attributes,
                        };
                        break;
                    }
                    default:
                        throw new InvalidOperationException("Unrecognized html command");
                }

                if (!parser.Settings.IsTrusted(trustContext))
                {
                    return parser.FormatUnsupportedCmd(funcName);
                }
                return new ParseNode("html", parser.Mode)
                {
                    ["attributes"] = attributes,
                    ["body"] = Registries.Ordargument(body),
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var elements = BuildHTML.BuildExpression(group.List("body"), options, false);

                var classes = new List<string> { "enclosing" };
                var attributes = group.Get<Dictionary<string, string>>("attributes")
                                 ?? new Dictionary<string, string>(StringComparer.Ordinal);
                if (attributes.TryGetValue("class", out var classValue) &&
                    !string.IsNullOrEmpty(classValue))
                {
                    classes.AddRange(Regex.Split(classValue.Trim(), @"\s+"));
                }

                var span = BuildCommon.MakeSpan(classes,
                    elements.Cast<VirtualNode>().ToList(), options);
                foreach (var kv in attributes)
                {
                    if (kv.Key != "class")
                    {
                        span.SetAttribute(kv.Key, kv.Value);
                    }
                }
                return span;
            },
            MathmlBuilder = (group, options) =>
            {
                return BuildMathML.BuildExpressionRow(group.List("body"), options);
            },
        });
    }
}
