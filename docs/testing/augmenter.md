# 增益研究者验证记录（#5）

实现日期：2026-10-20 至 2026-10-21。父规格 GitHub #2，直接阻塞 #3 已关闭；读取三票完整正文及评论（均无评论）。玩法与测试入口沿用 `docs/specs/sts1-event-return.md` 的 A01、A08–A10，未重新解释 Q1–Q20。

## 实现与机制证据

- 个人事件注册到共享事件候选池，由 `IsAllowed` 限定第二幕且每人至少两张可移除牌；注册池不改变个人选择语义。
- J.A.X. 使用事件牌池，0 费技能，默认 `Innate`，原生命令自损 3，再加 2 力量，升级加 3。public 的自损命令只传卡牌来源，beta 另传 `CardPlay`，分别与目标原生 `Bloodletting.OnPlay` 一致。
- 变化使用原生强制两张选牌、`Niche` RNG、显式本角色候选的 `CardCmd.Transform`，升级返回的 `cardAdded`（真正加入牌组的实例），不碰旧对象或共享模板。准入按可移除牌数；主动选项另查原生变化资格（排除任务牌），不足两张禁用。
- 初始遗物根据角色 `StartingRelics` 的模型 ID 匹配实际持有实例，移除后才领取突变之力。五名原版角色已测试；不按栏位、不拿替换遗物抵费。未扩展 Mod 多初始遗物玩法。
- 突变之力沿用 Balance 的 `AfterRoomEntered(CombatRoom)` 时点：加 3 力量并用原生命令施加可见 Debuff。两版原生房间/钩子调用链及目标命令已核对；能力层数保存剩余拥有者回合（3→2→1→移除），到期一次施加 -3 力量。其他玩家与敌方回合不推进；人工制品能拦截 Debuff，保留力量增益。没有增加另一套计时/存档框架。
- 中英文事件/牌/遗物/能力文本通过现有本地化补丁合并；缓存按语言与表名区分，避免新增表互相污染。图片随 PCK 导出，事件不需要额外自定义场景。

方法体与签名查询命令：`tools/ExportApiReference.ps1`，类型包括 `CardCmd`、`CardSelectCmd`、`CharacterModel`、`RelicCmd`、`ArtifactPower`、`Hook`、`CombatRoom`、`Bloodletting`；分别使用 `-Branch public` 与默认 beta。输出在忽略的 `.local/api/queries/`，不分发反编译代码。来源、素材路径及 GPLv3 声明见 `THIRD_PARTY_NOTICES.md`。

## 自动化

使用现有 Godot 4.5.1 mono / .NET 9 宿主、RitsuLib 0.6.7 与两版真实游戏程序集：public 0.107.1（`5926027`），public-beta 0.111.0（`41cef1e`）。public 测试挂载的是本机 beta 游戏资源，不代表完整 public 环境。

测试直接进入事件并选择真实 `EventOption`，检查事件结束、牌组、生命、遗物、费用/队友不变及重复选择不重复结算。选牌以既有 `ICardSelector` 回答，保持工厂、原生命令及 RNG。计时通过 `Hook.AfterRoomEntered` / beta `Hook.AfterSideTurnEnd`（public 为 `Hook.AfterTurnEnd`，内部同样派发 `AfterSideTurnEnd`） 驱动，覆盖五个回合、敌方/队友回合、两场战斗及人工制品。第二场显式建立新 `CombatState` 并清理上一场能力。

宿主仍替代 `CombatManager.IsInProgress`、`IsEnding` 和空手通知，不启动完整房间、行动/网络循环。没有新增全局补丁或修改旧测试准备数据。回合钩子通过不等于真实调度或联机通过；默认固有的模型关键词与普通实例已测，真实开局入手还需用户实测。PCK 测试加载事件/卡牌图片、五张遗物/能力图标，切换中英文并格式化文案；不代表真实 UI 布局或视觉验收。

TDD：J.A.X. 初测因未注册事件失败后通过；变化、遗物交换、开战能力分别先观察缺少选项/力量的失败再实现。public 编译发现自损 API 与回合钩子入口名差异后局部适配，生产项目增加现有测试项目同名的 `STS2_PUBLIC` 条件编译符号。完整命令与最终结果在下节填写。

## 命令与结果

- `./tools/Test.ps1 -Filter 'Augmenter'`：beta 本票 7 项通过。
- `./tools/Test.ps1 -Branch public -Filter 'Augmenter'`：public 本票 7 项通过。
- `./tools/Build.ps1 -Branch public-beta` 与 `./tools/Build.ps1 -Branch public`：两目标开发包构建成功，均 0 警告、0 错误，包含 DLL、manifest、PCK、LICENSE 与归属声明。日志：`.local/augmenter-build-beta.log`、`.local/augmenter-build-public.log`。
- `./tools/Check.ps1`：退出 0，PowerShell 语法、Git 空白检查、两目标完整回归各 34 项（15 你好世界 + 5 牧师 + 7 老乞丐/泉水 + 7 本票）、两目标开发包全部通过；两目标测试和构建均 0 警告、0 错误。日志：`.local/augmenter-final-check.log`，同次 transcript 为 `.local/checks/last-run.log`。
- `git diff --cached --check`：通过。
- 审查起点：`59e69c819192bb7068fc86944937fc0954de52fd`；本轮只审查/提交 #5 文件。开始时已有文档、路径、工具等未提交改动，保留原状，不纳入本票提交。

## 代码审查

按 implement 要求使用 code-review，提交前分别运行两个只读子代理。因用户要求先审查后提交，实际审查命令为 `git diff --cached 59e69c819192bb7068fc86944937fc0954de52fd --`；起点即开始时 HEAD，尚无本票提交，暂存区包含且仅包含本票改动。规范来源为项目指令、`docs/agents/review.md`、机制/开发说明及 code-review 的代码异味启发。

