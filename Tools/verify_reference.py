"""Independent validation of the imported 4.6.0 catalog and asset hashes."""
import collections
import hashlib
import json
from pathlib import Path

root=Path(__file__).resolve().parents[1]
manifest=json.loads((root/'Documentation/ReferenceImport.json').read_text('utf-8'))
for item in manifest['files']:
    target=root/item['target']
    assert target.is_file(),str(target)
    assert hashlib.sha256(target.read_bytes()).hexdigest()==item['sha256'],str(target)
catalog=json.loads((root/'Assets/LoopSort/Resources/LoopSort/Data/main.json').read_text('utf-8'))
assert catalog['version']=='4.6.0'
assert len(catalog['levels'])==1299
assert [x['id'] for x in catalog['levels']]==list(range(1,1300))
supported=[]
for level in catalog['levels']:
    if level['unsupported']:continue
    assert len(level['points'])>=3
    assert len({p['order'] for p in level['points']})==len(level['points'])
    assert not any(p['order']==180 for p in level['points'])
    assert len({c['id'] for c in level['carriers']})==len(level['carriers'])
    colors=collections.Counter(group['color'] for carrier in level['carriers'] for group in carrier['groups'])
    assert all(count%4==0 for count in colors.values()),level['id']
    assert all(len(c['groups'])<=4 for c in level['carriers'])
    supported.append(level['id'])
assert all(i in supported for i in range(1,20))
assert len(catalog['levels'][0]['points'])==4
assert len(catalog['levels'][32]['carriers'])==9
result={'version':'4.6.0','catalogRecords':1299,'supportedLayouts':len(supported),'supportedIds':supported,
        'assetHashesChecked':len(manifest['files']),'status':'passed'}
destination=root/'Documentation/Validation/import-verification.json'
destination.parent.mkdir(parents=True,exist_ok=True)
destination.write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result))
