from pathlib import Path
p=Path('AllMatters_docs/jigsaw-visual-detail/round2.js')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('const ns=layouts[i%3];return','const ns=layouts[i%3],xs=ns.map(n=>n[0]),ys=ns.map(n=>n[1]),cx=(Math.min(...xs)+Math.max(...xs))/2,cy=(Math.min(...ys)+Math.max(...ys))/2;return')
s=s.replace('viewBox="-1 -1 9 7"','viewBox="${cx-5} ${cy-2.35} 10 4.7"')
s=s.replace('`-3 -6 ${26/R2.zoom} ${14/R2.zoom}`','`${(R2.double?9:7)-13/R2.zoom} ${-1-7/R2.zoom} ${26/R2.zoom} ${14/R2.zoom}`')
p.write_text(s,encoding='utf-8')
