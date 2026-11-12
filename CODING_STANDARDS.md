# 编码标准

## Harmony patch 目标验证

**规则**：任何 `HarmonyPatch` 目标类型与方法，必须在 `.local/api/queries/<目标>-<版本>/` 的反编译文件中查到后方可使用。

patch 必须引用实际存在的方法名、参数列表和签名；不得凭猜测命名或使用不存在的类型、成员。

### 查询命令

两目标 API 查询见 `docs/development.md` 的「API 查询」节：

```powershell
# 1. 先查类型是否存在
./tools/ExportApiReference.ps1 -Find <TypeName>

# 2. 确认方法签名（不导出方法体）
./tools/ExportApiReference.ps1 -Branch public-beta -Types <FullTypeName> -Signatures -Member <MethodName>
./tools/ExportApiReference.ps1 -Branch public -Types <FullTypeName> -Signatures -Member <MethodName>

# 3. 需要方法体实现时才完整导出
./tools/ExportApiReference.ps1 -Branch <target> -Types <FullTypeName> -Member <MethodName>
```

### 审查检查

`/code-review` 时，reviewer 应检查每个 `HarmonyPatch` 的目标类型与方法名是否在两目标的 API 查询记录中都存在。

patch 目标不一致或方法签名不存在的改动标记为阻塞项。

---

## Git 最佳实践

参见 `docs/git-best-practices.md`，涵盖：
- stash 安全操作（避免误删）
- worktree 清理（归档会话、释放文件句柄）
- PR 合并后同步（本地与远程对齐）

---

## 测试编写规范

**写任何新测试前，必须**：

1. **查看既有测试**：列出测试文件，读取 2-3 个代表性样本
   ```powershell
   # 列出测试文件
   rg --files src/Tests -g '*Tests.cs' | head -5
   
   # 读取前两个示例
   head -50 src/Tests/ClericTests.cs
   head -50 src/Tests/HelloWorldTests.cs
   ```

2. **识别项目框架与宿主初始化**：
   - 项目框架：查看 `src/Tests/AllMatters.Tests.csproj` 的 `<PackageReference>` 标签
   - 实际测试模式：阅读既有测试，识别是否使用框架特性或自定义架构
   - 宿主初始化：查看 `HelloWorldTests.cs` 等测试入口，识别测试前必须的初始化步骤（如 Harmony 补丁、ModManager 初始化）

3. **复用现有 API 和模式**：
   - 复用相同的测试结构（如元组数组 + 静态方法）
   - 复用相同的验证函数（如 `Check()` 断言）
   - 复用相同的模型初始化（如 `Player.CreateForNewRun<>()`）
   - 不引入新的测试框架或验证库

4. **验证框架与依赖**：
   - 提交测试前，确认没有添加新的测试依赖（如 NUnit、MSTest）
   - 使用 `dotnet build src/Tests` 编译测试项目，确保无编译错误

**禁止**：
- 凭猜测使用测试 API
- 跳过既有测试文件的阅读
- 引入新的测试框架或依赖
- 修改宿主初始化逻辑（除非与当前工作相关）

## 测试名必须名副其实

审查者按下面两条判断测试是否名副其实（属于判断，不由自动检查覆盖）：

1. **名称中的每个行为都在测试体内被构造并断言**。测试名声称超时、损坏、库存耗尽或端到端等行为时，测试体里要有构造该行为的步骤，以及针对该行为结果的断言。
2. **断言值来自被测代码**。断言比较的值由被测实现产生；测试自己构造的对象、或策略把输入原样返回的状态，都不算被测代码的结果。

## 占位代码

生产源码中的占位标记由 `tools/Check.ps1` 的 Placeholder scan 拦截，规则见 [开发环境](docs/development.md#自动检查与-pr-范围)。

*其他编码规则待补充。*
