# 一代事件回归工单索引

日期：2026-10-20。用户已确认 13 票拆分，子工单已发布为 GitHub #3–#15，全部标记 `ready-for-agent`。T01–T13 仅保留为原方案对照编号，执行状态以 GitHub 为准。

父规格：[GitHub #2](https://github.com/Endercloud001/AllMatters/issues/2)。已读取完整正文及全部评论（0 条），正文与本地规格一致；发布前仓库 Issues 仅检索到父规格，未发现重复实现工单。

本目录是已发布工单的文档索引与正文副本，不作为第二套本地 issue tracker。逐票正文 Blocked by 已使用真实 GitHub 编号；21 条 GitHub 原生阻塞关系已设置，并逐票读取核验与正文一致。父规格未编辑、未评论、未关闭。

现有生产代码没有事件实现，不需要先做独立的大重构或纯框架票。首票牧师携带必要接入；其他票只依赖实际需要的产物，不按旧实施阶段顺序强行串行。

## 拆分与直接阻塞

| 方案编号 / GitHub | 工单 | 直接阻塞 | 可独立交付 |
| --- | --- | --- | --- |
| T01 / [#3](https://github.com/Endercloud001/AllMatters/issues/3) | [牧师：贯通事件注册、资源及一次服务](01-cleric.md) | 无 | 玩家能在正式加载的 AllMatters 中遇到牧师，选择治疗、净化或离开；以这个完整玩法贯通必要依赖、资源、测试入口和开发安装包。 |
| T02 / [#4](https://github.com/Endercloud001/AllMatters/issues/4) | [老乞丐与神圣泉水：付费删牌和诅咒清除](02-beggar-fountain.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 玩家可用 75 金币从老乞丐处删除一张牌，或通过神圣泉水清除全部可移除诅咒；两个小事件各自完整可玩。 |
| T03 / [#5](https://github.com/Endercloud001/AllMatters/issues/5) | [增益研究者：J.A.X.、变化升级与突变之力](03-augmenter.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 玩家在增益研究者处完整选择默认固有的 J.A.X.、变化升级两张牌，或交出初始遗物换取第三回合衰退的突变之力。 |
| T04 / [#6](https://github.com/Endercloud001/AllMatters/issues/6) | [诅咒书本：独立阅读、三选一及三本书效果](04-cursed-tome.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 玩家能独立完成书本阅读与风险选择，活着支付拿书代价后主动选一本，并在后续战斗使用该书完整效果。 |
| T05 / [#7](https://github.com/Endercloud001/AllMatters/issues/7) | [变化大转盘：六结果、稀有度分布与空池处理](05-wheel.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 玩家实际旋转转盘，并按同一已确定结果看到和领取六类奖励或代价；无目标/空遗物池不会造成重抽或卡住。 |
| T06 / [#8](https://github.com/Endercloud001/AllMatters/issues/8) | [红面具大人之墓：固定献金及持面具分支](06-red-mask-tomb.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 玩家在第三幕墓穴支付固定 150 金币获得红面具，或戴已有面具领取 222 金币，两个分支均实际可达。 |
| T07 / [#9](https://github.com/Endercloud001/AllMatters/issues/9) | [大图书馆：跨职业阅读与睡觉](07-library-reading.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 玩家在第二幕图书馆从其他四个原版角色的合法候选中选一张牌，或选择睡觉治疗；打扫由后续瓶装票补齐。 |
| T08 / [#10](https://github.com/Endercloud001/AllMatters/issues/10) | [大图书馆：打扫、三种瓶装绑定及跨战斗恢复](08-library-bottles.md) | [#9](https://github.com/Endercloud001/AllMatters/issues/9) | 在已有图书馆中补齐打扫，玩家获得可用瓶子并绑定一张牌；升级、战斗内变化及下一场战斗均保持正确规则。 |
| T09 / [#11](https://github.com/Endercloud001/AllMatters/issues/11) | [蒙面强盗：交钱或三人帮事件战斗](09-masked-bandits.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 全队能共同选择各自交出全部金币，或进入三人帮战斗并各领取一次红面具及继承奖励。 |
| T10 / [#12](https://github.com/Endercloud001/AllMatters/issues/12) | [心灵绽放：双乐加维林与三类非战斗选择](10-mind-bloom.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 第三幕玩家可完整选择双乐加维林凯旋、升级全牌与绽放印记、富有或无伤痛，并获得对应各自效果。 |
| T11 / [#13](https://github.com/Endercloud001/AllMatters/issues/13) | [竞技场：首战零战利品、中场及随机精英第二战](11-colosseum.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 全队在第三幕完成活雾与潮湿邪教徒首战，返回中场选择逃离或继续随机原版精英第二战并领取指定奖励。 |
| T12 / [#14](https://github.com/Endercloud001/AllMatters/issues/14) | [public 完整环境：安装并实机验证牧师](12-public-runtime.md) | [#3](https://github.com/Endercloud001/AllMatters/issues/3) | 在完整 public 游戏环境中按说明加载双目标产物中的 public 包，能实际遇到并完成牧师服务，区分 DLL 编译与真实游戏兼容。 |
| T13 / [#15](https://github.com/Endercloud001/AllMatters/issues/15) | [11 事件集成验收与双分支开发包交付](13-integration.md) | [#4](https://github.com/Endercloud001/AllMatters/issues/4), [#5](https://github.com/Endercloud001/AllMatters/issues/5), [#6](https://github.com/Endercloud001/AllMatters/issues/6), [#7](https://github.com/Endercloud001/AllMatters/issues/7), [#8](https://github.com/Endercloud001/AllMatters/issues/8), [#10](https://github.com/Endercloud001/AllMatters/issues/10), [#11](https://github.com/Endercloud001/AllMatters/issues/11), [#12](https://github.com/Endercloud001/AllMatters/issues/12), [#13](https://github.com/Endercloud001/AllMatters/issues/13), [#14](https://github.com/Endercloud001/AllMatters/issues/14) | 将已交付事件组合后验证无跨事件回归，交付安装说明和双分支开发包及真实验收记录，为后续正式发布提供清晰证据。 |

## 依赖理由

- T01：提供其余事件复用的真实加载、语言/资源与行为测试入口；这是其他内容票的直接依赖。
- T02：两个事件共用已存在的删除行为入口，规模较小；不依赖书本事件，永久/死灵诅咒边界可用合法测试准备单独验证。
- T03：卡牌、遗物和能力随完整事件交付，不先发孤立依赖票。
- T04：三本书是同一选择的必要候选，合并为一个完整事件交付；用户已确认保持三本书合票。
- T05：含必要转盘呈现；无需等图书馆或其他事件完成。
- T06：红面具已由原生提供，因此不依赖强盗事件。
- T07：阅读与睡觉可独立演示；将新瓶装生命周期留给下一票以控制单会话规模。
- T08：只直接依赖图书馆已有可玩事件，不依赖增益研究者、转盘或书本。
- T09：与墓穴共享的只是原生红面具，并非硬依赖；不把战斗票强制串联。
- T10：凯旋、绽放印记及其他分支合成完整事件；用户已确认完整事件合票，不因可复用代码而人为依赖强盗或竞技场。
- T11：不依赖心灵绽放先完成：相似奖励可复用已有实现，但共享实现机会不是先验硬阻塞。
- T12：仅依赖有可安装牧师的第 1 票，可与内容票交错推进；不要求其他十个事件先实现。
- T13：只等待各完整行为切片和 public 环境；无需直接重复依赖 T01（由内容票传递）或 T07（由 T08 传递）。

## 按父规格验收覆盖

| 验收场景 | 主要负责票 |
| --- | --- |
| A01 | T01, T02, T03, T06, T09；T13 负责组合验收 |
| A02 | T02；T13 负责组合验收 |
| A03 | T01, T12；T13 负责组合验收 |
| A04 | T04；T13 负责组合验收 |
| A05 | T04；T13 负责组合验收 |
| A06 | T04；T13 负责组合验收 |
| A07 | T04；T13 负责组合验收 |
| A08 | T03；T13 负责组合验收 |
| A09 | T03；T13 负责组合验收 |
| A10 | T03；T13 负责组合验收 |
| A11 | T02；T13 负责组合验收 |
| A12 | T10；T13 负责组合验收 |
| A13 | T10；T13 负责组合验收 |
| A14 | T10；T13 负责组合验收 |
| A15 | T05；T13 负责组合验收 |
| A16 | T05；T13 负责组合验收 |
| A17 | T05；T13 负责组合验收 |
| A18 | T06；T13 负责组合验收 |
| A19 | T07；T13 负责组合验收 |
| A20 | T07；T13 负责组合验收 |
| A21 | T08；T13 负责组合验收 |
| A22 | T08；T13 负责组合验收 |
| A23 | T08；T13 负责组合验收 |
| A24 | T08；T13 负责组合验收 |
| A25 | T09；T13 负责组合验收 |
| A26 | T11；T13 负责组合验收 |
| A27 | T11；T13 负责组合验收 |
| A28 | T10, T11；T13 负责组合验收 |
| A29 | T02, T04, T05, T06, T07, T08, T09, T10；T13 负责组合验收 |
| A30 | T11；T13 负责组合验收 |
| A31 | T09, T10, T11；T13 负责组合验收 |
| A32 | T01, T12；T13 负责组合验收 |

## 确认与发布

用户已明确回复“合适，发布即可”，原 13 票粒度与最小直接依赖保持不变。远端正文、标签和 open 状态已逐一读回核实；本地正文与已发布正文一致。

当前可开始的工单：[#3 牧师与事件加载链](https://github.com/Endercloud001/AllMatters/issues/3)。其余依赖按图推进；#14 public 完整环境不会阻塞 beta 内容票；#15 集成直接等待 #4–#8、#10–#14，#3/#9 已通过前置票传递，不重复设边。

原生依赖验收：21 条边均已写入并读回核对，无多余直接依赖。父规格 #2 保持原正文、标签、open 状态及 0 评论；没有新增实现提交或启动开发。
