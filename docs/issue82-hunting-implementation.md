# #82 追猎非战斗实现与复测

本记录对应 `feat/issue82-hunting`，由主控集成前补提交号和包。权威输入是 GitHub #73、#82、H（#63 comment 6584771226）和 U（#71 comment 6637806785）；不覆盖后续战斗票或 #88 的最终控制台验收。

## 实现与验收映射

| #82 条目 | 实现入口 | 已有证据 |
| --- | --- | --- |
| 15/14/13、最低 0、探索计数独立 | JigsawRun / JigsawHunting | 领域剩余额度、连续进房、双Boss按时测试 |
| 最后安全根房、全队实际离开后激活 | JigsawExploration / JigsawEditorAction / JigsawHuntingNative | 两版真实 ENet `shop-hunting absent`：3 原角色、2 实际进程；一人滞后仍可购物，完成后发面具、重访受罚 |
| 原队员唯一面具，不交换/入池 | ArchitectMask / JigsawMaskNotTradable | 两版原生获取两次唯一；ENet 缺席第 3 角色仍各一份 |
| 独立 RootHunted / Active 保存 | JigsawSave.Hunting / Checkpoint v2 | 安全根房完成前后 JSON 恢复；真实奖励点保存恢复；恢复呈现专项 |
| 末地图 Boss 奖励前清除 | Hook.AfterCombatVictory adapter | 两版真实 CombatRoom+await 原生 hook 后 ToSave(room)→内存文件→RunState 恢复，面具及 RootHunted 不返还；不同 room 同名 Boss 通知不清 |
| 商店免费购买/删牌拒绝，库存保留 | JigsawHuntingServices | 两版原生所有货架 wrapper / 免费删牌 / direct removal sender；ENet 重访保库存、价格、Used、RNG |
| 原版隐藏商品/删牌禁用外观 | NMerchantInventory / NMerchantSlot patches | 真正原生商店场景的货架隐藏、Used 动画、Cost 隐藏、默认 Back 焦点；Godot cancel→原生 Close（宿主边界见下） |
| 休息只许原本可用 Heal/Mend，遗物新增项不执行 | RestSiteSynchronizer / NRestSiteButton | 两版原生禁 Smith/新增项不执行不写历史、真实 Heal；额外操作仍可再次 Heal；真实按钮 shader 灰暗、Godot 点击禁项无历史；单人不造 Mend |
| N/1/0 短句与解除提示 | JigsawStepCounter / JigsawScreen | 真正 topbar hover 捕获 N/1/0 LocString；无额外按时/面具发放文字 |
| am_steps | JigsawStepsCommand / 已有 managed action | 两版原生有序执行0/1/40/intMax、非法参数原子拒绝；ENet Host40/1/0/99同步、Client拒绝；不改Steps/FirstProgress或解除追猎 |
| 完成后地图选择点设 0，下一房立即受罚 | BeginEnter / EnterPreparedCore | 无当前根与完成根待投票两种领域表示，含首次 Boss，均经 JSON 恢复；真实 native 选择完整点保存恢复→首次 Boss 入口→再次保存恢复；命令当刻无面具。ENet `shop-hunting-completed absent` 覆盖全员完成、票未齐时设 0，下一入口同步唯一面具 |
| 原生治疗加成、奖励与无合法选项出口 | 原生 Heal / NRestSiteRoom / RestSiteRoom.Exit | 真实 RegalPillow 加成与 DreamCatcher 卡牌奖励经实际原生选择领取；真实休息场景的 Godot 点击 Proceed→生产地图完成根→实际下一入口。beta 的 BeforeLocalRestSiteExited 清空剩余禁项并完成原生等待；public 没有该等待 API，仅验证其真实退出/下一入口 |

## 保存兼容

新完整点使用已有 Checkpoint.Version=2，完整保存 RemainingSteps、Active、RootHunted、Pending、BossChecked、BossOnTime。v2 缺字段和未知版本明确拒绝，主会话和辅助恢复读取规则一致。不会从个人面具或余额 0 推断根房状态。

v1 没有 Hunting 的旧完整点仅在 `0 <= Steps < Budget` 时迁移：余额为旧预算减进入次数、尚未追猎、当前根房安全；若地图第一 Boss 已访问，则在旧单调 Steps 下可确定按时。正常编辑/生成/Ancient 的 Steps=0 保留原预算。迁移不发奖、不换池、不改变 RNG/原队员；下个正常完整保存点写 v2。

v1 已到/超过预算无法确定安全根房、完成边界或 Boss 首次时序，明确拒绝并提示恢复耗尽前完整备份。该语义拒绝不承诺原生自动尝试 backup。原生确因文件解析失败选择 backup 时，整份角色、地图、历史从该同一文件恢复；不从 backup 单独补 Hunting，不写坏主文件或备份。两版 `legacy complete points` 专项实际覆盖主文件、原生选中的备份、边界拒绝以及 v2 缺 Hunting。

## 自动验证与限制

