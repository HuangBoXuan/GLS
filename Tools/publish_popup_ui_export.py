"""Merge a popup-only Unity export without replacing other exported artwork."""
import argparse
import collections
import html
import json
from pathlib import Path
import shutil
import sys

sys.dont_write_bytecode = True
from publish_current_ui_export import ROOT, png_info, current_export_root


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--staging', required=True, type=Path)
    parser.add_argument('--archive', required=True, type=Path)
    args = parser.parse_args()
    source, archive = args.staging.resolve(), args.archive.resolve()
    target = current_export_root()
    assert not archive.is_relative_to(ROOT) and not archive.exists()
    old = json.loads((target / 'manifest.json').read_text('utf-8-sig'))
    incoming = json.loads((source / 'manifest.json').read_text('utf-8-sig'))
    assert old['status'] == incoming['status'] == 'complete'
    updates = incoming['images']
    required = {'settings', 'pause', 'languages', 'revive-offer', 'result-fail',
                'advertising', 'delete-data', 'booster-reward-undo',
                'booster-reward-shuffle', 'booster-reward-belt', 'booster-reward-truck'}
    assert required.issubset({Path(i['file']).stem for i in updates})
    assert len({i['file'] for i in updates}) == len(updates)
    before = {i['file']: png_info(target / i['file']) for i in old['images']}
    changed, unchanged = [], []
    for item in updates:
        path = (source / item['file']).resolve()
        destination = (target / item['file']).resolve()
        assert path.is_relative_to(source) and destination.is_relative_to(target)
        assert item['file'].startswith(('UI-Screens/', 'UI-Elements/')) and path.suffix == '.png'
        width, height, sha = png_info(path)
        assert [width, height] == [item['width'], item['height']]
        item['sha256'] = sha
        (unchanged if before.get(item['file']) == (width, height, sha) else changed).append(item)
    archive.mkdir(parents=True)
    for name in ('manifest.json', 'index.html', 'validation.json', 'README.txt'):
        shutil.copy2(target / name, archive / name)
    for item in changed:
        destination = (target / item['file']).resolve()
        backup = (archive / item['file']).resolve()
        assert destination.is_relative_to(target) and backup.is_relative_to(archive)
        if destination.exists():
            backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(destination, backup)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source / item['file'], destination)
    merged = {i['file']: i for i in old['images']}
    merged.update({i['file']: i for i in updates})
    images = list(merged.values())
    refreshed = {i['file'] for i in changed}
    highlighted = {i['file'] for i in updates if i['file'].startswith('UI-Screens/') or i['file']=='UI-Elements/pause-banner-icon.png'}
    verified = []
    for item in images:
        width, height, sha = png_info(target / item['file'])
        assert [width, height] == [item['width'], item['height']]
        if item['file'] not in refreshed:
            assert (width, height, sha) == before[item['file']], 'Unrelated export changed'
        item['sha256'] = sha
        verified.append(dict(file=item['file'], width=width, height=height, sha256=sha))
    report = dict(status='passed', pngFiles=len(images), popupScreens=sum(i['file'].startswith('UI-Screens/') for i in updates),
                  renderedScreens=sum(i['file'].startswith('UI-Screens/') for i in updates),
                  updatedFiles=[i['file'] for i in changed], unchangedCandidates=len(unchanged),
                  preservedFiles=len(images)-len(changed),
                  preservedModelFiles=sum(not i['file'].startswith(('UI-Screens/', 'UI-Elements/')) for i in images),
                  files=verified, previousPopupArchive=str(archive), apkBuilt=False, published=False)
    manifest = dict(old, images=images, counts=dict(collections.Counter(i['category'] for i in images)),
                    popupExportUpdated=True, previousPopupArchive=str(archive),
                    lastPopupScreens=report['renderedScreens'],
                    uiFiles=sum(i['file'].startswith(('UI-Screens/', 'UI-Elements/')) for i in images))
    (target / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n', 'utf-8')
    (target / 'validation.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', 'utf-8')
    (target / 'popup-update.json').write_text(json.dumps({k: v for k, v in report.items() if k != 'files'}, ensure_ascii=False, indent=2)+'\n', 'utf-8')
    cards = []
    # Put the refreshed dialogs first; retain every prior model and UI link.
    ordered = sorted(images, key=lambda i: (i['file'] not in highlighted, not i['file'].startswith('UI-Screens/'), i['file']))
    for item in ordered:
        file = html.escape(item['file'], quote=True)
        badge = ' · 本次导出' if item['file'] in highlighted else ''
        cards.append(f'<article><a href="{file}"><img loading="lazy" src="{file}" alt=""></a>'
                     f'<div>{html.escape(item["label"])}{badge}</div><small>{file}</small></article>')
    (target / 'index.html').write_text('<!doctype html><meta charset="utf-8"><title>LoopSort UI 与模型</title>'
        '<style>body{background:#372337;color:#fff0d0;font:16px sans-serif;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(230px,1fr));gap:16px}article{background:#66425a;padding:14px;border-radius:14px}img{width:100%;height:280px;object-fit:contain;background:#f5dfb9}small{overflow-wrap:anywhere}</style>'
        '<h1>LoopSort 当前弹窗、UI 与模型</h1><p>本次更新的弹窗与图片排在前面，其余导出内容保留。</p><main>'+''.join(cards)+'</main>', 'utf-8')
    (target / 'README.txt').write_text('当前 UI 与模型图片导出\n\nUI-Screens：最新弹窗和已有真实界面截图，1080×1920。\n'
        '新增弹窗包含暂停、语言选择、广告复活、四种广告道具领取、数据删除及横幅暂停状态。\n'
        'UI-Elements：当前精灵图片，保留透明通道；pause-banner-icon.png 来自实际 UI 组合图标。\n'
        'Models / Source-Meshes / Tracks：既有模型导出原样保留。\n'
        'index.html：浏览入口。本次更新标记见 popup-update.json，全部尺寸与散列见 validation.json。\n'
        '被替换图片备份：'+str(archive)+'\n没有生成新美术或 APK。\n', 'utf-8')
    print(json.dumps({k: v for k, v in report.items() if k not in ('files', 'updatedFiles')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
