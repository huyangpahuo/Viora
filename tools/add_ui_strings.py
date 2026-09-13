import json, io

for path, strings in {
    'src/Viora.Localization/Assets/zh-Hans.json': {
        "Nav.Group.Community": "社区",
        "About.Subtitle": "应用信息、文档与反馈。",
        "Convert.ReplaceImage": "更换图片",
        "Convert.PrevPreset": "上一个预设",
        "Convert.NextPreset": "下一个预设",
        "Theme.Sakura": "樱花",
        "Theme.Mint": "薄荷",
        "Theme.Sunset": "落日",
        "Theme.Midnight": "午夜",
        "Community.Subtitle": "认识其他 Viora 用户的去处,未来也是插件分享广场。",
        "Community.Empty": "暂无社区空间。这里将由数据驱动——插件分享广场与交流频道上线后会出现在这里。",
    },
    'src/Viora.Localization/Assets/en.json': {
        "Nav.Group.Community": "Community",
        "About.Subtitle": "App info, docs and feedback.",
        "Convert.ReplaceImage": "Replace image",
        "Convert.PrevPreset": "Previous preset",
        "Convert.NextPreset": "Next preset",
        "Theme.Sakura": "Sakura",
        "Theme.Mint": "Mint",
        "Theme.Sunset": "Sunset",
        "Theme.Midnight": "Midnight",
        "Community.Subtitle": "Meet other Viora users — and the future home of community plugin sharing.",
        "Community.Empty": "No community spaces yet. This list is data-driven — the plugin-sharing hub and chat spaces will appear here.",
    },
}.items():
    with io.open(path, encoding='utf-8') as f:
        data = json.load(f)
    data.update(strings)
    with io.open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(path, len(data))
