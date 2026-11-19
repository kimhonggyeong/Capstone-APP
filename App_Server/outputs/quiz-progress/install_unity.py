from pathlib import Path
import hashlib
import json

work = Path(__file__).resolve().parent
root = Path('C:/Users/hk010/Ant Teacher_V2/Assets/Scripts/Term/Quiz').resolve()
entries = json.loads((work / 'unity-install.json').read_text(encoding='utf-8'))
# Verify all destinations and the original before writing any file.
for entry in entries:
    target = Path(entry['destination']).resolve()
    if target.parent != root:
        raise RuntimeError(f'Unexpected destination: {target}')
    if target.exists():
        expected = entry['expected_sha256']
        if not expected or hashlib.sha256(target.read_bytes()).hexdigest() != expected:
            raise RuntimeError(f'Target changed since review: {target}')
for entry in entries:
    Path(entry['destination']).write_bytes(Path(entry['source']).read_bytes())
print(json.dumps({'installed':[entry['destination'] for entry in entries]},ensure_ascii=False))
