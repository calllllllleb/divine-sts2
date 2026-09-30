using System.Reflection;
using System.Reflection.Emit;

namespace Sts2.NativeSim.Core;

/// <summary>
/// Conservatively recognizes the exact-DLL straight-line pattern that builds
/// a fixed first MoveState. It refuses mutable-field reads, branching, calls
/// selecting another state, and every constructor shape it cannot prove.
/// Author: XuShuxi.
/// </summary>
internal static class StaticInitialMoveProof
{
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    public static string? FixedMoveId(object monster, object machine)
    {
        try
        {
            MethodInfo? generate = monster.GetType().GetMethod("GenerateMoveStateMachine",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (generate is null || generate.DeclaringType != monster.GetType()) return null;
            byte[]? bytes = generate.GetMethodBody()?.GetILAsByteArray();
            if (bytes is null) return null;
            List<(OpCode code, MemberInfo? member)> instructions = [];
            for (int position = 0; position < bytes.Length;)
            {
                short value = bytes[position++];
                if (value == 0xfe) value = (short)(0xfe00 | bytes[position++]);
                if (!Codes.TryGetValue(value, out OpCode code)) return null;
                if (code.FlowControl is FlowControl.Branch or FlowControl.Cond_Branch
                    || code.Name is "ldfld" or "ldflda" or "ldsfld" or "ldsflda") return null;
                int operand = position;
                int length = code.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, position),
                    _ => 4
                };
                MemberInfo? member = null;
                if (code.OperandType is OperandType.InlineMethod or OperandType.InlineField)
                    member = generate.Module.ResolveMember(BitConverter.ToInt32(bytes, operand));
                if (code.Name is "call" or "callvirt")
                {
                    if (member is not MethodInfo called) return null;
                    string owner = called.DeclaringType?.FullName ?? "";
                    if (owner.Contains(".Random.", StringComparison.Ordinal)
                        || called.ReturnType.FullName?.Contains("MonsterState", StringComparison.Ordinal) == true)
                        return null;
                    if (called.DeclaringType == monster.GetType()
                        && (!called.Name.StartsWith("get_", StringComparison.Ordinal)
                            || !(called.Name.EndsWith("Damage", StringComparison.Ordinal)
                                 || called.Name.EndsWith("Repeat", StringComparison.Ordinal))))
                        return null;
                }
                instructions.Add((code, member));
                position += length;
            }
            // XuShuxi: This IL shape means local 1 is assigned exactly once
            // from a freshly constructed MoveState and passed directly as the
            // sole machine constructor's initial state.
            int initialAssignments = 0, machineConstructors = 0;
            for (int index = 0; index < instructions.Count; index++)
            {
                var (code, member) = instructions[index];
                if (code.Name == "stloc.1")
                {
                    initialAssignments++;
                    if (index == 0 || instructions[index - 1].code.Name != "newobj"
                        || instructions[index - 1].member?.DeclaringType?.Name != "MoveState") return null;
                }
                if (code.Name == "newobj" && member?.DeclaringType?.Name == "MonsterMoveStateMachine")
                {
                    machineConstructors++;
                    if (index < 2 || instructions[index - 1].code.Name != "ldloc.1"
                        || instructions[index - 2].code.Name != "ldloc.0") return null;
                }
            }
            if (initialAssignments != 1 || machineConstructors != 1) return null;
            object initial = ReflectionTools.Get(machine, "_initialState")!;
            if (initial.GetType().Name != "MoveState") return null;
            return Convert.ToString(ReflectionTools.Get(initial, "Id"));
        }
        catch
        {
            return null;
        }
    }
}
