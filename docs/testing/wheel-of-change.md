# 变化大转盘验证记录（#7）

日期：2026-10-21。读取 GitHub #7、父规格 #2、直接阻塞 #3 正文及全部评论（均 0 条）；#3 为 closed/completed。沿用 Q1–Q20、A15/A16/A17/A29 和已确认的“进入事件 → 选项 → 可观察资源与结束状态”测试入口。实现与审查起点为 `21b3d756aeffffba1a4f67c65f40d635d31c1588`，当前分支 `main`。本票不包含开始前已有的工作流、工程配置、规格或需求文档改动。

## 实现与源码依据

- 无额外准入条件，个人事件。沿用 Balance 每幕推进事件 RNG 的方式，并按当前总楼层推进以区分同幕不同次进入，再用原生 `NextInt(6)` 等概率确定六种结果。同一节点读档仍使用同一抽取位置。转盘动画不消费 RNG，直接使用该结果；随后显示结果页，由玩家确认结算。选项默认的一次执行语义阻止重复旋转与重复领取。
- 金币按幕 100/200/300；原生命令全额治疗、加入一张原生 BadLuck（霉运，保留永久）、选择一张可移除牌且不可取消，或 Unblockable/Unpowered 固定损失 7 生命。伤害选项使用原生致死提示，保留防死亡/复活命令链。
- 遗物候选为玩家本职业及 SharedRelicPool 的已解锁一般掉落遗物，同时检查个人抓取袋剩余候选、`IsAllowed`、持有排除与 Common/Uncommon 稀有度。先 `NextInt(2)` 等概率选档，再在该档逐件等概率抽取。保留原生主袋优先：该档无主袋候选时，在同档合法候选的随机排列中，通过精确 ID/稀有度过滤的原生 `PullFromFront` 寻找并消耗多人宝箱回退袋候选；该档完全为空后才用另一档。双档空说明后结束。通过原生 `RelicCmd.Obtain` 取得，保留拾取效果及个人/共享袋移除。事件池遗物不进入此候选，后续图书馆瓶子按规格只注册事件池即可保持隔离。
- 两版 `RelicGrabBag.GetAvailableDeque` 都可能 Common → Uncommon → Rare 升档，`RelicFactory` 还可能回退 Circlet，因此不使用不加限制的默认抽取；精确过滤的原生入口不会返回不同 ID 或稀有度。源码导出在忽略目录 `.local/api/queries/{public-v0.107.1,public-beta-v0.111.0}/`；生产代码未复制游戏源码。
- `WheelSpinPresentation` 用内置 Control/TextureRect/Label/Tween 展示 Balance 图标转盘及指针，显示 3 秒减速及 0.8 秒停留；只向本地选择者显示，其他玩家执行相同确定性模型逻辑。贴图从三点钟方向起顺时针为金币、宝箱、心、诅咒、划掉卡牌、血滴；右侧指针与最终旋转角度使用相同索引，停稳时下方显示本地化结果名。场景退出会释放等待与画面。无需自定义 Godot 脚本注册或额外小游戏场景资源；图片和语言打入现有 PCK。
- 复制前核对 Balance 本地 LICENSE（GPLv3）、对应事件代码、图片及 `image_gen/source/events/WheelOfChange.png` 主图。后续视觉修订复制其已分发的 `images/event_extras/{wheel,wheelArrow}.png`，并保留其 ActsFromThePast 来源署名。来源与许可声明边界见 `THIRD_PARTY_NOTICES.md`。未复制上游小游戏代码、按钮或音频，未执行上游构建脚本。

## 自动化与双目标

环境：Godot mono 4.5.1、.NET SDK 9.0.319、RitsuLib 0.6.7。beta v0.111.0，程序集 `0.1.0+41cef1ea4657c524aa50e870df009e56337e8c32`；public v0.107.1，程序集 `0.1.0+59260271157f76a2896f0eab5bc6ea1245d8b314`。

执行命令与结果：

- `./tools/Test.ps1 -Filter 'Wheel of Change'`：初始金币测试先因尚未注册事件而失败，接通后通过。后续扩展六类结果、两档不等数量候选、双向回退、双空池/无目标、快照恢复、中英文及真实 Godot 动画。
- `./tools/Test.ps1 -Filter 'Wheel of Change bilingual'`：1 项通过，六个落点及退出清理均走真实 Tween。宿主 `_Ready` 时直接添加根节点会触发 Godot busy 错误，已改为等待下一处理帧；没有修改生产生命周期来迁就测试。
- 首次全量检查 beta 53 项通过；public 测试编译暴露 RNG 克隆 API 差异（public Seed/Counter，beta ToSerializable）。按实际 API 仅修正测试准备；随后两目标均 53 项通过并产出开发包。
- 双轴审查发现多人回退袋遗漏后，新增 `./tools/Test.ps1 -Filter 'Wheel of Change relics retain'` 先失败（日志 `.local/wheel-fallback-red.log`），修复后 `./tools/Test.ps1 -Branch All -Filter 'Wheel of Change relics retain'` 两目标各 1 项通过（`.local/wheel-fallback-green.log`）。覆盖两档回退、另一档主袋有候选、回退条目消费及 Rare 回退排除。
- 修复后的最终 `./tools/Check.ps1` 退出 0：beta/public 各 54 项通过（含原有你好世界及其他事件回归、本票 7 项），两目标生产与测试编译均 0 警告/0 错误，资源导出与双开发包生成成功。日志 `.local/wheel-final-check.log` 及 `.local/checks/last-run.log`。没有在检查通过后继续改动行为代码。

