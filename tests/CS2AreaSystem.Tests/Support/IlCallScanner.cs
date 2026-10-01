#nullable enable
using System.Reflection;
using System.Reflection.Emit;
namespace CS2AreaSystem.Tests.Support;
// One compiled call site: the calling method, the called method and the last string literal loaded before the call.
public sealed record IlCallSite(MethodBase Caller, MethodBase Target, string? LastString, int Offset);
// Finds call sites in compiled assemblies (the shipped game DLLs), so lifecycle contracts are checked against the
// binaries the mod runs with rather than against decompiled text.
public static class IlCallScanner {
    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
    public static List<IlCallSite> Find(Assembly assembly, Func<MethodBase, bool> isTarget) {
        var sites = new List<IlCallSite>();
        Type?[] types;
        try { types = assembly.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types; }
        foreach (var type in types) {
            if (type == null) continue;
            MethodBase[] methods;
            try { methods = type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All)).ToArray(); } catch (Exception) { continue; }
            foreach (var method in methods) sites.AddRange(Find(method, isTarget));
        }
        return sites;
    }
    public static List<IlCallSite> Find(MethodBase method, Func<MethodBase, bool> isTarget) {
        var sites = new List<IlCallSite>();
        byte[]? il;
        try { il = method.GetMethodBody()?.GetILAsByteArray(); } catch (Exception) { return sites; }
        if (il == null) return sites;
        string? lastString = null;
        for (int offset = 0; offset < il.Length;) {
            int start = offset;
            short value = il[offset] == 0xFE ? (short)(0xFE00 | il[offset + 1]) : il[offset];
            if (!OpCodesByValue.TryGetValue(value, out var opCode)) return sites;
            offset += opCode.Size;
            if (opCode == OpCodes.Ldstr) {
                try { lastString = method.Module.ResolveString(BitConverter.ToInt32(il, offset)); } catch (Exception) { lastString = null; }
            }
            else if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt || opCode == OpCodes.Newobj) {
                MethodBase? target = null;
                try {
                    target = method.Module.ResolveMethod(BitConverter.ToInt32(il, offset),
                        method.DeclaringType is { IsGenericType: true } d ? d.GetGenericArguments() : null,
                        method.IsGenericMethod ? method.GetGenericArguments() : null);
                } catch (Exception) { }
                if (target != null && isTarget(target)) sites.Add(new IlCallSite(method, target, lastString, start));
            }
            offset += OperandSize(opCode.OperandType, il, offset);
        }
        return sites;
    }
    // The type a compiler-generated state machine or closure belongs to.
    public static Type Outer(Type type) {
        while (type.DeclaringType != null && type.Name.StartsWith("<", StringComparison.Ordinal)) type = type.DeclaringType;
        return type;
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
