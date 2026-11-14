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


def fixture(modern=True):
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
    for n in range(36):
        fields = dict(name=f'Synthetic Sector {n}', x=float(n % 6 * 10), y=float(n // 6 * 10), level=n % 20 + 1, discovered=n < 30, spotted=n < 33, population=float(n * 100))
        if modern:
            fields['marketPriceControl'] = obj('MarketPriceControl', prices=[obj('ItemMarketPrice', itemID=18, buyingPrice=float(10 + n), sellingPrice=float(15 + n), tradePrice=-1.0 if n == 0 else float(12 + n), IsResourceStarved=n % 4 == 0)])
        sectors.append(obj('Sector', **fields))
        stock = [obj('ItemStock', itemType=kind, itemID=18, stock=float(n + 1), lastStockCount=0.0) for kind in [1, 2, 3, 4]]
        st = dict(id=100 + n, sectorIndex=n, discovered=True, factionIndex=n % 3, level=n % 10, modelIndex=2)
        st['itemStock' if modern else 'market'] = obj('Stock', items=stock) if modern else stock
        if modern: st['modules'] = [obj('Module', persistentItems=[obj('ItemStock', itemType=3, itemID=18, stock=2.0)])]
        stations.append(obj('Station', **st))
    root = dict(savedVersion=2.251 if modern else 2.05, credits=1000.0, expansion_BaseBuilding=modern,
                character=obj('Character', name='Synthetic Commander', level=10, codex=obj('Codex', records=[] if modern else [obj('TradeRecord', stationID=100+n, items=[obj('CodexItem', itemID=18, price=float(20+n))]) for n in range(36)])),
                spaceShipData=obj('Ship', shipModelID=55, HPstatus=95.0, weapons=[obj('Weapon', weaponIndex=0, slotIndex=1, rarity=2)], equipments=[obj('Equipment', equipmentID=29, qnt=1, rarity=1)]),
                sectors=sectors, stationList=stations)
    if modern: root['galacticMarket'] = obj('GalacticMarket')
    value(obj('GameDataInfo', **root)); out.write(bytes([11])); return out.getvalue()


def request(path, data=None, headers=None):
    req = urllib.request.Request('http://127.0.0.1:5187'+path, data=data, headers=headers or {})
    with urllib.request.urlopen(req) as response: return json.load(response)


def import_fixture(data, profile='Unknown'):
    boundary = 'SyntheticFixtureBoundary'
    body = (f'--{boundary}\r\nContent-Disposition: form-data; name="mechanics"\r\n\r\n{profile}\r\n'
            f'--{boundary}\r\nContent-Disposition: form-data; name="save"; filename="SaveGameData_99.dat"\r\nContent-Type: application/octet-stream\r\n\r\n').encode() + data + f'\r\n--{boundary}--\r\n'.encode()
    return request('/api/import', body, {'Content-Type': 'multipart/form-data; boundary='+boundary, 'X-Star-Valor': 'import'})


if __name__ == '__main__':
    if len(sys.argv) == 3 and sys.argv[1] == '--fixture':
        with open(sys.argv[2], 'wb') as f: f.write(fixture())
        print('Wrote synthetic fixture'); sys.exit(0)
    for modern in [False, True]:
        data = fixture(modern); before = hashlib.sha256(data).digest()
        imported = import_fixture(data, 'Galaxy Reforged' if modern else 'Vanilla'); ident = imported['id']
        safe = request(f'/api/{ident}/intelligence?spoilers=false'); full = request(f'/api/{ident}/intelligence?spoilers=true')
        assert len(safe['sectors']) == 30 and len(full['sectors']) == 36
        assert len(safe['stations']) == 30 and len(full['stations']) == 36
        assert len(safe['markets']) == 30 and len(safe['inventories']) == (150 if modern else 120)
        assert safe['markets'][0]['source'] == ('Sector prices' if modern else 'Codex prices')
        assert safe['provenance']['includesSpoilers'] is False and safe['connections'] == []
        assert 'Synthetic Sector 35' not in json.dumps(safe)
        assert safe['markets'][0]['priceSemantics'] == 'Unverified'
        assert request(f'/api/{ident}/resolver?spoilers=false')['observed'] is not None
        assert request(f'/api/{ident}/export.json?spoilers=false')['hiddenSectors'] == 6
        for route in ['/schema', '/export.json?raw=true']:
            try: request(f'/api/{ident}'+route); raise AssertionError('Raw gate bypass')
            except urllib.error.HTTPError as e: assert e.code == 400
        assert hashlib.sha256(data).digest() == before
        print(('Modern' if modern else 'Legacy') + ' API import, projection, discovery, resolver, exports and raw gates passed')
