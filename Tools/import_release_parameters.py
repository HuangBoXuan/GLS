"""Read a private Feishu cells-get snapshot; emit client inputs and a redacted audit."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE_URL = 'https://youzu.feishu.cn/wiki/KA8ewKltSi6MbCkQfv9c1Sj8ncg?sheet=QvT1Xg'


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', 'utf-8')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--snapshot', required=True, type=Path)
    parser.add_argument('--private-dir', required=True, type=Path)
    args = parser.parse_args()
    private = args.private_dir.resolve()
    assert not private.is_relative_to(ROOT), 'Administrative credentials must stay outside the project'
    data = json.loads(args.snapshot.read_text('utf-8-sig'))
    assert not data['has_more'] and all(not r['truncated'] for r in data['ranges'])
    cells = {}
    for block in data['ranges']:
        for row_index, row in zip(block['row_indices'], block['cells']):
            for col, cell in zip(block['col_indices'], row):
                cells[f'{col}{row_index}'] = cell.get('value', '')
    get = lambda key: str(cells.get(key, '')).strip()
    assert get('C2') == 'LoopSort' and get('C4') == 'com.falcogames.loopsort'
    assert all(get(key) for key in ['C3', 'C8', 'C12', 'C13', 'C14', 'C17', 'C18', 'E23', 'E24', 'E25'])
    target = ROOT / 'ProjectSettings/LoopSortProduct.local.json'
    config = json.loads((target if target.exists() else ROOT / 'Configuration/Products/loopsort0-development.template.json').read_text('utf-8-sig'))
    config.update(businessGameId=get('C3'), androidPackageName=get('C4'), maxSdkKey=get('C8'),
                  appsFlyerDevKey=get('C17'), adMobAndroidAppId=get('C18'), privacyPolicyUrl=get('C12'),
                  termsUrl=get('C13'), accountDeletionUrl=get('C14'), maxEnabled=True,
                  appsFlyerEnabled=True, adRevenueEnabled=True)
    config['placements'] = [dict(gameId=config['gameId'], placement=placement, format=fmt, adUnitId=get(cell))
                            for placement, fmt, cell in [('daily_gift', 'rewarded', 'E23'),
                                                         ('level_complete', 'interstitial', 'E24'),
                                                         ('home_banner', 'banner', 'E25')]]
    write(target, config)
    write(ROOT / 'ProjectSettings/LoopSortProductBuild.local.json', {'adReviewKey': get('C11'), 'sourceUrl': SOURCE_URL})
    write(private / 'LoopSort-backoffice.private.json', dict(reportKey=get('C9'), eventKey=get('C10'),
          adReviewKey=get('C11'), businessGameId=get('C3'),
          adMobAdUnits={fmt: get(cell) for fmt, cell in [('rewarded', 'G23'), ('interstitial', 'G24'), ('banner', 'G25')]},
          sourceUrl=SOURCE_URL))
    services_path = ROOT / 'Assets/LoopSort/Resources/LoopSort/Data/peripheral-services.json'
    services = json.loads(services_path.read_text('utf-8-sig'))
    services.update(privacyPolicyUrl=get('C12'), termsUrl=get('C13'), accountDeletionUrl=get('C14'), adPrivacyUrl=get('C12'))
    write(services_path, services)
    template = dict(config, maxSdkKey='', appsFlyerDevKey='')
    write(ROOT / 'Configuration/Products/loopsort-android.release.template.json', template)
    assignments = {'C3': 'businessGameId (publisher ID; not a Lua namespace)', 'C4': 'Android package',
        'C8': 'MAX client SDK / product build input', 'C9': 'backoffice only / private Report Key',
        'C10': 'backoffice only / private Event Key', 'C11': 'private build input / MAX Ad Review',
        'C12': 'privacy UI / product config', 'C13': 'terms UI / product config',
        'C14': 'deletion UI / product config', 'C17': 'AppsFlyer client SDK input',
        'C18': 'Android manifest AdMob application ID', 'E23': 'MAX rewarded daily_gift',
        'E24': 'MAX interstitial level_complete', 'E25': 'product-owned MAX banner home_banner',
        'G23': 'MAX dashboard Google rewarded mapping', 'G24': 'MAX dashboard Google interstitial mapping',
        'G25': 'MAX dashboard Google banner mapping'}
    audit = dict(sourceUrl=SOURCE_URL, sourceRevision=data['revision'], sourceSha256=hashlib.sha256(args.snapshot.read_bytes()).hexdigest(),
                 product='LoopSort', moduleGameId=config['gameId'], businessGameId=get('C3'),
                 androidPackageName=config['androidPackageName'], parameterDestinations=assignments,
                 missingInSheet=[key for key in ['C5', 'C15', 'C16', 'C19'] if not get(key)],
                 administrativeCredentialsEmbedded=False, registrationConfirmed=config['registrationConfirmed'],
                 developmentOnly=config['developmentOnly'],
                 note='Ads sheet does not establish platform registration, API/CDN, signing or real consent. Backoffice mapping requires dashboard access.')
    write(ROOT / 'Documentation/ReleaseReadiness/parameter-import.json', audit)
    print(json.dumps({'clientParametersImported': True, 'mappedCells': len(assignments),
                      'rawCredentialsPrinted': False, 'registrationConfirmed': config['registrationConfirmed']}))


if __name__ == '__main__':
    main()
