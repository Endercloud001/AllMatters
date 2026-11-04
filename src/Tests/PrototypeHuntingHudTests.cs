using Godot;
using System.Reflection;
internal static class PrototypeHuntingHudTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingHud actual control four inputs geometry and focus",Hud),("PrototypeHuntingHud built standalone mod actual registration and ready hook",Entry)];
    private static Task Entry()
    {
        string pack=System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
#if STS2_PUBLIC
        const string target="public";
#else
        const string target="public-beta";
#endif
        string path=Path.Combine(Path.GetDirectoryName(pack)!,target,"AllMattersHuntingPresentationProbe","AllMattersHuntingPresentationProbe.dll");
        var assembly=System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(TestRunner).Assembly)!.LoadFromAssemblyPath(Path.GetFullPath(path));
        var entry=assembly.GetType("PrototypeHuntingHudMod",true)!;
        const string id="AllMattersHuntingPresentationProbe";
        try
        {
            entry.GetMethod("Initialize")!.Invoke(null,null);
            var info=HarmonyLib.Harmony.GetPatchInfo(HarmonyLib.AccessTools.Method(typeof(MegaCrit.Sts2.Core.Nodes.CommonUi.NGlobalUi),"_Ready"));
            NativeEventTests.Check(info!=null&&info.Owners.Contains(id),"Built standalone DLL actually registers its native global-UI ready hook under Godot/Ritsu");
            Console.WriteLine($"HUD_ENTRY target={target} assembly={path} actualInitializerInvoked=true nativeGlobalUiReadyPostfixInstalled=true; no actual NGlobalUi Ready/game launch/user visual proof");
        }
        finally{new HarmonyLib.Harmony(id).UnpatchAll(id);}
        return Task.CompletedTask;
    }
    private static async Task Hud()
    {
        var tree=(SceneTree)Engine.GetMainLoop();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        foreach(var size in new[]{new Vector2I(1280,720),new Vector2I(1920,1080),new Vector2I(2560,1440),new Vector2I(3440,1440)})
        {
            using var viewport=new SubViewport{Size=size};tree.Root.AddChild(viewport);
            var parent=new Control{Size=size};viewport.AddChild(parent);
            var focus=new Button{Text="原有焦点控件",Position=new Vector2(20,300)};parent.AddChild(focus);focus.GrabFocus();
            var panel=PrototypeHuntingHudPanel.Attach(parent);
            try
            {
                for(int index=0;index<4;index++)
                {
                    panel.ShowExample(index);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                    var rect=panel.GetGlobalRect();
                    NativeEventTests.Check(rect.Position.X>=0&&rect.Position.Y>=0&&rect.End.X<=size.X&&rect.End.Y<=size.Y,"Actual Godot container fits supplied viewport");
                    NativeEventTests.Check(viewport.GuiGetFocusOwner()==focus&&panel.FocusMode==Control.FocusModeEnum.None&&panel.MouseFilter==Control.MouseFilterEnum.Ignore,"Display changes retain original focus and panel ignores pointer");
                    NativeEventTests.Check(panel.CurrentText==PrototypeHuntingHudPanel.Examples[index],"Each explicit domain input displays its distinct complete message");
                    Console.WriteLine($"HUD_VIEW example={index} viewport={size} rectangle={rect} nativeControls=true focusRetained=true mouseFilter=Ignore text={panel.CurrentText.Replace('\n','|')}; fixture input, standalone viewport, no native GlobalUi/physical user/occlusion proof");
                }
                using var key=new InputEventKey{Pressed=true,CtrlPressed=true,Keycode=Key.F8};panel._Input(key);
                NativeEventTests.Check(panel.Example==0,"Preview key cycles without consuming or changing gameplay input");
            }
            finally{tree.Root.RemoveChild(viewport);viewport.Free();}
        }
    }
}
