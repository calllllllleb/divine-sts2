// Source-certified current public counter -> native instance semantics. Author: XuShuxi.
using Sts2.NativeSim.Protocol;

namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    // XuShuxi: These models use only the displayed residue for future gameplay.
    // _isActivating is presentation-only; JossPaper resets EtherealCount at
    // AfterCombatEnd. An animation's peak counter is rejected until settled.
    private static readonly IReadOnlyDictionary<string, (string Property, int MaximumExclusive)> PublicRunCounterContracts
        = new Dictionary<string, (string, int)> {
            ["HAPPY_FLOWER"] = ("TurnsSeen", 3), ["NUNCHAKU"] = ("AttacksPlayed", 10),
            ["IRON_CLUB"] = ("CardsPlayed", 4), ["BOOK_OF_FIVE_RINGS"] = ("CardsAdded", 5),
            ["JOSS_PAPER"] = ("CardsExhausted", 5), ["EMBER_TEA"] = ("CombatsLeft", 6),
            ["WINGED_BOOTS"] = ("TimesUsed", 4)
        };

    private RunRelicFace PublicRootRelic(object relic) => new(Entry(relic),
        Convert.ToBoolean(ReflectionTools.Get(relic, "ShowCounter"))
            ? Convert.ToInt32(ReflectionTools.Get(relic, "DisplayAmount")) : null);

    private static int NativePublicRunCounter(RunRelicFace face)
        => face.ModelId == "WINGED_BOOTS" ? face.DisplayAmount is { } remaining ? 3 - remaining : 3
            : face.DisplayAmount ?? throw new ProtocolException("run_public_counter_evidence_missing", $"{face.ModelId} requires its visible counter.");

    private void ValidatePublicRunCounter(RunRelicFace face)
    {
        if (!PublicRunCounterContracts.TryGetValue(face.ModelId, out var contract))
        {
            object model = Mutable("AllRelics", face.ModelId);
            if (PublicRootRelic(model) != face)
                throw new ProtocolException("run_public_counter_evidence_missing", $"{face.ModelId} has no certified public counter reconstruction.");
            return;
        }
        int counter = NativePublicRunCounter(face);
        if (counter < 0 || counter >= contract.MaximumExclusive
            || face.ModelId == "WINGED_BOOTS" && face.DisplayAmount is <= 0)
            throw new ProtocolException("run_public_counter_not_settled", $"{face.ModelId}: counter does not identify a settled source-native instance.");
    }
}
