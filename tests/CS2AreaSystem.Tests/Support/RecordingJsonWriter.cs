using System.Globalization;
using System.Text;
using Colossal.UI.Binding;
namespace CS2AreaSystem.Tests.Support;
// Builds JSON text from IJsonWriter calls and verifies declared array/map sizes like the native binder expects.
public sealed class RecordingJsonWriter : IJsonWriter {
    private readonly StringBuilder text = new();
    private readonly Stack<(char Kind, uint Declared, uint Written)> scopes = new();
    private bool pendingValue;
    public string debugName => "test";
    public string Json => text.ToString();
    private void Value(string json) {
        if (!pendingValue && scopes.Count > 0 && scopes.Peek().Kind == '[') {
            var top = scopes.Pop(); if (top.Written > 0) text.Append(','); scopes.Push((top.Kind, top.Declared, top.Written + 1));
        }
        pendingValue = false; text.Append(json);
    }
    private void Open(char kind, uint size) { Value(kind == '[' ? "[" : "{"); scopes.Push((kind, size, 0)); }
    private void Close(char kind) {
        var top = scopes.Pop();
        if (top.Kind != kind) throw new InvalidOperationException("scope mismatch");
        if (kind == '[' && top.Written != top.Declared) throw new InvalidOperationException($"array declared {top.Declared} but wrote {top.Written}");
        text.Append(kind == '[' ? ']' : '}');
    }
    public void TypeBegin(string name) { Open('{', 0); text.Append("\"__Type\":").Append(Quote(name)); var t = scopes.Pop(); scopes.Push((t.Kind, t.Declared, 1)); }
    public void TypeEnd() => Close('{');
    public void MapBegin(uint size) => Open('{', size);
    public void MapEnd() => Close('{');
    public void ArrayBegin(uint size) => Open('[', size);
    public void ArrayEnd() => Close('[');
    public void PropertyName(string name) {
        var top = scopes.Pop(); if (top.Written > 0) text.Append(','); scopes.Push((top.Kind, top.Declared, top.Written + 1));
        text.Append(Quote(name)).Append(':'); pendingValue = true;
    }
    public void WriteNull() => Value("null");
    public void Write(bool value) => Value(value ? "true" : "false");
    public void Write(int value) => Value(value.ToString(CultureInfo.InvariantCulture));
    public void Write(uint value) => Value(value.ToString(CultureInfo.InvariantCulture));
    public void Write(long value) => Value(value.ToString(CultureInfo.InvariantCulture));
    public void Write(ulong value) => Value(value.ToString(CultureInfo.InvariantCulture));
    public void Write(float value) => Value(value.ToString("R", CultureInfo.InvariantCulture));
    public void Write(double value) => Value(value.ToString("R", CultureInfo.InvariantCulture));
    public void Write(string value) => Value(value == null ? "null" : Quote(value));
    private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value);
    public System.Text.Json.JsonElement Parse() => System.Text.Json.JsonDocument.Parse(Json).RootElement;
}
