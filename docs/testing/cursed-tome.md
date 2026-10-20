# 诅咒书本验证记录（#6）

日期：2026-10-21。父规格 #2；直接阻塞 #3 已 closed/completed。读取 #6、#2、#3 正文与全部评论（均 0 条），沿用 Q1–Q20、A04–A07/A29 及已确认公开行为测试入口。审查起点 `87a438449ea1ab9573045bd9727aeeead36ede6d`，当前分支 `main`；本票不包含开始前已有的工作流、工程配置和需求文档改动。

## 实现与机制

- 仅第二幕且全队无三书任一本准入；每名玩家有自己的事件实例，不共享选书池。初页阅读免费；中间只有继续，依次损失 1/2/3；拿走额外 15，停止额外 3，进阶 0–10 数值一致。拿走先结算伤害，再进入三选一；只能取得一本。
- 所有损血选项使用原生 `ThatDoesDamage` 提示与 `CreatureCmd.Damage`，保留 Unblockable/Unpowered、防死亡和复活钩子；结算后检查 `Creature.IsDead`，死亡结束自己的事件，不发书。public 的伤害来源重载用命名参数消除歧义；beta 使用带 cardPlay 的重载。
- 发现实际分支差异：beta 0.111.0 的原生 `KillWithoutCheckingWinCondition` 在非战斗多人环境阻止死亡并回复 1 HP；public 0.107.1 没有这个分支。本实现保留该原生安全措施，不强制 Kill，不修改全局死亡规则。因此 beta 多人致死代价可能由游戏保命后继续选书，不能承诺其一定死亡。两目标均以伤害结算后的实际存活状态为准，队友继续独立选择。真实双端仍待验证。
- 死灵之书按已解析能量费用判断，每个拥有者回合首次至少 2 费的攻击多打一次，其他玩家回合不重置；取得时加入一张永久、不可打出、不可升级的死灵诅咒。诅咒消耗后原实例回手。尼利在拥有者回合结束从原生合法本职业候选生成三个升级实例，可跳过，选中后随机洗入抽牌堆。英雄宝典在每场第一拥有者回合生成本职业能力牌，本回合免费，原生命令处理手满及费用清理。
- 逻辑及必要素材改编自 Balance GPLv3，逐项来源见根目录 `THIRD_PARTY_NOTICES.md`。未复制游戏资源或 ActsFromThePast 文件，未执行上游构建/部署脚本。

## 自动化命令与证据

- 首次 `./tools/Test.ps1 -Filter 'Cursed Tome'` 在未实现时失败于“Cursed Tome is missing from registered events”，见 `.local/cursed-tome-red.log`；注册、实现、资源接通后同一阅读测试通过。
- 定向命令：`./tools/Test.ps1 -Branch All -Filter 'Cursed Tome'`；两目标各 12 项通过，exit 0，日志 `.local/cursed-tome-tests.log`。
- 最终完整检查：`./tools/Check.ps1`（两分支完整套件、语法/空白、两分支资源导出及开发包）；两目标各 47 项通过，包含既有你好世界、牧师、老乞丐/泉水与增益研究者回归；两目标编译均 0 警告、0 错误，两份开发包生成，整体 exit 0。日志 `.local/checks/last-run.log` 与 `.local/cursed-tome-check.log`。
- 测试日志的无 UI 资源缓存/能量图标 fallback 和测试程序集未关联正式 Mod 的诊断来自既有宿主，不是正式加载结果；beta 多人致死用例会触发原生阻止战斗外多人死亡的 ERROR 诊断，但实际断言的结果为原生回复 1 HP，未屏蔽这些日志。
- public-beta：v0.111.0，程序集 ProductVersion `0.1.0+41cef1ea4657c524aa50e870df009e56337e8c32`；public：v0.107.1，`0.1.0+59260271157f76a2896f0eab5bc6ea1245d8b314`。Godot mono 4.5.1；.NET SDK 9.0.319；RitsuLib 0.6.7 对应分支模块。
- 覆盖：全队书籍准入、免费离开、各阅读页、所有进阶、21/9 生命、主动三选一及重复旧选项、相同书籍独立选择、各伤害页致死提示和原生蜥蜴尾巴、原生多人致死差异、死灵书费用/拥有者/回合限制、真实诅咒消耗回手、尼利候选升级及跳过、英雄宝典后续回合/第二场重置及费用到期、物品序列化恢复、中英文及实际 PCK 图片加载。

### 保存节点

