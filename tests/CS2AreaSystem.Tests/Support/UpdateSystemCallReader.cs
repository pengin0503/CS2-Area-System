#nullable enable
using System.Reflection;
using System.Reflection.Emit;
using Game;
namespace CS2AreaSystem.Tests.Support;
// One compiled call to Game.UpdateSystem: UpdateAt/UpdateBefore/UpdateAfter<System[, Anchor]>(phase) or Update(phase).
public sealed record UpdateSystemCall(string Method, Type? System, Type? Anchor, SystemUpdatePhase Phase);
// Reads UpdateSystem calls from method IL (the game's SystemOrder, a mod's OnLoad), so schedule tests check the
// resolved generic arguments and phase constants of the compiled assemblies rather than source text.
public static class UpdateSystemCallReader {
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);
    public static List<UpdateSystemCall> Read(Type type) {
        var calls = new List<UpdateSystemCall>();
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            calls.AddRange(Read(method));
        return calls;
    }
    public static List<UpdateSystemCall> Read(MethodInfo method) {
        var calls = new List<UpdateSystemCall>();
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il == null) return calls;
        int? constant = null;
        for (int offset = 0; offset < il.Length;) {
            short value = il[offset] == 0xFE ? (short)(0xFE00 | il[offset + 1]) : il[offset];
            var opCode = OpCodesByValue[value];
            offset += opCode.Size;
            int? loaded = LoadedConstant(opCode, il, offset);
            if ((opCode == OpCodes.Call || opCode == OpCodes.Callvirt) &&
                method.Module.ResolveMethod(BitConverter.ToInt32(il, offset), GenericArguments(method.DeclaringType), method.GetGenericArguments()) is MethodInfo target &&
                target.DeclaringType == typeof(UpdateSystem) && target.GetParameters() is var parameters &&
                parameters.Length >= 1 && parameters[0].ParameterType == typeof(SystemUpdatePhase)) {
                if (constant == null) throw new InvalidOperationException($"{method.DeclaringType}.{method.Name}: phase of {target.Name} is not a constant");
                var generic = target.IsGenericMethod ? target.GetGenericArguments() : Type.EmptyTypes;
                calls.Add(new UpdateSystemCall(target.Name, generic.ElementAtOrDefault(0), generic.ElementAtOrDefault(1), (SystemUpdatePhase)constant.Value));
            }
            offset += OperandSize(opCode.OperandType, il, offset);
            constant = loaded;
        }
        return calls;
    }
    private static Type[]? GenericArguments(Type? type) => type != null && type.IsGenericType ? type.GetGenericArguments() : null;
    private static int? LoadedConstant(OpCode opCode, byte[] il, int operand) {
        if (opCode == OpCodes.Ldc_I4_M1) return -1;
        if (opCode.Value >= OpCodes.Ldc_I4_0.Value && opCode.Value <= OpCodes.Ldc_I4_8.Value) return opCode.Value - OpCodes.Ldc_I4_0.Value;
        if (opCode == OpCodes.Ldc_I4_S) return (sbyte)il[operand];
        if (opCode == OpCodes.Ldc_I4) return BitConverter.ToInt32(il, operand);
        return null;
    }
    private static int OperandSize(OperandType type, byte[] il, int operand) => type switch {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, operand),
        _ => 4,
    };
}
