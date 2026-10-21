# 竞技场（GitHub #13）验证与交付

日期：2026-10-22。父规格 #2，直接阻塞 #3 已关闭；三票正文及评论（均 0 条）已读取。遵循 `docs/v0.1-scope.md` 的已确认玩法和父规格公开行为测试入口，不重新解释 Q1–Q20。

## 实现与来源

- 第三幕共享事件，唯一初始选择进入活雾 + 潮湿邪教徒首战。新遭遇使用原生怪物、独立双槽布局，`ShouldGiveRewards=false`；奖励入口亦为空，不生成默认金币、卡牌、遗物或药水。
- 首战胜利返回中场；懦弱结束且无奖励，死斗只开启一次第二战。实例阶段状态拒绝重复点击和已过期选项。
- 原生共享事件 RNG 的三个离散分支各对应残杀千足虫、蜂群术士、感染棱柱，不查询发现记录。直接使用完整原版遭遇，保留千足虫三节、专用场景、原生行动与缩放。
- 第二战根据 `ParentEventId` 和指定原版精英识别，在原生奖励生成入口提供每人 100 金币、随机稀有遗物和 Boss 策略本职业稀有三选一；可跳过并继续执行原生奖励修改钩子。与心灵绽放共用该策略，不影响地图上的普通精英，不往 `ExtraRewards` 塞不可序列化的带战斗标记卡牌奖励。
- beta 通过 `EventSynchronizer.BeforeExitingRoom` 和 `GenerateInternalCombatStateIfNecessary` 的公开入口在全队 Resume 前重置同步器。public 没有此同步器；肖像事件按原生本地拥有者路径创建新 CombatState，不触碰 Combat layout 专用状态。未使用 beta 私有字段假定 public 等价。
- 游戏在首战胜利后保存完成战斗房间与原生房间历史。恢复父事件时以首战 ID 和 `TurnsTaken>0` 重建中场；相同访问的事件 RNG 可重现第二遭遇，已选第二战由原生 CombatRoom 保存具体遭遇 ID。原生普通事件没有逐页面独立存档，测试不虚构不存在的保存节点。
- 两目标的第二战房间入口都会在原生记录器已停止时记录包含选定未完成 CombatRoom 的原生快照，时点在第二房间准备和动作之前，不使用进程全局事件标志。`PreFinishedRoom` 保留具体第二遭遇、竞技场父事件和终结战斗标记；复制当前快照历史节点后追加即将进入的第二房间，不改变真实历史，也不放宽普通事件战斗的中途存档限制。已验证 recording 状态、快照恢复具体遭遇及原生奖励身份；实际回放文件完整重播仍待实机验证。
- 中英文文案、上游重画事件图、首战场景和 PCK 接入；中文初页／中场按用户附图改写，战斗及奖励说明按已确认玩法。源码／图片来源与权属边界见 `THIRD_PARTY_NOTICES.md` 的 Colosseum 条目；未复制游戏程序集、反编译源码或额外原生美术。

方法体核查来自配置的真实目标 DLL：public-beta v0.111.0，ProductVersion `0.1.0+41cef1ea4657c524aa50e870df009e56337e8c32`；public v0.107.1，ProductVersion `0.1.0+59260271157f76a2896f0eab5bc6ea1245d8b314`。核查 EventModel、EventSynchronizer、beta EventCombatSynchronizer、EventRoom、CombatRoom、CombatManager、RunManager、CombatReplayWriter、两版真实 `NMultiplayerTest.RunReplay` 入口及三个精英遭遇。导出资料保留于忽略目录 `.local/api/queries/{public-beta-v0.111.0,public-v0.107.1}/`，不分发。

## 自动化证据

使用 implement 与 tdd；沿用已确认的“进入事件 → 执行真实选项 → 检查模型／奖励／结束状态”入口。宿主复用现有 Godot 4.5.1 mono、.NET SDK 9.0.319、RitsuLib 0.6.7 两目标模块。

命令与日志（大日志仅留本地）：

