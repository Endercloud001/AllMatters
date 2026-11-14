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

本节当时尚未实现的 Matriarch 左侧随从已由下方2026-11-14增量覆盖；Soul Fysh、Seapunk 等剩余规则仍待实现。Waterfall 已由实施索引后续记录及下节自然致死增量覆盖；旧版资源边界不代签当前目标。

这些测试使用单人传输服务下的 1–4 个战斗角色；它们不证明四端点联机。public 宿主使用共享 beta PCK，不代表 public 正式资源验收。真实场景输入、目标选择和布局、整场战前/胜利恢复及最终整批 Check 需另行验证。
## Waterfall 首次自然致死尾部修复（2026-11-14）

源码 `c973505` 修复一个直接调用 Trigger 的旧六例未能发现的生产缺陷：原生 `CreatureCmd.Damage` 首次致死，经 SteamEruption.AfterDeath 等待 Trigger 复活后，Kill 仍执行死亡能力清理，把新 Artifact1、原 Regen 和隐藏状态全部清成0。真实 public red2 为 Build0、Host1，明确输出 `Regen=0, hidden=0, Artifact=0`，不是编译或夹具失败。

新增 `Hook.ShouldPowerBeRemovedOnDeath(PowerModel)` postfix 保留原 listeners；仅当前合法受罚 Waterfall、仍存活在同一 combat、Steam 尚在且 ABOUT_TO_BLOW 阶段，保留精确属于该怪物的当前正面/隐藏能力。可见当前负面仍走原清理；安全根、过期实例和最终死亡不扩大豁免，不新增保存标记。

新增第7例以1/4个 combat角色×safe/hunted执行原生 Pressurize→完整首次致死 Damage→实际 Artifact 挡一次 Weak→原生 AboutToBlow/Explode 最终死亡。独立保存致死前真实 Regen 实例/原生缩放量和隐藏实例，核完整返回后不变；safe 明确按原生清除两者；最终独立 Artifact/Strength 以及 Regen/隐藏状态均清除。测试没有直接调用 Trigger，也没有跳过 Kill/AfterDeath。Regen普通来源沿原生人数缩放，本片不重算该算法。

实施树 `waterfall-lethal-20261114` 的 `.local/executor-waterfall-lethal/` 保存实际红绿；`Test.ps1 -Branch All -Filter 'Jigsaw hunting Waterfall' -SkipResourceExport` 双版各7 PASS，补 safe 断言后精确新例双版各1 PASS，Build/Host/Engine/Metadata均0。beta 编译既有 CS8785 warning 未冒称零警告。目标 public0.107.1/beta0.113.0，RitsuLib0.6.11，使用匹配资源，不沿用旧共享beta资源结论。

独立 Standards、Spec 对 fixed `776e2ef` 两文件 staged diff均无阻塞；Standards 提出的 safe 清理反例已补并只读增量复核通过。正常 commit hooks 成功，提交差异与已审差异完全相同；CA1310无可用code fix提示不改变退出0。正式UI、布局、完整恢复、真实远端及整批Check/包仍未由本片代签。

## Matriarch 左侧睡眠随从（2026-11-14）

源码 `3008bf6` 经 `4df655b` 合入。受罚顶层 Underdocks 的原生 `LagavulinMatriarchBoss.GenerateMonstersWithSlots` 前置一只本 Mod Lagavulin，保留原生创建/HP/RNG和睡眠链；精确 mutable 实例完成原 `AfterAddedToRoom` 后授固定 `MinionPower1`。不虚构槽位、不加持久 marker；原族母多人生命减半与睡眠不变。真实 Damage 杀死族母后，仍在睡眠的爪牙自然清理。

真实 public 入房先报敌人数1而非2，修后通过；新增非Jigsaw无acts反例又捕获 predicate 在校验session/root之前读取Act的越界，真实red后将校验前置。安全根、真实事件子房、非Jigsaw及独立同类来源不加随从；1–4角色验证固定Minion1、原睡眠/能力与HP。初次扩测中的Metallicize类型和事件父房准备错误属夹具失败，修复后完整重跑，没有把它们归为产品red。

