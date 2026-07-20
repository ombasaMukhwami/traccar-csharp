namespace Traccar.Model;

/// <summary>
/// A computed (scripted) attribute bound to a device. When evaluated, the expression result is
/// written back to the position under the given attribute key. Maps to Java's Attribute model and
/// the tc_attributes table.
/// </summary>
public class DeviceAttribute : ExtendedModel
{
    public long DeviceId { get; set; }

    public string? Description { get; set; }

    /// <summary>Target position field or attribute key where the computed result is stored.</summary>
    public string? Attribute { get; set; }

    /// <summary>NCalc expression evaluated against position properties and attributes.</summary>
    public string? Expression { get; set; }

    /// <summary>Result type hint: "number", "boolean", or null/empty for string.</summary>
    public string? Type { get; set; }

    /// <summary>
    /// Sort order and phase selector. Negative → evaluated early (before other enrichment handlers);
    /// non-negative → evaluated late (after other enrichment handlers).
    /// </summary>
    public int Priority { get; set; }
}
