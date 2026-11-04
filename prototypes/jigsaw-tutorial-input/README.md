# 手柄输入比较：一次性原型（B方案已选定）

来源：[明确拼图教程、正式界面提示与输入可达性要求](https://github.com/Endercloud001/AllMatters/issues/71)。

## 具体问题

虚拟指针与区域焦点＋网格光标，哪一种更便于用手柄拿放、精确连拆线、操作镜头和切换区域？只回答这个输入问题，不预先决定正式界面布局，不实现完整玩法。两种模式保留同一布局，可随时切换。

## 运行

双击本目录 `input-prototype.html`。若浏览器不发现手柄，在工作树根目录执行：

```powershell
python -m http.server 8771 --bind 127.0.0.1 --directory prototypes/jigsaw-tutorial-input
```

打开 `http://127.0.0.1:8771/input-prototype.html?variant=grid` 或 `?variant=pointer`，先按手柄任意按钮。使用浏览器标准映射；非标准设备须核对实际按键。所有状态只在内存中。

- A：拿取、放下、激活当前按钮。
- X：持有时逆时针旋转，空手时选两端连线／拆线。
- B：取消持有或待连端点；帮助内返回。
- Y：持有时归还池。
- LB／RB：切换碎块池、地图、操作区。
- 左摇杆／方向键：虚拟指针连续移动／微调；网格模式空手按方向选节点，持有时逐格移动。
- 右摇杆：镜头平移；LT／RT：缩放。
- Start：帮助；Select：切方案。

原有键鼠操作保留，右键状态分工不变。帮助内列出实验键盘模拟，仅用于检查，不将纯键盘加入正式支持范围。

点击“引导场景”依次体验：拿放取消、精确连拆线、帮助与确认、边缘与镜头。两人仅本机切换模拟；自动按钮只到已有共识入口，不运行自动生成。第二名模拟玩家不会自动确认。

## 来源与构建

- 基页、素材与几何编辑模型：`prototype/jigsaw-interaction`，e3f2887，`prototypes/jigsaw-interaction/interaction-prototype.html`。
- 最新受控池与完整见证：`prototype/jigsaw-auto-budget`，8c570ff，`prototypes/jigsaw-auto/controlled.json`；本目录 `fixture.json` 为原样内容重新排版。没有复用旧房型池或旧四块见证。
- `input-adapter.js` 只替换原型初始输入并添加导航比较；复用原有拿放、旋转、取消与连拆线语义。已有模型检查不能代替最新完整自动独立验收。
- `python prototypes/jigsaw-tutorial-input/build.py` 重新生成单文件。不要并入正式功能。

## 已检查与未验证

2026-11-04：Node/Python语法检查通过；Chrome无界面浏览器在1500×1000运行无脚本错误或页面溢出。复用六块见证，池内余8块。浏览器操作拿取、旋转、帮助取消持有、确认票保留通过；标准模拟手柄通过实际 `navigator.getGamepads` 轮询路径拿取、旋转、打开／关闭帮助、切方案及摇杆移动。

这些是Agent浏览器证据，实体手柄用户反馈仍待完成。没有验证正式Godot UI、双版输入适配、原生网络、存档、先古之民奖励或探索界面。本页不是完整输入支持承诺的验收工具。正式功能与完整剧情、美术均未制作。

用户决定及各项状态统一记录在 `SESSION.md`，完整待确认要求见 `REQUIREMENTS-DRAFT.md`。

2026-11-05更新：用户选择B，明确本人无法参与实体手柄测试，由Agent衡量为主。不再等待用户实体设备体验，不把模拟检查写成实体或正式Godot通过。B样例25个节点全部方向可达，连拆线、移动取消恢复、返回不撤票及帮助关闭检查通过，见 `AGENT-INPUT-CHECK.json`。空手返回仅标示原版暂停菜单入口，本比较页不实现暂停菜单；原版继续、弹窗及探索需正式接入后验收。A保留为比较历史。

2026-11-05用户完整确认要求并授权resolution/关闭；来源票的resolution是最终状态入口。实体手柄及正式游戏验收限制保留。
