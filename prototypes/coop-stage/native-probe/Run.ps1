$ErrorActionPreference='Stop'
$root=(Resolve-Path '../AllMatters').Path
$dep=Join-Path $root '.local/dependencies/RitsuLib-0.6.7'
$godot='E:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64'
$evidence='prototypes/coop-stage/native-probe/evidence'
New-Item -ItemType Directory -Force $evidence | Out-Null
foreach($target in @('0.107.1','0.111.0')){
 $game=if($target -eq '0.107.1'){Join-Path $root '.local/api/public-github-0.107.1'}else{$godot}
 & dotnet build prototypes/coop-stage/native-probe/Probe.csproj "-p:DependencyRoot=$dep" "-p:RitsuLibReferenceTarget=$target" "-p:GameDir=$game" "-p:GodotDir=$godot" --nologo -v quiet *> "$evidence/$target-build.log"
 $buildExit=$LASTEXITCODE
 if($buildExit -ne 0){throw "Probe build failed ($target): exit $buildExit"}
 & dotnet prototypes/coop-stage/native-probe/bin/Debug/net9.0/Probe.dll "$dep/compat/$target" "$dep/shared" $game $godot *> "$evidence/$target-run.log"
 $runExit=$LASTEXITCODE
 "target=$target buildExit=$buildExit runExit=$runExit" | Tee-Object -FilePath "$evidence/$target-status.txt"
 Get-Content "$evidence/$target-run.log" -Tail 5
 if($runExit -ne 0){throw "Probe run failed ($target): exit $runExit"}
}
