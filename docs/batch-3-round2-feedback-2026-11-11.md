# 批次 3 第二轮实测反馈

起点 `de7ec932688d63650a127fc2e2103653d99eb307`，集成分支 `integration/batch-3-20261111`；本轮集成工作树开始干净。主 checkout 的既有 RETROSPECTIVE 不纳入。各实施子任务从同一起点创建项目内独立 worktree，地图输入与商店完成热点分别独占，集成由主会话执行。

用户本轮确认：多人拼图期间血条正确隐藏。未扩大为其余场景通过。

最新需求覆盖上一轮“全员同目标”规则：每位玩家单击节点显示本人角色头像；票未齐时不进入；全员票齐，同目标直接进入，分目标在所投节点中按原版规则随机决定一次。须核对原版双版本源码，不把 Host 点击后全员进入作为客户端输入正常的证据。

其余修改：结束提示改为“令先古之民决定将重新安排地图整体布局，请谨慎选择。”；复现第二幕生成地图、Ancient 后真实商店购买并原生退出回地图不能点击下一节点。

诊断优先覆盖 `JigsawNativeSession.ShowMap` 的实际 NRun/GlobalUi 层级与原生商店出口。上一轮直接创建并挂载 screen 的输入测试只证明该宿主路由，未覆盖正式 ShowMap 建立的遮挡及退出状态。现有实际游戏日志只读复制至 `.local/round2-feedback-20261111/user-godot.log`；仍为 beta 0.111.0、Workshop RitsuLib 0.6.10 覆盖本地 0.6.7。此前旧日志另存 `user-prior-godot.log`，不得混用旧异常作为本轮新根因。

开工双版冒烟各 1 项通过：`tools/Test.ps1 -Branch All -Filter 'Jigsaw native progress failed revisit preserves its complete before-entry point' -SkipResourceExport`，日志 `.local/round2-feedback-20261111/smoke.log`。

## 已核实的根因与修复

地图投票的精确失败是缺少视觉反馈：正式 `ShowMap` 层级中 Client 2 单击后，三端 `DestinationVotes` 均已有玩家 2 的票、房主未投时未进入，但节点上不存在该玩家头像。补充节点投票头像和改票移动；恢复按个人票抽取目的地，重复点击仍只有本人一票。新开原生多人大厅、Ancient 奖励及全员继续后，客户端先投不替房主选择、头像跨端可见，均以引擎输入验证。不宣称已复现独立网络权限错误，也不将“无头像”当成用户所有点击失败的唯一解释。

原版双版 `MapSelectionSynchronizer` 均在全员票齐后由 Host 从个人票数组抽取；重复目标因此有票数权重。本实现复用已有目的地随机状态和进入事务，预览不推进随机状态，最终只决定一次，失败重试保留已选目的地。

商店问题已精确复现：第二幕真实商店买牌后用顶栏 `NMapScreen.Open(true)` 开图，地图仍为 `CanReturnToRoom` 预览且本人尚未记录完成，`ActivateMap` 在处理选择前返回。商店自带 `HideScreen` 离开按钮则走另一条正常路径。修复应允许原生可旅行商店在实际选目标时完成本人离店，开预览不离店，仍能关闭地图回原商店购物；不得放行未结束战斗或 Ancient。

商店修复已集成：`CanVoteRequested` 查询当前本人完成或原生可旅行商店；实际点击合法目标才完成本人并提交投票，旧会话与非法目标不完成。单人专项双版各 1 项，覆盖两种出口、真实买牌、顶栏开关预览后继续购买及引擎下一节点。第二幕三进程商店专项双版各 1 项，Host 首票只完成本人且不推进，全员投票后进入并重访，售罄库存、删牌及 RNG 保持正确。证据位于 `round2-shop-exit/.local/round2/{merchant-final-red,merchant-final-dual,shop-topbar-dual-green}.log`；最终两个专项日志无 ERROR/FATAL。该测试从已生成的探索检查点进入第二幕商店，不宣称此专项重复完整 Ancient 领奖流程。

地图专项在独立树双版各通过：正式地图三进程投票/头像/改票/分票进入 1 项，新开原生多人→Ancient→首个地图客户端投票 1 项，领域 3 项。红绿日志位于 `round2-map-input/.local/round2/{avatar-red,avatar-green2,fresh-map-green,domain-green}.log`。新开 ENet 旧宿主的握手仍有原有 `InitialGameInfoMessage` handler 提示，不宣称该日志零诊断；恢复三进程专项另检查全部子进程非预期错误。

## 完整检查与审查记录

首次完整 Check 两版均因既有提示测试仍要求旧文案失败；已更新键盘接近、鼠标接近、错误优先及错误过期后说明断言为新文案，未删除测试。第二次 beta 仍编译了最后一处更新前的断言；public 已通过新提示断言和商店/战斗恢复项，但新增 fresh 地图测试硬编码三人在局，与既有 pending-absent 场景实际只剩两人在局冲突，需按该场景真实剩余名单校验。两次失败日志保留于 `.local/round2-feedback-20261111/final-check{,-2}.log`，不隐去首次结果。

独立 Spec 审查未发现实现缺失、越界或错误；Standards 要求补测试 helper `NCapstoneSubmenuStack._Ready` 的 public 反编译记录，已导出并复核解除（`.local/api/queries/round2-submenu-public/`），同时指出并复核上述提示断言修正。完整检查结果由实施方实际输出记录，不从审查摘要推断。

`c71a0a0` 测试修正精确按 `remainingRoles` 核对当前在局投票人，仍要求原生及拼图原名单各 3 人，并保留客户端首票、头像跨端及全员等待断言；断线、缺席延迟、缺席失败三个专项双版共 6 项通过。两轴实际读取更新 diff 后均无剩余发现。xUnit 双版各 27 项通过，0 失败/跳过（`.local/round2-feedback-20261111/xunit-{public-beta,public}.log` 和 TRX）。

第三次完整 Check 退出 0：public-beta/public 各 390 项 Godot 测试通过，两版构建及脚本、空白、占位扫描、资源导出全部通过。证据 `.local/round2-feedback-20261111/final-check-3.log`、`final-check-3-{beta,public}-host.log`。完整宿主仍有 beta 20/public 21 条引擎诊断，元数据错误均 0；故障注入、旧宿主脚本重绑定及退出提示边界与首次交付一致，不宣称零诊断。最终资源输入未改，资源包复用本次新鲜导出。

最终统一包从审查后提交重新构建，提交及清单见 `dist/batch-3-integration/delivery.md`。三端需使用同分支同一包与完整 RitsuLib 0.6.10，按指南优先复测本轮客户端头像/改投、分票进入和商店两种返回地图入口。本轮自动结果不代替真实 Steam 三端或完整 public 安装验收，#81/#94 组合实机条件仍未全部签收。

不新增 hash、冻结 contract、baseline 或 gate；不改已有安全措施。全部输出位于项目目录；不启动游戏/Steam、不改安装、不推送、不关闭 Issue。自动验证、统一测试包与真实端人工验收分别记录。