测试准备按实际目标模型的稀有度筛选，未假定一代稀有度：二代苦无和手里剑为 Rare，红面具为 Common，不能当作 Uncommon 或排除候选。未修改原生红面具稀有度或掉落池。

### 保存语义与宿主边界

两版原生普通事件保存房间入口状态。`RunManager` 进入地图点前保存资源与 RNG；`EventRoom.ToSerializable` 保存事件 ID/IsPreFinished，不保存转盘任意动画帧或结果页，先古专属完成保存不适用于本事件。因此中断后读取原生入口快照，玩家资源与事件一起回到入口，事件原生 RNG 重现相同结果，再领取只产生一份效果；没有新增中间页存档或另一套状态保存。

测试使用真实 `EventRoom` 房间格式及 `Player.ToSerializable/FromSerializable`，覆盖六种结果的入口快照重进、相同结果与最终资源一致，以及结算后物品快照恢复不重复拾取。没有运行 Save and Quit UI、磁盘写入或下一地图节点实际调度。不能把单独重建事件而保留结算后玩家资源当成受支持的原生读档节点。

沿用既有 Godot headless 宿主和事件作用域：CombatManager.IsInProgress 临时 false、IsEnding=false、空手通知被宿主替代；模型、选项执行、RNG、牌组移除、伤害、治疗、遗物获取、保存格式及资源加载保持真实。动画测试在独立测试树挂载 Control，验证实际 Tween 的六扇区最终角度与随后选项一致，并覆盖场景退出；这证明画面节点/动画执行，未验证正式游戏布局、输入遮挡或肉眼观感。未新增全局宿主替代。两人模型验证个人资源隔离，不等于真实联机。public 行为测试使用 beta PCK 支撑原生资源，不等于完整 public 环境验证。

测试日志仍包含既有宿主的程序集未关联正式 Mod 注册信息（`not associated with any mod`）及 Sentry GDExtension/资源缓存提示：宿主手动注入模型，未走正式游戏 Mod 加载。这些日志未被消音，退出状态与断言结果另记；本记录不宣称测试日志无错误，也不以此证明正式注册或多人类型排序已通过。

## 双轴代码审查

按 implement → code-review 技能，以 `21b3d75` 为固定起点，对本票 staged diff 分别启动 Standards 与 Spec 两个独立审查；实现前已有改动未暂存、未纳入本票。Standards 初审与复审均 0 项可操作问题。Spec 初审 1 项 P2：`RelicGrabBag.Contains` 不查询多人宝箱回退袋，导致仍有合法遗物却提示空池或错用另一档。经上述公开行为回归复现后修复，复审确认随机候选、精确过滤、消费及其他档回退正确，剩余代码问题 0 项。仍保留 1 项验收缺口：beta 正式游戏实际可玩验证。

## 用户安装与实机验收

按项目 AGENTS.md，Agent 不启动 Steam/游戏、不复制到游戏 mods、不修改游戏安装或存档。beta 实机实际可玩验收尚待用户完成；本票保持未完全验收状态，不据自动化关闭 GitHub #7。完整 public 加载、真实主客机联机和回放均未验证。

开发包（在仓库根目录下）：

- `dist/public-beta/AllMatters/`：beta v0.111.0 对应 DLL、manifest、PCK、LICENSE、来源声明。
- `dist/public/AllMatters/`：public v0.107.1 对应开发包；只证明对应程序集构建及本记录注明的自动化范围。
- 独立依赖：`.local/dependencies/RitsuLib-0.6.7/`，来自 [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)。需完整 variant-pack（shared、compat、manifest 等），不可只装单个 compat DLL。

用户退出游戏后备份现有安装，将匹配分支的 AllMatters 目录完整更新到 `mods/AllMatters/`，另将完整 RitsuLib 包装入 `mods/STS2-RitsuLib/`，再由 Steam 启动匹配游戏版本。保留既有其他 Mod，遇到兼容问题记录清单和日志。

验收步骤：

