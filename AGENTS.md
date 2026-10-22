# AllMatters

开发前读取 `docs/v0.1-scope.md`。构建、版本验证和本机路径见 `docs/development.md`；移植时查阅 `docs/references.md`。

涉及卡牌、能力、牌池、展示或事件流程时，先从 `docs/game-mechanics.md` 定位目标分支源码；API 查询、测试和日志命令见 `docs/development.md`。开始工单或准备 PR 时运行 `tools/ReviewScope.ps1`；审查时读取 `docs/agents/review.md`。

游戏安装与实机验收由用户手动完成。Agent 负责自动化测试、构建和提供安装/验收步骤，不启动游戏或 Steam，不向游戏 `mods` 目录复制产物、依赖或改动现有安装。交付流程见 `docs/development.md` 的“实机验收交付”。

代码仅在 `src/`；文档在 `docs/`。当前工作区的 `docs` 是指向同级 `AllMatters_docs` 的 Windows junction。本机依赖、工具、日志与参考源码保存在 Git 忽略的 `.local/`，独立于该 junction；参考仓库位于 `.local/references/`，不参与编译。仅借鉴参考源码，不执行参考仓库的构建脚本。

默认不新增 hash、冻结 contract、baseline 或 gate。只有能明确说出具体失败场景，并解释 Git、版本号、主键、事务、唯一约束、类型和普通测试为什么不足时才允许加入。保留已有安全措施；认证、数据安全、不可逆操作和正式发布按项目要求处理。

## Agent skills

### Issue tracker

任务跟踪使用 Endercloud001/AllMatters 的 GitHub Issues。操作约定见 `docs/agents/issue-tracker.md`。

### Triage labels

使用 Matt 默认五种标签。映射见 `docs/agents/triage-labels.md`。

### Domain docs

单一领域上下文。探索领域时读取根目录 `GLOSSARY.md`；变更设计前读取相关 `docs/adr/`。消费规则见 `docs/agents/domain.md`。
