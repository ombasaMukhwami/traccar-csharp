using System.Text.RegularExpressions;

namespace Traccar.Protocols.Helpers;

/// <summary>Mirrors Java's helper.PatternBuilder — a fluent DSL for building regex patterns from protocol grammars.</summary>
public class PatternBuilder
{
    private readonly List<string> _fragments = new();

    public PatternBuilder Optional() => Optional(1);

    public PatternBuilder Optional(int count)
    {
        _fragments.Insert(_fragments.Count - count, "(?:");
        _fragments.Add(")?");
        return this;
    }

    public PatternBuilder Expression(string s)
    {
        // trailing | is a literal delimiter, not alternation
        if (s.EndsWith("|", StringComparison.Ordinal)) s = s[..^1] + @"\|";
        _fragments.Add(s);
        return this;
    }

    public PatternBuilder Text(string s)
    {
        _fragments.Add(Regex.Escape(s));
        return this;
    }

    public PatternBuilder Number(string s)
    {
        s = s.Replace("dddd", "d{4}").Replace("ddd", "d{3}").Replace("dd", "d{2}");
        s = s.Replace("xxxx", "x{4}").Replace("xxx", "x{3}").Replace("xx", "x{2}");
        s = s.Replace("d", @"\d").Replace("x", "[0-9a-fA-F]");
        s = s.Replace(".", @"\.");
        // trailing | is a literal delimiter
        if (s.EndsWith("|")) s = s[..^1] + @"\|";
        if (s.StartsWith("|")) s = @"\|" + s[1..];
        _fragments.Add(s);
        return this;
    }

    public PatternBuilder Any()
    {
        _fragments.Add(".*");
        return this;
    }

    /// <summary>Matches literal bytes expressed as pairs of hex digits, e.g. "0D0A".</summary>
    public PatternBuilder Binary(string s)
    {
        _fragments.Add(Regex.Replace(s, "([0-9a-fA-F]{2})", @"\$1"));
        return this;
    }

    public PatternBuilder Or()
    {
        _fragments.Add("|");
        return this;
    }

    public PatternBuilder GroupBegin() => Expression("(?:");

    public PatternBuilder GroupEnd() => Expression(")");

    public PatternBuilder GroupEnd(string s) => Expression(")" + s);

    public Regex Compile() => new(ToString(), RegexOptions.Singleline | RegexOptions.Compiled);

    public override string ToString() => string.Concat(_fragments);
}
