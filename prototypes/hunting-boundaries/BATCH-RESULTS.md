# 真实API实验进展（历史批次；收口见CLOSEOUT）

2026-11-04 收口更新：人工清单已由用户明确确认通过，public界面按用户声明验收；新增双版1–4人原生开战/两次实际复生/第三阶段9回合串联实验。当前结论、13组逐项答案与适配限制以 [CLOSEOUT.md](CLOSEOUT.md) 为准。以下旧批次的“待验/未收到/未解决”描述保留为历史，不覆盖本次结论。

2026-11-04后续用户实机已回填部分观察：商店禁用、五虫及意图、锻造禁用、空箱、女王两火炬头符合预期；旧五虫血条重叠，新210px间距已获用户本次窗口实机通过。精确范围及截图见[用户验收记录](USER-ACCEPTANCE-2026-11-04.md)，以下“尚无用户结果”为历史批次状态。整票未解决。

## 用户复验与选择弹层补验（2026-11-04）

启动错误补证：旧女王初始化先Inject火炬头，随后原生ModelDb.Init重复构造同一ID，真实用户启动日志及隔离仅女王启动顺序均重现DuplicateModelException。修正为生产探针初始化不Inject，模型交原生扫描；测试宿主后加载DLL所需Inject移入测试调用方。startup-queen-before.log为失败，startup-six-fixed.log为双版六包同载初始化→原生ModelDb.Init/序列化缓存/InitIds通过。休息/商店/宝箱依赖清单更正为实际STS2-RitsuLib ID。未改安装或存档。

最新acceptance-overlay-check.log记录完整Check.ps1退出0，双版测试及构建/资源/脚本检查全PASS；已知public资源负例等诊断仍保留。


用户明确通过beta单角色窗口下五虫210px间距，出生空过与奇偶意图截图已保留，见USER-ACCEPTANCE-2026-11-04.md。不是全分辨率/物理点击/多人通过。

`./tools/Test.ps1 -Branch public-beta -Filter PrototypeHuntingChoiceScreen -SkipResourceExport`，choice-overlay-r2退出0，原生三组选择×中英×左右共12输入均实际执行NOverlayStack.Push→AfterOverlayOpened/Shown→SelectHolder→CardsSelected→Remove→Hidden/Closed→队列释放；screenCount=0，原生节点实际失效，展示牌不进牌堆/永久牌组。原生堆栈的成员及Tween保持运行；raw backstop与Ready的外部订阅、地图覆盖查询、ActiveScreenContext.Update隔离，不能宣称游戏焦点恢复。public继续既有资源负例，不外推beta通过。r1夹具在父节点仍Ready时AddChild失败，改为先等ProcessFrame；保留失败日志。

## 续验批次（2026-11-04）

从833d63f另建 `prototype/hunting-boundaries-continuation` 工作树，复制原树未提交商店实验为输入，原树保持原样。先核实地图/本票完整正文和全部评论、完整规则/入口研究修订/多人Godot补验及原生阻塞和指派。逐组新增具体输入、历史证据、自动缺口与用户边界表 `CONTINUATION-MATRIX.md`；原matrix/runtime记录仍保留。

