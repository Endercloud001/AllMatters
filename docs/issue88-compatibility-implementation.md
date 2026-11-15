# #88 兼容规则实施记录

日期：2026-11-12。工作分支 `feat/issue88-delivery`，起点为集成提交 `2f95862408c7141a0c86269b955626981af284f8`。本记录仅覆盖已实施的兼容切片，不代表 #88 或 #82–#88 整批完成。

## Ancient 奖励候选

沿用 #73 的范围：拼图模式获取 Ancient 奖励前排除羽翼之靴和黄金罗盘，其他模式及遗物本身行为保留。

`JigsawNeowRewards` 继续在绑定当前拼图 session 的玩家范围内限制 Neow 候选，将黄金罗盘加入现有排除类型。Tezcatara 不调用 `IsAllowedAtNeow`，因此单独在其实际 `OptionPool3` getter 返回的本次新列表中移除黄金罗盘；判断 Ancient owner 所属的原生跑局，不读取全局偏好来影响其他跑局。保留其他候选顺序、原生三个选项池及各池一次抽样，不先抽后重抽，也不拦截遗物获取或重造地图。

双版目标均已核实于 `.local/api/queries/{public-v0.107.1,public-beta-v0.111.0}/` 的 `RelicModel`、`Tezcatara`、`AncientEventModel`、`Neow` 方法体。

## 已执行的验证

|阶段|命令与结果|证据|
|---|---|---|
|真实失败|`Test.ps1 -Branch public -Filter 'Jigsaw compatibility Ancient'`；构建 0，宿主 1，实际 Ancient 候选仍含黄金罗盘，候选排除断言失败|`.local/issue88/compass-red-public.log` 与 `compass-red-public-host.log`|
|双版通过|`Test.ps1 -Branch All -Filter 'Jigsaw compatibility Ancient' -SkipResourceExport`；双版各 1 项通过，构建/宿主均退出 0|`.local/issue88/compass-green-{public-beta,public}.log`|
|日志诊断|两版 `ReadTestLog.ps1`：EngineErrors 0、ModMetadataErrors 0，无异常或准备失败|同上归档日志|

测试通过真实 `RunManager` 新局与 `EventModel.BeginEvent` 生成可见选项，24 个固定种子分别执行普通→拼图→普通：普通样本实际出现黄金罗盘；拼图候选仅移除罗盘并保留顺序，前两个池的选项不变，仍显示三个选项，RNG 状态与原生三次抽样一致；前后普通模式的候选、选项和 RNG 完全一致。Neow 资格同时检查现有靴子限制及罗盘限制。

## 独立审查

首轮审查范围为起点 `2f95862` 上的 Ancient 三文件切片。随后独立 Standards 与 Spec 审查均重新读取扩展后的七文件暂存 diff，包含 `JigsawCompatibility.cs`、`JigsawNativeSession.cs`、`JigsawFlightCompatibilityTests.cs` 和原生控制台测试；不是对整张 #88 的完成审查。

- **Standards**：独立审查实际 diff、项目规则、双版 patch 目标和测试模式，无规范违规或可操作的代码异味。
- **Spec**：独立对照 #73/#88、双版候选方法体和红绿日志，无此切片范围内的规格不符。确认其他模式矩阵和正式 public 运行未由本切片代签。

两轴均完成源代码只读检查，未代跑测试或修改实现。

## Flight 新局与保存大厅

Flight 新局切片已取得真实失败并修复：`Enable` 在创建 session 前检查实际 modifiers，明确拒绝 Flight 组合。测试覆盖偏好两值、Standard/Custom/Daily 和直接激活入口，拒绝不留下半份 session，原生 Flight 实例和自由旅行保持。`Test.ps1 -Branch All -Filter 'Jigsaw compatibility' -SkipResourceExport` 双版各 2 项通过，Build/Host 0、EngineErrors/ModMetadataErrors 0；归档 `compatibility-new-green-{public-beta,public}.log`。新增改动已纳入上述扩展七文件独立双轴审查，两轴均无可操作发现。

Flight 保存/大厅切片也已实施并双版各 3 项通过（兼容过滤词整体），日志 `compatibility-final-green-{public-beta,public}.log`，Build/Host 0、EngineErrors/ModMetadataErrors 0。真实普通 Flight 保存经原生大厅 canonicalization 仍成功；完整拼图扩展加 Flight 会明确拒绝，直接 `FromSerializable` 抛出 `InvalidDataException`，原文件、源保存 modifiers 和 session 均不改变。

