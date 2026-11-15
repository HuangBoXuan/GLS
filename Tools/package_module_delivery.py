"""Package an immutable local SDK module and review sources; never publish it."""
import argparse
import hashlib
import json
import shutil
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--receipt', required=True, type=Path)
    parser.add_argument('--private-backoffice', type=Path)
    args = parser.parse_args()
    receipt = json.loads(args.receipt.read_text('utf-8-sig'))
    assert receipt['status'] == 'built' and not receipt['registered'] and not receipt['published']
    source = Path(receipt['modulePath']).resolve()
    assert sha(source / 'manifest.json') == receipt['manifestSha256']
    manifest = json.loads((source / 'manifest.json').read_text('utf-8-sig'))
    for item in manifest['files']:
        file = (source / item['path']).resolve()
        assert file.is_relative_to(source) and file.stat().st_size == item['size'] and sha(file) == item['sha256']
    out = ROOT / 'Builds/PlatformDelivery' / receipt['moduleVersion']
    assert out.resolve().is_relative_to(ROOT) and not out.exists(), 'Delivery folders are immutable'
    out.mkdir(parents=True)
    shutil.copytree(source, out / 'module')
    with zipfile.ZipFile(out / 'module.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=5) as archive:
        for file in sorted(source.rglob('*')):
            if file.is_file():
                archive.write(file, file.relative_to(source).as_posix())
    review = Path(receipt['preparationDirectory']) / 'output/review/loopsort0' / receipt['moduleVersion']
    forbidden = []
    client_input = ROOT / 'ProjectSettings/LoopSortProduct.local.json'
    if client_input.exists():
        config = json.loads(client_input.read_text('utf-8-sig'))
        forbidden += [config.get('maxSdkKey'), config.get('appsFlyerDevKey')]
    if args.private_backoffice:
        config = json.loads(args.private_backoffice.read_text('utf-8-sig'))
        forbidden += [config.get(k) for k in ('reportKey', 'eventKey', 'adReviewKey')]
    forbidden = [key.encode() for key in forbidden if key]
    review_files = 0
    with zipfile.ZipFile(out / 'review-source.zip', 'w', zipfile.ZIP_DEFLATED, compresslevel=5) as archive:
        for file in sorted(review.rglob('*')):
            if file.is_file():
                assert not any(key in file.read_bytes() for key in forbidden), 'Product credential found in module review'
                archive.write(file, file.relative_to(review).as_posix())
                review_files += 1
    shutil.copy2(args.receipt, out / 'module-build-receipt.json')
    result = dict(status='verified local module', moduleVersion=receipt['moduleVersion'],
                  manifestSha256=receipt['manifestSha256'], manifestFiles=len(manifest['files']),
                  reviewFiles=review_files, productKeysInModuleReview=False, isolatedLuaTests=receipt['testsRun'],
                  registered=False, published=False, releaseApkBuilt=False,
                  files={file.name: dict(bytes=file.stat().st_size, sha256=sha(file))
                         for file in out.iterdir() if file.is_file()})
    (out / 'artifact-integrity.json').write_text(json.dumps(result, indent=2) + '\n', 'utf-8')
    (out / 'README.md').write_text('# LoopSort current local module\n\n'
        'Current mining artwork, home, enlarged sign-in and native product-ad hooks. '
        'Module manifest/file hashes and review sources are verified locally.\n\n'
        'This is not a registered release, signed APK/AAB, uploaded module or store-approved app. '
        'Product SDK/admin credentials stay outside the downloadable module.\n\n'
        'See Documentation/ReleaseReadiness/README.md in the project for remaining production requirements.\n', 'utf-8')
    shutil.copy2(out / 'artifact-integrity.json', ROOT / 'Documentation/ReleaseReadiness/module-artifact.json')
    print(json.dumps({key: value for key, value in result.items() if key != 'files'}))


if __name__ == '__main__':
    main()
