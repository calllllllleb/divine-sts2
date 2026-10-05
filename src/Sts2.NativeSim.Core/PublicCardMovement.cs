// Public native movement evidence; never inspect hidden pile order. Author: XuShuxi.
using System.Reflection;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private sealed record PublicMoveSource(string Zone, object? Face, int Count);
    private readonly Dictionary<object, PublicMoveSource> _publicMoveSources = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _publicMoveEvents = [];
    private bool _capturePublicMoves;
    private static readonly AsyncLocal<string?> PublicAddPosition = new();
    private static readonly AsyncLocal<int> PublicDrawScope = new();
    private static readonly AsyncLocal<int> PublicShuffleScope = new();

    public async Task<Sts2.NativeSim.Protocol.EnvironmentResult> StepAsync(string actionId, bool record = true)
    {
        _publicMoveSources.Clear(); _publicMoveEvents.Clear(); _capturePublicMoves = true;
        try { return await StepInternalAsync(actionId, record); }
        finally { _capturePublicMoves = false; }
    }

    private void PublicPositionInvalidation(string kind = "POSITION_INVALIDATION")
        => _publicMoveEvents.Add(new { sequence = _publicMoveEvents.Count, kind });

    private static string PublicPileZone(object pile) => ReflectionTools.Get(pile, "Type")!.ToString() switch
    {
        "Draw" => "DrawPile", "Hand" => "Hand", "Discard" => "DiscardPile", _ => "Other"
    };

    private static void CapturePublicRemove(object __instance, object __0)
    {
        var environment = _activeEnvironment;
        if (environment is null || !environment._capturePublicMoves) return;
        string zone = PublicPileZone(__instance);
        environment._publicMoveSources[__0] = new(zone,
            zone is "Hand" or "DiscardPile" ? environment.PublicCardFace(__0) : null,
            ReflectionTools.Enumerate(ReflectionTools.Get(__instance, "Cards")).Count);
    }

    private static void CapturePublicAdd(object __instance, object __0)
    {
        var environment = _activeEnvironment;
        if (environment is null || !environment._capturePublicMoves) return;
        environment._publicMoveSources.Remove(__0, out PublicMoveSource? source);
        string destination = PublicPileZone(__instance);
        if (destination == "DrawPile")
        {
            // A random insertion or shuffle cannot reveal a realized slot/face.
            if (PublicShuffleScope.Value != 0 || source?.Face is null
                || PublicAddPosition.Value is not ("Top" or "Bottom"))
                environment.PublicPositionInvalidation();
            else environment._publicMoveEvents.Add(new {
                sequence = environment._publicMoveEvents.Count,
                kind = PublicAddPosition.Value == "Top" ? "PUT_ON_TOP" : "PUT_ON_BOTTOM",
                card = source.Face, source_zone = source.Zone
            });
        }
        else if (source?.Zone == "DrawPile")
        {
            // Only shipped Draw's first-card operation proves a top draw. The
            // face is read after entering the public Hand, never while hidden.
            if (destination == "Hand" && PublicDrawScope.Value != 0 && PublicShuffleScope.Value == 0)
                environment._publicMoveEvents.Add(new {
                    sequence = environment._publicMoveEvents.Count, kind = "DRAW",
                    card = environment.PublicCardFace(__0), source_zone = "DrawPile", pile_count_before = source.Count
                });
            else environment.PublicPositionInvalidation();
        }
    }

    private static void InvalidatePublicPileMutation(object __instance)
    {
        var environment = _activeEnvironment;
        if (environment is not null && environment._capturePublicMoves && PublicPileZone(__instance) == "DrawPile")
            environment.PublicPositionInvalidation();
    }

    // AsyncLocal scopes survive native awaits. Postfix restores the caller's
    // context; the native task retains its captured scope without wrapping it.
    private static void EnterPublicAddScope(object __2, ref string? __state)
    { __state = PublicAddPosition.Value; PublicAddPosition.Value = __2.ToString(); }
    private static void ExitPublicAddScope(string? __state) => PublicAddPosition.Value = __state;
    private static void EnterPublicDrawScope(ref int __state)
    { __state = PublicDrawScope.Value; PublicDrawScope.Value++; }
    private static void ExitPublicDrawScope(int __state) => PublicDrawScope.Value = __state;
    private static void EnterPublicShuffleScope(ref int __state)
    {
        __state = PublicShuffleScope.Value; PublicShuffleScope.Value++;
        var environment = _activeEnvironment;
        if (environment is not null && environment._capturePublicMoves) environment.PublicPositionInvalidation("SHUFFLE_INVALIDATION");
    }
    private static void ExitPublicShuffleScope(int __state) => PublicShuffleScope.Value = __state;

    private void InstallPublicMovementObservers(Action<MethodInfo, string> prefix, Action<MethodInfo, string> postfix)
    {
        Type pile = T("MegaCrit.Sts2.Core.Entities.Cards.CardPile");
        prefix(pile.GetMethod("RemoveInternal")!, nameof(CapturePublicRemove));
        postfix(pile.GetMethod("AddInternal")!, nameof(CapturePublicAdd));
        foreach (string method in new[] { "RandomizeOrderInternal", "MoveToBottomInternal", "MoveToTopInternal", "Clear" })
            prefix(pile.GetMethod(method)!, nameof(InvalidatePublicPileMutation));
        Type command = T("MegaCrit.Sts2.Core.Commands.CardPileCmd");
        MethodInfo add = command.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method => method.Name == "Add"
            && method.GetParameters()[0].ParameterType.IsGenericType && method.GetParameters()[1].ParameterType == pile);
        prefix(add, nameof(EnterPublicAddScope)); postfix(add, nameof(ExitPublicAddScope));
        MethodInfo draw = command.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method => method.Name == "Draw"
            && method.GetParameters().Length == 4);
        prefix(draw, nameof(EnterPublicDrawScope)); postfix(draw, nameof(ExitPublicDrawScope));
        MethodInfo shuffle = command.GetMethod("Shuffle")!;
        prefix(shuffle, nameof(EnterPublicShuffleScope)); postfix(shuffle, nameof(ExitPublicShuffleScope));
    }

    private object[] PublicMovementEvidence()
    {
        if (_publicMoveSources.Values.Any(source => source.Zone == "DrawPile")) PublicPositionInvalidation();
        return _publicMoveEvents.ToArray();
    }
}
