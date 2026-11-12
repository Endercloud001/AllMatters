# Issue #84：Underdocks 已实现切片

本页记录 Gardener、Skulking Colony、Terror Eel 三项增强。它们属于 #84 的部分实现，不能据此关闭整张工单。

## 行为与边界

| 对象 | 受罚根战斗中的行为 | 保留的原生行为 |
| --- | --- | --- |
| Phantasmal Gardener | 各原始怪物的初始 Skittish 固定为 9 | 原始四槽位、招式和之后其他来源的 Skittish 多人缩放 |
| Skulking Colony | 初始 Hardened Shell 固定为 20，另加固定 Plating 6 | 原生生命及多人缩放、首个 ZOOM_MOVE，以及之后其他来源的能力缩放 |
| Terror Eel | 原生 TerrorMove 完成后，对它的同一目标集合追加 Weak 99 | 原先 Vulnerable 99 的顺序、Artifact 消耗、非目标玩家及后续 CRASH_MOVE |

生产变更位于 `src/Jigsaw/JigsawHuntingUnderdocks.cs`。开场固定数值只对本次授予的具体可变能力实例豁免一次缩放，并在 finally 清理；同一生物后来获得的其他实例仍走原生缩放。Plating 覆写与基础 PowerModel 的缩放入口分别处理。Eel 包装原生异步任务，等待其完成后再追加能力，不重新挑选目标。

作用域限定于有效 Jigsaw 会话、已受罚的顶层 Underdocks 根战斗；其他战斗保留原始流程。Harmony 目标和内部行为已查阅 public v0.107.1、public-beta v0.111.0 的本地反编译查询。

## 已执行验证

统一命令：

```powershell
./tools/Test.ps1 -Branch All -Filter 'Jigsaw hunting Underdocks' -SkipResourceExport
```

两个目标各通过 3 个用例，构建和宿主退出均为 0。现有执行记录报告 EngineErrors、ModMetadataErrors 均为 0，FirstException、Preparation 为空。每例覆盖 1–4 个战斗角色及未受罚对照；Eel 另含 Artifact 0/1 与真实招式目标子集。断言读取实际能力量、原生 History、生命、槽位和后续招式。

本地证据保存在该实现 worktree 的 `.local/issue84-underdocks/`：

- `final-three-green-public.log`、`final-three-green-public-beta.log`：两个宿主各 3 项通过与退出状态。
- `eel-green-all.log`：统一双目标运行摘要。
- `gardener-red-public.log`、`colony-red-public.log`、`eel-red-public.log`：对应行为失败。
- `progress.md`：红绿过程、Plating 实际覆写反例及双版本源码位置。

主控此前收到独立 Standards、Spec 审查无阻塞项的结论；原审查未发现持久报告文件。后续复核须以暂存的三文件切片为范围，排除未运行的 Matriarch 草稿，并补留报告。

2026-11-13 已由两名新的独立只读审查者复核，范围均为 `git diff --cached 2f95862408c7141a0c86269b955626981af284f8 -- src/Jigsaw/JigsawHuntingUnderdocks.cs src/Tests/JigsawHuntingUnderdocksTests.cs src/Tests/HelloWorldTests.cs`，实际读取索引、相关源码及双版本目标方法，未运行构建或测试：

- Standards 无阻塞项。所有 Harmony 目标、测试声称的真实断言和已有用例注册方式符合仓库标准。唯一非阻塞建议是复用 `IsHuntedCombat` / `IsHuntedElite` 中重复的根房间作用域条件，减少以后修正边界时遗漏。
- Spec 无阻塞项。逐项对照 #63 resolution 6584771226 的 H 表：四只 Gardener 初始9、Colony 固定Shell20另加Plating6、Eel同一目标先Vulnerable99再Weak99均满足当前切片；固定能力缩放仅豁免本次可变实例。事件反例尚缺运行测试，源码边界已检查。未发现范围扩张。

两轴均排除未运行的 Matriarch 草稿，不据此声明 #84 整票完成。final-three-green 双版各3日志是实施方执行证据，审查者只读核对，非独立重跑。

## Matriarch 生命切片（2026-11-13）

在前述三项之外，Matriarch 原生多人生命减半及睡眠保持已执行红绿验证。实际 public 开场失败为 A0、1 人、受罚时预期 111/111，实际 222/222；记录在 `matriarch-red-public-host.log`。生产仅包装 `LagavulinMatriarch.AfterAddedToRoom`，等待原生 Sleep 完成，再对有效受罚的顶层 Underdocks `LagavulinMatriarchBoss` 调用 `CreatureCmd.SetMaxAndCurrentHp`。数值取原生已缩放最大生命的整数二分之一，不执行伤害、不更改 Min/MaxInitialHp 或现有普通/精英作用域。

统一四用例回归使用同一 `Test.ps1 -Branch All -Filter 'Jigsaw hunting Underdocks' -SkipResourceExport` 命令，两个目标各通过 4 项，Build/Host 退出均为 0，EngineErrors、ModMetadataErrors 均为 0，FirstException、Preparation 为空。记录为 `.local/issue84-underdocks/matriarch-four-green-{public,public-beta}.log`，控制台摘要为 `matriarch-four-green-all.log`。新增用例通过真实 Boss 开场覆盖 A0/A7/A8 × 1–4 个角色 × 未受罚/受罚，对照普通原生多人缩放，断言最大/当前生命向下取整减半、Asleep 3、原生 Plating、睡眠状态与招式、无 DamageReceivedEntry。ToughEnemies 原生阈值是 A8，基础生命由 222 变为 233。

此新增切片于2026-11-13完成独立双轴审查。两名只读审查者实际读取 `git diff --cached 936f557 -- src/Jigsaw/JigsawHuntingUnderdocks.cs src/Tests/JigsawHuntingUnderdocksTests.cs docs/issue84-underdocks-implementation.md`、相关源码和双版原生方法，未自行运行测试。Standards为0阻塞/0建议；Spec无当前切片错误或范围扩张，确认原生缩放后再减半、等待Sleep、不触发伤害/苏醒。事件同名/非Jigsaw的实际负例尚缺，左侧随从和整票其余规则仍待；当前源码边界经过检查不能代签这些运行路径。

## 尚未完成

Matriarch 左侧既有 Lagavulin 随从、Soul Fysh、Waterfall Giant、Seapunk 等剩余规则仍待实现。

这些测试使用单人传输服务下的 1–4 个战斗角色；它们不证明四端点联机。public 宿主使用共享 beta PCK，不代表 public 正式资源验收。真实场景输入、目标选择和布局、整场战前/胜利恢复及最终整批 Check 需另行验证。