首次仅增加 `Restore` 拒绝的尝试揭示原生大厅将全部异常视作损坏并重命名主文件和备份；此为隔离内存 IO 的实证失败，未涉及真实存档。最终在 `LoadAndCanonicalizeMultiplayerRunSave` 唯一原生加载调用处使用同返回类型包装：实际原生加载仍执行一次，只有成功读取且同时含拼图身份/扩展和 Flight 的文档返回明确 `ValidationFailed`，避免进入损坏重命名 catch。其他文档及真正不可读存档保留原生加载/恢复/校验。两版方法体已核实该调用及 catch 行为。没有静默降级、删 Flight 或改存档 schema。

扩展审查确认 Flight 新局、直接恢复和大厅拒绝均处于本切片范围；源码检查不替代损坏/备份恢复、真实每日抽样及正常多人激活的运行证据。

全量 `Check.ps1 -Branch All` 已退出 0：beta 434 项、public 436 项通过，两版 Build/Host 均退出 0，资源、脚本及两版开发构建均通过。记录为 `.local/issue88/check-final.log` 与 `.local/tests/{public-beta,public}-last.log`。没有准备失败或 ModMetadataErrors。全量日志并非零诊断：ReadTestLog 计 beta 24/public 30，包括故障注入/损坏内存存档恢复、宿主场景脚本缺失与退出资源泄漏；public 另有旧 session 替换测试的两条原生 action queue 报错。相应测试仍通过，但这些记录不能当作正式游戏端零错误证据。定向兼容测试的 EngineErrors 0 与上述全量统计分别保留。

#84–#87 完整交付后，还需 #88 的 V14/S6/H13/U8 全部证据归属、组合回归、同一最终提交的双版包及准确许可声明。本切片已提交为 `080104f29997eddc5df85ca7b3869cc15d560dfc`，并快进合入集成分支；上述全量检查在该切片提交前执行，提交钩子完成后代码保持相同。

测试目标为 public v0.107.1 与 public-beta v0.111.0 的真实 DLL；public 宿主共用 beta PCK，不证明匹配 public 正式资源或真实游戏运行。未启动游戏/Steam、安装产物或改真实存档。保留用户已确认的 #93/#94 人工结果，实体设备及剩余分版实机责任在最终交付中逐项列明。

## U3 正式长译文按钮与引擎输入（2026-11-15）

本节双版归档目标为public0.107.1/beta0.113.0；后续用户升级beta目标至0.113.1，旧结果不重标版本。

335653f经fcc8831合入，仅两测试文件119+，生产未改。新case通过独立临时eo Translation消费JigsawScreen正式三个中文key，先核Translate值再创建/Refresh实际按钮；1280×720核manual/automatic的WordSmart及help的Off实际字体/rect/字形envelope和四种内容style边距，不把focus叠加框当内容margin。真实viewport motion/press/release分别核automatic callback恰1、manual领域投票、Help Reading/真实遮罩；finally仅移除自建翻译并恢复locale/viewport/原资源。

最小focus边距误算、首次本地依赖与public DLL-only配置缺匹配PCK均属准备失败，原归档保留；公版仅本地配置对齐现有public107-content后补跑。headless dummy.GetImage曾产生EngineError，即使Host0也未签strict；最终headless分支明确不调用GetImage并记录无可读截图，几何/文本/真实输入G不代像素截图或设备验收。

实施树translated-buttons-20261115/.local/tests/archives/translated-presentation-final-20261115-063547005-a1da9f335ff34a1293d4217d474e2c33/public-beta.log及translated-presentation-public-final-20261115-063649658-61bdd6f4790c45e5a95057f0a84596d1/public.log分别各20 strict通过，Build/Host/Engine/Metadata0，不把前者All的public准备失败写成功。两独立轴0发现，正常hooks，提交与已审内容一致。

集成fcc8831双版Presentation各20 strict通过，原日志/summary在.local/tests/archives/translated-buttons-integrated-20261115-064134237-e9f7444ae49d4235ac332ac7de4acf38。该片只补720p替代长译文按钮路径；三比例/4K已有NavigationLayout证据，不再新增重复矩阵，完整U3/U5与#88仍按各自实际端/设备范围签收。
