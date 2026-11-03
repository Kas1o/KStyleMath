using System.Globalization;
using System.Text.RegularExpressions;

namespace KStyleMath;

public static class IncludegraphicsFunctions
{
    private static readonly Regex NumberWithNoUnit = new(
        @"^[-+]? *(\d+(\.\d*)?|\.\d+)$", RegexOptions.Compiled);

    private static readonly Regex NumberWithUnit = new(
        @"([-+]?) *(\d+(?:\.\d*)?|\.\d+) *([a-z]{2})", RegexOptions.Compiled);

    private static Measurement SizeData(string str)
    {
        if (NumberWithNoUnit.IsMatch(str))
        {
            // str is a number with no unit specified.
            // default unit is bp, per graphix package.
            return new Measurement(ParseNumber(str), "bp");
        }
        else
        {
            var match = NumberWithUnit.Match(str);
            if (!match.Success)
            {
                throw new ParseError("Invalid size: '" + str
                    + "' in \\includegraphics");
            }
            var data = new Measurement(
                ParseNumber(match.Groups[1].Value + match.Groups[2].Value), // sign + magnitude, cast to number
                match.Groups[3].Value);
            if (!Units.ValidUnit(data))
            {
                throw new ParseError("Invalid unit: '" + data.Unit
                 + "' in \\includegraphics.");
            }
            return data;
        }
    }

    private static double ParseNumber(string str)
    {
        return double.TryParse(str.Trim(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
    }

    public static void Define()
    {
        Registries.DefineFunction(new FunctionSpec
        {
            Type = "includegraphics",
            Names = new List<string> { "\\includegraphics" },
            NumArgs = 1,
            NumOptionalArgs = 1,
            ArgTypes = new List<string> { "raw", "url" },
            AllowedInText = false,

            Handler = (context, args, optArgs) =>
            {
                var parser = context.Parser;
                var width = new Measurement(0, "em");
                var height = new Measurement(0.9, "em");    // sorta character sized.
                var totalheight = new Measurement(0, "em");
                var alt = "";

                if (optArgs.Count > 0 && optArgs[0] != null)
                {
                    var attributeStr = ParseNodeUtil.AssertNodeType(optArgs[0], "raw").Str("string");

                    // Parser.js does not parse key/value pairs. We get a string.
                    var attributes = attributeStr.Split(',');
                    for (int i = 0; i < attributes.Length; i++)
                    {
                        var keyVal = attributes[i].Split('=');
                        if (keyVal.Length == 2)
                        {
                            var str = keyVal[1].Trim();
                            switch (keyVal[0].Trim())
                            {
                                case "alt":
                                    alt = str;
                                    break;
                                case "width":
                                    width = SizeData(str);
                                    break;
                                case "height":
                                    height = SizeData(str);
                                    break;
                                case "totalheight":
                                    totalheight = SizeData(str);
                                    break;
                                default:
                                    throw new ParseError("Invalid key: '" + keyVal[0] +
                                        "' in \\includegraphics.");
                            }
                        }
                    }
                }

                var src = ParseNodeUtil.AssertNodeType(args[0], "url").Str("url");

                if (alt == "")
                {
                    // No alt given. Use the file name. Strip away the path.
                    alt = src;
                    alt = Regex.Replace(alt, @"^.*[\\/]", "");
                    int dot = alt.LastIndexOf('.');
                    alt = dot < 0 ? "" : alt.Substring(0, dot);
                }

                if (!parser.Settings.IsTrusted(new Dictionary<string, object>
                {
                    ["command"] = "\\includegraphics",
                    ["url"] = src,
                }))
                {
                    return parser.FormatUnsupportedCmd("\\includegraphics");
                }

                return new ParseNode("includegraphics", parser.Mode)
                {
                    ["alt"] = alt,
                    ["width"] = width,
                    ["height"] = height,
                    ["totalheight"] = totalheight,
                    ["src"] = src,
                };
            },

            HtmlBuilder = (group, options) =>
            {
                var height = Units.CalculateSize(group.Get<Measurement>("height"), options);
                double depth = 0;

                var totalheight = group.Get<Measurement>("totalheight");
                if (totalheight.Number > 0)
                {
                    depth = Units.CalculateSize(totalheight, options) - height;
                }

                double width = 0;
                var widthMeasurement = group.Get<Measurement>("width");
                if (widthMeasurement.Number > 0)
                {
                    width = Units.CalculateSize(widthMeasurement, options);
                }

                var style = new CssStyle { Height = Units.MakeEm(height + depth) };
                if (width > 0)
                {
                    style.Width = Units.MakeEm(width);
                }
                if (depth > 0)
                {
                    style.VerticalAlign = Units.MakeEm(-depth);
                }

                var node = new Img(group.Str("src"), group.Str("alt"), style);
                node.Height = height;
                node.Depth = depth;

                return node;
            },
            MathmlBuilder = (group, options) =>
            {
                var node = new MathNode("mglyph", new List<VirtualNode>());
                node.SetAttribute("alt", group.Str("alt"));

                var height = Units.CalculateSize(group.Get<Measurement>("height"), options);
                double depth = 0;
                var totalheight = group.Get<Measurement>("totalheight");
                if (totalheight.Number > 0)
                {
                    depth = Units.CalculateSize(totalheight, options) - height;
                    node.SetAttribute("valign", Units.MakeEm(-depth));
                }
                node.SetAttribute("height", Units.MakeEm(height + depth));

                var widthMeasurement = group.Get<Measurement>("width");
                if (widthMeasurement.Number > 0)
                {
                    var width = Units.CalculateSize(widthMeasurement, options);
                    node.SetAttribute("width", Units.MakeEm(width));
                }
                node.SetAttribute("src", group.Str("src"));
                return node;
            },
        });
    }
}
