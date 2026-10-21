# Issue #12：心灵绽放源码与资源清单

调查日期：2026-10-21。仅调研，不实现功能、不执行参考仓库脚本、不启动游戏。本报告为 [开发前调查](2026-10-21-issue-12-preflight.md) 的资源附件。需求以 [issue #12](https://github.com/Endercloud001/AllMatters/issues/12)、[事件范围表](../v0.1-scope.md) 和 [父规格](https://github.com/Endercloud001/AllMatters/issues/2) 为准。

## 来源和结论

主来源 `B = .local/references/STS2-Balance-MOD/`，本地版本 `d9eff9e067b07689e722c5283e7c3e7c451961ca`；主调查已在线核对该版本仍为上游 main。对照来源 `A = .local/references/ActsFromThePast/ActsFromThePast/`，为先前下载的源码归档，不将其视为当前 Workshop 发行版。以下路径简写以 B、A 为前缀。

实现所需的事件图、遗物图、睡眠能力图和乐加维林 Spine 视觉资源均已在 B 中找到，**无需为缺少基础美术而从零绘制**。代码能改造复用，但上游事件玩法、奖励及连续第二战与本票显著不同；双怪遭遇须新增。素材在仓库内可取得，不等于已完成第三方权利链核实，尤其是沿袭一代的 Spine 动画。

## 代码可复用范围

| 路径与位置 | 已确认事实 | 本票处理 |
| --- | --- | --- |
| `B/Sts2BalanceModCode/Events/MindBloom.cs:30`、`:44`、`:170` | `IsShared=true`，Act 3；升级分支遍历牌组中可升级牌，获得 MarkOfTheBloom。 | 复用共享事件和非战斗命令结构；沿项目命名注册，不复制完整事件。 |
| 同文件 `:52`、`:82`、`:186`、`:204` | 单人阈值 41／多人 38；凯旋随机第一幕 Boss、50 金币和稀有遗物；富有两张 Normality；治疗一张 Doubt；战后提供第二战。 | 改成统一 38、双乐加维林、100 金币与稀有遗物及 Boss 规则稀有牌；诅咒按范围表替换；删除第二战依赖。 |
| `B/Sts2BalanceModCode/Relics/MarkOfTheBloom.cs:15` | EventRelicPool、事件稀有度；实现 RitsuLib `IHealHookListener`，只将拥有者治疗倍率改为 0。 | 可直接改造；必须验证两版治疗链，覆盖战斗、休息、事件及队友隔离。 |
| `B/Sts2BalanceModCode/Monsters/Lagavulin.cs:33` | 默认 `StartsAwake=false`；血量普通 109–111／ToughEnemies 112–115；攻击 18／DeadlyEnemies 20，吸取 -1／-2 力量敏捷；默认 8 金属化与 3 睡眠。 | 复用怪物行为与进阶，创建两个独立可变实例；沿原生多人缩放链，勿自行乘血量。 |
| 同文件 `:111`、`:120`、`:129`、`:274` | 受伤醒、自然醒、睡醒后 Bounds/CenterPos/IntentPos/Hitbox 调整及 Spine 动作映射均存在。 | 保留这些视觉边界逻辑；需要双怪实际场景检查，不能仅移植攻击状态机。 |
| `B/Sts2BalanceModCode/Powers/AsleepLagavulinPower.cs:20` | 伤害目标必须为自身且 `UnblockedDamage>0` 才醒；每个怪物持有自身计数；第一回合玩家方开始获得金属化对应格挡，第三个敌方回合结束自然醒。 | 可改造复用；覆盖只打醒其中一只、未穿透不醒、各自自然醒，确认两版时序。 |
| `B/Sts2BalanceModCode/Encounters/DeadAdventurerLagavulinEncounter.cs:14` | 只有一个 `single` 槽位，且显式 `StartsAwake=true`。 | 仅参考注册／创建方式；不能直接用于本票，需双槽位默认睡眠遭遇。 |
| `B/Sts2BalanceModCode/Patches/Events/MindBloomCombatPatch.cs:22`、`:51`、`:82` | 同步器 reset 与回放初始化服务于第二战；奖励过滤只删额外金币、遗物，保留默认卡牌／其他奖励。 | 不整包移植。单场事件仍需目标版恢复与奖励核查，但不需要上游第二战状态。原过滤不足以实现本票精确奖励。 |

实施核查更正：上游 `Lagavulin.cs` 的 `RngConditionalBranchState` 是 `Runtime/Combat/ConditionalBranchState.cs` 中的自定义类型，并非原生类型。其本怪选择函数没有使用 RNG，可用原生 `ConditionalBranchState` 的有序条件分支承接，无需复制通用 Runtime。另外，`MetallicizePower` 也是上游自定义能力，必须一并移植；先前仅列睡眠能力的依赖清单不完整。对话使用 `L10NMonsterLookup` 和原模型 ID，需要改为本项目本地化入口。`BalanceMonsterTemplate`、`BalancePowerTemplate` 提供的是资源／文本约定，已有 AllMatters 结构可承接，不必复制上游全部抽象层。

项目内已经有 `src/Monsters/BanditMonster.cs:10`，通过 RitsuLib 工厂将 Node2D 根的上游场景转为 `NCreatureVisuals`；乐加维林场景同为 Node2D 根，可复用该创建方式。已有 `src/Encounters/RedMaskBandits.cs` 和强盗资源导出／测试作为事件战斗接入参考。不要仅因命名叫 BanditMonster 就强行让新怪物继承该类，可提取必要且清晰的共同实现。

源码链接：[MindBloom](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceModCode/Events/MindBloom.cs)、[MarkOfTheBloom](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceModCode/Relics/MarkOfTheBloom.cs)、[Lagavulin](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceModCode/Monsters/Lagavulin.cs)、[AsleepLagavulinPower](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceModCode/Powers/AsleepLagavulinPower.cs)、[事件补丁](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceModCode/Patches/Events/MindBloomCombatPatch.cs)。

## 美术与资源闭包

以下尺寸为读取本地 PNG 头得到，未据此声称资源在游戏内已成功导入。

| B 内实际文件 | 可获得性与用途 |
| --- | --- |
| `Sts2BalanceMod/images/events/MindBloom.png` | 5,586,900 字节，3440×1616；现成事件宽幅图。母版 `image_gen/source/events/MindBloom.png` 及旧原图备份存在，优先使用游戏目录的重画版本。 |
| `Sts2BalanceMod/images/relics/MarkOfTheBloom.png`、`big/MarkOfTheBloom.png`、`outlines/MarkOfTheBloom.png` | 分别 94×94、256×256、94×94，均存在；母版 `image_gen/source/relics/MarkOfTheBloom.png` 存在。 |
| `Sts2BalanceMod/images/powers/AsleepLagavulinPower.png` | 256×256，存在；**没有 `powers/big/AsleepLagavulinPower.png`**。上游 `BalancePowerTemplate.cs:14` 声明大图路径，因此本项目应显式复用同图为大小图，或按既有资源约定输出缺少的变体，勿照抄不存在的路径。 |
| `Assets/ActsFromPast/images/powers/actsfromthepast-metallicize_power.png` | 实施时补查到的金属化图标，位于 B 根目录的镜像树；需记录该具体来源及与怪物同类的权属证据边界，不能声称来自上游重画目录。 |
| `Sts2BalanceMod/monsters/lagavulin/lagavulin.tscn` | 2,273 字节；Node2D 根，有睡眠／清醒边界、意图与中心点、SpineSprite 和 SleepVfxPos。 |
| 同目录 `lagavulin_skel_data.tres` | 388 字节；引用同目录 `lagavulin.spatlas` 和 `lagavulin.spskel`。 |
| 同目录 `lagavulin.spatlas` | 1,912 字节；JSON atlas 内嵌数据指向 `lagavulin.png`，页面尺寸 1024×256。其中 `source_path` 仍写 `.atlas` 的原导入路径；本目录没有独立 `.atlas`，需沿项目已有 `.spatlas` 导出方式验证，不将此元数据字符串当作已证明的缺失运行时依赖。 |
| 同目录 `lagavulin.spskel` | 36,242 字节二进制；可读头含 Spine 4.2.11，字串中找到 Attack、Coming_out、Debuff、Idle_1、Idle_2。这只证明文件与动作名称线索存在，不能替代 Spine 加载、动画及睡醒验证。 |
| 同目录 `lagavulin.png` | 162,514 字节，1024×256；atlas 纹理齐全。 |

`tscn → tres → spatlas+spskel → PNG` 的显式外部资源链在本地齐全。场景无外部脚本、额外音频或其他纹理引用。复制到 AllMatters 时只改 `res://Sts2BalanceMod/` 路径和项目资源 ID，保留节点约定；新增一个双怪遭遇布局，不能把单槽位场景误当成双怪现成资源。仍须用当前 Godot／游戏 Spine runtime 进行导入与 PCK 资源加载测试。

英文／简中条目齐全：`Sts2BalanceMod/localization/{eng,zhs}/events.json:62–80`、`relics.json:44–46`、`powers.json:48–50`、`monsters.json:86–92`。事件文字包含错误奖励、旧诅咒、第一幕 Boss 与第二战说明，必须按本票重写；怪物与印记文本可改造复用，统一项目中文名“乐加维林”。

资源链接：[视觉场景目录](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/tree/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceMod/monsters/lagavulin)、[事件图](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceMod/images/events/MindBloom.png)、[配图说明](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/docs/events.md#事件配图)。

## 音频与额外依赖

上述 Balance 怪物、能力、事件未声明自定义音频文件或自定义 BGM，攻击只显式引用原生 `vfx/vfx_attack_blunt`；受击等默认反馈交给原生怪物／RitsuLib 流程。代码和资源检查未发现本票必须额外复制的音频；实际听感仍需实机验证，不能据此断言所有声音均正常。

B 的 `Assets/ActsFromPast/ActsFromThePast/bgm/mind_bloom.ogg` 确实存在，但本次 Balance 事件没有调用链指向它。本票无需为了名字相符而引入该音轨或全部 ActsFromPast 镜像资源。

## 许可、归属与仍需确认的部分

1. B 根 `LICENSE` 与 `README.md:253–258` 声明 GPLv3；AllMatters 已采用 GPL-3.0-only，并有 `THIRD_PARTY_NOTICES.md` 的复用先例。代码、重画事件图和印记图可按该上游声明继续准备，实际引入时追加逐项文件来源和修改说明。[上游许可](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/LICENSE)
2. `docs/events.md:15–17` 说明十张自制事件图经 imagegen 重画并指向提示词文件。但引用的 `image_gen/event-art-redraw.json` 在当前本地版本中不存在；本轮只能核实配图声明和实存母版，不能声称提示词证据齐全。保留来源陈述，不把“重画”推断为已独立审计权利链。
3. Lagavulin 的 Spine 资产在 B 的 `Assets/ActsFromPast/ActsFromThePast/monsters/lagavulin/` 也有镜像，A 的 Lagavulin 源码和 Balance 行为高度一致。应保留 Balance、ActsFromThePast 及底层游戏素材归属，不能将 Balance 根许可证表述为已证明原游戏动画被重新授权。沿项目既有强盗素材做法可以记录实际分发依据；若需要超出该依据的权利保证，应向来源作者确认，或改用有明确许可的替代视觉。
4. A 本地归档未见根 LICENSE，且无任何 PNG／TSCN（递归检索均为 0）；包含源码与多语言文本。其 README 明确 GitHub 不再更新发行版、最新版本在 Workshop。因此 A 只适合作行为对照，既不能作为已获直接复制授权的代码源，也不是本票美术的现成本地供给。[AFTP 仓库](https://github.com/Cany0udance/ActsFromThePast)

## 验证边界

本轮执行了路径检索、源码／场景／本地化读取、PNG 头尺寸和 Spine 字串检查；所有目标素材均为已有本地参考副本，没有下载或运行未知脚本，没有编译／导入这批新资源，没有拷贝至游戏安装目录。确认的是“可取得且静态引用基本闭合”，不是“资源已通过游戏加载”或“新事件已可玩”。开发需验证双槽位摆位、Spine 运行时加载、两只各自睡眠／受伤／自然醒，以及本票精确奖励和恢复行为。
