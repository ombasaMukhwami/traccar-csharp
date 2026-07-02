using Traccar.Model;

namespace Traccar.Server.Reports;

/// <summary>
/// Stateless trip/stop event detector. Mirrors Java's session.state.MotionProcessor exactly —
/// each call updates <paramref name="state"/> in place and may set <see cref="MotionState.Event"/>
/// to a TypeDeviceMoving or TypeDeviceStopped event.
/// </summary>
public static class MotionProcessor
{
    public static void UpdateState(
        MotionState state, Position? last, Position position, bool newState, TripsConfig config)
    {
        state.Event = null;

        // Data-gap: no position for longer than minimalNoDataDuration while moving → force stop.
        if (last != null)
        {
            long oldMs = ToEpochMs(last.FixTime);
            long newMs = ToEpochMs(position.FixTime);
            if (newMs - oldMs >= config.MinimalNoDataDuration && state.MotionStreak)
            {
                state.MotionStreak = false;
                state.Moving = false;
                state.MotionPositionId = 0;
                state.MotionTime = null;
                state.MotionDistance = 0;
                state.Event = new Event(Event.TypeDeviceStopped, last);
                return;
            }
        }

        bool oldState = state.Moving;
        if (oldState == newState)
        {
            if (state.MotionTime is not null)
            {
                long oldMs = ToEpochMs(state.MotionTime);
                long newMs = ToEpochMs(position.FixTime);
                double distance = position.GetDouble(Position.KeyTotalDistance) - state.MotionDistance;

                bool? ignition = null;
                if (config.UseIgnition && position.HasAttribute(Position.KeyIgnition))
                    ignition = position.GetBoolean(Position.KeyIgnition);

                bool generateEvent;
                if (newState)
                {
                    generateEvent = newMs - oldMs >= config.MinimalTripDuration
                                    || distance >= config.MinimalTripDistance;
                }
                else
                {
                    generateEvent = newMs - oldMs >= config.MinimalParkingDuration
                                    || ignition == false;
                }

                if (generateEvent)
                {
                    var ev = new Event(newState ? Event.TypeDeviceMoving : Event.TypeDeviceStopped, position.DeviceId)
                    {
                        PositionId = state.MotionPositionId,
                        EventTime = state.MotionTime,
                    };

                    state.MotionStreak = newState;
                    state.MotionPositionId = 0;
                    state.MotionTime = null;
                    state.MotionDistance = 0;
                    state.Event = ev;
                }
            }
        }
        else
        {
            state.Moving = newState;
            if (state.MotionStreak == newState)
            {
                state.MotionPositionId = 0;
                state.MotionTime = null;
                state.MotionDistance = 0;
            }
            else
            {
                state.MotionPositionId = position.Id;
                state.MotionTime = position.FixTime;
                state.MotionDistance = position.GetDouble(Position.KeyTotalDistance);
            }
        }
    }

    private static long ToEpochMs(DateTime? dt) =>
        dt.HasValue ? new DateTimeOffset(dt.Value, TimeSpan.Zero).ToUnixTimeMilliseconds() : 0L;
}
