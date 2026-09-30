using System.IO;
using System;
namespace CS2AreaSystem.Core.Persistence
{
    public static class ComponentSchema
    {
        public static Action<string> ReportUnsupported { get; set; }
        public static void RequireSupported(ushort version)
        {
            if (version == 1) return;
            var message = "Unsupported Area Core component schema " + version + "; refusing to reinterpret or overwrite saved data.";
            ReportUnsupported?.Invoke(message);
            throw new InvalidDataException(message);
        }
    }
}
