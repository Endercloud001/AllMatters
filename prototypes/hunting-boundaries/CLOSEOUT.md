# 追猎边界收口记录

2026-11-04。只回答[验证追猎逐敌机制、房间服务与战斗布局边界](https://github.com/Endercloud001/AllMatters/issues/70)，不交付完整追猎功能，不决定集成架构。本记录更新此前批次中“人工待验”和分离的阶段实验状态；历史失败、负例及资源限制不删除。

## 结论与证据级别

已测双版原生命令、逐敌状态机、指定服务入口可承载规则，但不能只改通用加血/力量、清奖励列表或添加Marker。需要逐敌接缝、原生异步完成边界、显式槽位和服务执行端适配。已证实的负例也是本票答案，不是规则通过：缺Marker/越宽/相交、最后敌人死亡时原生Ending拒绝孢子、直接发奖绕过清列表、遗物调用方要求实际入库时取消获得会中断后续非奖励工作。

用户明确确认上一轮整个人工清单均通过，public界面按用户声明验收通过。来源及范围见[用户验收记录](USER-ACCEPTANCE-2026-11-04.md)。没有取得匹配public PCK，也没有把beta共享资源运行改写成public完整场景运行。用户声明缺少新的逐设备日志和完整环境参数；它仍是本轮用户验收依据。HUD仍为示例四态，不能推定领域状态已接入。

## 最新连续阶段实验

命令：`./tools/Test.ps1 -Branch All -Filter 'continuous native start' -SkipResourceExport`。`evidence/stage-continuous-r8.log`、同名exit.txt：退出0，两目标各1项，分别运行1/2/3/4人，每人数9个第三阶段玩家回合（合计72行STAGE_CONTINUOUS）。

同一原版TestSubjectBoss战斗实际执行SetUpCombat→StartCombatInternal→AfterCreatureAdded/AfterAddedToRoom→首手5张→CreatureCmd.Kill及Adaptable死亡拦截→敌方复生→第二次死亡及复生→第三阶段后的9个玩家回合。原生复生Task完成后局部wrapper清除阶段二胆小/吮吸并挂载计时能力；首个完整玩家方StartTurn计为1，3/6/9额外Draw被拒，原生起手5张保持，原生玩家结束阶段清除细看。敌方实际执行Lacerate/BigPounce/BurningGrowl循环。

这是新的串联实证，替代“只预先启动计时器/手动派发钩子”的推断。此前受控挂起SetMaxHp的实验仍证明Respawns=2不等于完成复生。r1为夹具语法错误；r2/r3在未激活战斗时走生成牌历史路径缺房间历史，改为直接准备抽牌堆输入；r4/r5发现先前计时对象在后续原生回合未挂载，不能只置Armed，r6在真正第三阶段完成后Apply。全部失败日志保留。

限制：FTUE查询入口在访问真实进度前替换为true；输入抽牌堆直接准备90张Defend；beta外层readiness coordinator挂起，两个目标均显式调用原生StartTurn/两结束阶段/切边，敌方前恢复玩家HP保证长实验存活。public敌方结束自动启动下一玩家回合，beta需外层继续启动。原生开场、复生、抽牌和敌方行动保持真实；不宣称自动全员就绪、网络消息、检查点、保存或正式集成。测试末释放挂起任务并Reset，不遗留后台战斗循环。r6确认一次beta重复StartTurn会造成计数2；r7复验重现，r8移除重复启动并双版通过。

## 13组覆盖收口

P=public v0.107.1/59260271，B=public-beta v0.111.0/41cef1ea；Godot4.5.1 mono、RitsuLib0.6.7。未特别注明时为P/B、1–4人原生API/行为实验；所有命令/逐行输入/预期/实际/退出/日志索引在matrix.json、runtime-cases.json及evidence/runs.json。自动实证分层，不将人数输入冒充真实联网。matrix.json共13项均按“原型问题已有答案、带适配/验证边界”归档，不宣称所有逐子场景或正式功能完成；runtime-cases.json记录本轮6829条带来源观察。以下“边界”是后续集成必须消费的限制，并不批准正式功能。

|组/规则|输入与实际答案|主要实证|边界/后续输入|
|---|---|---|---|
|1 全场景追加/布局|各目标1440工厂输入、1080真实视觉及原生布局输入；原敌保留；每版288缺追加Marker、88通用越宽、44初始相交；五虫210px间距用户通过|layout-native-r5、five-worm-spacing-r2，退出0|逐场景数据已列出；临时Marker与fallback测量不是最终布局，不能删怪。用户确认战斗显示/输入，未逐一给全遭遇全分辨率证据|
|2 真菌兽/术士|首咬/两咬必生长/39与40阈值；独立六随机选择、受伤集合/同伴自身；死亡阻止/Artifact及最后孢子实际命令|全组MODEL/RANDOM、NATIVE_DEATH/ENDING，退出0|原生Ending须延至死亡效果完成，不能依赖宿主强制false；资源许可另行处理|
|3 第一二幕逐敌|再生/苏醒/五虫奇偶首次/墨宝/咆哮/神官/胆小/硬化覆甲/99弱/睡眠减血/Minion/瀑布正负隐藏状态均有逐项运行|全组TABLE/COMPANION/SPAWN/FATAL，退出0|局部适配与固定数值逐敌处理，不能按名字替代能力或把普通追加改Minion|
|4 无厌/知识恶魔|沙坑5、两堆各4、三轮逐玩家选择、15/16及6×3/7×3/治疗20；展示牌不入堆；B三组×中英×左右12次原生弹层关闭释放|全组MOVES/KNOWLEDGE、choice-overlay-r2，退出0|public完整场景按用户声明通过；自动public资源负例保留；物理输入和焦点按用户验收|
|5 第三幕/实验体|喷火器版本差异/领地/连枷新牌；二阶段3+N×9/10；真实复生异步边界及同场原生开战至第三阶段9回合|全组TABLE/MOVES/STAGE、stage-continuous-r6，退出0|readiness协调器隔离、显式原生回合分发；复生完成后才挂载计时和清能力，不以Respawns2提前执行|
|6 女王|6当前意图×1–4人、实际死亡一次覆盖、当次快照不行动、下一次攻10、激怒存活+2且不补；用户完整流程通过|全组LIVE_QUEEN/QUEEN、startup-six-fixed，退出0|模型扫描前不能提前Inject导致重复ID；恢复一次重燃事实由相关票承接|
|7 沙漏|每玩家12初值/显示/重置、真实12次出牌与五状态顺序、满手溢出、凋萎强化|全组CLOCK，退出0|public原攻击26/beta22保持，正式状态来源和恢复不由显示探针证明|
|8 HP/固定值/Fatal|1–4人只缩HP一次、Artifact1/3/5；Feed/贪婪/号角及普通/Minion真实差异；原生胜利结算|全组COVERAGE/FATAL/COMBAT_END，退出0|只按指定随从用Minion；受控遭遇与隔离IO不代表完整存档/第三方Fatal兼容|
|9 商店|收费/免费四类及删牌、程序发送/接收入口拒绝；动态折扣/补货和跨店费用；Cleric不误禁；用户重开/退出/输入通过|continuation-shop-r1及全组SHOP/UI_SHOP，退出0|内存库存不是重访保存；退出泄漏诊断保留，需集成时清理屏幕生命周期|
|10 休息|保留原禁用/图标、Heal/Mend/附加奖励、额外次数再过滤；禁止项不记历史；用户治疗/输入通过|全组REST/UI_REST，退出0|public Exit立即返回、beta等待全员；不能据public Exit给全队完成|
|11 空箱|双版真实空Tween与等待通知；B原场景OpenChest、Face、当地Spoils任务移牌移标记、异步非奖励每人1；用户动画/继续/真实客户端通过|treasure-package-r2、全组TREASURE/UI_TREASURE，退出0|直接GainGold/Obtain反例与返回值兼容见下；public完整场景按用户声明，单客户端远端Spoils仍pending，不伪造网络|
|12 安全/面具/Boss|事件内部连续房间继承安全、全队必要流程/真正离店后发；原生Kill→胜利→奖励前最终面具移除，首Boss/事件Boss保留|全组MASK/VISIT/COMBAT_END，退出0|聚合输入受控；统一进入、存档幂等、网络与恢复归多人/集成验证，未在本票实现|
|13 HUD|四态、四分辨率的Godot子视口边界/焦点及独立DLL注册；用户四态/焦点/可读性通过|全组HUD、用户验收，退出0/人工无命令|仍是示例状态，不是完整NGlobalUi领域接入，也不是全设备穷举|

## 宝箱直接发奖的明确适配限制

已有真实双版实验足以回答该边界，而非等待用户代测：异步奖励钩子直接GainGold11/ObtainAnchor绕过清列表；底层拒绝两命令能保持金币/遗物/历史/奖励袋不变，但返回无Owner对象会中断读取Owner的调用方。实验性的有Owner但未入库载体仅让Owner读取后非奖励计数继续；调用方若要求实际入库，仍抛错，非奖励计数0。负例断言通过只说明重现失败，绝不记为规则通过。

因此已验证的原生列表/Face/Spoils路径可交给集成，但六个包不是任意第三方宝箱奖励的通用拦截器。未知Mod若把非奖励工作写在“必须实际取得奖励”之后，通用取消返回无法同时满足双方假设；需要按具体来源/调用方适配并补运行证据。禁止以放行奖励或跳过整个钩子削减规则，也不提前指定全局上下文/返回载体为正式方案。该结论是适配边界，不要求用户改变玩法；正式兼容范围由集成票消费。

## 交接

原型分支`prototype/hunting-boundaries-continuation`。运行入口Run.ps1、六包Build.ps1、用户步骤ACCEPTANCE-INDEX.md，可用API及恢复输入INTEGRATION-INPUTS.md。原工作树和既有修改保留；未安装mod、未启动游戏/Steam、未触碰真实存档。大合包早于五虫210px修订，最新间距须用单独spacing210包或重新Build，不能把旧合包称最新。

本票完成的是可行性/边界问题的回答；正式布局修复、服务接入、统一多人/恢复、状态接入与素材正式分发不在本原型中实现。不关闭其他票，不选择集成架构，不把已测负例改成“所有规则均已实现”。

最终完整检查：./tools/Check.ps1，evidence/full-check-closeout-r2.log及exit.txt，退出0；beta273项/public272项，6829条运行观察，脚本/资源/双版测试和构建均PASS。保留已有资源缺失、模型关联及泄漏诊断，不把退出0写成干净运行或规则全部通过。
