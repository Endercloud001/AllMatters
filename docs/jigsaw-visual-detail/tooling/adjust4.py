from pathlib import Path
p=Path('AllMatters_docs/jigsaw-visual-detail/round4.js');s=p.read_text(encoding="utf-8-sig");s='\n'.join(l for l in s.splitlines() if not l.startswith('const originalScene='));s=s.replace("const R4=", "document.getElementById('dense')?.closest('label').remove();\nconst R4=")
s=s.replace('<option value="0">刚耗尽</option>','<option value="0">刚耗尽</option><option value="none0">0步 / 提示已消失</option>');s=s.replace('${R4.step}</strong>','${R4.step===\'none0\'?0:R4.step}</strong>');s=s.replace("R4.step==='1'?", "R4.step==='none0'?'':R4.step==='1'?")
s=s.replace("if(exploring){", "fitMap();if(exploring){")
s += '''
function fitMap(){const svg=document.getElementById('map'),bb=svg.getBBox(),pad=.6,z=Math.min(1,R2.zoom);svg.setAttribute('viewBox',`${bb.x-pad-(bb.width+pad*2)*(1/z-1)/2} ${bb.y-pad-(bb.height+pad*2)*(1/z-1)/2} ${(bb.width+pad*2)/z} ${(bb.height+pad*2)/z}`)}
window.addEventListener('resize',fitMap);
'''
p.write_text(s,encoding="utf-8")

