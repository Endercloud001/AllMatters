# 四态HUD独立显示验收包（尚无用户结果）

此包只挂一块真实游戏UI上的提示面板，按Ctrl+F8循环四份明确标记的示例输入。它不扣步、不发面具、不改战斗和服务、不保存状态。用途是回答提示是否分得清、是否遮挡或抢焦点；它不能证明追猎状态集成已完成。没有包含上游美术或AFTP DLL，也不需要AFTP资源包。

## 产物与已验证范围

在工作树运行 `./prototypes/hunting-boundaries/presentation/Build.ps1`，只输出开发目录，由用户手动安装和启动游戏：

- beta v0.111.0：`E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters_prototype70/dist/public-beta/AllMattersHuntingPresentationProbe/`
- public v0.107.1：`E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters_prototype70/dist/public/AllMattersHuntingPresentationProbe/`

各目录必须有同名 `.dll` 和 `.json` 两个文件，无PCK。依赖为项目既定 [RitsuLib作者发布](https://github.com/BAKAOLC/STS2-RitsuLib/releases)中的0.6.7完整包（与现有AllMatters相同依赖；保留variants/shared内容，不能只取编译引用DLL）。依赖本机来源见 `Paths.local.props` 的RitsuLibDir；不要把游戏/测试输出中的GodotSharp复制入mods。

两个目标已经实际在Godot宿主加载这个独立DLL、调用初始化、完成Ritsu原生脚本注册及安装NGlobalUi._Ready后缀钩子。共享显示代码已在1280×720、1920×1080、2560×1440、3440×1440的真实Godot子视口运行四态，检查矩形范围、原有控件焦点保持和MouseFilter=Ignore。没有启动游戏，不把上述运行冒充完整NGlobalUi.Ready或实机显示通过。

## 用户操作与预期

1. 选与正在运行的游戏版本匹配的目录，将整个独立目录放入你自行管理的mods目录。不要覆盖正式AllMatters目录。确保已有RitsuLib完整依赖。由你手动启动游戏，进入测试跑局。
2. UI右上方应出现“追猎显示实验·示例数据”。Ctrl+F8依次循环：剩1步/最后一步；剩0步/本房安全、全队完成后给面具；剩0步/追猎生效；剩0步/已按时抵达Boss。四态含义应互不混淆，正文不截断。
3. 在地图、商店、休息、战斗中查看面板；在常用分辨率及至少一个窄屏/宽屏模式复查。检查与顶栏、地图按钮、卡牌/意图、弹窗的重叠。当前位置是可改的实验位置，不是已选定的最终布局。
4. 面板显示前后分别使用鼠标、键盘、手柄切换/确认原游戏控件，打开并关闭地图和弹窗；面板不得抢走焦点或拦截其覆盖区域的鼠标操作。Ctrl+F8只切示例，不应改变原游戏流程。
5. 记录游戏完整版本、分辨率/窗口模式、输入设备、四态可读性、出现遮挡的具体界面和焦点问题。有问题时记录对应画面；游戏日志可由Agent用项目只读日志工具读取。
6. 结束后可由你禁用或移除此独立显示包。它没有状态存储，也没有把面具或展示牌写入跑局。

## 尚未交付的其他验收资产

本包没有五虫、女王、原生选择屏或空箱动画的游戏内场景操控入口。不能要求用户仅靠它完成这些验收。其对应的最小场景实验资产和必要Agent补验仍需继续准备；不把“装此包看HUD”写成整票完成条件已满足。

用户结果：待回填。此记录不替代多人验证票的原生联网/检查点恢复结果。
