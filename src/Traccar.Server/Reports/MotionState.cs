using Traccar.Model;

namespace Traccar.Server.Reports;

/// <summary>
/// Mutable state carried forward across positions during trip/stop detection.
/// Mirrors Java's session.state.MotionState, stripped of the device-persist methods
/// that are not used in the reporting path.
/// </summary>
public sealed class MotionState
{
    /// <summary>True while the device has been moving continuously (confirmed trip in progress).</summary>
    public bool MotionStreak { get; set; }

    /// <summary>Current raw motion flag from the last processed position.</summary>
    public bool Moving { get; set; }

    /// <summary>Position ID that started the pending motion-state change.</summary>
    public long MotionPositionId { get; set; }

    /// <summary>Fix-time of the position that started the pending motion-state change.</summary>
    public DateTime? MotionTime { get; set; }

    /// <summary>Total-distance value (metres) at the start of the pending motion-state change.</summary>
    public double MotionDistance { get; set; }

    /// <summary>Event emitted by the last <see cref="MotionProcessor.UpdateState"/> call; null if none.</summary>
    public Event? Event { get; set; }
}
