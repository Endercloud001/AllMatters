# 你好世界验证记录

日期：2026-10-20。需求依据：[设计与验收预期](../hello-world-design.md)。

## 运行方式

配置 Paths.local.props 中的 PublicGameDataDir 和 BetaGameDataDir，安装 .NET 9 SDK 及 Godot 4.5.1 mono。测试项目是 src/Tests 下的独立 Godot 项目；生产 DLL 排除其全部源码，不携带测试依赖。

在仓库根目录依次运行（GodotPath 按本机路径填写）：

```powershell
./tools/Test.ps1 -Branch public-beta -GodotPath 'E:/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe' -DotNetRoot docs/local/tools/dotnet9
./tools/Test.ps1 -Branch public -GodotPath 'E:/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe' -DotNetRoot docs/local/tools/dotnet9
./tools/Build.ps1 -Branch public-beta
./tools/Build.ps1 -Branch public
```

DotNetRoot 参数可省略，但 Godot 必须选用 .NET 9：本机 Godot 默认选择已安装的 .NET 10，与游戏当前 Harmony 不兼容。本机忽略目录 docs/local/tools/dotnet9 是只暴露已安装 .NET 9 host/fxr 与 shared/Microsoft.NETCore.App 的开发运行根目录，不随仓库分发；其他机器可指定自己的 .NET 9 运行根目录。测试编译仍使用系统 dotnet。

Test.ps1 可用 -Filter 'Mixed' 仅运行名称匹配的测试；无匹配、异常及超时均失败。两个目标共用测试项目输出位置，按顺序运行。

## 测试环境与边界

- 每次构建引用对应分支的真实 sts2.dll：public 0.107.1，public-beta 0.111.0。通过 Harmony 安装实际生产补丁。
- 运行于独立 Godot 4.5.1 无界面进程。两个目标都只读挂载本机 beta SlayTheSpire2.pck，提供语言表及现有图像；public 的资源版本及完整启动兼容性不由此得到验证。
- 使用游戏 TestMode 和测试用存档存储、真实玩家/战斗模型、公开 OnPlayWrapper、能力回合钩子、原版 CardFactory、RNG、PowerCmd、CardCmd 和 CardPileCmd。
- 测试宿主代替全局游戏循环：将 CombatManager 的进行中/结束中状态设为进行中/未结束，跳过依赖全局房间的空手通知。不替代本 Mod 的任何类，也不替代生成工厂、升级或入牌命令。
- 测试直接设置公开 AmountOnTurnStart 模拟原版回合快照。尚未跑完整房间调度、实际战斗画面、ModManager 正式安装加载、两进程联机或回放系统。
- 不修改游戏安装、Steam 分支或玩家存档。测试代码、游戏程序集、资源包与本机运行根目录不进入 Mod 安装包。

## 红绿过程

1. 原版卡牌升级测试先因获得固有失败；抑制 HelloWorld.OnUpgrade 后通过。
2. 通过公开打出接口测试升级来源，先因只有原版能力而失败；新增升级来源能力后通过。
3. 混合来源先因分开抽取与原版共同批次的选牌不一致失败；共同生成后，选牌及后续 RNG 输出均与原版一次工厂调用一致。
4. 展示测试先因升级来源缺少本地化标题失败；加入专属文案及图像路径复用后通过。

## 自动检查覆盖

共 14 个行为测试，覆盖：取消升级固有；普通单层；升级单层；混合共同生成及 RNG；英文文案与升级预览；普通两层/升级三层及连续回合；出牌与钩子顺序；满手牌溢出后仍升级；回合快照；回响形态重复施加；角色池、解锁及单人/多人限制；模板和其他生成实例隔离；非所属玩家回合；中英文切换。

这些检查验证局部规则及命令执行，不代表完整游戏验收。

## 本轮结果

| 目标程序集 | 无界面行为测试 | 开发构建与打包 |
| --- | --- | --- |
| public 0.107.1 | 14/14 通过 | 通过；0 警告、0 错误 |
| public-beta 0.111.0 | 14/14 通过 | 通过；0 警告、0 错误 |

两分支输出分别位于 dist/public/AllMatters 和 dist/public-beta/AllMatters，每包只有 AllMatters.dll 与 AllMatters.json。完整日志保存在忽略目录 docs/local/test-results；测试中显示的程序集版本 0.1.0.0 是游戏内部 AssemblyVersion，不用于识别游戏分支，分支依据来自对应引用目录及既有 API 调查。

代码审查在实现提交后，以实现前 1e3ec0c 为起点执行 Standards 与 Spec 两轴检查；结论待追加。

## 仍需在完整游戏中验收

1. 在只安装 AllMatters 的干净 mods 目录下分别启动 public 与 public-beta，确认能力模型发现、网络模型编号、日志和两个图标及计数的实际呈现。
2. 两分支分别实战覆盖单层、混合、多层、多回合、满手牌及回响形态；与常用 Mod 共存检查。
3. 两进程联机下，分别由主机和客机施加两类能力，跨回合及手牌溢出后核对层数、生成牌、升级状态和同步诊断。
4. 记录上述战斗的真实回放，重播核对生成顺序、升级份额及 RNG 同步结果。本轮确定性测试不能替代这些步骤。

public 本机尚无完整游戏安装；两分支的上述人工验收均未完成。输出仍为开发构建，不是正式发布。
