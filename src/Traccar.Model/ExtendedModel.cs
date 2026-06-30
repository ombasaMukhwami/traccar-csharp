using System.Globalization;

namespace Traccar.Model;

public class ExtendedModel : BaseModel
{
    public Dictionary<string, object> Attributes { get; set; } = new();

    public bool HasAttribute(string key) => Attributes.ContainsKey(key);

    public void Set(string key, object? value)
    {
        if (value is null)
        {
            return;
        }
        if (value is string stringValue && stringValue.Length == 0)
        {
            return;
        }
        Attributes[key] = value;
    }

    public void Remove(string key) => Attributes.Remove(key);

    public string? GetString(string key, string? defaultValue = null)
    {
        return Attributes.TryGetValue(key, out var value) ? Convert.ToString(value, CultureInfo.InvariantCulture) : defaultValue;
    }

    public double GetDouble(string key, double defaultValue = 0)
    {
        return Attributes.TryGetValue(key, out var value)
            ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
            : defaultValue;
    }

    public bool GetBoolean(string key, bool defaultValue = false)
    {
        return Attributes.TryGetValue(key, out var value)
            ? Convert.ToBoolean(value, CultureInfo.InvariantCulture)
            : defaultValue;
    }

    public int GetInteger(string key, int defaultValue = 0)
    {
        return Attributes.TryGetValue(key, out var value)
            ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
            : defaultValue;
    }

    public long GetLong(string key, long defaultValue = 0)
    {
        return Attributes.TryGetValue(key, out var value)
            ? Convert.ToInt64(value, CultureInfo.InvariantCulture)
            : defaultValue;
    }
}