- `./tools/Test.ps1 -Filter Colosseum`：首个测试先因事件未注册失败，随后首战、零奖励、中场和逃离通过。日志 `.local/colosseum-{red,first}.log`。
- `./tools/Test.ps1 -Branch All -Filter Colosseum`：两目标各 4 项通过，日志 `.local/colosseum-targets.log`。
- 回放快照回归：`./tools/Test.ps1 -Filter 'Colosseum resumes'` 先因空房间快照失败；修正后 `./tools/Test.ps1 -Branch All -Filter 'Colosseum resumes'` 两目标通过。日志 `.local/colosseum-replay-snapshot-{red,green}.log`。
- `./tools/Check.ps1`：初次及回放快照修正后的最终执行均退出码 0，`AllMatters checks PASSED`；两目标各 **87/87** 全量行为测试通过，两目标开发包构建成功。包含现有 PowerShell 语法和 Git 空白检查。日志 `.local/colosseum-{check,final-check}.log`、`.local/checks/last-run.log`。首次重编译测试报告已有 `Lagavulin.cs:199/200` 两个 CS8604 和 `MaskedBanditsTests.cs:155` 一个 CS8602；本票新增代码无编译警告。随后的开发包增量构建均为 0 警告、0 错误，不将增量输出解释为已消除上述旧警告。

四项测试覆盖：

1. 幕准入、首战强制选择、指定双怪、零奖励、返回中场、逃离无奖和重复旧回调。
2. 真实 EventSynchronizer 驱动双方实例，分别以 NetId 1/2 为本地上下文，逐一控制三种随机分支；完整原生遭遇、千足虫三节及场景；双方各领取金币和稀有遗物，选择／跳过稀有牌；第二战记录器初始化及其公开输入快照经原生 RunState／CombatRoom 反序列化后保留选定未完成遭遇、奖励身份、历史条目且不污染真实历史；原生完成房间及个人物品序列化。
3. 原生 EventRoom 身份保存、已完成首战历史下重建中场、同访问重建不换遭遇、三种蛋的原生升级修改。
4. 导出 PCK 中事件图片和首战双槽场景、中英文初页／中场选项与遭遇标题。

记录器测试先失败：宿主 `HeadlessBanditRoomEntrance` 比生产 Prefix 更早截断入口。将该宿主设为 `Priority.Last` 后，生产入口补丁先执行，再替代房间调度／UI；记录器断言通过。未以跳过生产补丁或替代记录器来掩盖失败。public 控制随机源使用其 Seed/Counter 构造，beta 使用原生 SerializableRng；不检查私有字段。

规格审查随后发现仅 `IsRecordingReplay` 通过不足以证明第二场恢复：初版 `ToSave(null)` 没有 `PreFinishedRoom`，两版真实 RunReplay 均会经 `LoadIntoLatestMapCoord` 重新 Roll/CreateRoom。新增公开 RecordInitialState 边界观察，断言其具体房间和历史；先复现失败，再以包含终结第二战房间的原生快照修正。普通保存的 `CombatRoom.ToSerializable` 限制未改动，快照仅供回放初始加载。仍未执行真实回放 UI／完整动作重播。

宿主边界：替代全局战斗进行中／结束／空手通知，以及 UI／房间调度；模型、命令、RNG、事件同步器、奖励生成／修改／选择和资源加载保留真实实现。胜利通过原生完成房间及历史准备驱动，不运行完整怪物回合。两 NetId 同进程不是两进程主客机。public DLL 行为使用 beta 的 PCK／Spine 运行环境作为资源宿主，不能据此宣称完整 public 游戏兼容。物品及房间序列化通过不能证明实际退出续局没有重复领奖。

## 代码审查

按 code-review 分开执行 Standards 与 Spec。固定起点为开始实现前的 `26e9208902e55d72a7e2a8acae3eff8f859d44ba`；按本项目既有审查方式，以相对该点的本票暂存变更审查，随后提交到当前 main。初始工作区已有的配置、文档、工具及测试项目改动不纳入本票提交。

## Standards

0 项确认发现。已审查相对固定起点的全部本票暂存变更，并核对项目指令、审查说明、开发流程及领域词汇。

未发现文档规范违例或需要修改的代码异味。生产代码位于 `src/`；复用了既有模型、同步器、奖励策略及测试宿主，没有引入额外框架、hash、contract 或 gate。来源声明覆盖改编源码和图片。

