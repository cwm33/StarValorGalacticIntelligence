import argparse
import hashlib
import json
import re
import struct
import subprocess
import tempfile

import UnityPy
from pathlib import Path
parser = argparse.ArgumentParser(description="Read Star Valor 2.251d baseline gear prices into a private catalog")
parser.add_argument("game", type=Path, help="Star Valor installation directory")
parser.add_argument("save", type=Path)
parser.add_argument("output", type=Path, help="New catalog file outside the repository and game")
parser.add_argument("--profile", choices=["Unknown", "Vanilla", "Galaxy Reforged"], required=True)
args = parser.parse_args()
here = Path(__file__).resolve().parent
output = args.output.resolve()
if output.is_relative_to(here.parents[1]) or output.is_relative_to(args.game.resolve()):
    parser.error("Keep the private catalog outside the repository and game installation.")

d = args.game / "Star Valor_Data"
digests = {
    "resources.assets": "C097740ED593AB575D836EA1830BF1D6DEF82464E2C049FEB82005E3817A8B5E",
    "globalgamemanagers": "43F186332F00BD900DC8DE95FBE7A8D708950E4F938F0E3BD4E03DFF9B2F2E35",
    "globalgamemanagers.assets": "A71B76C943085235CDA23E054DF74FEFA95373B85590B7A5D99A9223071BE1AF",
    "Managed/Assembly-CSharp.dll": "FEAAD40F8ED49F16DEA9ED6699171FE69ACDF30542E6C37421EB4AC7697D4D38",
}
for path, expected in digests.items():
    with (d / path).open("rb") as source:
        if hashlib.file_digest(source, "sha256").hexdigest().upper() != expected:
            parser.error(f"{path} differs from validated build 26582067.")
e=UnityPy.load(*(str(d/n) for n in ['resources.assets','globalgamemanagers','globalgamemanagers.assets']))
resource=next(o for o in e.objects if o.type.name=='ResourceManager')
pairs=[(p,v) for p,v in resource.read_typetree()['m_Container'] if p.startswith('equipments/')]
objects={o.path_id:o for o in e.objects if o.assets_file.name=='resources.assets'}
results={}
for i,(path,ref) in enumerate(pairs):
    assert re.match(r'equipments/\d{3}\.',path) and int(path[11:14])==i
    o=objects[ref['m_PathID']]
    assert o.type.name=='MonoBehaviour'
    raw=o.get_raw_data()
    def string(offset):
        length=struct.unpack_from('<i',raw,offset)[0]
        assert 0<=length<1024
        start=offset+4
        return raw[start:start+length].decode('utf8'),(start+length+3)&~3
    name,cursor=string(28)
    assert name.casefold()==path.split('/')[1]
    ref_name,cursor=string(cursor+4)
    space=struct.unpack_from('<f',raw,cursor+16)[0]
    tech,sort=struct.unpack_from('<ii',raw,cursor+36)
    assert 0<=space<=200 and 0<=tech<=200 and 0<=sort<=200
    results[i]=(40+(tech+sort)*70+space*40)*(1.4 if space==0 else 1)
with tempfile.TemporaryDirectory() as directory:
    equipment = Path(directory) / "equipment.json"
    equipment.write_text(json.dumps(results), encoding="utf-8")
    subprocess.run(["dotnet", "run", "--project", str(here), "--", str(args.save.resolve()),
                    str(equipment), str(output), args.profile], check=True)
