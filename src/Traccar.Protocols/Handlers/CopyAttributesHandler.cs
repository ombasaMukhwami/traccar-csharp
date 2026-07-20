using Microsoft.Extensions.Configuration;
using Traccar.Model;

namespace Traccar.Protocols.Handlers;

public sealed class CopyAttributesHandler(IConfiguration configuration)
{
    private readonly string? _copyAttributes = configuration[ConfigKeys.Processing.CopyAttributes];

    public void Process(Position position, Position? last)
    {
        if (last == null || _copyAttributes == null) return;

        foreach (var key in _copyAttributes.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
        {
            if (last.HasAttribute(key) && !position.HasAttribute(key))
                position.Attributes[key] = last.Attributes[key];
        }
    }
}
