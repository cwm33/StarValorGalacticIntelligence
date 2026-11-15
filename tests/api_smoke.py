"""Synthetic-only NRBF fixture and loopback API smoke checks. No game files needed.
Run after starting the API: python3 tests/api_smoke.py
Use --fixture PATH to write an explicitly synthetic fixture for browser checks.
"""
import hashlib
import io
import json
import struct
import sys
import urllib.request
import urllib.error


def fixture(modern=True, sector_count=36, credits=1000.0):
    out = io.BytesIO()
    def i(n): out.write(struct.pack('<i', n))
    def s(t):
        b = t.encode(); n = len(b)
        while n >= 128: out.write(bytes([(n & 127) | 128])); n >>= 7
        out.write(bytes([n])); out.write(b)
    out.write(b'\0'); [i(x) for x in [1, -1, 1, 0]]
    out.write(bytes([12])); i(999999); s('Synthetic, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null')
    next_id = 0
    def value(v):
        nonlocal next_id
        if v is None: out.write(bytes([10])); return
        if isinstance(v, bool): out.write(bytes([8, 1, int(v)])); return
        if isinstance(v, int): out.write(bytes([8, 8])); i(v); return
        if isinstance(v, float): out.write(bytes([8, 11])); out.write(struct.pack('<f', v)); return
        next_id += 1; ident = next_id
        if isinstance(v, str): out.write(bytes([6])); i(ident); s(v)
        elif isinstance(v, list):
            out.write(bytes([16])); i(ident); i(len(v))
            for item in v: value(item)
        else:
            name, fields = v
            out.write(bytes([5])); i(ident); s(name); i(len(fields))
            for key in fields: s(key)
            out.write(bytes([2] * len(fields))); i(999999)
            for item in fields.values(): value(item)
    def obj(type_name, **fields): return (type_name, fields)
    sectors = []
    stations = []
    for n in range(sector_count):
        fields = dict(name=f'Synthetic Sector {n}', x=float(n % 6 * 10), y=float(n // 6 * 10), incX=0.5, incY=0.0, level=n % 20 + 1, discovered=n < sector_count - 6, spotted=n < sector_count - 3, population=float(n * 100))
        if n < sector_count - 1:
            fields['jumpGates'] = [obj('JumpGate', connectedSector=n+1, discovered=True)]
        if modern:
            fields['marketPriceControl'] = obj('MarketPriceControl', prices=[obj('ItemMarketPrice', itemID=18, buyingPrice=float(10 + n), sellingPrice=float(15 + n), tradePrice=-1.0 if n == 0 else float(12 + n), IsResourceStarved=n % 4 == 0)])
        sectors.append(obj('Sector', **fields))
        stock = [obj('ItemStock', itemType=kind, itemID=18, stock=float(n + 1), lastStockCount=0.0) for kind in [1, 2, 3, 4]]
        st = dict(id=100 + n, sectorIndex=n, discovered=True, factionIndex=n % 3, level=n % 10, modelIndex=2)
        st['itemStock' if modern else 'market'] = obj('Stock', items=stock) if modern else stock
        if modern: st['modules'] = [obj('Module', persistentItems=[obj('ItemStock', itemType=3, itemID=18, stock=2.0)])]
        stations.append(obj('Station', **st))
    root = dict(savedVersion=2.251 if modern else 2.05, credits=float(credits), expansion_BaseBuilding=modern, currentSectorIndex=0,
                character=obj('Character', name='Synthetic Commander', level=10, codex=obj('Codex', records=[] if modern else [obj('TradeRecord', stationID=100+n, items=[obj('CodexItem', itemID=18, price=float(20+n))]) for n in range(sector_count)])),
                spaceShipData=obj('Ship', shipModelID=55, HPstatus=95.0, cargo=[obj('CargoItem', itemType=4, itemID=55, qnt=1, stockStationID=31)], weapons=[obj('Weapon', weaponIndex=0, slotIndex=1, rarity=2)], equipments=[obj('Equipment', equipmentID=29, qnt=1, rarity=1)]),
                sectors=sectors, stationList=stations)
    if modern: root['galacticMarket'] = obj('GalacticMarket')
    value(obj('GameDataInfo', **root)); out.write(bytes([11])); return out.getvalue()


def request(path, data=None, headers=None):
    req = urllib.request.Request('http://127.0.0.1:5187'+path, data=data, headers=headers or {})
    with urllib.request.urlopen(req) as response: return json.load(response)


def import_fixture(data, profile='Unknown', filename='SaveGameData_99.dat'):
    boundary = 'SyntheticFixtureBoundary'
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="mechanics"\r\n\r\n{profile}\r\n'
            f'--{boundary}\r\nContent-Disposition: form-data; name="save"; filename="{filename}"\r\nContent-Type: application/octet-stream\r\n\r\n').encode() + data + f'\r\n--{boundary}--\r\n'.encode()
    return request('/api/import', body, {'Content-Type': 'multipart/form-data; boundary='+boundary, 'X-Star-Valor': 'import'})


if __name__ == '__main__':
    if '--fixture' in sys.argv:
        import argparse
        parser = argparse.ArgumentParser()
        parser.add_argument('--fixture', required=True)
        parser.add_argument('--sectors', type=int, default=36)
        parser.add_argument('--credits', type=float, default=1000.0)
        parser.add_argument('--legacy', action='store_true')
        options = parser.parse_args()
        with open(options.fixture, 'xb') as f:
            f.write(fixture(not options.legacy, options.sectors, options.credits))
        print('Wrote synthetic fixture'); sys.exit(0)
    for modern in [False, True]:
        data = fixture(modern); before = hashlib.sha256(data).digest()
        imported = import_fixture(data, 'Vanilla', 'SaveGameData_99_Auto.dat' if modern else 'SaveGameData_99.dat'); ident = imported['id']
        safe = request(f'/api/{ident}/intelligence?spoilers=false'); full = request(f'/api/{ident}/intelligence?spoilers=true')
        assert len(safe['sectors']) == 30 and len(full['sectors']) == 36
        assert safe['cargo'][0]['storage'] == 'Station storage' and safe['cargo'][0]['item']['namespace'] == 'ship-model'
        assert len(safe['stations']) == 30 and len(full['stations']) == 36
        assert len(safe['markets']) == 30 and len(safe['inventories']) == (150 if modern else 120)
        assert safe['markets'][0]['source'] == ('Sector prices' if modern else 'Codex prices')
        assert safe['provenance']['includesSpoilers'] is False and len(safe['connections']) == 29 and len(full['connections']) == 35
        assert 'Synthetic Sector 35' not in json.dumps(safe)
        assert (safe['markets'][0]['priceSemantics'] != 'Unverified') == modern
        assert request(f'/api/{ident}/route?from=0&to=2') == {'path': [0, 1, 2], 'jumps': 2}
        for origin, target in [(2, 0), (29, 30), (0, 99)]:
            assert request(f'/api/{ident}/route?from={origin}&to={target}')['path'] is None
        assert request(f'/api/{ident}/route?spoilers=true&from=29&to=30')['jumps'] == 1
        assert safe['inventories'][0]['source'] == ('Station stock' if modern else 'Legacy station market')
        trade = request(f'/api/{ident}/trade?spoilers=false')
        assert trade['opportunities'] == []
        trial = request(f'/api/{ident}/trade?spoilers=false&experimental=true')['opportunities']
        assert bool(trial) == modern
        assert all(not r['executable'] and r['quantity'] is None and r['estimatedNetProfit'] is None for r in trial)
        if modern:
            assert trial[0]['costPerItem'] == 15 and trial[0]['proceedsPerItem'] == 39 and trial[0]['gateJumps'] == 29
        nearby = request(f'/api/{ident}/nearby-markets?jumps=8&query=18')
        if modern:
            assert nearby['buys'][0]['sectorId'] == 0 and nearby['buys'][0]['price'] == 15
            assert nearby['sells'][0]['sectorId'] == 8 and nearby['sells'][0]['price'] == 18
            trip = nearby['trades'][0]
            assert trip['totalJumps'] == 8 and trip['jumpsToSell'] == 8 and trip['spread'] == 3
            assert request(f'/api/{ident}/nearby-markets?jumps=4')['trades'] == []
            drive = request(f'/api/{ident}/nearby-markets?jumps=1&driveRange=30')
            assert drive['trades'][0]['totalJumps'] == 1 and drive['trades'][0]['sell']['sectorId'] == 18
        else:
            assert nearby == {'buys': [], 'sells': [], 'trades': []}
        advisor = request(f'/api/{ident}/advisor?spoilers=false&stations=3&equipment=0')
        assert len(advisor) == 30 and all(0 <= r['evidenceCoverage'] <= 1 for r in advisor) and any(r['evidenceCoverage'] < 1 for r in advisor)
        scenario = {'profile': 'Galaxy Reforged', 'gameVersion': '2.251', 'recipes': [], 'orders': [], 'available': []}
        plan = request('/api/industry/plan', json.dumps(scenario).encode(), {'Content-Type': 'application/json', 'X-Star-Valor': 'scenario'})
        assert plan['resources'] == []
        for invalid in [dict(scenario, recipes=None), dict(scenario, available=[None])]:
            try:
                request('/api/industry/plan', json.dumps(invalid).encode(), {'Content-Type': 'application/json', 'X-Star-Valor': 'scenario'})
                raise AssertionError('Accepted invalid planning scenario')
            except urllib.error.HTTPError as e: assert e.code == 400
        assert request(f'/api/{ident}/resolver?spoilers=false')['observed'] is not None
        assert request(f'/api/{ident}/export.json?spoilers=false')['hiddenSectors'] == 6
        for route in ['/schema', '/export.json?raw=true']:
            try: request(f'/api/{ident}'+route); raise AssertionError('Raw gate bypass')
            except urllib.error.HTTPError as e: assert e.code == 400
        assert hashlib.sha256(data).digest() == before
        print(('Modern' if modern else 'Legacy') + ' API import, projection, discovery, resolver, exports and raw gates passed')

    mod = import_fixture(fixture(True), 'Galaxy Reforged')['id']
    snapshot = request(f'/api/{mod}/intelligence')
    assert snapshot['provenance']['mechanicsProfile'] == 'Galaxy Reforged'
    assert len(snapshot['connections']) == 29
    assert all(link['confidence'] == 'Observed' for link in snapshot['connections'])
    assert request(f'/api/{mod}/route?from=0&to=2')['path'] is None
    assert snapshot['markets'][0]['priceSemantics'] == 'Unverified'
    assert request(f'/api/{mod}/trade?experimental=true')['opportunities'] == []
    catalog = request(f'/api/{mod}/resolver')['catalog']
    if catalog['categories'].get('commodity', 0) and all(s['kind'] == 'VanillaLocalization' for s in catalog['sources']):
        commodity = snapshot['markets'][0]['commodity']
        assert commodity['id'] == 18 and commodity['name'] == 'Energy Cells'
        assert commodity['confidence'] == 'BaseGameReference'
        stock = next(row['item'] for row in snapshot['inventories'] if row['item']['namespace'] == 'commodity')
        assert stock['name'] == 'Energy Cells' and stock['confidence'] == 'BaseGameReference'
    print('Galaxy Reforged profile remains unverified')
