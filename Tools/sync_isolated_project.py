"""Copy changed inputs into the existing isolated Unity project, preserving caches."""
import concurrent.futures
import filecmp
from pathlib import Path
import shutil
import sys

# Large resources otherwise incur a filesystem round trip for every 8 KiB.
filecmp.BUFSIZE=1024*1024

source=Path(__file__).resolve().parents[1]
target=Path(sys.argv[1]).resolve()
allowed=Path('D:/MiniProject/BuildTemp').resolve()
if not target.is_relative_to(allowed) or target==source:
    raise SystemExit('Expected an isolated project below D:/MiniProject/BuildTemp')

def sync(pair):
    origin,destination=pair
    if destination.is_file():
        a,b=origin.stat(),destination.stat()
        if a.st_size==b.st_size and a.st_mtime_ns==b.st_mtime_ns:return 0
        if a.st_size==b.st_size and filecmp.cmp(origin,destination,shallow=False):return 0
    destination.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(origin,destination)
    return 1

for directory in ('Assets','Packages','ProjectSettings','Tools'):
    pairs=[(p,target/p.relative_to(source)) for p in (source/directory).rglob('*')
           if p.is_file() and '__pycache__' not in p.parts]
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        changed=sum(pool.map(sync,pairs))
    print(f'{directory}: {changed} changed files synchronized',flush=True)

# Editor automation is unnecessary in a batch player build and can compete
# with the live editor. Keep this change in the isolated manifest only.
import json
manifest=target/'Packages/manifest.json'
settings=json.loads(manifest.read_text(encoding='utf-8-sig'))
if settings['dependencies'].pop('com.coplaydev.unity-mcp',None) is not None:
    manifest.write_text(json.dumps(settings,indent=2)+'\n',encoding='utf-8')
