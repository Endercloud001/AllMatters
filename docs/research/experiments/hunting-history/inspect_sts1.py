import json, re, sys, zipfile
sys.stdout.reconfigure(encoding='utf-8')
with zipfile.ZipFile(sys.argv[1]) as z:
    loc = json.loads(z.read('localization/zhs/monsters.json'))
    print('identity:', loc['Healer']['NAME'], '/', loc['TorchHead']['NAME'])
    for creature in ['healer','torchHead']:
        base = 'images/monsters/theCity/' + creature + '/'
        info = json.loads(z.read(base + 'skeleton.json'))
        print(creature, 'spine=', info['skeleton'].get('spine'), 'animations=', sorted(info['animations']))
        print('entries=', sorted(n for n in z.namelist() if n.startswith(base)))
    versions = [s.decode('ascii') for s in re.findall(rb'[\x20-\x7e]{5,}', z.read('com/megacrit/cardcrawl/core/CardCrawlGame.class')) if b'V2.3.4' in s]
    print('version constants=', versions)
