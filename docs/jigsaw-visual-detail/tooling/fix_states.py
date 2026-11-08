from pathlib import Path
p=Path('AllMatters_docs/jigsaw-visual-detail/round3.js');s=p.read_text(encoding='utf-8-sig');s=s.replace("svg.insertAdjacentHTML('beforeend',h);", "if(S3.scene==='focus')h+=rIcon([3,0,'monster']);\nif(S3.scene==='held'){[...svg.querySelectorAll('image')].filter(e=>!e.getAttribute('href').includes('map_dot')).slice(3,7).forEach(e=>e.style.opacity='.65')}\nsvg.insertAdjacentHTML('beforeend',h);if(S3.scene==='explore')document.getElementById('speech').textContent='还可前行9步。';")
p.write_text(s,encoding='utf-8')
