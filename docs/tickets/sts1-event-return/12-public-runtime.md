# T12：public 完整环境：安装并实机验证牧师

GitHub：[#14](https://github.com/Endercloud001/AllMatters/issues/14)；已发布并标记 `ready-for-agent`，执行状态以 GitHub 为准。

## Parent

[一代事件回归父规格 #2](https://github.com/Endercloud001/AllMatters/issues/2)

## What to build

在完整 public 游戏环境中按说明加载双目标产物中的 public 包，能实际遇到并完成牧师服务，区分 DLL 编译与真实游戏兼容。

本票按父规格 A03, A32 验收。

## Acceptance criteria

- [ ] 确认完整 public 游戏、原生库与资源可用，并记录实际版本来源；不能把第三方 DLL 或 beta 资源当成完整 public 环境。
- [ ] 按项目对应 RitsuLib 运行包和 public AllMatters 安装说明完成真实加载；日志、图片、中英文、治疗和删牌都实际可用。
- [ ] 验证牧师和现有你好世界未出现分支或资源回归，记录执行步骤、版本、结果与证据。
- [ ] 需要用户操作 Steam 或提供完整安装时明确交接；不自动修改 Steam 分支、存档或覆盖既有游戏环境。
- [ ] 该环境供后续集成验收使用，不将本票列为其他 beta 内容实现的阻塞；若实际游戏版本变化，先记录与规格目标的差异，不宣称已验收旧目标。
- [ ] 遵循父规格及范围事件表；未修改行为继承 Balance，不重新访谈 Q1–Q20，不引入表外功能、额外 hash/冻结 contract/baseline/gate。
- [ ] 除环境/集成票的专门边界外，本票包含相应中英文、图片/场景/PCK、模型行为、真实目标双分支构建及 beta 实际可玩验证；普通测试只针对具体失败风险，公开行为入口优先。
- [ ] 本票引入的代码/素材先核对许可与归属；记录实际命令、版本、结果和宿主替代边界。真实联机/回放/完整 public 未执行时如实列出，不能以无界面通过替代实机证据。

## Blocked by

- [#3: feat: 牧师：贯通事件注册、资源及一次服务](https://github.com/Endercloud001/AllMatters/issues/3)
