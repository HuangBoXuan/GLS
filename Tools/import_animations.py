"""Preserve same-build blend shapes/clip curves lost by the OBJ export.

Run with the analysis Python environment providing UnityPy 1.25.4.
Only the supplied APK data.unity3d is read; no neighboring gameplay is imported.
"""
import hashlib
import json
import math
import struct
import sys
from pathlib import Path
sys.dont_write_bytecode=True
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

PROJECT=Path(__file__).resolve().parents[1]
SOURCE=Path(r'D:\MiniProject\analysis\LoopSort-4.6.0-20261026/work/apk/base/assets/bin/Data/data.unity3d')
OUT=PROJECT/'Assets/LoopSort/Resources/LoopSort/Animations'

def save(name,data):
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/(name+'.json')).write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')

def streamed_curves(values,count):
    blob=struct.pack('<'+'I'*len(values),*values);offset=0;curves=[[] for _ in range(count)]
    while offset<len(blob):
        time,keys=struct.unpack_from('<fI',blob,offset);offset+=8
        for _ in range(keys):
            index,*coefficients=struct.unpack_from('<I4f',blob,offset);offset+=20
            if math.isfinite(time) and time>=0:
                curves[index].append({'time':time,'coefficients':coefficients})
    return curves

def main():
    meshes=[];clips=[];transform_clips=[]
    for obj in UnityPy.load(str(SOURCE)).objects:
        if obj.assets_file.name!='resources.assets':continue
        if obj.type.name=='Mesh' and obj.path_id in [377,393,395,413,427]:
            source=obj.read();handler=MeshHandler(source);handler.process();tree=obj.read_typetree();shapes=tree['m_Shapes']
            channels=[]
            for channel in shapes['channels']:
                frames=[]
                for index in range(channel['frameIndex'],channel['frameIndex']+channel['frameCount']):
                    shape=shapes['shapes'][index]
                    vertices=shapes['vertices'][shape['firstVertex']:shape['firstVertex']+shape['vertexCount']]
                    frames.append({'weight':shapes['fullWeights'][index], 'vertices':[
                        {'index':v['index'],'position':v['vertex']} for v in vertices]})
                channels.append({'name':channel['name'],'frames':frames})
            data={'id':obj.path_id,'name':source.m_Name,'vertices':handler.m_Vertices,
                  'normals':[v[:3] for v in handler.m_Normals], 'uv':handler.m_UV0,
                  'submeshes':[[i for triangle in group for i in triangle] for group in handler.get_triangles()],
                  'channels':channels}
            save(str(obj.path_id),data);meshes.append({'id':obj.path_id,'vertices':len(handler.m_Vertices),'channels':len(channels)})
        elif obj.type.name=='AnimationClip' and obj.path_id in [557,558,561]:
            tree=obj.read_typetree();muscle=tree['m_MuscleClip'];clip=muscle['m_Clip']['data'];stream=clip['m_StreamedClip']
            if obj.path_id==561:
                paths={1539332272:'Model/Screw1',3267954954:'Model/Screw2',1662528209:'Model/Rope2',
                       952543276:'Model/Screw1 Hole',3193100930:'Model/Screw2 Hole'}
                values=streamed_curves(stream['data'],stream['curveCount']);bindings=[];offset=0
                for binding in tree['m_ClipBindingConstant']['genericBindings']:
                    count=4 if binding['attribute']==2 else 3
                    bindings.append({'path':paths[binding['path']],'attribute':binding['attribute'],
                                     'curves':[{'keys':curve} for curve in values[offset:offset+count]]})
                    offset+=count
                save('sticky-complete',{'sourceClip':561,'name':tree['m_Name'],'duration':muscle['m_StopTime'],
                                        'bindings':bindings})
                transform_clips.append({'id':561,'name':tree['m_Name'],'duration':muscle['m_StopTime']})
                continue
            clips.append({'id':obj.path_id,'name':tree['m_Name'],'duration':muscle['m_StopTime'],
                          'curves':streamed_curves(stream['data'],stream['curveCount']),
                          'constants':clip['m_ConstantClip']['data']})
    save('curtain-clips',{'clips':clips})
    provenance={'source':str(SOURCE),'sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'meshes':meshes,
                'clips':[{'id':c['id'],'name':c['name'],'duration':c['duration']} for c in clips]+transform_clips,
                'outputs':[{'path':file.relative_to(PROJECT).as_posix(),'sha256':hashlib.sha256(file.read_bytes()).hexdigest()}
                           for file in sorted(OUT.glob('*.json'))]}
    (PROJECT/'Documentation/AnimationImport.json').write_text(json.dumps(provenance,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(provenance['meshes']))

if __name__=='__main__':main()
