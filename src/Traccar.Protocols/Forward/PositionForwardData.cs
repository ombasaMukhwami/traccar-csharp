using Traccar.Model;

namespace Traccar.Protocols.Forward;

public sealed record PositionForwardData(Position Position, Device? Device);
