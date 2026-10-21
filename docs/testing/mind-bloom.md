# 心灵绽放 #12：实现与验证记录

日期：2026-10-21。需求：[issue #12](https://github.com/Endercloud001/AllMatters/issues/12)、父规格 #2 与 [范围表](../v0.1-scope.md)。已在线确认阻塞项 #3 关闭。沿用已确认玩法、公开行为测试入口和现有 Godot 宿主。实现前起点 `496c8a8686f222b2d8d6486ae3e20074f79bc3d9`，当前分支 `main`。本轮提交仅包含 #12 的实现、资源、来源说明、调研和本记录，保留开始前已有的文档、csproj、工具及其他未提交改动。

## 行为与实现依据

- Act 3，原生共享选择。每个事件实例只结算 Owner 一次；37 层及以前富有，38 层起无伤痛，单双人一致。富有 +999 金币、Normality/Greed 各一张；无伤痛走原生治疗并加入 Enthralled/Regret 各一张。没有使用 Writhe 或上游 Doubt。
- 觉醒升级全部可升级永久牌组实例，获得事件遗物 MarkOfTheBloom。RitsuLib `IHealHookListener` 仅对其 Owner 将正治疗倍率置零；战斗、休息和事件仍通过原生 `CreatureCmd.Heal`，不全局禁止队友治疗。金币、加牌、遗物和升级均沿原生命令。
- 凯旋是一场默认沉睡的双一代 Lagavulin 战斗。新建独立双槽位遭遇，各自可变怪物与睡眠计数。保留 Balance 的普通/进阶 HP 109–111/112–115、攻击 18/20、力量敏捷吸取 -1/-2、3 回合睡眠和基础 8 金属化。多人血量及金属化缩放交给原生创建/能力链。
- 非穿透格挡不醒，未格挡伤害只唤醒受伤者并移除其金属化；自然醒按各自敌方回合推进。保留睡眠/清醒 Spine 动作及边界容器。上游随机分支在该怪中未使用 RNG，改用原生有序 `ConditionalBranchState`，没有复制通用 Runtime。
- 事件保持普通插画布局，使用原生 `EnterCombatWithoutExitingEvent(..., false)` 创建一次终结战斗，保留 `ParentEventId`；不返回第二战页，不设置 Boss 房间，不接上游 Boss 包装器/二战同步补丁。public 仅本地实例建房，beta 由原生同步器汇总各实例 ready；本票不注入两者私有字段。

### 奖励与保存

只对 `MindBloomEncounter` 的 `RewardsSet.WithRewardsFromRoom` 使用局部 Prefix，在原生奖励阶段关联房间并创建每位玩家的 100 金币、随机稀有遗物及基础三选一 CardReward。卡牌参数为正常 Boss 的角色牌池、Encounter 来源、BossEncounter 稀有度策略；beta 保留 `IsFromCombat`。原生人口/解锁筛选、升级概率、奖励修改、额外奖励、多选钩子、选择同步与跳过行为仍由原生奖励流程执行。

这样不生成并丢弃默认战利品，也不将带 `IsFromCombat` 的 CardReward 放入 `ExtraRewards`；两版 `CardReward.ToSerializable` 会拒绝该额外标记，而原生 CombatRoom 保存只遍历其额外奖励。合法 pre-finished 房间可原生往返恢复，恢复后仍使用本遭遇奖励策略。未为此新增存档格式、hash、冻结 contract、baseline 或 gate。

静态证据及源路径见 [开发前调查](../research/2026-10-21-issue-12-preflight.md)。这里的合法房间/玩家序列化测试不证明任意战斗时点可保存，也不等同真实退出续局、重复领奖防护或回放已验证；实际节点行为仍须实机确认。

## 资源及归属

Balance 版本 `d9eff9e067b07689e722c5283e7c3e7c451961ca`，本次在线核对与本地一致。GPLv3 声明、逐项文件与修改已登记根 `THIRD_PARTY_NOTICES.md`。本轮移植事件、怪物、睡眠/金属化能力、印记及必要双语文本；事件文案按项目规格重写。

事件宽幅图、印记三种图标、睡眠图标、Lagavulin 的场景/骨骼数据/图集/二进制骨骼/PNG 来自 Balance。金属化能力另有直接依赖，图标取自 Balance 的 `Assets/ActsFromPast/images/powers/actsfromthepast-metallicize_power.png` 镜像；初步调研误称随机分支原生且漏列金属化，现已在 [资源附件](../research/2026-10-21-issue-12-source-assets.md) 更正。两个能力均明确复用各自同图为大小图，避免缺图路径。

素材沿用 Balance GPLv3 仓库声明和本项目现有强盗素材归属记录方式；不宣称已完成一代 Spine 或镜像图标的独立权属审计。没有从单独 AFTP 源码归档复制内容，没有额外引入镜像 BGM。游戏 DLL/PCK/原生 Spine 库/反编译源码均不进入开发包。PCK 导出复用现有纹理导入和原样场景/Spine 打包流程。

## 自动化验证

目标实际程序集：public v0.107.1，ProductVersion `0.1.0+59260271157f76a2896f0eab5bc6ea1245d8b314`；public-beta v0.111.0，ProductVersion `0.1.0+41cef1ea4657c524aa50e870df009e56337e8c32`。Godot 4.5.1 mono、net9.0、RitsuLib 0.6.7 对应 compat 模块。

执行入口：

```powershell
./tools/Test.ps1 -Filter 'Mind Bloom'
./tools/Test.ps1 -Branch All -Filter 'Mind Bloom'
./tools/Test.ps1 -Branch All -Filter 'Mind Bloom Lagavulin retains'
./tools/Test.ps1 -Branch All
./tools/Build.ps1 -Branch public-beta
./tools/Build.ps1 -Branch public
```

最终全量 `./tools/Test.ps1 -Branch All` 以退出码 0 完成，两目标各 **82/82** 通过，含本票 7 项及既有 75 项回归。public-beta 和 public 的 `Build.ps1` 均退出码 0，产出实际 DLL、manifest、PCK、LICENSE 和来源说明。生产构建无新增编译警告；测试编译保留既有 `MaskedBanditsTests.cs:155` 的 CS8602，本票测试无编译警告。没有 Godot 原生 `ERROR:`/`WARNING:` 资源错误。

日志仍含既有测试宿主手工注入模型时“程序集未关联正式 mod”的注册诊断、资源未预缓存提示；正式 ModManager 加载和网络排序不是该宿主验证内容。不能将这些宿主诊断隐去后推断正式加载或真实多人已通过。

定向测试覆盖：

1. Act 准入、单双人 37/38 分支互斥、原生选择结算只作用于 Owner、两种精确诅咒、999 金币/全治疗、旧选项不重复执行及完成后玩家序列化。
2. 觉醒升级所有合法牌而不升级诅咒；获得一次印记；原生事件治疗、实际休息治疗入口、战斗关联治疗均只阻断持有者；升级/遗物完成后原生序列化。
3. 本地身份分别为玩家 1/2，原生两版事件入口只创建一个双怪战斗，非 Boss、无第二战；每人原生生成/领取金币与稀有遗物，合法职业稀有牌基础三选一，一人选择一人跳过；合法战后房间保存往返和恢复后的奖励策略。
4. 真正调用原生伤害命令，未穿透格挡不醒，穿透仅醒目标；逐回合执行睡眠动作和公开能力钩子，另一只第三回合自然醒；单/双人使用原生 HP 缩放。
5. 原生 PrayerWheel 后续奖励修改仍能增加奖励；原生三种蛋的候选升级效果保留，基础 Boss 奖励仍为稀有。
6. 普通与进阶 HP、原生攻击意图、连续两次攻击再力量/敏捷吸取。
7. 生成 PCK 的事件图、两语言表、五种图标、原生怪物 Visuals 的六个 Spine 动作及睡/醒边界容器、双怪槽位。

TDD 记录：事件缺失先失败；觉醒空实现先因牌未升级失败；战斗空实现先因零房间失败。随后逐段接入。中途修正的测试宿主准备包括初始牌组计数、休息奖励同步器初始化、原生领奖历史节点，以及真实睡眠动作完成后才滚动下一行动；没有替换卡牌/奖励工厂或伤害/治疗命令来绕过业务失败。日志在 `.local/mind-bloom-{red-choices,red-awakening,red-fight,targeted,sleep,ascension,full,build-beta,build-public}.log`，不提交大日志。

宿主边界：复用既有 `HeadlessCombatHost` 的进行中/结束/空手通知替代和 `HeadlessBanditRoomEntrance` 的房间调度/UI 截取。模型、命令、目标 DLL、RitsuLib、RNG、同步器和资源加载保持实际实现。单进程按两个 NetId 执行不是真实主客机；手工调用公开回合钩子不等于完整战斗调度；public 资源检查仍借助本机 beta 的 PCK/Spine 原生环境。因此不将无界面通过称为正式 Mod 加载、两目标完整运行或网络/回放验收。

## 代码审查

按 implement 要求使用 code-review 技能分开进行 Standards 与 Spec 审查。由于要求审查后才提交，审查输入为相对固定起点 `496c8a8` 的本票已暂存变更（`git diff --cached 496c8a8`），不把既有工作区改动混入；审查时没有本轮提交清单。审查期间主代理自查补齐新遭遇 title/loss 的中英本地化及标题断言，并修正本票测试可空警告，两个审查者均收到该暂存范围更新；随后定向资源检查、最终全量测试和双目标构建通过。

## Standards

0 项确认发现。已核实固定起点有效、暂存 diff 非空，并审查本票代码、资源、本地化、来源声明和验证记录。未发现违反项目指令的代码结构、依赖扩张、额外 hash/contract/gate、游戏安装改动或验证范围夸大；现有你好世界测试入口仅追加本票测试集合。

## Spec

0 项确认代码问题或范围扩大。已核对 Act 3 准入、共享选择按 Owner 各结算一次、37/38 分界、四种诅咒映射、觉醒与持有者治疗阻断、双乐加维林独立睡眠/唤醒、原生多人缩放及单场事件战斗。奖励仅对新遭遇替换，继续经过原生生成、修改、选择/跳过流程；保存方案避免将带战斗标记的 CardReward 放入 ExtraRewards。

验收证据缺口单列：A14 完整游戏战斗、A28 真实领奖、实际退出续局、A31 两进程联机与回放以及完整 public 环境尚未执行；本次审查不表示 issue #12 已可关闭。Standards/Spec 各 0 项确认代码发现，人工验收待补。

## 手动安装和待验收范围

Agent 未启动游戏/Steam、修改现有安装或复制产物到游戏 mods。开发输出：

- `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/dist/public-beta/AllMatters/`
- `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/dist/public/AllMatters/`

由用户退出游戏后备份原包，再把对应分支完整目录复制为游戏 `mods/AllMatters/`。保留 DLL、manifest、PCK、LICENSE 与来源文件。独立依赖是 `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/.local/dependencies/RitsuLib-0.6.7/` 的完整 variant-pack，安装为 `mods/STS2-RitsuLib/`，保留根 DLL、manifest、shared、compat 与包内资源；不要只复制单个 compat DLL。之后通过 Steam 启动对应游戏分支。

1. 用可丢弃测试局，在第三幕输入 `event ALL_MATTERS_EVENT_MIND_BLOOM` 可直接查看事件。该命令绕过自然准入，另用第三幕未知房间确认自然入池。
2. 分别在累计 37/38 层检查第三选项切换；另开测试局分别领取富有、无伤痛、觉醒，记录金币、诅咒、升级和印记。检查持有者的战斗、休息与牧师治疗为零，队友可正常治疗。
3. 中英文检查插画、选项和两只怪物的站位、意图及睡醒图像。攻击一只但不穿透格挡时保持睡眠，穿透后只有该只醒；另一只自行第三回合醒。胜后检查每人基础 100 金币、随机稀有遗物与稀有牌奖励；可选择或跳过，没有第二战或过幕。
4. 带原生奖励修改遗物复测；修正规则允许的额外奖励不能误认为默认奖励重复。记录实际遗物和修改结果。
5. 在游戏实际允许保存的事件入口、战后领奖前后退出续局，检查继续流程和领取状态，无重抽/重复领取；不要求新增任意中途存档。
6. 两进程真实主客机检查共享投票、一次建房、两只行动、每人领奖和后续可继续；另记录真实回放重播结果。

当前没有本票 beta 实机反馈、真实主客机、真实退出续局或回放证据，完整 public 游戏环境亦未运行。上述实机项目仍待用户执行，本票不因代码提交而宣称已全部验收或关闭。
