# #83 实施审查记录

固定审查起点 `f754aa24dee2cc1f660b6153c834c1a571aadd94`，实现分支 `feat/issue83-treasure`。首轮审查实际读取九文件暂存差异，未提交；规格轴与规范轴独立，只读 Git、源码及已有日志。

## 首轮结论

未确认生产逻辑缺陷，但规格轴有两项 P2 自动证据缺口，暂不按整票通过：

- #83 AC2 的直接 `GainGold/Obtain`、Owner/实际入库反例尚未执行。未知第三方直接支付明确不受支持；仍需真实反例证明该限制及未使用全局取消器，不能仅凭研究说明。
- 完成点仅执行原生读回和字段检查，尚未真正 `SetUpSaved→LoadIntoLatestMapCoord` 证明不重开宝箱、不重复 Face/任务/附奖。

另外，旧任务恢复 drain 已有真实 fault sibling 反例；新增 Pause/Menu 入口还需直接验证，不能由反射 `LoadCheckpoint` 结果代签。Tween 初轮虽断言通过但有宿主 NullReference/泄漏，需清理并复验。以上均已交实施者补切片，不推给用户实机。

规范轴未确认逻辑性硬违规；新增 `NGame.ReturnToMainMenu` 的 public 真实方法体已找到，需放入规范查询目录。联机加载重复 drain 是非阻塞重复代码建议，后续可明确单一边界负责，不扩大本票重构。

## 已有切片及后续

已执行真实红绿切片覆盖 Face/Spoils、无 Face 的原生金币/共享遗物、同步异步列表附奖、半份任务保存、已开始/未开始来源任务的完成等待、fault sibling 的旧任务收口。真实双进程 ENet 双版覆盖完成意图早于开箱消息及挂起的 Spoils 任务；适用人数、缺席原队员和原生保存分层记录。

public 使用 beta PCK 只能签 Collection 级真实 `AnimIn`/Tween；beta 整房原生 `OpenChest` 单独记录。public 整房正式资源仍待集中验收，不以跳过 `UpdateChestSkin` 代签。

实施者正在补最终三项证据及宿主 Free 清理；最终暂存增量、完整 Check 与双版构建完成后再更新本页结论。暂不将目标测试通过扩写为整票完成。

## 最终增量复审

两轴已读取最终九文件暂存差异及 `review-slices-final.log`，均通过；无剩余代码阻塞。双版各 11 项，日志无 Godot ERROR、RID 或 ObjectDB 泄漏。

- 直接附奖 fixture 真实 `GainGold(7)`／`Obtain<Lantern>`：列表奖励清空，直接金币与遗物仍实际入库，返回 Owner 正确。证明具体未支持边界，不冒称直接支付也被取消。
- 完成点经过真实 IO、`LoadCheckpoint`、`SetUpSaved` 及 `LoadIntoLatestMapCoord` 回到 MapRoom，保留已完成根且不重复扣步，不再仅断言字段。
- Pause 退出与关闭会话后的菜单入口均等待挂起旧任务；仅替换 drain 后的菜单尾部，断言旧任务结束前不清局、结束后真实 CleanUp 一次，文档保留宿主边界。
- public NGame 目标记录已补规范目录；保存失败仅对有效未关闭会话恢复来源接收，fault 使用 WhenAll 等全部 sibling 收口，失败仍不可提交为成功完成。

完整 `Check.ps1 -Branch All` 正在运行；最终退出状态与双版构建尚未签署。两层恢复入口重复 drain 保留为非阻塞维护建议。

## 验证及集成收口

首次全量检查因 #82/#83 重复注册原生脚本而失败；修复为复用既有一次性注册入口，规范轴已增量审查通过。第二轮 `final-check2.log` 九类检查全部 PASS，命令退出 0：beta 431、public 433 项，双版 Build 均 0 警告／0 错误。主控已独立读取最终九类 PASS 与暂存空白检查。

提交 `90c7970c16ea160e85d0257c53d7cdaf76233c0e`，10 个已审文件；hook 仅展开单行控制语句，不改逻辑。已 fast-forward 合入集成分支，主控正在运行合入后宝箱专项与双版构建。正式 public 整房资源、真实玩家机器、实体设备及未知后台直接支付等限制继续按实现记录保留，票通过不代表这些已验收。

合入后 `Test.ps1 -Branch All -Filter 'Jigsaw hunting treasure'` 双版各 11 项通过，双版 `Build.ps1 -SkipResourceExport` 成功，整体退出 0。证据在集成 worktree `.local/issue83/integration-{test,build-beta,build-public}.log`。#84 已合并该提交，测试与公共保存接缝所有权正式交接。
