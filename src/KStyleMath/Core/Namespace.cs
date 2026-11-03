namespace KStyleMath;

/// <summary>TeX-style scoped namespace with undo stack.</summary>
public sealed class Namespace<T>
{
    public Dictionary<string, T> Current;
    public Dictionary<string, T> Builtins;
    public List<Dictionary<string, T>> UndefStack = new();

    public Namespace(Dictionary<string, T> builtins = null, Dictionary<string, T> globalMacros = null)
    {
        Current = globalMacros ?? new Dictionary<string, T>(StringComparer.Ordinal);
        Builtins = builtins ?? new Dictionary<string, T>(StringComparer.Ordinal);
        UndefStack = new List<Dictionary<string, T>>();
    }

    public void BeginGroup() => UndefStack.Add(new Dictionary<string, T>(StringComparer.Ordinal));

    public void EndGroup()
    {
        if (UndefStack.Count == 0)
            throw new ParseError("Unbalanced namespace destruction: attempt " +
                                 "to pop global namespace; please report this as a bug");
        var undefs = UndefStack[^1];
        UndefStack.RemoveAt(UndefStack.Count - 1);
        foreach (var kv in undefs)
        {
            if (kv.Value == null) Current.Remove(kv.Key);
            else Current[kv.Key] = kv.Value;
        }
    }

    public void EndGroups()
    {
        while (UndefStack.Count > 0) EndGroup();
    }

    public bool Has(string name) => Current.ContainsKey(name) || Builtins.ContainsKey(name);

    public T Get(string name)
    {
        if (Current.TryGetValue(name, out var v)) return v;
        if (Builtins.TryGetValue(name, out v)) return v;
        return default;
    }

    public void Set(string name, T value, bool global = false)
    {
        if (global)
        {
            for (int i = 0; i < UndefStack.Count; i++) UndefStack[i].Remove(name);
            if (UndefStack.Count > 0) UndefStack[^1][name] = value;
        }
        else
        {
            var top = UndefStack.Count > 0 ? UndefStack[^1] : null;
            if (top != null && !top.ContainsKey(name))
                top[name] = Current.TryGetValue(name, out var old) ? old : default;
        }
        if (value == null) Current.Remove(name);
        else Current[name] = value;
    }
}
