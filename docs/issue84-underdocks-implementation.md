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

## 尚未完成

Matriarch 的原生多人生命减半及睡眠保持测试仍是未编译、未运行的草稿，不属于上述三项验证或提交范围。Matriarch 左侧既有 Lagavulin 随从、Soul Fysh、Waterfall Giant、Seapunk 等剩余规则仍待实现。

这些测试使用单人传输服务下的 1–4 个战斗角色；它们不证明四端点联机。public 宿主使用共享 beta PCK，不代表 public 正式资源验收。真实场景输入、目标选择和布局、整场战前/胜利恢复及最终整批 Check 需另行验证。
