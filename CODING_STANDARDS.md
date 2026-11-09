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

*其他编码规则待补充。*
