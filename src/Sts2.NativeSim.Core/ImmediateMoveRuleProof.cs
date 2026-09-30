using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace Sts2.NativeSim.Core;

/// <summary>
/// Conservatively certifies shipped SetMoveImmediate trigger structure.
/// Hidden NextMove equality is evaluated later under each hypothetical world;
/// all unknown shapes fail closed. Author: XuShuxi.
/// </summary>
internal static class ImmediateMoveRuleProof
{
    private sealed record Instruction(OpCode Code, MemberInfo? Member);
    private static readonly Dictionary<short, OpCode> Codes = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(code => code.Value);

    // XuShuxi: SetMoveImmediate itself reads NextMove. Only a read in the
    // gameplay caller is a hidden predicate; public and transient installs
    // are certified after execution, when the actual argument is available.
    public static bool StackCallerReadsImmediatePredicate(object monster)
    {
        MethodInfo? caller = FindCaller(monster);
        return caller is not null && Decode(caller)?.Any(instruction =>
            instruction.Member is MethodInfo method && method.Name == "get_NextMove") == true;
    }

    public static PersistentNativeCombatEnvironment.ImmediateRuleSnapshot? FromStack(
        object monster, object? destinationHint)
    {
        MethodInfo? caller = FindCaller(monster);
        if (caller is null) return null;
        List<Instruction>? code = Decode(caller);
        if (code is null) return null;
        List<int> setCalls = code.Select((instruction, index) => (instruction, index))
            .Where(row => row.instruction.Member is MethodInfo method && method.Name == "SetMoveImmediate")
            .Select(row => row.index).ToList();
        if (setCalls.Count != 1) return null;
        int setIndex = setCalls[0];
        if (code.Any(instruction => instruction.Member?.Name is "StateLog" or "get_StateLog" or "_currentState"))
            return null;
        List<int> nextReads = code.Select((instruction, index) => (instruction, index))
            .Where(row => row.instruction.Member is MethodInfo method && method.Name == "get_NextMove")
            .Select(row => row.index).ToList();
        string source = $"{caller.DeclaringType?.FullName}.{caller.Name}";
        if (nextReads.Count == 0)
        {
            if (destinationHint is null) return null;
            string? destination = PublicRegisteredDestination(monster, destinationHint, code, setIndex);
            return destination is null ? null : new(CombatId(monster), destination, null, source);
        }
        if (nextReads.Count != 1 || nextReads[0] >= setIndex) return null;
        int branchIndex = -1;
        for (int index = nextReads[0] + 1; index < setIndex; index++)
        {
            if (code[index].Code.FlowControl != FlowControl.Cond_Branch) continue;
            branchIndex = index;
            break;
        }
        if (branchIndex < 0) return null;
        List<(int index, string id)> stateReads = [];
        for (int index = nextReads[0] + 1; index < setIndex; index++)
        {
            MemberInfo? member = code[index].Member;
            object? value = null;
            Type? valueType = null;
            try
            {
                if (member is MethodInfo method && method.IsSpecialName
                    && method.Name.StartsWith("get_", StringComparison.Ordinal)
                    && method.Name != "get_NextMove" && method.GetParameters().Length == 0
                    && method.DeclaringType is not null && method.DeclaringType.IsAssignableFrom(monster.GetType()))
                {
                    valueType = method.ReturnType;
                    value = method.Invoke(monster, null);
                }
                else if (member is FieldInfo field && field.DeclaringType is not null
                    && field.DeclaringType.IsAssignableFrom(monster.GetType()))
                {
                    valueType = field.FieldType;
                    value = field.GetValue(monster);
                }
            }
            catch { return null; }
            if (value is null || valueType?.Name != "MoveState") continue;
            string? id = RegisteredId(monster, value);
            if (id is not null) stateReads.Add((index, id));
        }
        string? destinationId = destinationHint is null ? null : RegisteredId(monster, destinationHint);
        if (destinationId is null)
            destinationId = stateReads.Where(row => row.index > branchIndex)
                .OrderBy(row => row.index).Select(row => row.id).LastOrDefault();
        if (destinationId is null) return null;
        string[] triggers = stateReads.Where(row => row.index < branchIndex && row.id != destinationId)
            .Select(row => row.id).Distinct(StringComparer.Ordinal).ToArray();
        if (triggers.Length != 1) return null;
        if (!stateReads.Any(row => row.index > branchIndex && row.id == destinationId)) return null;
        return new(CombatId(monster), destinationId, triggers[0], source);
    }

