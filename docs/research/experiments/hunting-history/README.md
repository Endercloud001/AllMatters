# 历史实现最小只读探针

2026-11-03。未运行游戏或生成的程序集。

## STS1元数据

```powershell
python docs/research/experiments/hunting-history/inspect_sts1.py E:/SteamLibrary/steamapps/common/SlayTheSpire/desktop-1.0.jar
```

输入为本机已有JAR，结果sts1-metadata.txt；退出0。脚本只打开ZIP、解析中文本地化和骨骼JSON、读取版本常量，未提取素材到项目。字节码阅读使用：

```powershell
C:/Users/Endercloud/.jdks/ms-21.0.10/bin/javap.exe -classpath E:/SteamLibrary/steamapps/common/SlayTheSpire/desktop-1.0.jar -p -c com.megacrit.cardcrawl.monsters.city.Healer
C:/Users/Endercloud/.jdks/ms-21.0.10/bin/javap.exe -classpath E:/SteamLibrary/steamapps/common/SlayTheSpire/desktop-1.0.jar -p -c com.megacrit.cardcrawl.monsters.city.TorchHead
```

退出0，原始完整字节码仅留本机`.local/history/`，不分发游戏代码；方法/offset与行为摘要见STS1笔记。

## 当前双版本接口可引用性

```powershell
dotnet build docs/research/experiments/hunting-history/Probe.csproj -p:GameDir=E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/.local/api/public-github-0.107.1 --nologo --verbosity quiet
dotnet build docs/research/experiments/hunting-history/Probe.csproj '-p:GameDir=E:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64' --nologo --verbosity quiet
```

两次退出0，均0警告/0错误，见public.log/beta.log；对应public0.107.1及beta0.111.0。编译输入为未注册、原创的接口探针，不包含旧版专有源码。未执行DLL；不证明细看效果、Flash、第三阶段时序、存档或联机。bin/obj不提交。
