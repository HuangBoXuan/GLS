"""Install a validated UI-only export, preserving models and archiving prior UI."""
import argparse
import collections
import hashlib
import html
import json
import shutil
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]



def current_export_root():
    target = (ROOT / 'Exports/Restored-UI-Models').resolve()
    if (target / 'manifest.json').is_file():
        return target
    pointer = ROOT / 'Exports/image-archive.json'
    if pointer.is_file():
        target = Path(json.loads(pointer.read_text('utf-8'))['restoredUiExport']).resolve()
        if (target / 'manifest.json').is_file():
            return target
    raise FileNotFoundError('No complete UI export found; create a full export first.')


def png_info(path):
    data = path.read_bytes()
    if data[:8] != b'\x89PNG\r\n\x1a\n':
        raise ValueError(f'Not PNG: {path}')
    return (*struct.unpack('>II', data[16:24]), hashlib.sha256(data).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staging', required=True, type=Path)
    parser.add_argument('--archive', required=True, type=Path)
    args = parser.parse_args()
    target = current_export_root()
    source, archive = args.staging.resolve(), args.archive.resolve()
    assert not archive.is_relative_to(ROOT) and not archive.is_relative_to(target) and not target.is_relative_to(archive)
    assert not archive.exists(), 'Choose a new recoverable archive directory'
    old = json.loads((target / 'manifest.json').read_text('utf-8-sig'))
    new = json.loads((source / 'manifest.json').read_text('utf-8-sig'))
    assert old['status'] == new['status'] == 'complete'
    is_ui = lambda item: item['file'].startswith(('UI-Elements/', 'UI-Screens/'))
    ui = [item for item in new['images'] if is_ui(item)]
    assert len(ui) >= 100 and any(x['file'] == 'UI-Screens/daily-signin.png' for x in ui)
    for item in ui:
        path = (source / item['file']).resolve()
        assert path.is_relative_to(source) and path.suffix == '.png'
        width, height, sha = png_info(path)
        assert [width, height] == [item['width'], item['height']]
        item['sha256'] = sha
    kept = [item for item in old['images'] if not is_ui(item)]
    before = {i['file']: png_info(target / i['file'])[2] for i in kept}
    archive.mkdir(parents=True)
    # Resolve and check every directory before moving it on Windows.
    for name in ('UI-Elements', 'UI-Screens'):
        path = (target / name).resolve()
        dest = (archive / name).resolve()
        assert path.parent == target and dest.parent == archive
        if path.exists():
            shutil.move(str(path), str(dest))
        shutil.copytree(source / name, path)
    for name in ('manifest.json', 'index.html', 'validation.json', 'README.txt'):
        if (target / name).exists():
            shutil.copy2(target / name, archive / name)
    if (target / 'undo-receipt.json').exists():
        shutil.move(str(target / 'undo-receipt.json'), str(archive / 'undo-receipt.json'))
    images = kept + ui
    result = dict(old, images=images, counts=dict(collections.Counter(i['category'] for i in images)),
                  currentUiExport=True, previousUiArchive=str(archive), uiFiles=len(ui))
    (target / 'manifest.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    cards = []
    for item in images:
        file = html.escape(item['file'], quote=True)
        cards.append(f'<article><a href="{file}"><img loading="lazy" src="{file}" alt=""></a>'
                     f'<div>{html.escape(item["label"])}</div><small>{file}</small></article>')
    (target / 'index.html').write_text('<!doctype html><meta charset="utf-8"><title>LoopSort 当前 UI 与模型</title>'
        '<style>body{background:#372337;color:#fff0d0;font:16px sans-serif;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:16px}article{background:#66425a;padding:14px;border-radius:14px}img{width:100%;height:280px;object-fit:contain;background:#f5dfb9}small{overflow-wrap:anywhere}</style>'
        '<h1>LoopSort 当前 UI 与模型</h1><p>UI 图片与界面截图已更新；原有模型、源网格和轨道导出保留。</p><main>' + ''.join(cards) + '</main>', 'utf-8')
    verified = []
    for item in images:
        width, height, sha = png_info(target / item['file'])
        assert [width, height] == [item['width'], item['height']]
        if item['file'] in before:
            assert sha == before[item['file']], 'Existing model export changed'
        verified.append(dict(file=item['file'], width=width, height=height, sha256=sha))
    receipt = dict(status='passed', pngFiles=len(verified), refreshedUiFiles=len(ui),
                   preservedModelFiles=len(kept), files=verified, previousUiArchive=str(archive),
                   apkBuilt=False, published=False)
    (target / 'validation.json').write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + '\n', 'utf-8')
    (target / 'README.txt').write_text('当前风格 UI 图片资源导出\n\nUI-Elements：当前图标、面板、主界面与机制插画精灵切片，保留透明通道。\nUI-Screens：当前真实界面截图，含放大版七日签到，1080×1920。\nModels / Source-Meshes / Tracks：保留原有模型、源网格和轨道导出。\nindex.html：图片浏览。manifest.json / validation.json：来源、尺寸与完整性校验。\n旧 UI 已移到项目外备份：' + str(archive) + '\n本工具没有生成 APK 或上线发布。\n', 'utf-8')
    print(json.dumps({k: v for k, v in receipt.items() if k != 'files'}, ensure_ascii=False))


if __name__ == '__main__':
    main()
