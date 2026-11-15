"""Package Unity's isolated Development export using API-36-compatible tools."""
import argparse
import concurrent.futures
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import urllib.request
import xml.etree.ElementTree as ET
import zipfile

GRADLE_VERSION='8.11.1'
AGP_VERSION='8.10.1'
DOWNLOADS={
 'gradle':('https://downloads.gradle.org/distributions/gradle-8.11.1-bin.zip',
           'f397b287023acdba1e9f6fc5ea72d22dd63669d59ed4a289a29b1a76eee151c6'),
 'jdk':('https://download.visualstudio.microsoft.com/download/pr/1f2b3c02-2145-4a46-a595-f6d249a7c3fe/e00c46bbf94ca5c5e1da9f363f4d5edc/microsoft-jdk-17.0.21.0.2-windows-x64.zip',
        '49154292866386b2f4f09a3ecf3be07ab80a419149c84ec1d4419b9eec9e4b64')}

def sha(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()

def tools(cache,name):
    url,expected=DOWNLOADS[name]
    archive=cache/(name+'.zip')
    if not archive.exists() or sha(archive)!=expected:
        print('Downloading verified '+name+' build tools',flush=True)
        if name=='gradle':
            # Bounded range reads also work with distribution proxies that stall
            # on a single unbounded 137 MB response. Verify the complete digest.
            with urllib.request.urlopen(urllib.request.Request(url,method='HEAD'),timeout=20) as response:
                size=int(response.headers['Content-Length'])
            chunk=4*1024*1024
            def fetch(start):
                end=min(start+chunk,size)-1
                request=urllib.request.Request(url,headers={'Range':f'bytes={start}-{end}'})
                with urllib.request.urlopen(request,timeout=30) as response:
                    if response.status!=206:raise RuntimeError('Distribution server ignored bounded range')
                    data=response.read()
                    if len(data)!=end-start+1:raise RuntimeError('Incomplete build-tool range')
                    return start,data
            with archive.open('wb') as destination,concurrent.futures.ThreadPoolExecutor(max_workers=8) as executor:
                destination.truncate(size)
                for start,data in executor.map(fetch,range(0,size,chunk)):
                    destination.seek(start);destination.write(data)
        else:
            with urllib.request.urlopen(url,timeout=60) as response,archive.open('wb') as destination:
                shutil.copyfileobj(response,destination,1024*1024)
        if sha(archive)!=expected:raise RuntimeError(name+' download SHA-256 mismatch')
    directory=cache/name
    directory.mkdir(parents=True,exist_ok=True)
    candidates=list(directory.glob('*/bin/'+('java.exe' if name=='jdk' else 'gradle.bat')))
    if not candidates:
        print('Extracting '+name,flush=True)
        with zipfile.ZipFile(archive) as zipped:
            for item in zipped.infolist():
                if not (directory/item.filename).resolve().is_relative_to(directory.resolve()):raise RuntimeError('Unsafe archive entry')
            zipped.extractall(directory)
        candidates=list(directory.glob('*/bin/'+('java.exe' if name=='jdk' else 'gradle.bat')))
    if len(candidates)!=1:raise RuntimeError('Ambiguous build-tool installation')
    return candidates[0]

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--project',required=True)
    parser.add_argument('--apk',required=True)
    parser.add_argument('--tools',default='D:/MiniProject/BuildTemp/LoopSort-ad-toolchain')
    args=parser.parse_args()
    project=Path(args.project).resolve();apk=Path(args.apk).resolve();cache=Path(args.tools).resolve()
    allowed=Path('D:/MiniProject/BuildTemp').resolve()
    if not project.is_relative_to(allowed) or not cache.is_relative_to(allowed):raise RuntimeError('Use an isolated BuildTemp directory')
    if apk.exists():raise RuntimeError('Choose a new immutable APK path')
    configs=[parent/'Assets/Resources/LoopSortProduct/configuration.json' for parent in project.parents]
    config=next((path for path in configs if path.is_file()),None)
    if config is None or not json.loads(config.read_text(encoding='utf-8')).get('maxEnabled'):
        raise RuntimeError('Expected the isolated player client configuration')
    if any((parent/'Assets/Resources/LoopSortAdTest').exists() for parent in project.parents):
        raise RuntimeError('Remove obsolete diagnostic resources before packaging')
    # Check actual application flags and generated payload, not an invented host.
    launcher=project/'launcher/build.gradle'
    if not launcher.exists() or 'signingConfig signingConfigs.debug' not in launcher.read_text():raise RuntimeError('Expected a debug-signed Unity Development project')
    cache.mkdir(parents=True,exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
        futures={name:executor.submit(tools,cache,name) for name in DOWNLOADS}
        installed={name:future.result() for name,future in futures.items()}
    root=project/'build.gradle';text=root.read_text(encoding='utf-8')
    text=re.sub(r"(id ['\"]com\.android\.(?:application|library)['\"] version ['\"])[^'\"]+",lambda m:m[1]+AGP_VERSION,text)
    root.write_text(text,encoding='utf-8')
    properties=project/'gradle.properties'
    properties.write_text('\n'.join(line for line in properties.read_text().splitlines()
        if not line.startswith('android.bundle.enableUncompressedNativeLibs='))+'\n',encoding='utf-8')
    android='{http://schemas.android.com/apk/res/android}'
    ET.register_namespace('android','http://schemas.android.com/apk/res/android')
    for module in project.rglob('build.gradle'):
        if module==root or 'build' in module.relative_to(project).parts:continue
        body=module.read_text(encoding='utf-8')
        if 'com.android' not in body and "'android-library'" not in body:continue
        for manifest in (module.parent/'src/main/AndroidManifest.xml',module.parent/'AndroidManifest.xml'):
            if not manifest.exists():continue
            xml=ET.parse(manifest);package=xml.getroot().attrib.pop('package',None)
            if package:
                if not re.search(r'\bnamespace\s',body):body=body.replace('android {',"android {\n    namespace '"+package+"'",1)
                xml.write(manifest,encoding='utf-8',xml_declaration=True)
        body=body.replace('android {','android {\n    buildFeatures { buildConfig true }',1) if 'buildFeatures {' not in body else body
        ndk=re.search(r'ndkPath\s+[\"\x27]([^\"\x27]+)',body)
        if ndk and 'ndkVersion ' not in body:
            revision=Path(ndk[1])/'source.properties'
            if revision.is_file():
                version=re.search(r'Pkg.Revision\s*=\s*([\d.]+)',revision.read_text())
                if version:body=body.replace('android {',"android {\n    ndkVersion '"+version[1]+"'",1)
        module.write_text(body,encoding='utf-8')
    environment=os.environ.copy();environment['JAVA_HOME']=str(installed['jdk'].parents[1])
    environment['PATH']=str(installed['jdk'].parent)+os.pathsep+environment['PATH']
    log=project/'android-player-modern-gradle.log'
    print('Packaging Development APK with AGP '+AGP_VERSION+' / Gradle '+GRADLE_VERSION+' / JDK 17',flush=True)
    with log.open('w',encoding='utf-8') as output:
        result=subprocess.run([str(installed['gradle']),'--no-daemon','--console=plain',':launcher:assembleDebug'],
            cwd=project,env=environment,stdout=output,stderr=subprocess.STDOUT,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    if result.returncode:raise RuntimeError('Gradle packaging failed. Inspect private log: '+str(log))
    built=project/'launcher/build/outputs/apk/debug/launcher-debug.apk'
    if not built.is_file():raise RuntimeError('Expected Gradle APK output is missing')
    apk.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(built,apk)
    receipt={'apk':str(apk),'sha256':sha(apk),'bytes':apk.stat().st_size,'development':True,'debugSigned':True,
        'agp':AGP_VERSION,'gradle':GRADLE_VERSION,'jdk':'Microsoft OpenJDK 17.0.21.0.2',
        'afEnabled':False,'productionReady':False,'published':False,'devicePlaybackVerified':False,'backendVerified':False}
    Path(str(apk)+'.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
    print('Android player APK built: '+str(apk),flush=True)

if __name__=='__main__':main()