两版 `RunManager.EnterMapCoord` 在退出旧房间后、进入新房间前调用 `SaveManager.SaveRun(null)`。普通 `EventRoom.ToSerializable` 保存事件 ID 和 IsPreFinished，不保存阅读页或选项；只有先古事件的完成路径会额外保存为 pre-finished。未给本事件新增中间页自动保存或任意时点存档。

因此在各阅读页、末页、付费选书页或刚领取后退出，再次加载普通入口快照时，玩家生命/牌组/遗物与事件一起回到入口。自动化用真实 `Player.ToSerializable/FromSerializable` 和 `EventRoom` 格式模拟上述入口快照，分别在 0–6 次选项后恢复，重新完整领取后仍只支付 21 并获得一本书及一张诅咒；另检查领取后的物品快照恢复不重跑拾取加诅咒。此处是序列化行为检查，未驱动真实 Save and Quit UI、磁盘存档、下一地图节点或网络恢复。实际各页退出读档与下一节点保留奖励待用户验收。

### 宿主替代与限制

沿用既有 Godot headless 宿主、真实目标 DLL、原生模型/命令/工厂/RNG/本地化。原宿主默认替代 CombatManager.IsInProgress=true、IsEnding=false、空手通知；新增仅测试使用的可释放事件作用域，把 IsInProgress 临时设为 false，使事件伤害不写不存在的战斗历史。作用域结束恢复原值。为原生序列化测试初始化模型缓存，beta 额外先初始化 AssemblyInfo。没有替代伤害、防死亡、费用、候选、遗物授予或序列化逻辑。

三书战斗测试由宿主准备 CombatState 并驱动真实公开回合钩子/重放次数 Hook 和消耗命令，不运行整场战斗调度或实际攻击动画；两人模型测试并非两进程联机。public 使用本机 beta PCK 提供原生资源，不能证明完整 public 游戏加载或视觉兼容。

## 用户实机交付与未验证范围

按 implement → code-review 技能完成两个独立并行审查轴，以开始实现时的 `87a4384` 为起点审查本票 staged diff（不含原有未提交改动）。Standards：0 项规范违反，1 项 P3 可选建议（死灵书两个钩子重复资格条件，可日后提取私有判断）；当前条件一致，沿用上游两钩子实现，不作为阻塞。Spec：0 项代码缺陷/表外功能；1 项尚未完成的 beta 用户实机验收。审查确认保留 beta 原生多人死亡安全措施与普通事件入口快照语义，自动化不能替代双端/实机/回放。审查后的改动仅为测试空值注解、遗物参数缩进及记录补全；随后重新执行完整 Check。

Agent 不启动 Steam/正式游戏，不复制到游戏 mods，不修改存档。安装与实机验收按项目 AGENTS.md 由用户完成。本次未取得新的 beta 用户实测，#6 的 beta 实际可玩验收仍未完成，不关闭 issue。

开发包：

- `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/dist/public-beta/AllMatters/`
- `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/dist/public/AllMatters/`

依赖完整包：`E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/.local/dependencies/RitsuLib-0.6.7/`，上游 [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)。将所用分支 AllMatters 目录完整复制到对应游戏的 `mods/AllMatters/`；RitsuLib 完整 variant-pack 安装为独立 `mods/STS2-RitsuLib/`，保留 shared/compat 等结构，不只复制单个 compat DLL。先退出游戏再更新，由 Steam 启动。

待人工检查：

1. beta 正式加载并在第二幕遇到诅咒书本；中英文图片、各页唯一继续、损血数值、末页与三书提示正确；全队持书不再准入。
2. 分别离开、停止、领取三书；低生命选项有原生致死提示且可点；单人无保命死亡不发书，有蜥蜴尾巴先复活后选书；多人按目标原生保命规则处理，队友仍可独立结束。
3. 后续实战检查死灵书首个合格攻击多打一遍、降费不触发、次回合重置；诅咒永久且消耗回手。尼利升级三选一/跳过并洗入抽牌堆；英雄宝典只在首回合生成，本回合后恢复费用，手满走原生溢出。
4. 各阅读页、拿书付费后的选择页、领取后退出并读档；核对入口快照整体恢复，不叠加费用/书/诅咒；进入下一节点保存后奖励应保留且不再次加入诅咒。
5. 真实双端独立选择同一本/不同本、停止和致死分支；录制并重播相关回放。完整 public 正式加载、死亡/复活、联机和回放均未执行，按环境/集成票及本票实际验收边界继续跟进。
