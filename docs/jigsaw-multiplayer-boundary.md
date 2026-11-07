# #80 多人接入与保存职责

2026-11-07 用户经 grill-with-docs 确认，决定记录于 [#80 评论](https://github.com/Endercloud001/AllMatters/issues/80#issuecomment-6706936501)。本文记录实施边界，不代表功能已经交付。

## 收口责任

| 工单 | 责任与收口证据 |
|---|---|
| #76 | 保存基础及奖励安全点；在 #80 接入当前在局名单、个人 Ancient 完成和必要保存点后，按自身交付事实独立收口，不等待 #81 的完整大厅集成验收。当前仍开放。 |
| #80 | 正式多人编辑、统一接受顺序（包括 Host）、双按钮共识、断线归位、进入各自 Ancient 与必要保存接入。不得仅移除单人限制；不得以编解码、模拟人数代签实际原生发送/广播或真实多端。 |
| #81 | 双端部分领奖后的原生大厅恢复统一集成验收，以及探索、可靠共同进入和完整阶段恢复。复用 #76 保存基础，不另造保存路径。本轮不启动整票。 |

不修改父规格 #73，不自动关闭 #76，不改变 GitHub 原生依赖。原生关系读取结果：#76 ← #75（已关闭），#77 ← #76（开放），#80 ← #74（已关闭），#81 ← #77/#80（开放）。按上表避免将 #81 的集成验收反向作为 #76 的前置条件。

“在局玩家”沿用 GLOSSARY.md：当前连接并参与共同决策者；原存档角色集合不能冒充在线名单。缺席者的 Ancient 完成记录保留；新会话临时票重新收集，不将持有、待连或继续票当作持久完成事实。

## 本轮起点及准备证据

起点 `3f81d4bb286bfb5b03dde661e3a56097b2e2dc1e`，当前 main，已包含 PR #90。开工未提交清单及 tracked 补丁分别保存在忽略目录 `.local/issue80/pre-existing-status.txt`、`pre-existing.patch`，不纳入本票暂存。保存基础来源为 [大厅检查点交接](jigsaw-lobby-checkpoints.md)。

两目标测试项目准备编译退出 0，日志 `.local/issue80/{public,public-beta}-preparation-build.log`。这只证明当前工作区可编译，没有新增行为测试、TDD red 或多人运行证据。

两版真实 DLL 已核对 NetHostGameService：public 0.107.1 使用无参构造，public-beta 0.111.0 使用 PeerVersionInfo 构造；均有 StartENetHost、SendMessage 单发/广播及 ClientDisconnected。beta 的 StartTestHost(AbstractTestNetHost, …) 在 public 不存在，不能共用该测试入口。ENetClient 的构造、ConnectToHost 和 SendMessageToHost 签名两版一致。签名仅证明 API 存在，尚不能证明传输或原生生命周期正确。

生产缺口：JigsawNativeSession.Enable 拒绝多人；CreateScreen、AncientFinished 和自动选择使用 Players[0]；JigsawScreen 输入直接修改本地 JigsawRun。接下来必须沿实际原生发送、Host 接受、广播、生产 UI 状态及 Ancient 完成回调建立纵向 red/green，不能把上述准备编译算作完成。

## 批次与未验范围

#80 属第 3 批多人拼图及探索，与 #81 一起集中人工验收。当前统一多人包未就绪，无需用户安装或重复单人验收。每版真实三端编辑、争用、最后票/撤票竞态、断线、Host 退出及各自 Ancient 未验；#81 的大厅三存档两在局及部分领奖恢复也未验。

自动 Godot 宿主、实际原生网络调用与真实多进程/多端分别留证；public 宿主共享 beta 资源不能代签正式 public 资源运行。单人保存/退出/继续与既有跨幕素材、字体、步数的人工通过保留。不得启动游戏或 Steam、安装 mods 或修改真实存档。
