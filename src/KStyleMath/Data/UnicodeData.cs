using System.Text.Json;

namespace KStyleMath;

public static class UnicodeData
{
    public sealed class AccentInfo
    {
        public string Text;
        public string Math;
    }

    public static readonly Dictionary<string, AccentInfo> Accents = new(StringComparer.Ordinal);
    public static readonly Dictionary<string, string> Symbols = new(StringComparer.Ordinal);

    static UnicodeData()
    {
        var root = DataLoader.Load("unicode").RootElement;
        foreach (var a in root.GetProperty("accents").EnumerateObject())
        {
            Accents[a.Name] = new AccentInfo
            {
                Text = a.Value.GetProperty("text").GetString(),
                Math = a.Value.TryGetProperty("math", out var m) && m.ValueKind == JsonValueKind.String
                    ? m.GetString() : null,
            };
        }
        foreach (var s in root.GetProperty("symbols").EnumerateObject())
            Symbols[s.Name] = s.Value.GetString();
    }
}