实施树 `matriarch-minion-20261114/.local/executor-minion/final4-{public,public-beta}-host.log` 双版各7 PASS，Build/Host/Engine/Metadata全0。集成 `4df655b` 实际运行 `Test.ps1 -Branch All -Filter 'Jigsaw hunting Underdocks' -SkipResourceExport`，双版各7 PASS且四诊断0；日志在集成树 `.local/executor-20261114/minion-integrated-{public,public-beta}-host.log`。资源未变化，复用该树有效PCK。正常hooks退出0，CA1310无可用code fix提示保留，不能写成零警告；提交patch与双轴已审暂存patch完全一致。

Standards与Spec两轴独立读取最终230+/1−两文件及实际双版日志，均无发现。正式场景布局、完整战前/奖励fresh恢复和真实玩家端仍归#84，不以本片关闭整票。

## SoulFysh 左右随从与正式布局（2026-11-14）

源码`2d3658c`经`d9b80b4`合入：受罚顶层Underdocks的SoulFyshBoss/SecondBoss在原生生成列表左加Toadpole、右加SludgeSpinner，精确mutable随从完成原AfterAddedToRoom后各授Minion1，保原怪HP、首招、原生人数缩放与死亡链。真实Boss致死后原生清理随从；safe、事件子房、非Jigsaw、异幕、无acts、普通根及过期/异常原Task等边界不追加或不授予。

正式原生布局在1/4角色×1280/1680宽720核HP/意图包络、画布边界、鼠标目标与方向导航；单人紧凑真red发现Boss血条分别压左右随从约7logical，限定当前受罚三怪行的PositionEnemies补丁窄调X后同例green。四人夹具曾误把玩家原生重叠和两次挂载绝对坐标差当新增故障，改用同场safe玩家身份对的既有重叠对照，敌人相关几何仍严格；这些夹具失败不冒称产品red。原生too wide警告保留，实测几何及输入通过，不写零warning。

实施和集成双版SoulFysh各10 PASS、集成Underdocks相邻各7 PASS，Build/Host/Engine/Metadata均0。原始日志为实施树`.local/executor-soulfysh/final-suite-{beta,public}-host.log`、集成树`.local/integration-slices/soulfysh-integrated-{beta,public}-host.log`及`soulfysh-underdocks-regression-{beta,public}-host.log`。独立Standards/Spec无阻塞，正常hooks三处仅换行已复核。覆盖1/4 combat角色，不代签2/3角色、完整存档fresh、真实多游戏端或实机；整票仍未完成。

## Seapunk 普通与事件原生开场（2026-11-14）

源码`a7c9c52`经`efdc880`集成：受罚Underdocks普通/Unknown根在原生生成结束后相对原名单恰加一只mutable原生Seapunk；已有同类仍相对+1。保持原顺序、slot、HP、起手及RNG，新增者为正常primary非Minion且必须击败。Unknown内连续普通/具名Elite/具名Boss分别增强；safe、非Jigsaw、异幕及顶层Elite/Boss/SecondBoss排除。三种专用scene保原marker，Seapunk锚点从实际生成slot取得；GremlinMerc的Slots为空但真实merc存在，不能用空声明推断没有原marker。

真实beta入房原为expected1/actual0；补生产后green，生成矩阵又捕获GremlinMerc地面锚点错误并修正。14原生普通池（10regular/4weak）×1–4角色验证生成与缩放；原生三招循环、Bubble的Block7/Strength1及必须击败均走原生命令。Unknown重复绑定RunState、SecondBoss缺doubleBoss准备及直接执行MoveState绕过历史均为夹具失败，未冒称产品red。

实施和集成专项双版各5 PASS，Build/Host/Engine/Metadata全0。实施树`.local/executor-seapunk/final-suite-{beta,public}-host.log`；集成树`.local/integration-slices/seapunk-integrated-{beta,public}-host.log`。共享接缝相邻回归Forest入口1、Mystic入口1、Glory Axebots正式scene1及Underdocks7双版通过；日志实施树`regression-{forest,mystic-entry,glory-scene}-*-host.log`和`regression-underdocks-fixed-*-host.log`。Underdocks旧event断言总数1实际2失效，严格改为原Boss+恰一非Minion Seapunk，仍排除族母Lagavulin/Minion；原red日志保留。

独立Standards与Spec及旧oracle增量复审无阻塞；正常hooks成功，提交patch与已审暂存patch完全一致。14场正式HP/意图/鼠标/焦点布局及完整fresh恢复另片，不以三scene生成锚点代签正式输入；真实端及#84整票未完成。
