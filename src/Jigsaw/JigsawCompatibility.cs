using MegaCrit.Sts2.Core.Models.Modifiers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using HarmonyLib;
using System.Reflection.Emit;

namespace AllMatters.Jigsaw;

internal static class JigsawCompatibility
{
    internal static void RequireSupportedNewRun(RunState native)
    {
        if (native.Modifiers.Any(modifier => modifier is Flight))
            throw new InvalidOperationException("拼图模式不能与 Flight 组合。");
    }

    internal static void RequireSupportedCheckpoint(RunState native)
    {
        if (native.Modifiers.Any(modifier => modifier is Flight))
            throw new InvalidDataException("拼图检查点包含不支持的 Flight 组合，原存档未改写。");
    }
}

// Native canonicalization treats every exception as corrupt and renames both
// files. An unsupported combination is readable data: report it before that
// destructive catch, retaining native recovery for genuinely unreadable saves.
[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.LoadAndCanonicalizeMultiplayerRunSave))]
internal static class JigsawFlightLobbyCompatibility
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var nativeLoad = AccessTools.Method(typeof(RunSaveManager), nameof(RunSaveManager.LoadMultiplayerRunSave));
        var calls = code.Where(instruction => instruction.Calls(nativeLoad)).ToArray();
        if (calls.Length != 1)
            throw new InvalidOperationException($"RunSaveManager.LoadAndCanonicalizeMultiplayerRunSave: expected one native load, found {calls.Length}.");
        calls[0].opcode = OpCodes.Call;
        calls[0].operand = AccessTools.Method(typeof(JigsawFlightLobbyCompatibility), nameof(LoadSupported));
        return code;
    }

    private static ReadSaveResult<SerializableRun> LoadSupported(RunSaveManager manager)
    {
        var loaded = manager.LoadMultiplayerRunSave();
        if (loaded is not { Success: true, SaveData: { } save } || !JigsawSaveSource.IsJigsaw(save) ||
            !save.Modifiers.Any(modifier => modifier.Id == ModelDb.Modifier<Flight>().Id))
            return loaded!;
        return new(ReadSaveStatus.ValidationFailed, "拼图检查点包含不支持的 Flight 组合，原存档未改写。");
    }
}
