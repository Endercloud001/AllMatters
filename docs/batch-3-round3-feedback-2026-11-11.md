# 批次 3 第三轮实测反馈

起点 `decb472a80e7448bf7fbb1b510f9db107ca6983f`，集成 `integration/batch-3-20261111` 开始干净。三个独立项目内 worktree 分别处理 Continue 布局、双 Boss 路线和战斗切换耗时调查；主会话独占集成。主 checkout 的既有 RETROSPECTIVE 排除。

用户本轮确认：多人地图选择节点和进入节点（包括商店）均表现正常。只记录此确认，不扩大为 #81/#94 所有组合签收。

需求：缩窄 Ancient 的“继续前行”，已点击玩家头像放按钮中上方居中；调查进战斗切换的短暂卡顿，提供实测结论及解决方案；A10 第三幕击败第一 Boss 后只能前往第二 Boss，不得再去普通节点或重访商店。

沿用完整 RitsuLib 0.6.10、public 0.107.1 和 beta 0.111.0。新日志只读复制至 `.local/round3-feedback-20261111/user-godot.log`。卡顿调查区分主线程同步耗时、原生资源预加载和网络等待，headless 证据不能代签 GPU/真实端帧时；不为加速移除完整存档、奖励或可靠进入边界。

不启动游戏或 Steam、不写安装、不推送、不发布或关闭 Issue；全部产出项目内，不新增 hash、冻结 contract、baseline 或 gate。最终完整检查、双轴审查及统一包与人工验收分别记录。

## 实施与证据

- Continue：720p 为 225、1080p 为 300 逻辑宽度，头像作为按钮子行位于上方居中。双版实际引擎点击专项各 1 项，内部 2 分辨率 × 1–3 人；既有 presentation 两版各 19 项通过。集成专项日志 `.local/round3-feedback-20261111/ui-integrated.log`。旧全员继续规则不改。
- 双 Boss：原共同目的地函数只禁止第一 Boss 之前进入第二 Boss，未限制其后的其他节点。现第一 Boss 完成后只返回第二 Boss，第二 Boss 完成后为空，共享于 Voting/Available/BeginEnter；已有商店重访亦不可绕过。领域回归双版各 25 项；原生 A10 三幕入口及第三幕双 Boss 池/自动生成各 1 项、非 A10 幕入口各 1 项。原生幕入口真实执行，Boss 完成为领域操作，不代签实机击杀；红绿在 `round3-boss/.local/round3/`。
- 战斗卡顿：详见 [调查与解决方案](batch-3-round3-combat-investigation.md)。只交调查和明确分支方案，没有以不充分的 headless 帧证据修改产品性能路径。临时仪表已还原，真实帧/GC/GPU 归因留待开发环境捕获。

开工双版冒烟各 1 项通过；最终完整检查与审查结果随后记录。

首次完整检查 public 391 项及两版构建通过，beta 在既有 editing-leave 恢复编辑鼠标输入等待超时；集成树和 UI 独立树聚焦各 1 项随后通过，尚未稳定重现，不称已修复。失败日志还含 RitsuLib sidecar 向已离开玩家发送 Hello 的诊断，不能仅据此认定原因。Editing 阶段新增头像行和 Continue 按钮均隐藏，决策区宽度不变；未发现本轮布局改变其点击矩形的机制证据。

beta 第二次完整检查在旧商店测试的完整存档比较失败。已通过强制跨秒精确复现，仅 `save_time` 和 `run_time` 变化；`511c56a` 只对齐重新采样的这两个时钟字段，完整比较其余保存状态，仍要求拒绝无效库存、保留库存实例。双版专项各 1 项通过，证据 `round3-boss/.local/round3/shop-clock-{red,green}.log`。两个完整失败日志均保留于本轮 `.local`，没有改生产以绕过。

独立 Spec/Standards 初审及商店测试增量复审无未解决发现；性能测量 JSON、原生源码及日志分别核对。xUnit 双版各 27 项通过，0 失败/跳过，证据 `.local/round3-feedback-20261111/xunit-{public-beta,public}.log` 和 TRX。

最终 public 完整检查 391 项通过，beta 第三次完整检查 391 项通过；项目脚本、资源、占位扫描及两版构建通过。分别保留 `final-check.log` 的 public 成功和 `final-check-beta-3.log` 的 beta 成功，不冒称首轮双版退出 0；public 全套后仅新增上述时钟测试修正，双版专项已复验。完整宿主 beta 20/public 21 条诊断、元数据错误均 0，与既有故障注入/宿主提示边界一致，非零诊断不得省略。原始宿主为 `check1-public-host.log`、`check3-beta-host.log`。

统一包从最终审查提交重新构建，五文件及提交号记录在 `dist/batch-3-integration/delivery.md`。真实 public 安装与 Steam 三端仍按指南集中验收；本轮用户已确认的地图/商店操作不回退为未确认，但不扩大到 #81/#94 所有恢复组合。
