"""Import only the supplied 4.6.0 extraction; never reads the sibling game project."""
import hashlib
import json
import math
import re
import shutil
import argparse
from pathlib import Path

PROJECT=Path(__file__).resolve().parents[1]
REFERENCE=Path(r'D:\MiniProject\analysis\LoopSort-4.6.0-20261026')
OUT=PROJECT/'Assets/LoopSort/Resources/LoopSort'

def save(path,value):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(value,ensure_ascii=False,separators=(',',':')),encoding='utf-8')

def corrected_feature_members(level_id, feature, identifiers):
    # Source 544's IceCon list includes J, which spline 544 places in the
    # upper-left row, outside the lower-row chain. Keep that cart playable.
    # Limit this authored-data correction to the exact affected main-level row.
    if level_id == 548 and feature == 'icecon' and identifiers == ['F','H','I','J','K']:
        return ['F','H','I','K']
    return identifiers


def guide_first_levels(levels):
    first={}
    for level in sorted(levels,key=lambda row:row['id']):
        ids=set()
        if not level.get('closed',True):ids.add(20)
        for carrier in level['carriers']:
            for field,identifier in [('curtainColor',21),('barrier',2),('iceRequires',4),('lockColors',23),
                                     ('ropeLocks',25),('carpetLocks',26),('tagColor',24),('generator',27)]:
                if carrier.get(field):ids.add(identifier)
            for group in carrier.get('groups',[]):
                if group.get('hidden'):ids.add(22)
                if group.get('sticky'):ids.add(28)
                if group.get('keyColor'):ids.add(23)
        for identifier in ids:first.setdefault(identifier,level['id'])
    return first

def write_guide_levels(levels):
    first=guide_first_levels(levels)
    destination=PROJECT/'Assets/StreamingAssets/LoopSort/lua/game/guide_levels.lua'
    destination.parent.mkdir(parents=True,exist_ok=True)
    destination.write_text('-- Derived from the imported APK main-level catalog.\nreturn {\n'+
        ''.join(f'    [{identifier}]={level},\n' for identifier,level in sorted(first.items()))+'}\n',encoding='utf-8')
    return first

def sampled_spline_length(points,closed,subdivide):
    """Dreamteck BSpline at sampleRate 8, matching Runtime/NativeSpline.cs."""
    controls=[(p['x'],p.get('y',0),p['z']) for p in points]
    if len(controls)<2:return 0
    for _ in range(subdivide):
        expanded=[]
        for i,point in enumerate(controls):
            expanded.append(point)
            if closed or i+1<len(controls):
                expanded.append(tuple((a+b)*.5 for a,b in zip(point,controls[(i+1)%len(controls)])))
        controls=expanded
    def control(i):
        if closed:return controls[i%len(controls)]
        if i<0:return tuple(a+(b-a)*i for a,b in zip(controls[0],controls[1]))
        if i>=len(controls):return tuple(b+(b-a)*(i-len(controls)+1) for a,b in zip(controls[-2],controls[-1]))
        return controls[i]
    intervals=7*(len(controls) if closed else len(controls)-1)
    previous=None;distance=0
    for i in range(intervals+1):
        value=i/intervals*(len(controls) if closed else len(controls)-1)
        segment=math.floor(value);t=value-segment;t2=t*t;t3=t2*t
        weights=(1-3*t+3*t2-t3,4-6*t2+3*t3,1+3*t+3*t2-3*t3,t3)
        point=tuple(sum(control(segment+j-1)[axis]*weight for j,weight in enumerate(weights))/6 for axis in range(3))
        if previous is not None:distance+=math.dist(previous,point)
        previous=point
    return distance

