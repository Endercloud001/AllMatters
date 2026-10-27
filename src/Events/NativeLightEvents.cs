using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AllMatters.Events;

[HarmonyPatch]
internal static class NativeLightOptions
{
    private static IEnumerable<MethodBase> TargetMethods()
        => new[] { typeof(Bugslayer), typeof(CrystalSphere), typeof(InfestedAutomaton), typeof(PunchOff),
            typeof(ColorfulPhilosophers), typeof(Reflections), typeof(SpiralingWhirlpool), typeof(TheLegendsWereTrue) }
            .Select(t => AccessTools.Method(t, "GenerateInitialOptions"));

    private static void Postfix(EventModel __instance, ref IReadOnlyList<EventOption> __result)
    {
        var model = __instance;
        var options = __result.ToList();
        switch (model)
        {
            case Bugslayer:
                options.Add(NativeEventActions.Option(model, "BUGSLAYER.pages.INITIAL.options.AM_REST", async () =>
                {
                    await CreatureCmd.Heal(model.Owner!.Creature, model.Owner!.Creature.MaxHp * .25m);
                    NativeEventActions.Finish(model, "BUGSLAYER.pages.AM_REST.description");
                }));
                break;
            case CrystalSphere or InfestedAutomaton or PunchOff:
                options.Add(NativeEventActions.Option(model, model.Id.Entry + ".pages.INITIAL.options.AM_LEAVE", () => NativeEventActions.Leave(model)));
                break;
            case ColorfulPhilosophers:
                options.Add(NativeEventActions.Option(model, "COLORFUL_PHILOSOPHERS.pages.INITIAL.options.AM_GEM", async () =>
                {
                    await RelicCmd.Obtain<PrismaticGem>(model.Owner!);
                    NativeEventActions.Finish(model, "COLORFUL_PHILOSOPHERS.pages.DONE.description");
                }, HoverTipFactory.FromRelic<PrismaticGem>().ToArray()));
                break;
            case Reflections:
                options.Add(NativeEventActions.Option(model, "REFLECTIONS.pages.INITIAL.options.AM_MOVE", async () =>
                {
                    bool mirror = model.Rng.NextInt(2) == 0;
                    await NativeEventActions.LoseHp(model, 10);
                    if (!model.Owner!.Creature.IsDead && mirror) await RelicCmd.Obtain<DollysMirror>(model.Owner!);
                    NativeEventActions.Finish(model, "REFLECTIONS.pages.AM_MOVE.description");
                }).ThatDoesDamage(10));
                break;
            case SpiralingWhirlpool:
                options.Add(NativeEventActions.Option(model, "SPIRALING_WHIRLPOOL.pages.INITIAL.options.AM_REACH", async () =>
                {
                    await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(model.Owner!).ToMutable(), model.Owner!);
                    await CardPileCmd.AddCurseToDeck<Writhe>(model.Owner!);
                    NativeEventActions.Finish(model, "SPIRALING_WHIRLPOOL.pages.AM_REACH.description");
                }));
                break;
            case TheLegendsWereTrue:
                options.Add(NativeEventActions.Option(model, "THE_LEGENDS_WERE_TRUE.pages.INITIAL.options.AM_EXIT", async () =>
                {
                    var rarity = new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare }[model.Rng.NextInt(3)];
                    await NativeEventActions.LoseHp(model, 12);
                    if (!model.Owner!.Creature.IsDead)
                        await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(model.Owner!, rarity).ToMutable(), model.Owner!);
                    NativeEventActions.Finish(model, "THE_LEGENDS_WERE_TRUE.pages.SLOWLY_FIND_AN_EXIT.description");
                }).ThatDoesDamage(12));
                break;
        }
        __result = options;
    }
}

[HarmonyPatch(typeof(WelcomeToWongos), "Leave")]
internal static class WongoLeave
{
    private static bool Prefix(WelcomeToWongos __instance, ref Task __result)
    {
        __result = Leave(__instance);
        return false;
    }
    private static async Task Leave(EventModel model)
    {
        await NativeEventActions.LoseHp(model, 8);
        NativeEventActions.Finish(model, "WELCOME_TO_WONGOS.pages.AM_LEAVE.description");
    }
}