    // XuShuxi: Recognize only the direct registered-property argument shape,
    // not arbitrary callbacks or computed destinations. The getter must be a
    // pure field read on this monster; its value must be the executed argument
    // and a member of this monster's own FSM. Unknown IL continues to fail closed.
    private static string? PublicRegisteredDestination(
        object monster, object destination, List<Instruction> code, int setIndex)
    {
        if (setIndex < 3 || code[setIndex - 1].Code != OpCodes.Ldc_I4_0
            && code[setIndex - 1].Code != OpCodes.Ldc_I4_1) return null;
        Instruction producer = code[setIndex - 2];
        if (producer.Code != OpCodes.Call && producer.Code != OpCodes.Callvirt) return null;
        if (producer.Member is not MethodInfo getter || !getter.IsSpecialName
            || !getter.Name.StartsWith("get_", StringComparison.Ordinal)
            || getter.IsStatic || getter.GetParameters().Length != 0
            || getter.ReturnType.Name != "MoveState" || getter.DeclaringType is null
            || !getter.DeclaringType.IsAssignableFrom(monster.GetType())) return null;
        List<Instruction>? body = Decode(getter);
        if (body is not { Count: 3 } || body[0].Code != OpCodes.Ldarg_0
            || body[1].Code != OpCodes.Ldfld || body[2].Code != OpCodes.Ret
            || body[1].Member is not FieldInfo field || field.IsStatic
            || field.FieldType != getter.ReturnType || field.DeclaringType is null
            || !field.DeclaringType.IsAssignableFrom(monster.GetType())) return null;
        return ReferenceEquals(field.GetValue(monster), destination) ? RegisteredId(monster, destination) : null;
    }

    private static MethodInfo? FindCaller(object monster)
    {
        Assembly game = monster.GetType().Assembly;
        foreach (StackFrame frame in new StackTrace().GetFrames() ?? [])
        {
            if (frame.GetMethod() is not MethodInfo method || method.DeclaringType?.Assembly != game) continue;
            if (method.Name is "SetMoveImmediate" or "get_NextMove") continue;
            List<Instruction>? code = Decode(method);
            if (code?.Any(instruction => instruction.Member is MethodInfo called
                    && called.Name == "SetMoveImmediate") == true) return method;
        }
        return null;
    }

    private static uint CombatId(object monster)
    {
        object creature = ReflectionTools.Get(monster, "Creature")!;
        return Convert.ToUInt32(ReflectionTools.Get(creature, "CombatId"));
    }

    private static string? RegisteredId(object monster, object state)
    {
        object machine = ReflectionTools.Get(monster, "MoveStateMachine")!;
        foreach (object? pair in ReflectionTools.Enumerate(ReflectionTools.Get(machine, "States")))
        {
            if (pair is null || !ReferenceEquals(ReflectionTools.Get(pair, "Value"), state)) continue;
            return Convert.ToString(ReflectionTools.Get(pair, "Key"));
        }
        return null;
    }

    private static List<Instruction>? Decode(MethodInfo method)
    {
        try
        {
            byte[]? bytes = method.GetMethodBody()?.GetILAsByteArray();
            if (bytes is null) return null;
            List<Instruction> result = [];
            for (int position = 0; position < bytes.Length;)
            {
                short value = bytes[position++];
                if (value == 0xfe) value = (short)(0xfe00 | bytes[position++]);
                if (!Codes.TryGetValue(value, out OpCode code)) return null;
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
                    member = method.Module.ResolveMember(BitConverter.ToInt32(bytes, operand));
                result.Add(new(code, member));
                position += length;
            }
            return result;
        }
        catch { return null; }
    }
}
