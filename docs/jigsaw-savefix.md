# #76 用户实机反馈修复

修复源码：`ecbdb0c5810f1082b720076252c5fb8aa83e6a3d`。不再交付 ZIP 或 RitsuLib；只交付与目标版本匹配的未压缩 AllMatters 目录，位于 `dist/issue76-savefix/ecbdb0c/`。

## 已确认的两处缺陷

1. 保存解析：旧代码 Harmony detour 闭合泛型 `LoadWithAggressiveRecovery<SerializableRun>`；CLR 共享引用类型泛型代码，使 PrefsSave/ProfileSave/SerializableProgress 也被错误地按跑局类型处理。用户日志在 beta 明确记录 schema2 被按最低13拒绝、progress24 被按当前20拒绝，并移动为 .corrupt。隔离有效偏好存档重现同样行为，移除泛型补丁后转绿。新实现仅观察非泛型跑局加载与实际原生读取，保留真实主/backup来源，不改变原生其他存档解析。
2. 拼图入口：真实角色选择场景根节点 offset_right/offset_bottom 都为0，旧按钮依赖该根节点底部锚定，落到视口外。使用真实资源根尺寸的回归得到red；改为 TopLevel/视口内定位，绑定确认输入可见并生效，两版转绿。

双轴审查检查四路径 staged diff，起点 `bae75d83a13d366a13e40239db7e025161d7e628`，无遗留发现。用户安装目录实际 RitsuLib 为0.6.10；使用该安装的只读引用运行完整双版检查，两目标各281项通过，原始日志保留于 `.local/implementation-76/bugfix-save/`。资源/宿主边界仍然成立，不代签用户再次启动后的实际效果。

## 真实存档保护

没有写入、删除、重命名或覆盖任何真实存档。用户这次被移走的 profile(schema2)、prefs(schema2)、progress(schema24) 文件仍存在；已复制到 `.local/implementation-76/bugfix-save/saved-copies/`。三个副本在隔离Mock IO中通过beta原生解析。**仅修复 Mod 不会自动把这些 .corrupt 文件还原进游戏；本轮未执行真实存档恢复，也没有判定用户新进度可以被覆盖。** 原始副本包含个人进度，不提交或打进Mod目录。

## 更新与测试

完全退出游戏后，由用户将修复版 AllMatters 目录替换旧安装目录；保留已安装的 RitsuLib。必须重启游戏进程，不能在仍运行的旧进程中热换 DLL。旧 `8273ae2` 测试包存在上述存档风险，请勿继续使用。

角色选择页下部左侧应显示“拼图模式”。勾选后以标准单人新局开始，应在 Ancient 奖励之前进入拼图。若仍无入口或出现保存报错，保留最新日志，不继续覆盖进度。此包仍为单人开发测试包，#76的多人/大厅及其他未验边界不因本次修复自动通过。
