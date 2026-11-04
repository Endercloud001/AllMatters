"""Throwaway input comparison; reuses accepted manual page and latest auto pool."""
import json
from pathlib import Path

here = Path(__file__).resolve().parent
root = here.parent.parent
source = root / 'prototypes/jigsaw-interaction/interaction-prototype.html'
fixture_path = here / 'fixture.json'
fixture = json.loads(fixture_path.read_text(encoding='utf-8'))
adapter = (here / 'input-adapter.js').read_text(encoding='utf-8')
html = source.read_text(encoding='utf-8')
html += '\n<script>const inputFixture=' + json.dumps(fixture, ensure_ascii=False) + ';\n' + adapter + '\n</script>\n'
(here / 'input-prototype.html').write_text(html, encoding='utf-8')
print('Built single-file input-prototype.html')
