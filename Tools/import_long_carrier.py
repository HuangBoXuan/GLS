"""Extract the supplied APK's long truck, including its stretch/attachment bindings."""
import json
import shutil
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
SOURCE=Path('D:/MiniProject/analysis/LoopSort-4.6.0-20261026')
OUT=ROOT/'Assets/LoopSort/Resources/LoopSort'

def rows(kind):
    return [json.loads(line) for line in (SOURCE/f'exports/structure/00_data/{kind}.jsonl').read_text(encoding='utf-8').splitlines()]

def main():
    nodes=json.loads((SOURCE/'exports/hierarchy/00_data.json').read_text(encoding='utf-8'))
    # The original regular-truck importer omitted the two animated doors outside
    # Model Root. Add those nodes without regenerating unrelated artwork/data.
    regular_path=OUT/'Data/carrier-visual.json'
    regular=json.loads(regular_path.read_text(encoding='utf-8'))
    door_names={'Carrier/Pivot/TopPart1','Carrier/Pivot/TopPart2V2'}
    doors={n['path_id']:n for n in nodes if n['asset_file']=='resources.assets' and n['hierarchy_path'] in door_names}
    regular['nodes']=[n for n in regular['nodes'] if n['path'] not in door_names]
    regular['parts']=[p for p in regular['parts'] if p['path'] not in door_names]
    regular['nodes'] += [dict(path=n['hierarchy_path'],transform=n['transform']) for n in doors.values()]
    for row in rows('SkinnedMeshRenderer'):
        tree=row['tree'];node=doors.get(tree['m_GameObject']['m_PathID'])
        if row['asset_file']=='resources.assets' and node:
            regular['parts'].append(dict(path=node['hierarchy_path'],mesh=tree['m_Mesh']['m_PathID'],wheel=False,materialIds=[m['m_PathID'] for m in tree['m_Materials']]))
    regular_path.write_text(json.dumps(regular,separators=(',',':')),encoding='utf-8')
    nodes={n['path_id']:n for n in nodes if n['asset_file']=='resources.assets' and n['hierarchy_path'].startswith('Long Carrier/')}
    bindings={}
    for kind in ['Transform','RectTransform','MeshRenderer','SkinnedMeshRenderer']:
        for r in rows(kind):
            if r['asset_file']=='resources.assets' and r['tree']['m_GameObject']['m_PathID'] in nodes:
                bindings[r['path_id']]=nodes[r['tree']['m_GameObject']['m_PathID']]['hierarchy_path'].replace('Long Carrier','Carrier',1)
    config=next(r['tree'] for r in rows('MonoBehaviour.recovered') if r.get('class')=='LongCarrier')
    filters={r['tree']['m_GameObject']['m_PathID']:r['tree'] for r in rows('MeshFilter') if r['asset_file']=='resources.assets'}
    parts=[]
    for r in rows('MeshRenderer')+rows('SkinnedMeshRenderer'):
        tree=r['tree'];node=nodes.get(tree['m_GameObject']['m_PathID'])
        if r['asset_file']!='resources.assets' or node is None:continue
        path=node['hierarchy_path']
        if not (path.startswith('Long Carrier/Pivot/Model Root') or path in ['Long Carrier/Pivot/TopPart1','Long Carrier/Pivot/TopPart2V2']):continue
        mesh=tree.get('m_Mesh') or filters.get(node['path_id'],{}).get('m_Mesh')
        if not mesh or mesh['m_FileID']!=0:continue
        parts.append(dict(path=path.replace('Long Carrier','Carrier',1),mesh=mesh['m_PathID'],wheel='Wheel' in path,
            materialIds=[m['m_PathID'] for m in tree['m_Materials']]))
    required={p['path'] for p in parts}
    for part in list(required):
        while '/' in part:
            part=part.rsplit('/',1)[0]
            if part!='Carrier':required.add(part)
    data=dict(parts=parts,nodes=[dict(path=n['hierarchy_path'].replace('Long Carrier','Carrier',1),transform=n['transform']) for n in nodes.values() if n['hierarchy_path'].replace('Long Carrier','Carrier',1) in required])
    for field in ['FrontParts','MiddleParts','StretchParts']:
        data[field[0].lower()+field[1:]]=[bindings[p['m_PathID']] for p in config[field] if p['m_PathID'] in bindings]
    collider=next(r['tree'] for r in rows('BoxCollider') if r['asset_file']=='resources.assets' and r['path_id']==4786)
    data['colliderCenter']=dict(collider['m_Center']);data['colliderCenter']['z']-=2.3
    data['colliderSize']=collider['m_Size']
    data['outlet']=next(n['transform']['m_LocalPosition'] for n in nodes.values() if n['hierarchy_path']=='Long Carrier/Transfer Project Point')
    needed={p['mesh'] for p in parts}
    for row in json.loads((SOURCE/'analysis/unity/00_data-inventory.json').read_text(encoding='utf-8')):
        if row['type']!='Mesh' or row['asset_file']!='resources.assets' or row['path_id'] not in needed:continue
        output=next(o for o in row['outputs'] if o['path'].endswith('.obj'))
        shutil.copyfile(SOURCE/output['path'],OUT/f'Models/{row["path_id"]}.obj')
        needed.remove(row['path_id'])
    if needed:raise ValueError(f'Missing source meshes: {needed}')
    (OUT/'Data/long-carrier-visual.json').write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
    print(f'Imported long truck: {len(parts)} parts, source stretch bindings retained.')

if __name__=='__main__':main()
