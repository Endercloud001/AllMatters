# AllMatters

《杀戮尖塔 2》Mod，作者 Endercloud001。当前为开发初始化骨架，尚未实现 v0.1 的事件或卡牌调整。

v0.1 计划从 [STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD) 移植 11 个一代回归事件（排除对对碰）、关联遗物和必要依赖，并让升级后的「你好世界」在每回合开始生成一张升级过的随机普通牌。

- [范围与依赖清单](docs/v0.1-scope.md)
- [环境、构建与双分支验证](docs/development.md)
- [参考源码与许可记录](docs/references.md)
- [实施顺序](docs/implementation-plan.md)
- [双分支 API 比较与编译验证](docs/research/dual-branch-api.md)

快速开始（PowerShell）：复制 `Paths.example.props` 为 `Paths.local.props`，填入游戏 DLL 目录，然后运行 `./tools/Build.ps1`。输出在 `dist/public-beta/AllMatters/`。`-Branch public` 使用另一套公开版 DLL。

本地文档实际保存在相邻 `AllMatters_docs`，通过 `docs` junction 作为普通文档纳入 Git。其他机器正常 clone 后使用仓库内的 `docs/` 即可。游戏 DLL、参考源码和本机配置不会提交。