工作树 `.local/issue82/` 保留本轮命令输出及退出状态：`hunting-final4.log` 两版各 13 项通过，包含上述领域、服务、Boss/选择点保存恢复和生产 checkpoint reader；`review-enet.log` 两版各 2 项通过，分别保留滞后玩家最后安全商店场景与全完成后设 0 场景；`review-hover.log` 两版各 1 项通过，实际探索界面及 topbar 显示 N/1/0 短句。最终整组、构建和检查结果待下方补录。旧 `jigsaw-final.log` 为复审修正前 beta 194 项通过、public 因测试 helper 重命名漏改引用而编译失败的历史记录，不能代签最终双版结果。

最终 `tools/Check.ps1 -Branch All` 退出 0（`check-final.log`）：PowerShell、workflow、whitespace、占位扫描、资源导出全部通过；public-beta 全量 418 项通过，public 全量 421 项通过；两版 Build 均成功。该轮运行期间完成最后 U 地图文案修正，因此 beta 418 编译早于该纯显示修正，public 421 包含修正及新增地图文案生命周期用例。随后 `notice-beta-final.log` 在最终代码上补跑同一真实 Label 生命周期专项 1 项通过（退出 0），验证耗尽安全根显示惩罚句、全员完成发面具后空、按时 Boss 空、末 Boss 解除句显示 5 秒后空。最终双版 Build 均在 U 修正后执行。两版用例总数还含既有分支差异，不能直接相减当新增数。

使用 public v0.107.1 和 public-beta v0.111.0 的真实 DLL；编译依赖 RitsuLib 0.6.7。用户实际环境 RitsuLib Workshop 0.6.10 需集中安装验收，不能用编译依赖版本代签。真实 ENet 是本机独立 Godot 进程，区别于真实玩家机器；未启动游戏/Steam、未安装包、未修改真实存档。

Godot UI 宿主注册原生 ScriptPath 一次，挂载真正 NRestSiteButton / MerchantInventory / NRestSiteRoom 场景，补场景所有者、窗口、隐藏 console、HotkeyManager、ScreenShake 与 map 的 runState。休息测试跳过已看过的原生教学提示，预先移除 Heal/Mend 构造无合法选项的边界，不代替正式环境中遗物禁用治疗的组合；不调用业务点击回调、不手动完成原生 completion。真实 Godot 输入走 Proceed 原生回调；退出时清理 UI 后归还场景所有权，并清理测试启动的原生战斗。所有场景、Harmony 宿主补丁和窗口尺寸均清理。测试工程现有 GameResourcePack 指向本机 beta PCK，public DLL 下的场景不等于 public 原版资源实机；日志中的 public NHotkeyIcon 不匹配属于这项明确限制，不能签署 public 正式资源 UI。损坏旧存档专项中原生解析报错/内存文件改名是测试预期，均发生于 MockGodotFileIo。实体手柄、四人紧凑/宽屏、完整 Boss 实际击杀→奖励→新幕、普通事件购物实机和原生遗物禁 Heal 的正式组合仍按集中验收/受影响票补足；普通事件治疗已有真实 Cleric EventRoom/原生选项扣费及治疗验证。

控制台 `am_steps N` 范围 0..2147483647，仅 Host 在稳定非战斗探索阶段使用，仍保留原生 DebugOnly/Mod/成就检查。当前房间设0不立即发面具；调大不解除追猎。命令不在战中另写快照，沿下一正常完整点保存。

用户本轮确认：已完成房间、等待下一目的地时设 0，命令本身不发面具；下一个房间立即受罚，包括首次 Boss。无当前根房和完成根待投票都遵循此规则。成功 BeginEnter 在进入边界激活追猎；native 入口前同步面具，并把 pending hunting 与面具放在同一完整进入检查点中，供失败重试/恢复使用。首次 Boss 的按时状态为 false。原本剩 1 进入的安全根房及其内部子战斗仍等所有在场玩家真实完成后激活。

## 第 4 批集中复测步骤

1. 两版分别安装主控提供的统一批次包，记录游戏/Mod/Ritsu版本、角色数、窗口、设备、存档来源。
2. 进入稳定探索，依次执行 `am_steps 0`、`1`、`40`；验证所有端 HUD 一致，下一房只扣一次，Steps/首次进度不伪造。缺参、多参、负数、小数、文本、2147483648拒绝。Client发起拒绝。
3. 两端剩1进入商店，一人购物/删牌并留店，一人实际选目的地：留店者可继续服务且全队无面具。第二人离店后原队员各1面具；重访商店商品隐藏、删牌打叉；包括免费服务均不可得。恢复仍保真实原货架。
4. 追猎休息检查 Heal/Mend 原本禁用条件、遗物新增项灰暗不能执行、治疗加成/奖励及额外操作仍生效；单人不增加 Mend。键鼠返回与手柄取消都应可继续。
5. 耗尽事件根房及内部战斗安全至全队完成；普通事件购物/治疗放行。追猎中具名事件Boss不能清面具；双Boss首场不解除；末地图Boss胜利奖励前解除、读奖励点不返还；新幕清旧状态。
6. 在根房未全员完成、全员完成后、末Boss奖励点分别保存恢复，核对额度/根房快照/追猎/面具同点一致。使用副本检查旧v1未耗尽迁移和边界拒绝，不替换真实用户存档。
