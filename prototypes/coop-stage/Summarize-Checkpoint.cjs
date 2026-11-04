// Evidence completeness check only; does not rerun native/model/browser experiments.
const fs = require('node:fs');
const path = require('node:path');
const directory = process.argv[2] || path.join(__dirname, 'checkpoint-evidence');
const observations = JSON.parse(fs.readFileSync(path.join(directory, 'observations.json'), 'utf8').replace(/^\uFEFF/, ''));
const commands = JSON.parse(fs.readFileSync(path.join(directory, 'commands.json'), 'utf8').replace(/^\uFEFF/, ''));
const scenarios = ['normal', 'missing-main', 'native-invalid', 'native-invalid-rename-fails', 'write-fails'];
if (observations.length !== 80 || commands.length !== 2 || commands.some(command => command.exitCode !== 0))
  throw Error('Incomplete native evidence or failed command; inspect original logs.');
for (const target of ['public', 'public-beta'])
  for (const multiplayer of [false, true])
    for (const synchronous of [false, true])
      for (const adapted of [false, true])
        for (const scenario of scenarios) {
          const group = observations.filter(row => row.target === target && row.multiplayer === multiplayer &&
            row.synchronous === synchronous && row.adapted === adapted && row.scenario === scenario);
          if (group.length !== 1) throw Error(`Missing/duplicate native case: ${JSON.stringify({target,multiplayer,synchronous,adapted,scenario})}`);
          const row = group[0], fallback = !['normal', 'write-fails'].includes(scenario);
          if (!row.nativeMatches || row.modMatches !== (adapted || !fallback)) throw Error('Unexpected native/Mod result.');
          if (scenario === 'write-fails' && (!row.writeError || row.savedEvents || row.latest !== 'B'))
            throw Error('Write failure evidence does not preserve B.');
        }
console.log(JSON.stringify({
  purpose: 'verify archived evidence completeness; no new behavioral experiment',
  observations: observations.length,
  originalCounterexamples: observations.filter(row => !row.adapted && !row.modMatches).length,
  adaptedMatching: observations.filter(row => row.adapted && row.modMatches).length,
  commands
}, null, 2));
