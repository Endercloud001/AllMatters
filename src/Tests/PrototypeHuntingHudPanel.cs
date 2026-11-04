// THROWAWAY presentation only. Four explicit inputs; no budget coordinator,
// gameplay hooks, relic grant, network messages or persistence.
using Godot;

public partial class PrototypeHuntingHudPanel : PanelContainer
{
    private Label? _text;
    public int Example {get;private set;}
    public string CurrentText=>_text?.Text??"";
    public static readonly string[] Examples=
    ["剩余 1 步 · 最后一步\n此步抵达首个 Boss 仍按时。",
     "剩余 0 步 · 本房安全\n本房间不受罚；全队完成必要流程后获得面具。",
     "剩余 0 步 · 追猎生效\n面具已生效；可查看遗物说明了解惩罚。",
     "剩余 0 步 · 已按时抵达 Boss\n本次已达标，追猎未触发；第二 Boss 不重新判定超时。"];
    public static PrototypeHuntingHudPanel Attach(Control parent)
    {
        if(parent.GetNodeOrNull<PrototypeHuntingHudPanel>("HuntingBoundaryPresentation") is {} existing)return existing;
        var panel=new PrototypeHuntingHudPanel{Name="HuntingBoundaryPresentation",MouseFilter=MouseFilterEnum.Ignore,FocusMode=FocusModeEnum.None};
        parent.AddChild(panel);panel.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        panel.OffsetLeft=-640;panel.OffsetRight=-20;panel.OffsetTop=84;panel.OffsetBottom=190;
        return panel;
    }
    public override void _Ready()
    {
        var style=new StyleBoxFlat{BgColor=new Color(0.07f,0.09f,0.13f,0.94f),ContentMarginLeft=16,ContentMarginRight=16,ContentMarginTop=10,ContentMarginBottom=10};
        AddThemeStyleboxOverride("panel",style);
        var column=new VBoxContainer{MouseFilter=MouseFilterEnum.Ignore,FocusMode=FocusModeEnum.None};AddChild(column);
        var caption=new Label{Text="追猎显示实验 · 示例数据 · Ctrl+F8 切换",MouseFilter=MouseFilterEnum.Ignore,FocusMode=FocusModeEnum.None};caption.AddThemeFontSizeOverride("font_size",16);column.AddChild(caption);
        _text=new Label{AutowrapMode=TextServer.AutowrapMode.WordSmart,MouseFilter=MouseFilterEnum.Ignore,FocusMode=FocusModeEnum.None};_text.AddThemeFontSizeOverride("font_size",20);column.AddChild(_text);
        ShowExample(0);SetProcessInput(true);
    }
    public void ShowExample(int example){Example=example%Examples.Length;if(_text!=null)_text.Text=Examples[Example];}
    public override void _Input(InputEvent input)
    {
        if(input is InputEventKey{Pressed:true,Echo:false,CtrlPressed:true,Keycode:Key.F8})ShowExample(Example+1);
    }
}

#if HUNTING_PRESENTATION_MOD
[MegaCrit.Sts2.Core.Modding.ModInitializer(nameof(Initialize))]
public static class PrototypeHuntingHudMod
{
    public static void Initialize()
    {
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(PrototypeHuntingHudMod).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("AllMattersHuntingPresentationProbe"));
        new HarmonyLib.Harmony("AllMattersHuntingPresentationProbe").Patch(HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.CommonUi.NGlobalUi),"_Ready"),postfix:new HarmonyLib.HarmonyMethod(typeof(PrototypeHuntingHudMod),nameof(Attach)));
        GD.Print("[AllMattersHuntingPresentationProbe] Four fixture HUD examples only; Ctrl+F8 cycles. No gameplay or save state.");
    }
    private static void Attach(MegaCrit.Sts2.Core.Nodes.CommonUi.NGlobalUi __instance)=>PrototypeHuntingHudPanel.Attach(__instance);
}
#endif