- Standards：0 项发现；未发现文档规范违例或需要重构的异味。既有回归、宿主顺序、局部双分支适配、归属与验证边界均保留。
- Spec：0 项代码问题；A01、A08–A10 实现与规格一致，无范围外功能。首次审查时用户 beta 实机验收仍待反馈；后续反馈和修正见末节。未测试两人同时持有不构成已证实缺陷：当前多人模型已覆盖队友回合不推进，代码不依赖固定 NetId；真实联机仍需独立验收。

两轴各 0 项代码发现；没有按一轴结果覆盖另一轴的结论。审查不替代下节用户实际验收。

## 用户手动验收与安装

Agent 不启动游戏或 Steam，不写入游戏 `mods`。构建产物位于仓库 `dist/public-beta/AllMatters/` 和 `dist/public/AllMatters/`。待两目标构建完成后，由用户备份原目录，再将匹配分支的完整 AllMatters 文件夹复制到游戏 `mods/AllMatters/`；依赖完整 `.local/dependencies/RitsuLib-0.6.7/` variant-pack 另装为 `mods/STS2-RitsuLib/`，保留 `compat/`、`shared/`、manifest 与许可证，不能只取一个 compat DLL。不混用两目标 AllMatters.dll。public 当前没有完整游戏环境，不将 DLL 参考目录当作安装目录。

beta 通过 Steam 启动，开启控制台后用 `event ALL_MATTERS_EVENT_AUGMENTER` 定位事件（强制命令仅用于观察选项，不证明自然准入）：

1. 在第二幕满足全员至少两张可移除牌，确认自然事件可入池；第一/三幕或任一人不足两张时不入池。中文与英文分别检查标题、三选项、图片与提示。
2. 选择 J.A.X.，永久牌组只加一张且事件结束。未升级 J.A.X. 开战实际在起手牌中；0 费，自损 3、获得 2 力量；升级后仍固有，自损 3、获得 3 力量。
3. 重新进入，选择当实验对象：必须选两张合法牌，不可取消；永久牌组两张替换结果已升级，其他牌不变，事件结束。
4. 五名原版角色分别测试喝突变剂，尤其把其他遗物放在前面：只失去真正初始遗物，获得突变之力；已丢失/替换初始遗物时锁定，不能免费领取。
5. 突变之力每场开战加 3 力量；前两个自己的回合结束不扣，第三个一次扣 3，第四/五回合不再扣；进入第二场重新计时。取得人工制品后核对 Debuff 拦截与力量保留。
6. 双端分别选择不同分支，核对只影响选择者；真实战斗中检查队友回合不会推进计时。独立检查可保存节点读档、真实回放与中英文 UI，记录具体版本及操作。

用户 beta 实机反馈见下节。真实联机、保存恢复、回放和完整 public 加载/画面/玩法尚未验证；默认固有实际起手、计时战斗的逐回合细节、五角色逐一交换与英文 UI 尚无逐项用户记录。用户总体反馈不扩大为所有这些场景均通过。修正后的变化分支仍待用户实际复验，不关闭 issue、不宣称整票验收或正式发布完成。

## beta 用户反馈及变化规则修正（2026-10-21）

用户报告已测试三个选项，仅第二项需要调整。两张截图显示 beta 0.111.0 中文增益研究者的图片、三选项，以及故障机器人牌组中未升级的 0 费 J.A.X.（固有，自损 3、加 2 力量）。这是用户实际加载与整体选项反馈；未将截图或整体反馈推断成真实联机、回放、所有角色或每个战斗回合均已验证。

用户明确新增/收敛规则：第二项显示“变化2张牌，然后升级得到的新牌”，变化结果只能来自当前角色，不能得到无色牌。本轮按该最新指示覆盖此前对原生默认变化池的继承，不擅自修改父规格已确认的其他规则。

通过 `./tools/Test.ps1 -Filter 'Augmenter transforms colorless'` 在真实事件入口复现失败：来源为无色牌时结果仍为无色。两版 `CardFactory.GetDefaultTransformationOptions` 按来源牌池选择；事件牌默认转入无色池，故 J.A.X. 也存在同一风险。修正为传入本角色已解锁、符合人数限制且为普通/罕见/稀有的候选，使用原生 `CardTransformation(original, candidates)` 与 `CardCmd.Transform` 抽取并处理实例、排除原牌 ID、牌组记录和升级。选牌预览也传入同一候选。没有全局修改其他事件或游戏变化规则。

新增一组回归逐一覆盖五角色、两张无色来源以及 J.A.X.+可移除诅咒来源，核对得到两张本角色合法奖励牌且已升级、旧对象未升级。中文描述按要求调整，英文同步保持相同含义。修正版 `./tools/Test.ps1 -Branch All -Filter 'Augmenter transforms colorless'` 两目标各通过新增的一组回归。随后 `./tools/Check.ps1` 退出 0：两目标各 35 项完整回归（15 + 5 + 7 + 8）、两目标开发包及脚本语法/空白检查全部通过，编译均 0 警告、0 错误。最终日志 `.local/augmenter-class-final-check.log`，定向日志 `.local/augmenter-class-final-targeted.log`。两轴补审分别报告 Standards 0 项、Spec 0 项，确认与最新用户规则一致。

修正版包已重新生成在上述两目标目录；用户需手动更新并复验第二项精确文案与无色/J.A.X. 来源的变化结果，Agent 未写游戏 mods。原包三个选项的用户反馈保留，不据此宣称修正版变化分支已经实机通过。
