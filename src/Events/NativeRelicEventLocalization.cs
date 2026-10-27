using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace AllMatters.Events;

[HarmonyPatch(typeof(LocManager), nameof(LocManager.GetTable))]
internal static class NativeRelicEventLocalization
{
    private static void Postfix(LocManager __instance, string name, LocTable __result)
    {
        bool zh = __instance.Language == "zhs";
        if (name == "events") __result.MergeWith(new Dictionary<string, string>
        {
            ["LUMINOUS_CHOIR.pages.INITIAL.options.AM_MUSHROOM.title"] = zh ? "采摘蘑菇" : "Pick a Mushroom",
            ["LUMINOUS_CHOIR.pages.INITIAL.options.AM_MUSHROOM.description"] = zh ? "失去[red]14[/red]点生命，获得[gold]奇怪蘑菇[/gold]。" : "Lose [red]14[/red] HP. Obtain [gold]Strange Mushroom[/gold].",
            ["LUMINOUS_CHOIR.pages.AM_MUSHROOM.description"] = zh ? "你避开了最大的那株蘑菇，伸手试图摘取旁边的一株小蘑菇。\n\n手掌碰到的瞬间，一阵剧痛在你的脑海中炸开。\n\n所幸你坚持了下来，随后头也不回地逃离了这片空地。" : "Avoiding the largest mushroom, you reach for a smaller one beside it.\n\nThe instant your palm touches it, a sharp pain explodes inside your head.\n\nFortunately, you endure it and flee the clearing without looking back.",
            ["BYRDONIS_NEST.pages.INITIAL.options.AM_HELP.title"] = zh ? "帮助异鸟" : "Help the Byrdonis",
            ["BYRDONIS_NEST.pages.INITIAL.options.AM_HELP.description"] = zh ? "失去[red]12[/red]点生命，获得[gold]带骨肉[/gold]。" : "Lose [red]12[/red] HP. Obtain [gold]Meat on the Bone[/gold].",
            ["BYRDONIS_NEST.pages.AM_HELP.description"] = zh ? "你举起武器冲了过去，在一场激烈的战斗后，[red]巨兽[/red]倒下了。\n\n[green]异鸟[/green]用喙撕下了[red]巨兽[/red]尸体的一部分，在赋予[jitter]神奇的力量[/jitter]之后赠予了你以表感谢。" : "You rush forward with your weapon raised. After a fierce battle, the [red]beast[/red] falls.\n\nThe [green]Byrdonis[/green] tears a piece from the [red]beast[/red] with its beak, imbues it with [jitter]magical power[/jitter], and gives it to you in thanks.",
            ["DOLL_ROOM.pages.INITIAL.options.AM_DOLL.title"] = zh ? "毒巫娃娃" : "Du-Vu Doll",
            ["DOLL_ROOM.pages.INITIAL.options.AM_DOLL.description"] = zh ? "获得[gold]毒巫娃娃[/gold]。" : "Obtain [gold]Du-Vu Doll[/gold].",
            ["DOLL_ROOM.pages.AM_DOLL.description"] = zh ? "你拿起了缠绕着诅咒气息的娃娃。那些阴暗的低语，似乎也能成为力量。" : "You take the doll wreathed in curses. Even those dark whispers can become strength.",
        });
        else if (name == "relics") __result.MergeWith(new Dictionary<string, string>
        {
            ["ALL_MATTERS_STRANGE_MUSHROOM.title"] = zh ? "奇怪蘑菇" : "Strange Mushroom",
            ["ALL_MATTERS_STRANGE_MUSHROOM.description"] = zh ? "当你有[gold]易伤[/gold]状态时，受到的额外伤害从[red]50%[/red]下降为[blue]25%[/blue]。" : "When [gold]Vulnerable[/gold], your additional damage taken is reduced from [red]50%[/red] to [blue]25%[/blue].",
            ["ALL_MATTERS_DU_VU_DOLL.title"] = zh ? "毒巫娃娃" : "Du-Vu Doll",
            ["ALL_MATTERS_DU_VU_DOLL.description"] = zh ? "你的牌组中每有一张[gold]诅咒[/gold]牌，你在战斗开始时就额外获得[blue]1[/blue]点[gold]力量[/gold]。" : "At the start of combat, gain [blue]1[/blue] [gold]Strength[/gold] for each [gold]Curse[/gold] in your Deck.",
        });
    }
}
