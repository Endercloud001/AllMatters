# 哨卫激光特效可行性核查

2026-10-27。结论：**可以加入，而且不需要修改哨卫 Spine 文件或战斗规则。** 一代激光是攻击行动额外创建的二维特效，并非 `attack` 骨骼动画自身带出的光束。调查时 AllMatters 仅播放攻击动作及原生命中划痕，因此不会呈现一代的连接光束。本文保留实现前的只读调查结论；用户批准后的实现、自动化结果与人工验证步骤见 [丛林迷宫验证记录](../testing/jungle-maze.md)。未宣称实机外观已验证。

## 一代原件证据

来源是本机正版 `E:/SteamLibrary/steamapps/common/SlayTheSpire/desktop-1.0.jar`，通过 `javap -c -p` 读取方法字节码与 Python zipfile 读取资源条目，不执行游戏类、不启动游戏。原始输出保留在 Git 忽略目录：

- `.local/sentry-sts1.javap.txt`：`com.megacrit.cardcrawl.monsters.exordium.Sentry`。
- `.local/sentry-laser-sts1.javap.txt`：`com.megacrit.cardcrawl.vfx.combat.SmallLaserEffect`。
- `.local/sentry-soundmaster-sts1.javap.txt`：`com.megacrit.cardcrawl.audio.SoundMaster`。

可复现命令（将最后类名替换为上述完整类名）：

```powershell
javap -classpath 'E:/SteamLibrary/steamapps/common/SlayTheSpire/desktop-1.0.jar' -c -p com.megacrit.cardcrawl.monsters.exordium.Sentry
```

| 事实 | 原件定位 |
| --- | --- |
| 攻击行动依次排入 `ChangeStateAction("ATTACK")`、`SFXAction("ATTACK_MAGIC_BEAM_SHORT", 0.5f)`、天蓝色 `BorderFlashEffect`、`SmallLaserEffect`，再执行伤害 | `Sentry.takeTurn` 字节码偏移 209–418 |
| 光束端点来自玩家与哨卫各自的 hitbox 中心；快速模式的 VFXAction 等待是 0.1 秒，普通模式是 0.3 秒 | `Sentry.takeTurn` 偏移 265–379 |
| 伤害单独执行，`AttackEffect.NONE`；并不依赖光束特效本身造成伤害 | `Sentry.takeTurn` 偏移 382–414 |
| 激光从 `ImageMaster.vfxAtlas` 读取 `combat/laserThin`，总生命周期 0.5 秒 | `SmallLaserEffect` 构造偏移 4–88 |
| 端点决定距离与角度；青色主光束叠加偏蓝副光束，以加色混合渲染；主宽度 50，副宽度在 50–90 间变化，并有轻微随机抖动 | `SmallLaserEffect` 构造与 `render` 方法（混合因子 770/1） |
| 前半段以 pow2In 淡入，后半段以 bounceIn 淡出，时间结束标记 isDone | `SmallLaserEffect.update` 偏移 17–95 |
| 短光束音效实际文件为 `audio/sound/SOTE_SFX_SlowMagic_BeamShort_v1.ogg` | `SoundMaster` 初始化偏移 367–372；jar 条目核实 |

原始图集条目也已核实：`vfx/vfx.atlas` 的页面 `vfx2.png`（2048×1024）包含 `combat/laserThin`；非旋转，xy=(2,104)，packed size=(981,101)，orig=(1920,156)，offset=(934,28)。应按图集的裁剪和偏移语义转换，不能只假定它是独立满画幅图片。

## 本模组接入判断

1. **首选：独立 Godot 短光束效果。** 用加色 Sprite2D/着色器或 Line2D 复现青/蓝双层光束，约 0.5 秒自动释放；从哨卫中心指向每个存活玩家的视觉中心。由于本票已确认攻击全体，多人必须向每个实际受击玩家显示光束，不能照搬一代单人目标或参考移植中仅取第一个玩家的实现。
2. **通过现有攻击命令接入。** 二代 `AttackCommand.BeforeDamage(Func<Task>)` 与 `WithAttackerFx(Func<Node2D?>)` 提供接入位置；`NCombatRoom.GetCreatureNode(...).VfxSpawnPosition` 可得到视觉坐标。节点只负责呈现与自行释放，伤害继续使用已验证的原生命令。可保留当前攻击动作的固定前摇，不等待视觉节点“完成”信号，不以视觉 RNG 消耗玩法 RNG。房间或角色视觉节点不存在时跳过呈现，避免重现特效异常阻塞回合的问题。
3. **不需要额外动画转换。** 当前 Spine 的 `attack`、`idle` 足够，缺的是攻击过程中的额外二维效果。独立 Godot 实现可以同时面向 public/public-beta；两目标仍需资源加载、效果生命周期和多人端点测试。
4. **原生效果可复用但不等同于一代。** 二代 `NSweepingBeamVfx.Create(owner, targets)` 能形成扫射光束，但视觉与一代短脉冲不同；`NLaserVfx` 是持续伸缩的红色激光，不宜不加说明直接当成一代青色短激光。推荐按一代的样式新建小效果，而非依赖一个猜测存在的资源路径。
5. **素材与音效可取自已拥有的一代原包。** 需要时只导出指定图集区域和短光束音效，保留 Mega Crit/原作者权属并补充现有 THIRD_PARTY_NOTICES；也可自行绘制程序化光束，避免携带整张图集。没有必要打包整个 jar、vfx 图集或 Java 实现。

二代接口依据：本地真实 DLL 导出 `.local/api/sentry-laser-beta/` 中 `NLaserVfx`、`NSweepingBeamVfx`、`NHyperbeamVfx`、`Cards.SweepingBeam`；public `NLaserVfx` 导出位于 `.local/api/sentry-laser-public/`，public `AttackCommand.BeforeDamage(Func<Task>)` 已查询真实 DLL 签名。`Cards.SweepingBeam.OnPlay` 展示了在攻击前创建视觉效果的原生调用方式。以上接口证明存在接入能力，不等于自定义效果已实现或两版实机外观已验证。

另有移植参考：`.local/references/ActsFromThePast/ActsFromThePast/Effects/SmallLaserEffect.cs` 与 `Acts/Exordium/Enemies/Sentry.cs` 已用 Godot 实现类似效果，支持技术可行性判断；这属于第三方移植参考，不代替上述一代原包证据，也未复制其代码。仓库根未发现 LICENSE，建议在 AllMatters 中依据原件行为自行实现小型效果。

## 后续验证重点

自动化应覆盖真实攻击路径选中的光束资源、两端坐标与多人数量、0.5 秒后释放、缺少房间/视觉节点时安全跳过、连续两轮仍能推进且保持 9 伤和两张晕眩。实机需观察光束从哨卫核心出发、命中玩家位置、透明度与音效时机，以及快速模式和多人布局；无界面行为宿主不能替代这些外观验收。
