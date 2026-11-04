# 追猎边界：一次性原型（未解决）

只回答[验证追猎逐敌机制、房间服务与战斗布局边界](https://github.com/Endercloud001/AllMatters/issues/70)，不是正式追猎实现。分支 `prototype/hunting-boundaries`；无安装、无真实存档、无游戏/Steam启动。

## 输入与覆盖

完整规则直接使用[追猎 resolution](https://github.com/Endercloud001/AllMatters/issues/63#issuecomment-6584771226)，历史修订消费[补充 resolution](https://github.com/Endercloud001/AllMatters/issues/66#issuecomment-6600755901)。`matrix.json` 为13组要求索引；`cases.json` 是最初按版本/人数展开的未收口工作清单，不是当前运行结果。最新真实API记录与剩余缺口见 `BATCH-RESULTS.md`、`runtime-cases.json`；`observations.json` 保留首批历史记录。相邻探针通过不会自动批准整项要求。

复用研究分支 `research/hunting-entrypoints` 的四份逐敌/服务笔记及历史探针。Godot下RitsuLib实际注册已在多人验证票通过，早期独立.NET崩溃不作为排除依据。完整检查点/原生联网仍由多人验证票承担，本票没有重测或宣布通过。

## 运行

准备好RESOURCE-SEARCH.md所述精确作者PCK及本机路径后，一条命令运行：`./prototypes/hunting-boundaries/Run.ps1 -Branch All`。它先构建三个独立探针，再调用既有Godot测试宿主；后续可加 `-SkipResourceExport`。不安装、不启动游戏或Steam、不接触真实存档。

工作树根：`./tools/Test.ps1 -Branch All -Filter PrototypeHunting`。后续仅C#改动可加 `-SkipResourceExport`。配置使用本工作树忽略的 `Paths.local.props`，全部为已有绝对开发依赖路径。两目标必须顺序运行。全组前需先运行 `./prototypes/hunting-boundaries/presentation/Build.ps1` 和 `./prototypes/hunting-boundaries/five-worm/Build.ps1`，准备各自的独立DLL；资源实验还需按RESOURCE-SEARCH.md下载作者0.13.0的PCK到忽略目录，不能用缺少输入时跳过的结果冒充通过。

探针入口为 `src/Tests/PrototypeHunting*Tests.cs` 与原创规则模型，接入一次性Godot测试宿主。局部玩法Harmony适配限于测试期间并释放；独立HUD、五虫、女王、休息点游戏包分别见presentation/、five-worm/、queen/、rest/的ACCEPTANCE.md，没有正式追猎实现。全组入口会构建四个独立包。记录刷新命令：`./prototypes/hunting-boundaries/RefreshEvidence.ps1 -Run combined-r12`；它只整理日志，不批准完成状态。宝箱反例/返回值测量即使退出0，仍明确记录requirementSatisfied=false或未定，不计为规则通过。真实结束检测/原生结算补验及其IO隔离范围见BATCH-RESULTS.md。

## 已运行范围与限制

- 原生HP缩放、指定能力固定层数、实际Feed/HandOfGreed/GremlinHorn普通敌人和Minion对照；真实五虫死亡召唤及首次行动；双墨宝与两类随从、女王原生敌方快照。范围见BATCH-RESULTS。
- 原创真菌兽、神秘术士、火炬头调用真实命令；逐敌表、知识选择、阶段抽牌、沙漏计数、商店、休息、宝箱、面具钩子和原生事件/房间栈已有分层运行证据。显式驱动、背景宿主和局部适配均单独注明，不升级为正式集成。
- 全部原生Encounter专用场景的PackedScene/Marker审计，以及部分真实物种的Spine/Bounds读取；两目标均使用beta资源。无完整UI遮挡或public原版资源证明。
- 资源读取/转换/发布分开见 `RESOURCE-RESEARCH.md` 与新增 `RESOURCE-SEARCH.md`。作者AFTP0.13.0准确真菌兽、神秘术士、火炬头4.2.11已在双版宿主加载/播放，PCK仅在忽略目录；没有把相似怪代入，也没有分发STS1素材或取得新发布许可。

## 后续依赖批次

1. 逐项核对已运行子实验与13组完整要求，补齐BATCH-RESULTS中仍可自动执行的分支。添加接口穷举、随机目标、服务节点/输入处理器/计价、房间奖励链已补跑；真实结束检测/胜利清理也已补验并发现修正孢子接缝。原型遭遇和IO隔离不等于原版完整战斗/选择屏/开箱动画与槽位布局通过。
2. 资源可加载已有实证，发布许可仍待明确；继续构造回答剩余显示问题所需的最小验收资产，不扩成完整追猎系统。HUD、五虫、女王、休息点已有独立包，商店/选择屏/开箱等入口仍需继续。集成可消费的部分输入见INTEGRATION-INPUTS.md。
3. 真实HUD四态、焦点/分辨率/遮挡和战斗布局由用户实机确认。当前无用户结果，不把未做的自动工作转交用户，也不把共享beta资源当public原版资源验证。

全部13组仍开放；不发布resolution、不关闭票、不更新地图Decisions so far、不选择集成架构。