- 商店独立DLL双版实际运行：四类商品/删牌、注入鼠标/键盘/手柄处理器及收费/免费程序调用拒绝，钱/牌组/库存/RNG/次数不变；普通Cleric事件付款35/治疗20仍执行。命令 `./prototypes/hunting-boundaries/shop/Build.ps1 -Branch All` 后 `./tools/Test.ps1 -Branch All -Filter 'standalone shop DLL'`，退出0，见 `evidence/continuation-shop-r1.log`。两版均有308个资源退出仍占用、34个CanvasItem及其他RID泄漏；不称干净生命周期或完整商店屏幕通过。
- 原生空箱动画子链：P/B各1–4人；真实Collection.InitializeRelics/SpawnEmptyChestVfx/AnimIn/Tween→CompleteWithNoRelics→OnRelicsAwarded→AnimateRelicAwards，两个原生完成源由一次延迟通知完成，没有提前手动Complete，实际遗物增量0。命令 `./tools/Test.ps1 -Branch All -Filter PrototypeHuntingTreasureAnimation -SkipResourceExport` 退出0，`evidence/treasure-tween-r2.log`。人工重建Ready字段/节点与事件订阅，空原生hands不入树，Cmd.Wait仍在TestMode；不当作完整选择屏或P原版资源证据。
- beta原版 `rooms/treasure_room.tscn` 已实际入树并运行真实OpenChest：1–4人输入，原生Spine开箱/空文案和烟/Collection Tween/两等待源完成，真实异步奖励钩子每人非奖励计数1，金币/遗物增量0，继续按钮启用，经过地图旅行调用边界。FTUE入口在调用前返回完成任务，实际进度读写分支不进入；原生热键、多人指针hands、外层游戏/输入/地图宿主和屏幕跟踪外部接缝有明确隔离。保留原版完整场景/开箱命令，但不称真实游戏/网络/物理输入或完整地图旅行通过。专项迭代和最终结论以 `evidence/treasure-screen-r6.log` / 退出文件及后续全组日志为准。
- 后续 `treasure-package-r2.log`（退出0，B5项/P4项）已实际加载独立宝箱DLL，B原场景OpenChest组合Face及Spoils输入：全员Face消耗，本地Spoils任务/移牌/移标记/完成历史均保留；远端任务在单客户端宿主保持pending，不伪造联网完成；全员异步非奖励计数1，钱/遗物增量0。P仍只运行真实方法级Tween。独立包及视觉/物理输入步骤见treasure/ACCEPTANCE.md。测试退出保留2个CanvasItem/ObjectDB泄漏。直接异步GainGold/Obtain返回值兼容反例仍未解决。初次空箱子链失败是夹具错误：未给MegaLabel提供显式字体、把已有初始遗物误当新增；修正为真实前后差量。原版场景r1编译命名空间失败，r2–r4暴露外层NGame.CurrentRunNode及SetUpTest重置本地身份的宿主缺口；原始失败日志保留，不归咎玩法。
- 原生布局新增 `layout-native-r5.log`，命令 `./tools/Test.ps1 -Branch All -Filter PrototypeHuntingLayout -SkipResourceExport`，退出0。P/B各1–4人×90遭遇变体×3生态=1080输入，原始物种视觉Ready/Bounds、原生PositionEnemies、专用场景CreateScene/PositionCreaturesWithSlots、原生UpdateBounds及RandomizeEnemyScalesAndHues均实际运行；全部敌人保留且拒绝fallback资源。每版288输入缺原生追加Marker、88输入通用算法局部越宽、44输入初始Bounds相交。专用场景的fallback只是比较测量，不能算其原生实际布局；临时测量Marker也不是正式布局方案。public共享beta资源仍有NAeonGlassVfx缺脚本及TestSubject缺父级粒子节点等Godot错误，即使测量断言通过也不批准资源完整。raw creature/state-display叶隔离不证明真实意图文字、血条、玩家区、鼠标命中或手柄导航。缩放后的交集/准确资源与专用位置逐行保留，不据汇总删怪。研究接缝见LAYOUT-NEXT-RESEARCH.md。
- 用户确认暂无public PCK备份并授权线上查找/拉取。已核GitHub资源树/版本记录，只取四个小文本，无大包/安装改动。API记录v0.107.1的同仓资源目录却有v0.98.3记录且宝箱场景结构也不匹配，不能充作匹配资源。来源、大小、版本冲突见 `PUBLIC-RESOURCE-SEARCH.md`。完整public界面继续未验证；P仍运行方法级真实动画子链，原版场景测试未列入P测试集合，不伪造成功。

