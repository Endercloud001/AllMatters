using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.FeedbackScreen;
using MegaCrit.Sts2.addons.mega_text;

namespace AllMatters.Feedback;

internal static class FeedbackUi
{
    internal const string Category = "allmatters";
    internal const string CategoryText = "For AllMatters";
    internal static bool Busy { get; private set; }
    private static readonly ConditionalWeakTable<NSendFeedbackScreen, object> screens = new();
    internal static Func<FeedbackExportResult, bool> PresentResult = PresentNative;
    internal static Func<FeedbackSnapshot, Task<FeedbackExportResult>> ExportAsync = snapshot => Task.Run(() => FeedbackArchive.Export(snapshot));

    internal static T Field<T>(object owner, string name) => (T)AccessTools.Field(owner.GetType(), name).GetValue(owner)!;
    internal static void Set(object owner, string name, object value) => AccessTools.Field(owner.GetType(), name).SetValue(owner, value);

    internal static void Initialize()
    {
        string directory = System.IO.Path.GetDirectoryName(OS.GetExecutablePath())!;
        foreach (var failure in FeedbackArchive.Cleanup(directory))
            GD.PushWarning("[AllMatters] 诊断临时文件未能清理：" + failure);
    }

    internal static NSendFeedbackScreen? Screen(Node dropdown)
    {
        for (Node? node = dropdown; node != null; node = node.GetParent())
            if (node is NSendFeedbackScreen screen)
                return screen;
        return null;
    }

    internal static void Refresh(NSendFeedbackScreen screen)
    {
        screens.GetValue(screen, _ => new object());
        var dropdown = Field<NFeedbackCategoryDropdown>(screen, "_categoryDropdown");
        var label = Field<MegaLabel>(screen, "_sendLabel");
        label.SetTextAutoSize(dropdown.CurrentCategory == Category ? "Export" : new LocString("settings_ui", "FEEDBACK_SEND_BUTTON_LABEL").GetFormattedText());
        if (Busy)
            Field<NButton>(screen, "_sendButton").Disable();
    }

    internal static void Start(NSendFeedbackScreen screen)
    {
        if (Busy)
            return;
        Busy = true;
        var button = Field<NButton>(screen, "_sendButton");
        button.Disable();
        Field<MegaLabel>(screen, "_sendLabel").SetTextAutoSize("Export");
        Task<FeedbackExportResult> operation;
        try
        {
            var snapshot = FeedbackCapture.Capture(Field<string>(screen, "_descriptionText"), Field<byte[]?>(screen, "_screenshotBytes"));
            operation = ExportAsync(snapshot);
        }
        catch (Exception exception) { operation = Task.FromResult(new FeedbackExportResult(null, exception.Message)); }
        // The tree owns delivery, so hiding/freeing the source screen cannot cancel it.
        var tree = screen.GetTree();
        bool restored = false;
        void Poll()
        {
            if (!operation.IsCompleted)
                return;
            if (!restored)
            {
                restored = true;
                Busy = false;
                foreach (var tracked in screens)
                {
                    if (!GodotObject.IsInstanceValid(tracked.Key))
                        continue;
                    Field<NButton>(tracked.Key, "_sendButton").Enable();
                    Refresh(tracked.Key);
                }
                if (GodotObject.IsInstanceValid(screen) && !screens.TryGetValue(screen, out _))
                {
                    button.Enable();
                    Refresh(screen);
                }
            }
            var result = operation.IsCompletedSuccessfully ? operation.Result : new FeedbackExportResult(null,
                operation.Exception?.GetBaseException().Message ?? "诊断导出任务已取消。");
            if (PresentResult(result))
                tree.ProcessFrame -= Poll;
        }
        tree.ProcessFrame += Poll;
    }
    private static bool PresentNative(FeedbackExportResult result)
    {
        var container = NModalContainer.Instance;
        if (container == null || !GodotObject.IsInstanceValid(container) || container.OpenModal != null)
            return false;
        string body = result.Error ?? FeedbackArchive.SuccessMessage;
        if (result.Path != null)
        {
            try
            {
                if (OS.ShellShowInFileManager(result.Path) != Error.Ok)
                    body += "\n文件位置：" + result.Path;
            }
            catch (Exception) { body += "\n文件位置：" + result.Path; }
        }
        var popup = NErrorPopup.Create("AllMatters", body, false);
        if (popup == null)
            return false;
        container.Add(popup);
        if (result.Path != null)
        {
            var path = new LineEdit { Text = result.Path, Editable = false, Position = new Vector2(32, 32), CustomMinimumSize = new Vector2(700, 40) };
            popup.AddChild(path);
        }
        return true;
    }

}