验证记录明确区分目标 DLL 行为、PCK 加载、宿主替代与实机证据；已核对全量检查成功日志，双目标各 87 项通过。未将序列化、记录器初始化或同进程 NetId 测试夸大为真实读档、联机或回放验证。既有未提交改动未纳入本票审查范围。

Standards：0 项发现，无最高严重问题。

回放修正复审：快照仅复制当前地图历史节点并追加第二房间，未改动真实历史或放宽普通 CombatRoom 存档限制。测试观察公开 `RecordInitialState` 输入，并通过原生反序列化核对具体遭遇、父事件、奖励身份和历史隔离，没有依赖私有字段。验证记录仍明确区分快照恢复与真实回放动作重播；后者尚未验证。Standards 最终仍为 0 项确认发现。

## Spec

初版发现 1 项 P1，现已修复：#13 要求“两目标……回放初始化”，A30 要求“回放状态可继续”。原 `ToSave(null)` 未保存第二战房间，两版真实 `RunReplay` 会重新抽取地图房间。修订后的 `ColosseumCombatPatches.cs` 为回放保存选定的未完成第二战及独立历史节点，保留父事件奖励身份，不修改真实历史或放宽普通存档限制。已核对两版调用链及失败→通过日志；新增测试通过原生反序列化验证具体遭遇、历史和奖励策略。

最终暂存变更未发现其他明确的遗漏、错误实现或范围扩大。首战零奖励、中场选择、同步器恢复、完整三种原生精英、精确奖励及原生修改均对应规格。

验收证据缺口仍包括 beta 完整两战、实际退出续局、两进程主客机、真实回放重播及完整 public 运行；文档已如实列明，不能以本轮自动化结果关闭 #13。

Spec：1 项初版问题已解决，0 项待处理代码发现；实机验收待补。两轴最终汇总：Standards 0 项，Spec 1 项已修复、0 项未解决。

## 手动安装与未验证范围

Agent 未启动游戏／Steam，未向 `mods` 安装或改动既有安装。开发包目录：

- `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/dist/public-beta/AllMatters/`
- `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/dist/public/AllMatters/`

由用户退出游戏、备份原包，将与游戏分支对应的完整 AllMatters 目录替换至 `mods/AllMatters/`，保持 DLL、manifest、PCK、LICENSE 和来源声明齐全。独立依赖使用 [RitsuLib 0.6.7](https://github.com/BAKAOLC/STS2-RitsuLib/releases/tag/v0.6.7) 的完整 variant-pack，安装到 `mods/STS2-RitsuLib/`，保留 `shared/`、两个 `compat/`、manifest 等全部目录，不能只装单个 compat DLL。本机完整依赖来源 `.local/dependencies/RitsuLib-0.6.7/`。通过 Steam 启动，事件调试入口沿既有流程选择 `ALL_MATTERS_EVENT_COLOSSEUM`；共享池应仅在第三幕准入。

请按以下步骤留下实际结果：

1. beta 中英文查看初页、图片和选项；首战仅一只活雾和一只潮湿邪教徒，完整胜利后不出现任何战利品，直接返回中场。检查摆位、动作、音效和文字排版。
2. 分别测试懦弱直接结束，以及死斗只进入一次新战斗；三种精英逐一完整战斗，确认千足虫前中后三节及专用场景、原生缩放。
3. 第二战胜利各得 100 金币、随机稀有遗物和可跳过的本职业稀有三选一；有原生奖励修改时确认对应效果且无默认精英奖励叠加。
4. 在游戏真实允许保存的位置退出／续局：首战前、首战完成（恢复后应回中场）、第二战、领奖前后；分别记录恢复落点和物品数量，不将游戏不支持的页面保存视为已验证。特别核对不重复开奖、领取、开战及遭遇不重抽。
5. 两进程主客机各自投票、返回中场、再次投票、领奖，核对状态一致；分别播放两场实际回放，重点检查第二战初始房间及动作序列。

目前未执行 beta 完整两战、正式游戏资源展示、实际退出续局、两进程联机、真实回放重播及完整 public 游戏运行。A26／A27／A28／A30／A31 的这些实机证据尚缺；本次自动化／构建／代码提交不表示 #13 可以关闭或正式发布已完成。
