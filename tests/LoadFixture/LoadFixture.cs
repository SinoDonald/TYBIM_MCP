using System.Runtime.CompilerServices;
using Tybim.Mcp.Contracts;

public static class LoadFixture
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object CreateRequest() => new BridgeRequest();
#if PRIVATE_CAD_FIXTURE
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object CreateCadDocument()
    {
        var cad=new ACadSharp.CadDocument(ACadSharp.ACadVersion.AC1032);
        cad.Entities.Add(new ACadSharp.Entities.TextEntity{Value="B23(50X70)",Height=100});
        byte[] data;
        using(var memory=new System.IO.MemoryStream())
        {
            using(var writer=new ACadSharp.IO.DwgWriter(memory,cad)) writer.Write();
            data=memory.ToArray();
        }
        using(var reader=new ACadSharp.IO.DwgReader(new System.IO.MemoryStream(data)))
        {
            var restored=reader.Read();
            foreach(var entity in restored.Entities)
                if(entity is ACadSharp.Entities.TextEntity text && text.Value=="B23(50X70)") return restored;
            throw new System.InvalidOperationException("DWG text did not survive write/read roundtrip");
        }
    }
#endif
}
