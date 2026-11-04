// Read only native resource API inspection; no asset substitution or installation changes.
using Godot;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
internal static class PrototypeHuntingResourceTests
{
    internal static readonly (string Name,Func<Task> Test)[] Cases=[("PrototypeHuntingResource actual installed Spine resource loader metadata",Formats),("PrototypeHuntingResource unchanged STS1 data compatibility measurement",OldData),("PrototypeHuntingResource exact AFTP public release converted species",Converted)];
    private static async Task Converted()
    {
        STS2RitsuLib.RitsuLibFramework.EnsureGodotScriptsRegistered(typeof(MegaCrit.Sts2.Core.Models.CardModel).Assembly,STS2RitsuLib.RitsuLibFramework.CreateLogger("Prototype70Resource"));
        string pack=Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
        string source=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,"../.local/resource-search/ActsFromThePast.pck"));
        NativeEventTests.Check(ProjectSettings.LoadResourcePack(source,false),"Mount only author public resource pack in isolated host; existing paths cannot be overridden");
        var tree=(SceneTree)Engine.GetMainLoop();await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        PrototypeHuntingModelTests.Inject();
        foreach(var (species,animations,model) in new (string,string[],MegaCrit.Sts2.Core.Models.MonsterModel)[]{("fungi_beast",["Attack","Hit","Idle"],MegaCrit.Sts2.Core.Models.ModelDb.Monster<PrototypeHuntingFungus>()),("mystic",["Attack","Hit","Idle"],MegaCrit.Sts2.Core.Models.ModelDb.Monster<PrototypeHuntingMystic>()),("torch_head",["halberd_slam","idle"],MegaCrit.Sts2.Core.Models.ModelDb.Monster<PrototypeHuntingTorch>())})
        {
            string path="res://ActsFromThePast/monsters/"+species+"/"+species;
            using var data=ResourceLoader.Load<Resource>(path+"_skel_data.tres");
            NativeEventTests.Check(data.Call("is_skeleton_data_loaded").AsBool(),"Actual converted species skeleton loads through installed runtime");
            using var scene=ResourceLoader.Load<PackedScene>(path+".tscn");
            using var visuals=model.CreateVisuals();
            NativeEventTests.Check(visuals.SceneFilePath==path+".tscn","Prototype species model resolves exact authored resource rather than native fallback");
            try
            {
                tree.Root.AddChild(visuals);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                var skeleton=visuals.SpineBody?.GetSkeleton()?.GetData();
                NativeEventTests.Check(skeleton!=null&&animations.All(skeleton.HasAnimation),"Exact original species animation names survive conversion");
                foreach(string animation in animations){visuals.SpineBody!.GetAnimationState().SetAnimation(animation,true,0);await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);}
                var animator=model.GenerateAnimator(visuals.SpineBody!);animator.SetTrigger("Attack");
#if STS2_PUBLIC
                var track=visuals.SpineBody!.GetAnimationState().GetCurrent(0);
#else
                using var track=visuals.SpineBody!.GetAnimationState().GetCurrent(0);
#endif
                NativeEventTests.Check(track?.GetAnimationName()==(species=="torch_head"?"halberd_slam":"Attack"),"Native model animator trigger uses actual species attack, not default missing lowercase animation");
                animator.SetTrigger("Hit");animator.SetTrigger("Idle");
                var intent=visuals.GetNode<Node2D>("%IntentPos");
                Console.WriteLine($"RESOURCE_CONVERTED species={species} authorRelease=0.13.0 skeletonVersion={data.Call("get_version")} nativeLoaded=true modelCreateVisuals=true sceneReady={visuals.IsNodeReady()} boundsPosition={visuals.Bounds.Position} boundsSize={visuals.Bounds.Size} intentPosition={intent.Position} spineScale={visuals.GetNode<Node2D>("%Visuals").Scale} animations={string.Join(',',animations)} allPlayed=true source={path}.tscn; author PCK mounted read-only in host, no upstream DLL, no redistribution permission or user rendering proof");
            }
            finally{tree.Root.RemoveChild(visuals);visuals.Free();}
        }
    }
    private static Task Formats()
    {
        using var control=ResourceLoader.Load<Resource>("res://AllMatters/monsters/lagavulin/lagavulin_skel_data.tres");
        NativeEventTests.Check(control!=null,"Existing complete species resource is actually loaded");
        foreach(string type in new[]{"SpineSkeletonFileResource","SpineAtlasResource","SpineSkeletonDataResource"})
        {
            NativeEventTests.Check(ClassDB.ClassExists(type),"Installed native extension registers "+type);
            Console.WriteLine($"RESOURCE_API type={type} extensions={string.Join(',',ResourceLoader.GetRecognizedExtensionsForType(type))}");
            foreach(var method in ClassDB.ClassGetMethodList(type,true))
            {
                string name=method["name"].AsString();
                if(name.Contains("skeleton")||name.Contains("load")||name.Contains("atlas")||name.Contains("version")||name.Contains("json"))Console.WriteLine($"RESOURCE_METHOD type={type} name={name}");
            }
        }
        foreach(var property in control!.GetPropertyList())
        {
            string name=property["name"].AsString();
            if(name.Contains("atlas")||name.Contains("skeleton"))Console.WriteLine($"RESOURCE_PROPERTY name={name} type={property["type"]}");
        }
        return Task.CompletedTask;
    }
    private static async Task OldData()
    {
        var tree=(SceneTree)Engine.GetMainLoop();
        await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        using var control=ResourceLoader.Load<Resource>("res://AllMatters/monsters/lagavulin/lagavulin_skel_data.tres");
        NativeEventTests.Check(control.Call("is_skeleton_data_loaded").AsBool(),"Positive control loads skeleton through installed native runtime");
        Console.WriteLine($"RESOURCE_CONTROL skeletonVersion={control.Call("get_version")} loaded=true; this is loaded export version, not native DLL exact build");
        string pack=Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="ModResourcePack").Value!;
        string scratch=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(pack)!,"../.local/hunting70-sts1-PROTOTYPE"));
        string jar=System.Environment.GetEnvironmentVariable("ALLMATTERS_PROTO_STS1_JAR")??"E:/SteamLibrary/steamapps/common/SlayTheSpire/desktop-1.0.jar";
        using var archive=ZipFile.OpenRead(jar);
        foreach(string species in new[]{"healer","torchHead"})
        {
            string source="images/monsters/theCity/"+species+"/";
            string folder=Path.Combine(scratch,species);Directory.CreateDirectory(folder);
            string json=Read(source+"skeleton.json");
            using var document=JsonDocument.Parse(json);
            string version=document.RootElement.GetProperty("skeleton").GetProperty("spine").GetString()!;
            NativeEventTests.Check(version=="3.4.02","Actual original skeleton version retained");
            string imageName=species=="healer"?"healer.png":"skeleton.png";
            string imagePath=Path.Combine(folder,imageName);
            archive.GetEntry(source+imageName)!.ExtractToFile(imagePath,true);
            using var image=Image.LoadFromFile(imagePath);
            NativeEventTests.Check(!image.IsEmpty(),"Original species PNG decodes");
            using var texture=ImageTexture.CreateFromImage(image);
            string textureName="original_pixels.tres";
            NativeEventTests.Check(ResourceSaver.Save(texture,Path.Combine(folder,textureName))==Error.Ok,"Wrap same pixels as local Godot texture without editor import");
            string atlas=Read(source+"skeleton.atlas");
            // Only page reference changes; skeleton/regions/coordinates/animation data stay original.
            atlas=string.Join('\n',atlas.Replace("\r\n","\n").Split('\n').Select(line=>line.Trim()==imageName?textureName:line));
            string atlasPath=Path.Combine(folder,"original.spatlas"),jsonPath=Path.Combine(folder,"original.spjson");
            File.WriteAllText(atlasPath,atlas);File.WriteAllText(jsonPath,json);
            using var atlasResource=ResourceLoader.Load<Resource>(atlasPath);
            using var skeletonResource=ResourceLoader.Load<Resource>(jsonPath);
            using var data=(Resource)ClassDB.Instantiate("SpineSkeletonDataResource").AsGodotObject();
            data.Set("atlas_res",atlasResource);data.Set("skeleton_file_res",skeletonResource);data.Call("update_skeleton_data");
            using var sprite=(Node)ClassDB.Instantiate("SpineSprite").AsGodotObject();
            sprite.Set("skeleton_data_res",data);tree.Root.AddChild(sprite);
            NativeEventTests.Check(sprite.IsInsideTree(),"Native species sprite really enters the standalone tree");
            await tree.ToSignal(tree,SceneTree.SignalName.ProcessFrame);
            bool loaded=data.Call("is_skeleton_data_loaded").AsBool();
            Console.WriteLine($"RESOURCE_COMPAT species={species} originalSkeletonVersion={version} PNG={image.GetWidth()}x{image.GetHeight()} atlasResource={atlasResource!=null} skeletonResource={skeletonResource!=null} nativeSpriteInstantiated=true nativeSkeletonLoaded={loaded} scratch={folder}; no skeleton conversion or publication permission claimed, inspect native errors");
            tree.Root.RemoveChild(sprite);sprite.Free();
        }
        return;
        string Read(string entry){using var reader=new StreamReader(archive.GetEntry(entry)!.Open());return reader.ReadToEnd();}
    }
}

