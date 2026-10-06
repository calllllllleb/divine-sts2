// Current offered hover faces; no event private fields or latent candidates. Author: XuShuxi.
namespace Sts2.NativeSim.Core;

public sealed partial class PersistentNativeCombatEnvironment
{
    private object[] PublicEventOptionItems(object option)
    {
        List<object> items = [];
        if (ReflectionTools.Get(option, "Relic") is { } offered)
            items.Add(new { kind = "relic", model_id = Entry(offered) });
        foreach (object? tip in ReflectionTools.Enumerate(ReflectionTools.Get(option, "HoverTips")))
        {
            if (tip is null) continue;
            if (tip.GetType().Name == "CardHoverTip")
            {
                object card = ReflectionTools.Get(tip, "Card")!;
                object? enchantment = ReflectionTools.Get(card, "Enchantment");
                items.Add(new { kind = "card", model_id = Entry(card),
                    upgrades = Convert.ToInt32(ReflectionTools.Get(card, "CurrentUpgradeLevel")),
                    enchantment = enchantment is null ? null : new { model_id = Entry(enchantment), amount = Convert.ToInt32(ReflectionTools.Get(enchantment, "Amount")) } });
            }
            else if (ReflectionTools.Get(tip, "CanonicalModel") is { } model && T("MegaCrit.Sts2.Core.Models.RelicModel").IsInstanceOfType(model))
                items.Add(new { kind = "relic", model_id = Entry(model) });
        }
        return items.ToArray();
    }
}
