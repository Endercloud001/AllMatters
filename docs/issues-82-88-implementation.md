# #82–#88 实施索引

目标：public0.107.1 / public-beta0.113.0 / RitsuLib0.6.11。集成树`hunting-20261112`、分支`integration/issues-82-88-20261112`，源码基点=`5d127b0`。本页只导航已集成成果和验证基点；在制源码、未提交状态、Ready顺序及下一命令统一见[唯一看板](issues-82-88-board.md)。

需求以[#73](https://github.com/Endercloud001/AllMatters/issues/73)及各票明确修订为准；规格固定来源见[验证地图](issues-82-88-verification-map.md#规范与历史依据)，API/工具方法见[开发入口](development.md#api-查询)，来源与分发边界见[素材来源](issues-82-88-asset-sources.md)。覆盖矩阵集中在[验证地图](issues-82-88-verification-map.md)，不在此重复逐次执行日志。

## 已集成范围与证据入口

下表均为部分自动化交付；每条来源记录保留准确源码提交、命令、版本、人数/端数、审查及未验范围，不能把一票中的代表case写成整票通过。

|Issue|已集成成果|权威实施/证据记录|
|---|---|---|
|#82 限步/面具/服务|`46a2d5b`领域/保存/恢复投影、HUD、控制台和服务限制；`09fa14f`→`76e49d2`真实休息遗物及双run自然离开|[实施记录](issue82-hunting-implementation.md)、[双轴审查](issue82-review.md)；休息遗物及原native rest集成双版各2，四诊断0|
|#83 空宝箱|`90c7970`空奖励/Face/Spoils及任务等待；`fb58274`→`2915a6a`双版正式整房鼠标/Proceed与自然保存恢复|[实施记录](issue83-treasure-implementation.md)、[双轴审查](issue83-review.md)；最新集成双版各11，四诊断0；未知直接支付/未返回Task后台不宣称通用拦截|
|#84 密林|原生逐敌规则、Phrog五虫及Vantom普通墨宝、正式布局/鼠标/全焦点环、密林首普通及Phrog/Vantom战前与胜利奖励fresh|[密林记录](issue84-overgrowth-implementation.md)；Phrog最新组集成双版各8，Vantom最新组各13，四诊断0|
|#84 暗港|共享完整检查点`d8fb141`→`31bb112`；Gardener/Colony/Eel、Matriarch生命/睡眠随从/fresh、Waterfall复生/fresh、SoulFysh随从/fresh/布局、Seapunk14场开场/fresh/布局|[共享接缝](issue84-combat-checkpoint-seam.md)、[暗港记录](issue84-underdocks-implementation.md)；最新Waterfall`2fb2b0a`→`eb4837c`集成双版各8，四诊断0。Matriarch布局`6655b2c`→`10b05df`已集成，双版各5四诊断0|
|#85 巢穴/知识/蟹/Holes|ActTwo规则、Hive精英与事件开场/Hive首普通fresh；Knowledge三轮单owner规则、Tender真实屏与单ownerfresh；帝皇蟹固定Artifact3与原场景保持；Holes单owner及三端战前/奖励/完成点恢复|[巢穴记录](issue85-hunting-implementation.md)；蟹最新集成双版各9，Holes完成点`1956c17`双版event各4。Knowledge首轮双owner片及`2c45c7c`→`845d00e`自然调度已集成；蟹`859096a`→`f2906d4`完整fresh已集成，专项双版各1四诊断0|
|#86 荣耀普通/精英|Mecha/SoulNexus/Flail原生规则；Mystic正式模型/资源/三动作/fresh；`a050e7b`→`21a563f`12普通/3精英正式布局及输入|[荣耀记录](issue86-hunting-implementation.md)；布局集成双版各15、相关Forest/Mystic各3，四诊断0|
|#87 荣耀Boss|Aeonglass个人12计数/五状态/Wither及fresh；TestSubject复生/计时/Skittish/Suck/MultiClaw/fresh；Queen一次重燃/TorchHead/激怒/fresh/布局|[Boss记录](issue87-boss-implementation.md)；Queen布局后集成各9、Aeonglass/TestSubject fresh各1，双版四诊断0。间歇P87故障未修，个人正式UI片`8401f1a`→`b5088fd`已集成，双版各2（eng/zhs）四诊断0|
|#88 兼容/交付|`080104f`GoldenCompass/WingedBoots与Flight边界；控制台注册/执行；`4aae096`→`f5d8dde`64种子每日modifier字段/RNG验证|[兼容记录](issue88-compatibility-implementation.md)、[控制台](issues-82-88-console-reference.md)；最终当前源码Check/同提交包/集中人工矩阵仍待|

## 完整检查与已知诊断

最近已执行的双版全量基点是**历史提交`df6a1f9`**，不是当前HEAD：public509 / beta507 PASS，Build/Host/Metadata均0，EngineErrors分别25/26，不能称零错误或为后续切片签完整Check。

- 原始日志在兼容树`.local/host-20261113/{public,beta}-full-df6a1f9-host.log`。诊断分类在**主仓**`.local/main-control-20261113-resume/{public,beta}-df6a1f9-diagnostics.md`。
- public含18条坏档/名单保护负例＋1worker注入，其余视觉缺类1、managed action pop2、退出资源3；beta含19条保护负例＋1worker注入＋1原生CursedTome多人场外致死保护，其余视觉缺类1、treasure空Tween NRE1、退出资源3。CursedTome保护恢复1HP，不是实际击杀成功。
- 当前局部宿主已能运行匹配资源；P87原六项间歇故障单独暂停，事实与恢复条件见[看板](issues-82-88-board.md#暂停与等待)。未捕获原错的probe绿不构成修复。

最早开工基点`f7e915c`的beta0.111.0/public0.107.1冒烟为403/405 PASS、Engine22/23；仅保留主仓`.local/research/batch82-88/{public-beta-smoke,public-smoke}.log`历史锚点，不作为当前目标证据。

较早完整检查的逐次失败和过期派发安排已从入口移除；需要追溯时查Git历史及原日志，避免旧“未提交/缺public资源/下一片”覆盖现状。

## 环境、审查与交付边界

`1d1dac0`完成beta0.113.0兼容及RitsuLib0.6.11接入；`8a69527`将DLL/PCK/Spine绑定所选目标，public匹配资源位于主仓`.local/public-resource-recovery-20261113/public107-content/`。beta0.111.0旧通过数只作历史，不能代签当前目标；工具/API/构建方法只在[开发入口](development.md)维护。

#81仍需按具体分版/设备/故障项核对归属；其[#93/#94收尾确认](https://github.com/Endercloud001/AllMatters/issues/81#issuecomment-6814733380)已认可实现、自动验证及对应人工项，不能因父票OPEN将它们重新标未通过。其他适用真实端/设备验收仍逐项保留。

最终从复审后的同一源码提交执行项目完整Check、双版构建与许可检查，再按[实机验收交付](development.md#实机验收交付)集中提供人工批次。Agent不启动游戏/Steam、不写真实mods或存档；本地切片不授权推送、评论、关票或正式发布。原树、分支、stash及本机证据不因文档精简而删除。
