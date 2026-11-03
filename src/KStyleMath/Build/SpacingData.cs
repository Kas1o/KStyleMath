namespace KStyleMath;

public static class SpacingData
{
    private static Measurement Thin => new(3, "mu");
    private static Measurement Medium => new(4, "mu");
    private static Measurement Thick => new(5, "mu");

    public static readonly Dictionary<string, Dictionary<string, Measurement>> Spacings = new()
    {
        ["mord"] = new() { ["mop"] = Thin, ["mbin"] = Medium, ["mrel"] = Thick, ["minner"] = Thin },
        ["mop"] = new() { ["mord"] = Thin, ["mop"] = Thin, ["mrel"] = Thick, ["minner"] = Thin },
        ["mbin"] = new() { ["mord"] = Medium, ["mop"] = Medium, ["mopen"] = Medium, ["minner"] = Medium },
        ["mrel"] = new() { ["mord"] = Thick, ["mop"] = Thick, ["mopen"] = Thick, ["minner"] = Thick },
        ["mopen"] = new(),
        ["mclose"] = new() { ["mop"] = Thin, ["mbin"] = Medium, ["mrel"] = Thick, ["minner"] = Thin },
        ["mpunct"] = new()
        {
            ["mord"] = Thin, ["mop"] = Thin, ["mrel"] = Thick, ["mopen"] = Thin,
            ["mclose"] = Thin, ["mpunct"] = Thin, ["minner"] = Thin,
        },
        ["minner"] = new()
        {
            ["mord"] = Thin, ["mop"] = Thin, ["mbin"] = Medium, ["mrel"] = Thick,
            ["mopen"] = Thin, ["mpunct"] = Thin, ["minner"] = Thin,
        },
    };

    public static readonly Dictionary<string, Dictionary<string, Measurement>> TightSpacings = new()
    {
        ["mord"] = new() { ["mop"] = Thin },
        ["mop"] = new() { ["mord"] = Thin, ["mop"] = Thin },
        ["mbin"] = new(),
        ["mrel"] = new(),
        ["mopen"] = new(),
        ["mclose"] = new() { ["mop"] = Thin },
        ["mpunct"] = new(),
        ["minner"] = new() { ["mop"] = Thin },
    };

    public static Measurement GetSpacing(string prevType, string type, bool tight)
    {
        var table = tight ? TightSpacings : Spacings;
        return table.TryGetValue(prevType, out var row) && row.TryGetValue(type, out var m) ? m : null;
    }
}
