Viora 外置语言包 / External language packs
=============================================

把 <语言码>.json 放进本文件夹(如 fr.json、de.json、es.json),重启 Viora,
设置页的语言下拉会自动出现新语言。

Put <langcode>.json files into this folder (e.g. fr.json, de.json),
restart Viora, and the language appears in Settings automatically.

规则 / Rules
------------
- 文件名 = 语言码(如 fr.json → 法语);
- 键名与 en.json 保持一致;缺失的键会回退到英文;
- 'Language.DisplayName' 键定义下拉框里的显示名;
- Filename = language code (fr.json -> French). Keys mirror en.json; missing keys
  fall back to English. "Language.DisplayName" sets the dropdown display name.

模板 / Template: _template.json(en.json 全键副本,值为空待翻译 / all keys, empty values)
