using System.Runtime.CompilerServices;
using Tybim.Mcp.Contracts;

public static class LoadFixture
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object CreateRequest() => new BridgeRequest();
}