def main(levels_only=False):
    enums=json.loads((REFERENCE/'analysis/metadata-enums.json').read_text('utf-8'))
    aliases={k.lower():v for k,v in enums['BaseColor'].items()}
    feature_aliases={k.lower():v for k,v in enums['FeatureType'].items()}
    carriers=set(enums['CarrierSheet.CarrierType'])-{'None'}
    imports=[]
    def copy(source,target):
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(source,target)
        imports.append({'source':str(source.relative_to(REFERENCE)),'target':target.relative_to(PROJECT).as_posix(),
                        'sha256':hashlib.sha256(source.read_bytes()).hexdigest()})
    def color(token):
        token=token.strip();shade=0
        if 'light' in token.lower():shade=1;token=re.sub('light','',token,flags=re.I)
        elif 'dark' in token.lower():shade=2;token=re.sub('dark','',token,flags=re.I)
        elif token[:1].lower()=='l':shade=1;token=token[1:]
        elif token[:1].lower()=='d':shade=2;token=token[1:]
        base=aliases.get(token.lower())
        if base is None:raise ValueError('Unrecognized source color '+repr(token))
        return base+shade*100
    catalogs={}
    for scope in ['main','daily-challenge']:
        levels=[]
        for p in sorted((REFERENCE/'exports/levels'/scope).glob('[0-9]*.json')):
            source=json.loads(p.read_text('utf-8'));level=source['level'];spline=source['spline'];carrier=source['carrier']
            stacks={};unsupported=set();encoded={}
            for segment in carrier['ColorData'].split(':'):
                fields=segment.split(';');groups=[]
                for raw in fields[1:]:
                    if not raw or raw.startswith('-'):continue
                    base,_,features=raw.partition('_')
                    group={'color':0,'hidden':False,'sticky':False,'keyColor':0,'raw':raw}
                    for entry in features.splitlines():
                        feature,_,argument=entry.strip().partition('_');feature=feature.lower()
                        if feature in ['h','hidden']:group['hidden']=True
                        elif feature=='sticky':group['sticky']=True
                        elif feature in ['k','key'] and argument:group['keyColor']=color(argument)
                        # ParseColors 0x25036D8/0x25037C4 uses Enum.TryParse and
                        # silently skips unknown values. In particular G is not
                        # an alias of Glass in this build (main 450 and 972).
                        elif feature in feature_aliases and feature not in ['','n','none']:unsupported.add('block:'+entry.strip())
                    try:group['color']=color(base);groups.append(group)
                    except ValueError:unsupported.add('color:'+base)
                stacks[fields[0]]=groups
            feature_data={}
            if carrier['Features'] not in ['', '-']:
                for line in carrier['Features'].splitlines():
                    if not line or line.startswith('-'):continue
                    ids,separator,definition=line.strip().partition('-')
                    feature,_,argument=definition.strip().partition('_');feature=feature.lower()
                    identifiers=[name.strip() for name in ids.split(',')]
                    if scope == 'main':identifiers=corrected_feature_members(level['Id'],feature,identifiers)
                    if not separator or any(name not in carriers for name in identifiers):
                        unsupported.add('carrier:'+line);continue
                    for identifier in identifiers:
                        data=feature_data.setdefault(identifier,{})
                        if feature in ['h','hidden'] and argument:data['curtainColor']=color(argument)
                        elif feature in ['rk','rock']:data['barrier']=True
                        elif feature in ['i','ice'] and argument:data.setdefault('iceRequires',[]).append(argument.strip())
                        elif feature in ['l','lock'] and argument:data.setdefault('lockColors',[]).append(color(argument))
                        elif feature=='tag' and argument:data['tagColor']=color(argument)
                        elif feature in ['connected','connectedice','icecon']:
                            data.setdefault('ropeLocks',[]).append('rope:'+','.join(identifiers))
                        elif feature=='carpet':data.setdefault('carpetLocks',[]).append('carpet:'+','.join(identifiers))
                        elif feature=='tunnel':data['generator']=True
                        elif feature=='longtruck':
                            capacity,_,target=argument.partition(':')
                            try:capacity=int(capacity)
                            except ValueError:capacity=5
                            data['capacityGroups']=min(12,max(5,capacity))
                            if target:data['targetColor']=color(target)
                        else:unsupported.add('carrier:'+line.strip())
            points=[];placements=[];spares=[];spacing=spline['Spacing']
            for cell in spline['Spline'].split(':'):
                values=cell.split(';')
                if len(values)!=3:unsupported.add('spline-format');continue
                token,x,z=values[0],int(values[1])*spacing,int(values[2])*spacing
                if re.match(r'^\s*\d+(?:-|$)',token.split('\n')[0]):
                    for item in token.split('\n'):
                        if re.match(r'^\s*\d+(?:-|$)',item):
                            components=item.strip().split('-');points.append({'order':int(components[0]),'x':x,'y':float(components[1]) if len(components)>1 else 0,'z':z})
                name=token.split('\n')[0].split('-')[0].strip()
                lines=token.splitlines()
                yaw=float(lines[1]) if len(lines)>1 and re.fullmatch(r'[+-]?\d+(?:\.\d+)?',lines[1]) else 0
                if name in carriers:
                    offset=[float(v) if v.strip() else 0 for v in lines[2].split(',')] if len(lines)>2 else []
                    offset=(offset+[0,0,0])[:3];radians=math.radians(yaw)
                    placement={'id':name,'x':x+offset[0]*math.cos(radians)+offset[2]*math.sin(radians),
                               'y':offset[1]-1.0,'z':z-offset[0]*math.sin(radians)+offset[2]*math.cos(radians),
                               'yaw':yaw,'groups':stacks.get(name,[]),'curtainColor':0,
                               **feature_data.get(name,{})}
                    (spares if name in ['Y','Z'] and name not in stacks else placements).append(placement)
            points.sort(key=lambda x:x['order'])
            board_angle=level.get('CameraRotation',0)+spline.get('CameraRotation',0)
            radians=math.radians(board_angle);cosine=math.cos(radians);sine=math.sin(radians)
            for item in points+placements+spares:
                x,z=item['x'],item['z'];item['x']=x*cosine+z*sine;item['z']=-x*sine+z*cosine
            for item in placements+spares:item['yaw']+=board_angle
            for group in {key for item in placements for key in item.get('carpetLocks',[])}:
                covered=[item for item in placements if group in item.get('carpetLocks',[])]
                yaw=math.radians(covered[0]['yaw'])
                direction=1 if math.cos(yaw)>1e-6 else -1
                covered.sort(key=lambda item:direction*(item['x']*math.cos(yaw)-item['z']*math.sin(yaw)))
                for order,item in enumerate(covered):item['carpetOrder']=order
            if len(points)<3:unsupported.add('open-or-missing-track')
            levels.append({'id':level['Id'],'theme':level['Theme'],'slots':level['SlotCount'],
                'colorMix':level['ColorMix'],'cameraRotation':board_angle,
                'cameraOffset':spline['CameraOffset'],'closed':spline['Closed'],'subdivide':spline['Subdivide'],'points':points,
                'splineLength':sampled_spline_length(points,spline['Closed'],spline['Subdivide']),
                'carriers':placements,'spares':spares,'unsupported':sorted(unsupported),
                'sourceCarrier':carrier['Id'],'sourceSpline':spline['Id']})
        save(OUT/'Data'/(scope+'.json'),{'version':'4.6.0','levels':levels})
        catalogs[scope]={'levels':len(levels),'supported':sum(not x['unsupported'] for x in levels)}
    if levels_only:
        print(json.dumps({'catalogs':catalogs}))
        return
    objects=[x for p in (REFERENCE/'analysis/unity').glob('*-inventory.json') for x in json.loads(p.read_text('utf-8'))]
    named={}
    for row in objects:named.setdefault((row['type'],row.get('name','')),row)
    # Tutorial artwork is artist-owned in Theme/tutorialHand.png; importing
    # reference gameplay data must not restore the retired click-hand image.
    camera=json.loads(next((REFERENCE/'exports/configuration').glob('CameraConfig__*.json')).read_text('utf-8'))['tree']
    save(OUT/'Data/presentation.json',{'pitch':70,'targetRadius':1,'framing':camera['FramingSize'],
         'carrierLift':1.755,'generatorVisualOffset':-2.27,
         'top':camera['TopOffset'],'bottom':camera['BottomOffset'],'left':camera['LeftOffset'],'right':camera['RightOffset']})
    guides=[
        (21,'彩色幕布','tutorialCurtain','完成一辆与幕布同色的货车，即可打开幕布，露出里面的方块。'),
        (22,'隐藏方块','tutorialHidden','带问号的方块会隐藏颜色。让它露在货车最外侧，就能看见真实颜色。'),
        (20,'传送门','tutorialPortal','方块到达跑道尽头后，会从另一端继续传送。'),
        (2,'障碍货车','tutorialBarrier','带障碍杆的货车可以收集同色方块，但不能向传送带放出方块。'),
        (4,'冰冻货车','tutorialIce','完成关联的货车，即可破冰并解锁这辆货车。'),
        (23,'锁和钥匙','tutorialLock','让钥匙方块露出，收集钥匙，即可打开同色大锁和卷帘门。'),
        (25,'绳索货车','tutorialRope','被绳索绑住的货车可以收集方块。完成组内任意一辆货车，即可解开绳索。'),
        (26,'地毯','tutorialCarpet','每完成一辆货车，地毯便会向前卷起，露出下一辆货车。'),
        (24,'彩色货车','tutorialTag','整辆车的颜色代表它需要的颜色。这辆货车只能收集同色方块。'),
        (27,'方块生成器','tutorialGenerator','每次放出一组方块，清空后露出下一组。生成器里的方块数量有限。'),
        (28,'螺丝方块','tutorialScrew','螺丝把相邻方块连在一起。它们需要一起放出，请预留足够的传送带空间。')]
    write_guide_levels(json.loads((OUT/'Data/main.json').read_text('utf-8'))['levels'])
    feature_config=json.loads(next((REFERENCE/'exports/configuration').glob('Features v2*')).read_text('utf-8'))['tree']['Collection']['list']
    feature_scale={row['Key']:row['Value']['IconScaleMultiplier'] for row in feature_config}
    save(OUT/'Data/mechanic-guides.json',{'guides':[{'id':i,'title':title,'sprite':icon,'description':desc,'scale':feature_scale.get(i,1)} for i,title,icon,desc in guides]})
    for alias,name in {'click':'Click 6','receive':'Pop 7','complete':'Win 1','music':'Casual Music Loop 01'}.items():
        row=named.get(('AudioClip',name))
        if row:
            wav=next((o for o in row['outputs'] if o['path'].endswith('.wav')),None)
            if wav:copy(REFERENCE/wav['path'],OUT/'Audio'/(alias+'.wav'))
    colors=json.loads(next((REFERENCE/'exports/configuration').glob('Colors__*.json')).read_text('utf-8'))['tree']['Collection']['list']
    palette=[]
    for item in colors:
        key=item['Key'];value=item['Value']['Color'];identifier=key['<Base>k__BackingField']+100*key['<Shade>k__BackingField']
        if value['a']>0:palette.append({'id':identifier,**value})
    save(OUT/'Data/palette.json',{'colors':palette})
    # Reconstruct a mesh-only cart from this APK's serialized transforms and renderer bindings.
    nodes=json.loads((REFERENCE/'exports/hierarchy/00_data.json').read_text('utf-8'))
    filters={};renderers={}
    for kind in ['MeshFilter','MeshRenderer','SkinnedMeshRenderer']:
        for line in (REFERENCE/f'exports/structure/00_data/{kind}.jsonl').read_text('utf-8').splitlines():
            row=json.loads(line);key=(row['asset_file'],row['tree']['m_GameObject']['m_PathID'])
            (filters if kind=='MeshFilter' else renderers)[key]=row['tree']
    parts=[]
    node_map={n['hierarchy_path']:n for n in nodes if n['hierarchy_path'].startswith('Carrier/')}
    for name,node in node_map.items():
        if not (name.startswith('Carrier/Pivot/Model Root') or name in ['Carrier/Pivot/TopPart1','Carrier/Pivot/TopPart2V2']):continue
        key=(node['asset_file'],node['path_id']);renderer=renderers.get(key)
        if not renderer:continue
        mesh=renderer.get('m_Mesh') or filters.get(key,{}).get('m_Mesh')
        if not mesh or mesh['m_FileID']!=0:continue
        parts.append({'path':name,'mesh':mesh['m_PathID'],'transform':node['transform'],
                      'wheel':'Wheel' in name,'materialIds':[m['m_PathID'] for m in renderer['m_Materials']]})
    save(OUT/'Data/carrier-visual.json',{'parts':parts,'nodes':[{'path':p,'transform':n['transform']} for p,n in node_map.items() if p.startswith('Carrier/Pivot/Model Root') or p in ['Carrier/Pivot','Carrier/Pivot/TopPart1','Carrier/Pivot/TopPart2V2']]})
    required_meshes={part['mesh'] for part in parts}
    # Import the actual feature decorations, preserving each parent transform.
    # The group-wide lock/rope layout is rebuilt around this project's resolved carts.
    selections={
        'barrier':('Rock Carrier v2',['Pivot/Model Rock']),
        'ice':('Ice Carrier v2',['Pivot/cubeice']),
        'tag':('Tag Carrier',['Pivot/FlagV4(Clone)']),
        'sticky':('Sticky Block Group',['Model']),
        'key':('Key Block Group',['Point']),
        'lock':('Lock Carrier Group',['Left','Right','Center','Door','Lock Pivot']),
        'rope':('Connected Carrier Group v2',['Scale','Left','Right','Knot']),
        'carpet':('Carpet Carrier Group v2',['Scale','Left','Right']),
        'carpet-divider':('Carpet Carrier Group Divider Element',['Root']),
        'generator':('Tunnel Carrier',['Pivot/Spawner2SidedV4']),
        'portal':('Conveyor End',['Conveyor End']),
    }
    feature_visuals=[];material_keys=set()
    group_layouts={}
    for line in (REFERENCE/'exports/structure/00_data/MonoBehaviour.recovered.jsonl').read_text('utf-8').splitlines():
        row=json.loads(line)
        if row['class'] in ['LockCarrierGroup','ConnectedCarrierGroup','CarpetCarrierGroupV2']:
            group_layouts[row['class']]={key[0].lower()+key[1:]:value for key,value in row['tree'].items()
                                        if key in ['ForwardOffset','BackOffset','WidthOffset','CenterScaleOffset','DoorScaleOffset']}
    for alias,(root,prefixes) in selections.items():
        selected=[n for n in nodes if any(n['hierarchy_path']==root+'/'+prefix or
                  n['hierarchy_path'].startswith(root+'/'+prefix+'/') for prefix in prefixes)]
        paths={n['hierarchy_path'] for n in selected}
        for path in tuple(paths):
            while '/' in path:
                path=path.rsplit('/',1)[0]
                if path!=root:paths.add(path)
        visual_nodes=[];visual_parts=[]
        for node in nodes:
            path=node['hierarchy_path']
            if path not in paths:continue
            name='Feature'+path[len(root):]
            visual_nodes.append({'path':name,'transform':node['transform']})
            key=(node['asset_file'],node['path_id']);renderer=renderers.get(key)
            mesh=(renderer or {}).get('m_Mesh') or filters.get(key,{}).get('m_Mesh')
            if node not in selected or not renderer or not mesh or mesh['m_FileID']!=0:continue
            material_ids=[m['m_PathID'] for m in renderer['m_Materials']]
            visual_parts.append({'path':name,'mesh':mesh['m_PathID'],'materialIds':material_ids})
            required_meshes.add(mesh['m_PathID'])
            material_keys.update((node['asset_file'],m) for m in material_ids)
        visual={'id':alias,'nodes':visual_nodes,'parts':visual_parts}
        layout_class={'lock':'LockCarrierGroup','rope':'ConnectedCarrierGroup','carpet':'CarpetCarrierGroupV2'}.get(alias)
        if layout_class:visual['layout']=group_layouts[layout_class]
        transfer=next((n for n in nodes if n['hierarchy_path']==root+'/Transfer Project Point'),None)
        if transfer:visual['outlet']=transfer['transform']['m_LocalPosition']
        if alias=='generator':
            collider_node=next(n for n in nodes if n['hierarchy_path']==root+'/Pivot/Collider')
            pivot_node=next(n for n in nodes if n['hierarchy_path']==root+'/Pivot')
            for line in (REFERENCE/'exports/structure/00_data/BoxCollider.jsonl').read_text('utf-8').splitlines():
                row=json.loads(line)
                if row['asset_file']==collider_node['asset_file'] and row['tree']['m_GameObject']['m_PathID']==collider_node['path_id']:
                    visual['colliderSize']=row['tree']['m_Size']
                    visual['colliderCenter']={axis:row['tree']['m_Center'][axis]+pivot_node['transform']['m_LocalPosition'][axis] for axis in ['x','y','z']}
                    break
        feature_visuals.append(visual)
    source_materials={}
    for line in (REFERENCE/'exports/structure/00_data/Material.jsonl').read_text('utf-8').splitlines():
        row=json.loads(line);source_materials[(row['asset_file'],row['path_id'])]=row['tree']
    feature_materials=[]
    for asset,identifier in sorted(material_keys):
        tree=source_materials[(asset,identifier)];props=tree['m_SavedProperties']
        textures=dict(props['m_TexEnvs'])
        texture=textures.get('_BaseMap',textures.get('_MainTex',{}))
        feature_materials.append({'id':identifier,'name':tree['m_Name'],
                                  'textureScale':texture.get('m_Scale',{'x':1,'y':1}),
                                  'textureOffset':texture.get('m_Offset',{'x':0,'y':0})})
    save(OUT/'Data/feature-visuals.json',{'visuals':feature_visuals,'materials':feature_materials})
    for script in (PROJECT/'Assets/LoopSort/Runtime').glob('*.cs'):
        required_meshes.update(int(identifier) for identifier in re.findall(r'GameAssets\.Mesh\((\d+)\)',script.read_text('utf-8')))
    imported_meshes=set()
    for row in objects:
        if row['type']=='Mesh' and row['id'].startswith('00_') and row['path_id'] in required_meshes:
            mesh=next((o for o in row['outputs'] if o['path'].endswith('.obj')),None)
            if mesh:
                copy(REFERENCE/mesh['path'],OUT/'Models'/(str(row['path_id'])+'.obj'));imported_meshes.add(row['path_id'])
    if imported_meshes!=required_meshes:raise ValueError('Missing required meshes: '+str(sorted(required_meshes-imported_meshes)))
    save(PROJECT/'Documentation/ReferenceImport.json',{'sourceVersion':'4.6.0','sourceXapkSha256':'444456c6dc8948c1a4c7cec19c3a18a94883b0265f21af8f5d37e7467d0430e7',
         'catalogs':catalogs,'uiSource':'Theme atlases','cartMeshParts':len(parts),'files':imports})
    print(json.dumps({'catalogs':catalogs,'assets':len(imports),'cartParts':len(parts),'uiSource':'Theme atlases'}))

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--levels-only',action='store_true',help='Rebuild level catalogs without replacing art, UI, guides or import provenance.')
    main(parser.parse_args().levels_only)
