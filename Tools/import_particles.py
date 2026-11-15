"""Copy the supplied APK's particle data, textures and meshes without image edits.

Uses the existing UnityPy export; this does not launch Unity or the game.
"""
import json
import shutil
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
EXPORT = Path(r'D:\MiniProject\analysis\LoopSort-4.6.0-20261026/exports')
ROOT = PROJECT / 'Assets/LoopSort/Resources/LoopSort'
EFFECTS = {'complete':4848, 'ice':4867, 'hidden':4849, 'spawn':4890, 'trail':4846}

def rows(name):
    return [x for x in (json.loads(line) for line in (EXPORT/'structure/00_data'/name).read_text(encoding='utf-8').splitlines())
            if x['asset_file']=='resources.assets']

def main():
    systems=rows('ParticleSystem.jsonl');renderers=rows('ParticleSystemRenderer.jsonl')
    hierarchy=[x for x in json.loads((EXPORT/'hierarchy/00_data.json').read_text(encoding='utf-8')) if x['asset_file']=='resources.assets']
    materials=rows('Material.jsonl');material_ids=set();mesh_ids=set();effects=[]
    for name,source_id in EFFECTS.items():
        source=next(x for x in systems if x['path_id']==source_id)
        root=next(x for x in hierarchy if x['path_id']==source['tree']['m_GameObject']['m_PathID'])
        nodes=[]
        for node in hierarchy:
            path=node['hierarchy_path']
            if path!=root['hierarchy_path'] and not path.startswith(root['hierarchy_path']+'/'):continue
            particle=next((x['tree'] for x in systems if x['tree']['m_GameObject']['m_PathID']==node['path_id']),None)
            renderer=next((x['tree'] for x in renderers if x['tree']['m_GameObject']['m_PathID']==node['path_id']),None)
            if renderer:
                material_ids.update(x['m_PathID'] for x in renderer['m_Materials'] if x['m_PathID'])
                if renderer['m_RenderMode']==4 and renderer['m_Mesh']['m_PathID']:mesh_ids.add(renderer['m_Mesh']['m_PathID'])
            nodes.append({'path':'' if path==root['hierarchy_path'] else path[len(root['hierarchy_path'])+1:],
                          'transform':node['transform'],
                          'system':{k:v for k,v in particle.items() if not k.endswith('Module') or v['enabled']} if particle else None,
                          'renderer':renderer})
        nodes.sort(key=lambda n:(n['path'].count('/'),n['path']))
        effects.append({'id':name,'sourceId':source_id,'nodes':nodes})
    output_materials=[]
    for item in materials:
        if item['path_id'] not in material_ids:continue
        tree=item['tree'];props=tree['m_SavedProperties'];floats=dict(props['m_Floats']);colors=dict(props['m_Colors']);texture=dict(props['m_TexEnvs'])['_MainTex']
        output_materials.append({'id':item['path_id'],'name':item['name'],'texture':texture['m_Texture']['m_PathID'],
            'scale':texture['m_Scale'],'offset':texture['m_Offset'],'color':colors['_Color'],
            'srcBlend':floats['_SrcBlend'],'dstBlend':floats['_DstBlend'],'cull':floats['_Cull'],'zWrite':floats['_ZWrite'],
            'lit':floats['_LightingEnabled']>0,'soft':floats['_SoftParticlesEnabled']>0,
            'softNear':floats['_SoftParticlesNearFadeDistance'],'softFar':floats['_SoftParticlesFarFadeDistance'],
            'metallic':floats.get('_Metallic',0),'smoothness':floats.get('_Glossiness',0),'queue':tree['m_CustomRenderQueue']})
    for folder,ids,extension,target in [('Texture2D',{x['texture'] for x in output_materials if x['texture']},'png','Textures'),
                                        ('Mesh',mesh_ids,'obj','Models')]:
        for source_id in sorted(ids):
            matches=list((EXPORT/folder).glob('*__00_data_90744c19_'+str(source_id)+'.'+extension))
            if len(matches)!=1:raise RuntimeError(f'Expected one {folder} {source_id}, found {len(matches)}')
            prefix='particle-' if folder=='Texture2D' else ''
            destination=ROOT/target/(prefix+str(source_id)+'.'+extension)
            destination.parent.mkdir(parents=True,exist_ok=True)
            if not destination.exists() or destination.read_bytes()!=matches[0].read_bytes():shutil.copyfile(matches[0],destination)
    output=ROOT/'Animations/source-particles.json';output.parent.mkdir(parents=True,exist_ok=True)
    output.write_text(json.dumps({'source':'Loop Sort 4.6.0 resources.assets','effects':effects,'materials':output_materials},separators=(',',':')),encoding='utf-8')
    print(json.dumps({'effects':list(EFFECTS),'meshes':sorted(mesh_ids),'textures':sorted(x['texture'] for x in output_materials if x['texture'])}))

if __name__=='__main__':main()
