from PIL import Image,ImageDraw
from pathlib import Path
p=Path('AllMatters_docs/jigsaw-visual-detail'); fs=list((p/'assets').glob('character_icon_*.png'))+list((p/'assets').glob('map_circle_*.png'))+[p/'assets/map_monster_outline.png']; out=Image.new('RGB',(1000,((len(fs)+3)//4)*160),'#344550');d=ImageDraw.Draw(out); rows=[]
for i,f in enumerate(fs):
 im=Image.open(f).convert('RGBA');rows.append(f'|{f.name}|{im.width}×{im.height}|{im.getbbox()}|');im.thumbnail((110,110));x=(i%4)*250;y=(i//4)*160;out.paste(im,(x+70,y+5),im);d.text((x+5,y+125),f.stem,fill='white')
out.save(p/'state-assets.png')
Path('AllMatters_docs/research/jigsaw-round3-states.md').write_text('''# 第三轮状态资源调查

资源来自本机beta 0.111.0缓存，公共版与正式渲染未验证。导出脚本：../jigsaw-visual-detail/tooling/state_extract.gd。预览：../jigsaw-visual-detail/state-assets.png。

头像来源 res://images/ui/top_panel/character_icon_{角色}.png（outline同目录）；访问圈 res://images/atlases/compressed.sprites/map/map_circle_0..4.tres；普通怪物轮廓 res://images/atlases/compressed.sprites/map/map_monster_outline.tres。图集资源按region导出，未裁掉透明边缘。

原生 NMultiplayerVoteContainer 第126–127行给票图设置 player.Character.IconTexture 与 IconOutlineTexture；本地头像缓存用于示例，不推导正式玩家身份。NNormalMapPoint._Process 使用 sin(elapsed)*0.25+1.2，聚焦时父容器回到1；不能简单将脉动峰值与悬停缩放永远叠乘。已访问圈与角色头像、焦点是不同层。

HTML状态是预设展示，不计算资格或投票；尺寸和避让为提案。真实原生分歧动画、完整特殊节点动画、网络与输入均未验证。

|文件|原始尺寸|alpha边界（含透明阴影，非视觉中心）|
|---|---|---|
'''+ '\n'.join(rows),encoding='utf-8')
