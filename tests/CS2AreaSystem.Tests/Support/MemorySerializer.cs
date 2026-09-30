using System.Reflection;
using Colossal.Serialization.Entities;
using Unity.Entities;
namespace CS2AreaSystem.Tests.Support;
// Unmanaged game buffers cannot run outside Unity. Proxy the actual IReader/IWriter contract.
public class MemorySerializer : DispatchProxy {
    public List<object> Tokens = new();
    public int Position;
    public Func<Entity,Entity> Remap = entity => entity;
    protected override object Invoke(MethodInfo method, object[] args) {
        if (method!.Name == "Write") { Tokens.Add(args![0]!); return null; }
        if (method.Name == "Read") {
            var value = Tokens[Position++];
            args![0] = value is Entity entity ? Remap(entity) : value;
            return null;
        }
        if (method.Name == "Begin") {
            if (method.ReturnType == typeof(WriterBlock)) { Tokens.Add(0); return default(WriterBlock); }
            Position++; return default(ReaderBlock);
        }
        if (method.Name == "End") return true;
        throw new NotSupportedException(method.Name);
    }
    public static (IWriter Writer, IReader Reader, MemorySerializer Data) Create() {
        var writer=Create<IWriter,MemorySerializer>();
        var reader=Create<IReader,MemorySerializer>();
        var w=(MemorySerializer)(object)writer;
        var r=(MemorySerializer)(object)reader;
        r.Tokens=w.Tokens;
        return (writer,reader,r);
    }
    public static T RoundTrip<T>(T value) where T:struct,ISerializable {
        var io=Create(); value.Serialize(io.Writer);
        var result=default(T); result.Deserialize(io.Reader); return result;
    }
}