最新续验：`full-check-continuation-r3.log` 退出0；Passed 271 tests against 0.1.0.0. / Passed 270 tests against 0.1.0.0.，PowerShell语法/工作流/空白/资源导出/双版测试及构建全PASS。`runtime-cases.json`已刷新到该命令，共6727条观察；13组仍partial。所有失败迭代及Godot错误保留，`continuation-raw-runs.zip`保存去除尾随空白之前的原文；新增可读日志仅规范行尾。`runs.json`共154条，旧131条未丢弃。专项命令及精确层级见BATCH-RESULTS.md和CONTINUATION-MATRIX.md；退出0不等于规则批准。

本轮没有实现正式追猎、选择集成架构、修改玩法或取得任何用户实机结果。仍须补专用场景完整布局/原生启动和阶段链、OverlayStack生命周期、宝箱直接命令调用方及相应实机入口。完整检查点/原生联机由多人验证票消费。各组保持partial，不发布resolution、不关闭本票或其他票，不更新地图Decisions so far。

2026-11-04；只在 `prototype/hunting-boundaries` 工作树运行，无正式追猎功能、安装改动、Steam/游戏启动或真实存档操作。玩法数值和顺序直接取[完整追猎resolution](https://github.com/Endercloud001/AllMatters/issues/63#issuecomment-6584771226)，不是从研究摘要恢复规格。

## 宿主边界

两目标为真实 public v0.107.1/59260271 和 public-beta v0.111.0/41cef1ea 的程序集，Godot 4.5.1 mono、实际 RitsuLib 0.6.7 与现有 HeadlessCombatHost。两个目标顺序运行。资源均挂载本机 beta PCK 及现有 Mod PCK，public 原版资源没有独立证明。

现有宿主替代战斗进行/结束标志、空手通知等UI生命周期；各实验还会显式驱动部分钩子或局部调整状态机。真实卡牌、能力、RNG、伤害、生成、选择、房间和同步器方法保持运行。以下只按实际链路给证据，不升级为完整战斗、真实网络或完整保存恢复。

## 已运行的机制

| 组 | 运行输入与结果 | 仍不能据此宣称 |
|---|---|---|
| 1、3 | 原生异蛙死亡钩子的循环仅局部改为5，补第五状态分支；真实死亡生成5普通敌人，实际出生空过后1/3/5攻击、2/4寄生。每名原生目标受到3次6伤害、各2张感染。 | 完整五虫场景显示与点击/意图遮挡；全场真实回合循环。 |
| 1、3、8 | 双墨宝保留原生Jab起手并实际攻击；族母满血减半保持睡眠，新增本Mod乐加维林保睡并附Minion；异鱼的蝌蚪/旋螺保原起手、附Minion；实际主敌死亡联动杀死指定随从。 | 最终左右位置、分辨率与真实战斗结束奖励。 |
| 2 | 原创真菌兽28HP/孢子2/咬6/生长3，真实命令30轮；39/40阈值、强制分支不消费RNG。新增真实Ending检测，复现最后死亡易伤0反例并以原生暂缓接缝修正为2，实际阻止死亡/Artifact抵挡及原生结束清理另验。神秘术士56HP三状态，每存活玩家独立六选一、随机自身/同伴治疗12及力量2、死亡玩家跳过。 | 统计频率结论；完整启动/回合调度和动画/战斗实机；发布视觉许可。 |
| 3、5 | 逐敌局部适配在真实原生命令运行：再生6（准确类型为RegenPower）、直接苏醒、咆哮保昏眩另加全员2弱2脆、神官原目标2层、胆小9、硬化20+覆甲6、同目标99弱、瀑布负力量/敏捷等可见负面清除并保留正面/隐藏状态及原爆发、人工制品固定1/3/5、领地1、连枷新增攻击牌原生缠结及回合末清理。 | 所有补丁点已经形成正式接入方案；每种能力动画/意图显示、免疫和反制组合已穷尽。 |
| 4、5 | 无厌局部首手适配实际每目标沙坑5、原生随机插入抽牌/弃牌各4狂乱逃离；实验体A0/A9原生多爪连续3/4/5次、每次9/10，实际伤害与意图一致。 | 无厌完整原动画/后继流程，以及所有宠物目标回退。 |
| 4 | 原生知识恶魔外层循环与选择API运行，局部替换三轮候选，劫难/柔嫩原创IChoosable适配施加原生能力；每存活玩家三次选择，展示对象不入牌堆，第三轮瓦解21；A0/A9伤害和固定治疗20。 | 新展示牌实机画面、素材接入或原生网络选择同步。 |
| 5 | 原生两次复生实际运行；在第二次复生SetMaxHp前受控挂起，证明Respawns已为2而复生尚未完成；阶段二能力保留至完成。怪物持有的细看原型经真实Draw链验证完整玩家方回合3/6/9额外抽牌受限、正常起手与直接入手放行、回合末移除。 | 原生完整回合调度已经自动接上新计时；完整检查点恢复。回合钩子由实验显式驱动。 |
| 6 | 6种女王当前意图×1–4人×双版：实际聚合体死亡一次强制重燃；原生ExecuteEnemyTurn快照不访问新怪，下回合直接攻击；激怒给存活火炬头固定2、不补死槽。 | 完整敌方阶段后继/转回玩家方通过：EndEnemyTurn在宿主隔离；火炬头真实视觉/左槽显示。 |
| 7 | 原生沙漏逐玩家实例，本地初值/显示/重置改12；真实AutoPlay每人12张，顺序伤口/灼伤/虚空/晕眩/凋萎，满手溢出及凋萎两次强化保留。 | 保存、回放及UI已支持。 |
| 8 | 普通追加与Minion实际Feed/HandOfGreed击杀对照：普通触发3最大HP/20金币，Minion不触发；两者真实死亡均触发GremlinHorn1能量/1抽牌。 | 所有第三方Fatal/击杀效果兼容，或最后主敌奖励完整流程。 |
| 9 | 实际库存14入口付费/免费/零价/删牌均拒绝；MembershipCard取得/移除保留Courier价格，库存/RNG/钱/回调/次数不改。真实第二店删牌后，原店原对象重计价75→100。发现直接OneOff删牌绕过包装器的实际失败，补发送/接收两端局部拦截后双版拒绝。原生商店四节点真实Ready/隐藏/Used动画，注入鼠标/键盘/手柄处理器、重复刷新不改模型Used和库存。普通Cleric仍付款治疗。 | 真实鼠标命中/物理焦点、完整UI重开和原生重访保存；内存保留库存不是保存恢复。 |
| 10 | 原生同步器拒绝Smith/Shovel、不记历史；Heal、RegalPillow、AfterRestSiteHeal、MiniatureTent额外次数保持；Mend经受控远端结果运行实际治疗。原生RestSiteRoom.Exit双版完成时序另验。原生NRestSiteButton保留256px图标、实际灰化Shader及禁用释放处理；无可升级牌时原生Smith本身仍禁用。 | 真正UI选人/物理输入、原生网络完成广播；宿主灰化参数不是实机外观。 |
| 11 | 实际SpoilsActMap/SpoilsMap取消普通和600金币，保留任务/移牌/移标记/Face消耗。同步/异步奖励完成后清空、保留非奖励计数；CompleteWithNoRelics通知。追加实际TreasureRoom.EnterInternal→DoNormalRewards→DoExtraRewardsIfNeeded→Offer→Exit链，金币及粒子数量返回0、每玩家异步非奖励计数再增1，空投票完成。 | 整个NTreasureRoom开箱动画与所有第三方直接奖励路径。 |
| 12 | 实际原生事件完成、商店购买/Exit和连续房间栈；局部聚合/安全快照。早期显式胜利钩子之外，已在原型遭遇+受控活跃/Boss/event输入中运行真实CheckWinCondition→EndCombat→胜利/CombatWon/奖励生成，验证最终面具奖励前移除、首Boss/事件Boss/普通战保留。 | 真实统一进入/全队离店、原版Boss全场/完整启动回合或完整恢复；内部事件战斗UI调度仍替代，结束过程IO在调用方隔离。 |
| 13 | 四态显示探针在真实Godot子视口1280×720/1920×1080/2560×1440/3440×1440检查矩形与既有焦点；独立双版显示Mod已构建，Godot下实际调用其初始化/Ritsu注册、安装NGlobalUi.Ready钩子。 | 示例输入不证明领域状态集成；完整NGlobalUi.Ready/游戏渲染/物理输入/遮挡仍待用户，见presentation/ACCEPTANCE.md。 |

## 独立游戏探针与最新反例

- 五虫游戏包与宿主共用同份局部补丁，实际加载各目标独立DLL后，1–4人原生死亡/第五场景Marker/1→5队列/第五首次实际行动均通过。非异蛙Infested输入保持原生4虫。用户入口见five-worm/ACCEPTANCE.md；未启动游戏、未收到实机结果。
- 女王包只包含自编代码，精确火炬头视觉从作者原PCK单独只读输入。实际加载双目标独立DLL后，各6意图×1–4人重复原生快照/一次重燃/激怒不补召；原生CreateScene加入两左Marker，实际模型加载144×240火炬头和中英名称。补验改用真实第三幕上下文，基础50经一次原生缩放，4人实际260HP；以前act0通用夹具结果不能冒充第三幕精确HP。用户入口见queen/ACCEPTANCE.md。
- **宝箱反例已实际重现，整组仍未解决**：异步AfterModifyingRewards直接GainGold11/ObtainAnchor，事后清空RewardsSet仍实际增加11钱、1遗物及1条获得记录。`TREASURE_DIRECT_NEGATIVE`记录的是反例重现，不是该规则通过。
- 额外底层阻断实验：直接钱/遗物/记录/原袋均可保持不变，但若Obtain仅返回无Owner模型，原钩子后续读取Owner会抛空引用，非奖励计数停在0；给返回对象提供Owner上下文而不加入真实遗物库存，可让仅依赖Owner的钩子继续并保持清空列表；若调用方断言实际入库，则仍抛异常、非奖励计数0。`TREASURE_RETURN`明确标记返回载体只为实验，未选为正式方案，也不泛称任意Mod直发奖已兼容。需继续明确发奖来源/调用方适配边界，不能用跳过全部钩子或撤销非奖励效果掩盖。

上述反例的命令退出0表示测量按预期复现了缺口；runtime-cases的verdict/requirementSatisfied分别保留，不能用测试总数批准整票。当前可消费入口/领域状态与多人票输入另见INTEGRATION-INPUTS.md。

## 休息点原对象及原生回合补验（后续批次）

`rest-native-r2`两目标各47项通过；`rest-entry-r3`两目标各4项原生UI通过。休息点改用原对象：不包装/替换RestSiteOption，在原生Create/Reload后设置既有禁用标记和灰显材质，ChooseOption入口按实际RestSiteRoom过滤。原图标、对象身份、原本禁用条件保留；离开休息房间原生Smith不误禁。基本治疗/迷你帐篷/皇家枕头/远端修补受控输入在实际房间上下文复验通过。新增rest独立DLL实际加载初始化并驱动原生按钮，实机步骤见rest/ACCEPTANCE.md；没有用户实机结果。

`native-turn-r1/r2`分别通过双目标1–4人×9玩家回合。实际调用原生StartTurn→SetupPlayerTurn及玩家结束phase1/phase2，正常起手5张，3/6/9限制额外抽牌，结束清手并移除细看。穿插8次原生敌方StartTurn→ExecuteEnemyTurn→EndEnemyTurn→SwitchSides，推进真实RoundNumber至9。public原生敌方结束立即开始下一玩家回合，beta需外层循环再调StartTurn，探针分别消费其原生时序。使用原型真菌兽和预设已启用阶段计数，每敌方前恢复玩家HP仅作存活输入；不是实验体完整阶段战斗、StartCombat、自动准备调度或联网证明。原生复生及异步完成边界仍由另一实验覆盖。

## 原生二选一完整节点树补验与public资源反例

新增`PrototypeHuntingChoiceScreenTests`实际实例化原生NChooseACardSelectionScreen，设置原生三组二选一输入，执行Ready、生成两个原生NGridCardHolder、默认焦点/循环邻居、禁用跳过、SelectHolder完成源；每组左右选择和中英文共12次beta运行通过，展示牌不进任何牌堆或永久牌组。组合运行`combined-r12`双目标各51项、4536行观察，退出0。

这仍不是完整OverlayStack推入/移除或物理输入验收：输入管理器采用未入树原生节点，隔离图标查询、热键注册/注销及隐藏ExhaustPile的定位；原生输入管理器Ready会初始化并保存按键映射，本实验未执行。没有把这些替代项当作原生行为通过。

**public资源反例**：本机仅有beta PCK。public程序集不存在beta场景引用的NHotkeyIcon；实际载入报缺少脚本，public跳过按钮Ready还缺原ControllerIcon。public仅执行一次有界负例：主体两选项与完成源可运行，但明确`requirementSatisfied=false`，保留全部Godot错误，不计完整public选择屏通过。两目标Ritsu原生程序集注册已成功；这里是版本中没有该类型，不能沿用早期独立.NET失败否定Ritsu。已询问用户是否有public原生PCK备份；Agent不切换/修改安装。

相较之前两个Material/Shader诊断，完整选择树还暴露更多资源/CanvasItem退出泄漏，原日志保留；不能宣称干净生命周期。早期r1根节点Ready期间添加失败，r2/r3暴露宿主缺热键/隐藏牌堆定位，r4/r5记录版本差异；后续通过观察断言不抹去这些失败。

## 原生结束检测和结算补验

早期最后死亡探针在既有宿主强制IsEnding=false下得到易伤2，仅证明该条件下的死亡命令和手动玩家清理。新增显式作用域恢复真实IsInProgress/IsEnding，输入受控原生活跃状态后，双版本都实际暴露反例：最后敌人死亡时IsEnding=true，原生PowerCmd.Apply拒绝，易伤为0。失败原日志 `native-ending-negative-public-beta.log`、`native-ending-negative-public.log`各退出1保留。

本票原创孢子原型据此使用原生ShouldStopCombatFromEnding接缝（与Infested原生接缝相同），让死亡效果还存在时暂缓结束；怪物及能力按原生移除后自然释放，不新增持久化阶段或架构。1–4人、双目标真实getter对照：关闭此接缝易伤全0，默认接缝施加各2，死亡后IsEnding=true恢复；真实阻止死亡与第一名玩家人工制品抵挡另验。测试开关是实验输入，不要求正式保存一个重复flag。

再通过真实CreatureCmd.Kill→CombatManager.CheckWinCondition→EndCombatInternal→AfterCombatEnd→原生Player.AfterCombatEnd→AfterCombatVictory→CombatWon/Ended及原生奖励生成验证：AfterCombatEnd观察到易伤2，原生清理后胜利/奖励观察到0。五种上下文×1–4人×双目标（普通、单最终Boss、双Boss首/终、带ParentEventId且故意处在最终坐标的事件Boss边界输入）使用简单原型遭遇，验证最终面具在原生胜利回调后、事件/奖励生成前已移除，首Boss/事件Boss/普通战保留。不是原版Boss全场/完整启动或回合调度证明。

**数据边界**：上述EndCombat原生调用方的六处保存、回放、进度与成就调用在执行前改为计数器，连实际API及Ritsu保存postfix都不进入；没有真实存档/进度/Steam成就操作。普通测试若等误写后再失败已越过用户边界，因此只为本实验数据安全预先核对隔离调用形状/数量，不作为项目发布gate或新baseline。保存恢复仍归多人验证票。

## 影响集成的实际发现

- HP工厂已经缩放一次，二次缩放会重复放大。Artifact、Skittish之外，HardenedShell等原生指定层数也可能自动多人缩放；2人原生20硬化得到44，固定数值适配须局部处理，不能全局关闭原生缩放。墨宝Slippery属于未修改原生能力，实际随人数1/2/3/4，不能把“原生默认”误写成固定1。
- 左侧火炬头在原生遍历中先于女王行动。重燃下一阶段先各攻击10，随后女王激怒再给2力量；不能为了先攻击12擅改行动顺序。
- public喷火器仅状态、beta有直接伤害；public沙漏Ebb26、beta22（A0）。未修改值必须按目标版本保留。
- beta原生休息Exit会等待所有参与者完成；public实际立即返回，选项仍可未完成。public必须另有完成聚合，不能用Exit返回冒充全队结束。受控参与者退出不证明真实消息传递。
- 空遗物列表并不等于已通知所有等待者。原生CompleteWithNoRelics可完成空投票通知；金币并非都通过RewardsSet。ShouldGenerateTreasure提前return会遗漏Spoils任务链，应分开保留非奖励变化。
- 连续事件战斗恢复应经过EventSynchronizer.ResumeEvents。仅调用每个事件模型.Resume不清空上一战准备状态，第二场会报“already set to ready”。
- 早期CreateVisuals脚本路径绑定失败是宿主配置缺口。显式Ritsu注册真实游戏程序集后，原生CreateVisuals直接通过；不是资源/框架不支持。第五Marker可在实际原版异蛙场景实例增加。专用场景的追加位若未写入Encounter.Slots，原生排序会落在-1位置而先行动；补Marker还必须补声明。五虫1→5和女王左火炬头→女王的实际遍历顺序已验证。
- 全原生遭遇工厂覆盖：每目标1440组（90种变体×4人数×4生态/幕输入）；public88类型含BattlewornDummy三种必需Setting，beta90类型。保留原敌并按规则追加/施加状态，没有因专用场景缺Marker取消追加；原敌未全面AfterAdded，未知位排序和视觉容量继续作为缺口，不能宣称全场景完整战斗通过。
- STS1原始3.4.02加载骨架失败；作者AFTP0.13.0准确真菌兽/神秘术士/火炬头4.2.11已通过原生资源、模型CreateVisuals、动画及Animator输入。只读挂载PCK，不运行上游DLL、不复制其AI、不分发资产。可加载与可发布分别记录，详见RESOURCE-SEARCH.md。
- Calamity本体无IChoosable。原创选择适配在真实NCard中使用原生劫难名称/描述/画像，柔嫩使用原能力名称/描述/图标；中英文实际格式化并渲染节点，不入牌堆。柔嫩64px图标作为展示画像的可读性仍需用户确认；没有用其他卡画像冒充。
- 早期商店UI探针退出报告资源泄漏，原日志保留；后续含选择卡节点和额外帧的顺序运行需按最新日志复核，不从退出0自动推定零错误。TestMode会让原生视觉工厂返回null，宿主仅在同步显示调用内暂时关闭，购买仍在TestMode和实际拒绝补丁下执行。

## 未解决输入

机器侧剩余重点：专用场景的完整原生布局/槽位接入、原生整场回合/胜利到奖励链、原生选择屏与开箱视觉调用链所需最小宿主，以及真实游戏验收场景入口。全原生遭遇的添加接口、混合随机目标、原禁用条件、商店输入处理器/实际跨店计价、房间奖励包装链已补跑，但各自没有覆盖所有生命周期。面具故意放入真实Event袋的输入已验证Common/Uncommon/Rare/Shop前后抽取均不抽出；这不证明自动内容池注册或正式保存恢复。不得把尚能自动做的缺口归入用户UI兜底。

真实HUD四态、焦点/分辨率/遮挡及完整战斗布局由用户实机确认；当前无反馈。HUD独立显示包入口/步骤见presentation/ACCEPTANCE.md，其余场景验收资产仍需准备。资源可加载已取得新证据，发布权限仍未建立，见RESOURCE-SEARCH.md。RitsuLib Godot注册已有多人验证补验通过，不再沿用早期独立.NET失败将其排除。原生联网、原版存档大厅和完整检查点仍归[多人验证](https://github.com/Endercloud001/AllMatters/issues/68)，本票只交领域状态/输入，不预选集成架构。
