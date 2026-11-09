from PIL import Image
from pathlib import Path
p=Path('AllMatters_docs/jigsaw-visual-detail/assets')
for n in ['dialogue_nine_patch','dialogue_tail']:
 im=Image.open(p/(n+'.png')).convert('RGBA'); tint=Image.new('RGBA',im.size,(40,69,79,255));tint.putalpha(im.getchannel('A'));tint.save(p/(n+'-tinted.png'))
