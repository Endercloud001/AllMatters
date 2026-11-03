# 时序静态探针

输入 Probe.ps1；在研究工作树运行：

```powershell
./docs/research/experiments/hunting-timing/Probe.ps1 -Repository ../AllMatters
```

result.txt记录两版通过，退出0。输入为已有真实DLL导出的RunManager、CombatManager、RelicCmd文本，来源版本见总笔记。断言只检验已阅读方法体中的相对次序，不验证游戏执行、网络、恢复、补丁挂载或奖励行为。
