using System.Runtime.CompilerServices;
using System.Text;

namespace Traccar.Protocols;

internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
}
