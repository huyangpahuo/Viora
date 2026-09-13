# -*- coding: utf-8 -*-
"""i18n key coverage checker (brief §8: both packs complete; §14 Phase 9).

Compares keys across en.json and zh-Hans.json, and scans XAML/CS for {loc:Translate Key}
usages plus Tr.Get/Tr.Format keys, reporting any key missing from either pack.
Exit code 1 on any gap — wire into CI later.
"""
import json, os, re, sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), '..'))
ASSETS = os.path.join(ROOT, 'src', 'Viora.Localization', 'Assets')

def load(name):
    with open(os.path.join(ASSETS, name), encoding='utf-8') as f:
        return json.load(f)

en = load('en.json')
zh = load('zh-Hans.json')

problems = []

en_keys, zh_keys = set(en), set(zh)
for k in sorted(en_keys - zh_keys):
    problems.append(f"missing in zh-Hans: {k} = {en[k]!r}")
for k in sorted(zh_keys - en_keys):
    problems.append(f"missing in en: {k} = {zh[k]!r}")

# Scan sources for referenced keys
xaml_key = re.compile(r'\{loc:Translate\s+([A-Za-z0-9_.]+)\s*\}')
cs_key = re.compile(r'Tr\.(?:Get|Format)\(\s*"([A-Za-z0-9_.]+)"')

used = set()
for root, dirs, files in os.walk(os.path.join(ROOT, 'src')):
    if any(part in root for part in ('obj', 'bin')):
        continue
    for fn in files:
        if not fn.endswith(('.xaml', '.cs')):
            continue
        path = os.path.join(root, fn)
        text = open(path, encoding='utf-8').read()
        used |= set(xaml_key.findall(text))
        used |= set(cs_key.findall(text))

for k in sorted(used):
    if k not in en:
        if k.endswith('.'):
            continue  # dynamic prefix (e.g. "Theme.Color." + family) — covered by family keys
        problems.append(f"XAML/CS uses key not in en.json: {k}")

print(f"pack keys: en={len(en_keys)} zh-Hans={len(zh_keys)}; referenced in code={len(used)}")
if problems:
    print("\n".join(problems))
    sys.exit(1)
print("i18n coverage OK")