1. 按现有开发控制台事件入口 `event ALL_MATTERS_EVENT_WHEEL_OF_CHANGE` 进入（自然未知房间入池也需后续集成检查）。切换中文/英文，检查插画、事件名、旋转选项。
2. 旋转后检查六扇区可见、指针落点与后续结果一致，确认后只结算一次。三个幕金币应为 100/200/300；满血回复、霉运、指定删牌及固定损失 7 生命分别核对。
3. 删牌无目标时仍先显示移除结果，确认后解释并结束。普通/罕见遗物单空/双空需要合法开发准备，不能靠未出现某类结果判断池规则通过。
4. 在原生支持的事件入口/结果页/领取后退出并读档，核对玩家资源回到同一入口快照、再次旋转同一结果、最终只结算一份。选牌中间帧是否可保存以原生 UI 为准。
5. 反馈实际执行版本、场景与结果；真实联机需要双端分别核对个人选择、资源和退出，回放需实际重播并比对结果，均不可由单进程测试推定通过。

当前未验证：此次修订后的 beta 画面与完整六结果实机结算、真实选牌/保存退出流程、自然入池抽取、完整 public 游戏、真实联机与回放。用户已提供首版 beta 的中文事件初页、转盘及遗物结果页截图，证实这些页面曾实际加载；这些截图没有证明其他五类结果、领取、副作用、联机或回放通过。

## 2026-10-21 用户反馈修订

用户截图确认 beta 首版中文初页/转盘/遗物结果页可显示，但画面粗糙，并报告同局反复十次都抽到遗物。按本次要求改成两行地精对白（“你准备好了吗？”使用游戏 `[b]` 粗体），旋转选项改为“玩小游戏 / 随机获得奖品”，删除遗物描述的概率括注；英文同步简化。

随机复现命令：`./tools/Test.ps1 -Filter 'Wheel of Change repeated visits'`。使用截图的 `R8RJZHQFU7P2` 种子、同一玩家与同一幕，公开追加十次地图历史并各创建新事件实例，首版确实得到十次 RELIC（`.local/wheel-repeat-red.log`）。两版 `EventModel.BeginEvent` 都只按局种子、玩家位置、事件 ID 初始化事件 RNG；旧实现每次按幕推进相同次数，故同幕每次读到同一结果，并非十次独立随机都恰好抽中遗物。

修复增加总楼层推进，不使用全局随机、时间、额外 hash 或新保存体系。不同地图节点可出现不同结果；同一节点重建/读档保持结果不变。两目标复现回归均通过（`.local/wheel-repeat-green.log`），逐次同节点重建也验证不变。不同节点仍允许正常连续抽到相同结果，不强制去重或保底。

新增 600 个不同局种子的真实事件选项检查，保留宽范围频次检查以捕获固定/明显偏重分支，精确概率依据仍为原生 `NextInt(6)`：beta 金币 109、遗物 97、治疗 91、诅咒 99、删牌 102、受伤 102；不能将有限样本频率当作概率定义。

视觉修订改为有图标的贴图转盘和绘制指针，移除随轮旋转的文字；原生结果页继续解释效果。独立 Godot 测试树可通过 `ALLMATTERS_WHEEL_CAPTURE` 指定输出目录，在有图形渲染的 Godot 测试进程保存六个停点预览；这是测试场景预览，不是正式游戏实测。本轮使用正常 beta 测试构建，沿用 Paths.local.props 的 Godot 和 .NET 运行目录，以 `--path src/Tests --rendering-method gl_compatibility --resolution 1280x720 --position -10000,-10000 -- 'Wheel of Change bilingual'` 运行独立测试（未启动游戏/Steam）。输出 `.local/wheel-preview/wheel-{0..5}.png`，六张均已视觉检查，日志 `.local/wheel-capture.log`。预览曾发现 TextureRect 的原始最小尺寸使画面偏心/裁切，已将 ExpandMode 设置提前，并补充实际尺寸与旋转中心断言。

修订按 `code-review` 对起点 `6c60e493b7798bba9b0768dd27f0812a50ab9dfd` 的 staged diff 双轴审查：Standards 无规范违反，提出的 1 项 P3 截图等待后节点可能释放建议已修复并复审；Spec 无剩余代码问题。上次 `21b3d75 → 6c60e49` 的原始实现已有前文双轴审查证据，未把已有准备改动并入本次提交。

最终 `./tools/Check.ps1` 退出 0：beta/public 各 **56 项通过**，生产/测试编译零警告零错误，PCK 导出与双目标开发包成功；日志 `.local/wheel-polish-final-check.log` 和 `.local/checks/last-run.log`。public 的 600 种子频次为金币 94、遗物 113、治疗 90、诅咒 105、删牌 98、受伤 100。原有回归及本票六结果实际命令、动画、回退池、无目标、快照恢复均保留。构建产物仍在前述 dist 目录，未部署到游戏。

此轮没有新增 beta 实机玩法、完整 public、真实联机或回放证据；用户需要安装修订包后复验新文案、六类结果及指针对应，并检查真实保存退出。GitHub #7 不按自动化结果关闭。