[HarmonyPatch(typeof(NFeedbackCategoryDropdown), "PopulateOptions")]
internal static class FeedbackCategoriesPatch
{
    private static void Postfix(NFeedbackCategoryDropdown __instance)
    {
        string[] categories = FeedbackUi.Field<string[]>(__instance, "_categories");
        if (categories.Contains(FeedbackUi.Category))
            return;
        int index = categories.Length;
        FeedbackUi.Set(__instance, "_categories", categories.Append(FeedbackUi.Category).ToArray());
        var list = __instance.GetNode<Control>("DropdownContainer/VBoxContainer");
        var item = FeedbackUi.Field<PackedScene>(__instance, "_dropdownItemScene").Instantiate<NFeedbackCategoryDropdownItem>();
        list.AddChild(item);
        item.Init(index, FeedbackUi.CategoryText);
        item.Connect(NDropdownItem.SignalName.Selected, Callable.From<NDropdownItem>(selected =>
            AccessTools.Method(typeof(NFeedbackCategoryDropdown), "OnDropdownItemSelected").Invoke(__instance, [selected])));
        list.GetParent<NDropdownContainer>().RefreshLayout();
    }
}

[HarmonyPatch(typeof(NFeedbackCategoryDropdown), "OnDropdownItemSelected")]
internal static class FeedbackCategorySelectedPatch
{
    private static bool Prefix(NFeedbackCategoryDropdown __instance, NDropdownItem item)
    {
        var selected = (NFeedbackCategoryDropdownItem)item;
        var categories = FeedbackUi.Field<string[]>(__instance, "_categories");
        if (categories[selected.CategoryIndex] != FeedbackUi.Category)
            return true;
        AccessTools.Method(typeof(NFeedbackCategoryDropdown), "CloseDropdown").Invoke(__instance, null);
        FeedbackUi.Set(__instance, "_currentCategoryIndex", selected.CategoryIndex);
        FeedbackUi.Field<MegaLabel>(__instance, "_currentOptionLabel").SetTextAutoSize(FeedbackUi.CategoryText);
        return false;
    }

    private static void Postfix(NFeedbackCategoryDropdown __instance)
    {
        if (FeedbackUi.Screen(__instance) is { } screen)
            FeedbackUi.Refresh(screen);
    }
}

[HarmonyPatch(typeof(NSendFeedbackScreen), "SendButtonSelected")]
internal static class FeedbackSendPatch
{
    private static bool Prefix(NSendFeedbackScreen __instance)
    {
        if (FeedbackUi.Field<NFeedbackCategoryDropdown>(__instance, "_categoryDropdown").CurrentCategory != FeedbackUi.Category)
            return !FeedbackUi.Busy;
        FeedbackUi.Start(__instance);
        return false;
    }
}

[HarmonyPatch]
internal static class FeedbackRefreshPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NSendFeedbackScreen), "Relocalize");
        yield return AccessTools.Method(typeof(NSendFeedbackScreen), "Open");
    }
    private static void Postfix(NSendFeedbackScreen __instance) => FeedbackUi.Refresh(__instance);
}

[HarmonyPatch(typeof(NSendFeedbackScreen), "ClearInput")]
internal static class FeedbackPreserveInputPatch
{
    private static bool Prefix(NSendFeedbackScreen __instance)
    {
        var dropdown = FeedbackUi.Field<NFeedbackCategoryDropdown?>(__instance, "_categoryDropdown");
        return dropdown == null || dropdown.CurrentCategory != FeedbackUi.Category;
    }
}
