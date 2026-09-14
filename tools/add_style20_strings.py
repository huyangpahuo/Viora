import json, io

styles = [
    ("Stippling", "点描", "密集大小不同的圆点塑造明暗和体积", "Stippling", "Tone and volume built from dense ink dots."),
    ("CrossHatching", "交叉排线", "多方向线条叠加形成阴影和立体感", "Cross-Hatching", "Layered cross-hatch lines build the shading."),
    ("Etching", "蚀刻版画", "极细密的雕刻线、金属版画质感", "Etching", "Ultra-fine engraved lines, plate-print feel."),
    ("Fresco", "湿壁画", "石灰墙面、矿物颜料、古建筑壁画感", "Fresco", "Chalky mineral pigments on a lime wall."),
    ("MosaicGlass", "玻璃镶嵌", "彩色玻璃碎片拼接成完整图像", "Mosaic Glass", "Irregular glass shards with sparkling facets."),
    ("MetalEngraving", "金属雕刻", "金属表面蚀刻线、精密机械感", "Metal Engraving", "Bright engraved lines on brushed steel."),
    ("NeonSign", "霓虹灯牌", "发光管轮廓、夜间商业街视觉", "Neon Sign", "Glowing tube outlines on a night wall."),
    ("LiquidMetal", "液态金属", "镜面金属、流动反射、熔融形态", "Liquid Metal", "Molten chrome with flowing reflections."),
    ("Chrome", "镀铬", "高反射银色表面、未来工业感", "Chrome", "Mirror-finish chrome reflection bands."),
    ("GlowingWireframe", "发光线框", "物体由发光网格和轮廓线构成", "Glowing Wireframe", "Luminous grid and edge lines."),
    ("TornPaper", "撕纸", "不规则纸张边缘、层叠遮挡、拼接", "Torn Paper", "Stacked strips with torn paper edges."),
    ("TapeArt", "胶带艺术", "彩色胶带切割、粘贴形成图像", "Tape Art", "Colored tape strips forming the image."),
    ("StringArt", "绳线艺术", "大量交叉线形成轮廓和渐变", "String Art", "Crossing threads drawing the contours."),
    ("SandArt", "沙画", "沙粒堆积、颗粒渐变、流动纹理", "Sand Art", "Backlit sand grains forming the image."),
    ("SmokeArt", "烟雾", "烟雾形成轮廓、柔软扩散、朦胧结构", "Smoke Art", "Soft drifting smoke silhouettes."),
    ("LightPainting", "光绘", "长曝光光轨、发光笔触、摄影感", "Light Painting", "Long-exposure glowing light trails."),
    ("Kaleidoscope", "万花筒", "镜像对称、重复几何、强烈视觉旋转", "Kaleidoscope", "Mirrored radial symmetry."),
    ("LiquidMarble", "液体大理石", "大理石流纹、液体色彩、抽象纹理", "Liquid Marble", "Marbled swirls with stone veins."),
    ("Dithered", "抖动图", "有限色块通过像素抖动表现灰度", "Dithered", "Ordered dithering into a tiny palette."),
    ("GlobeRelief", "浮雕", "图像变成立体浮雕、凹凸起伏的表面", "Relief", "Embossed raised-surface render."),
]

generic = {
    "Segments": ("分割数", "Number of mirror segments", "万花筒镜像分割的数量", "Segments"),
    "Flow": ("流动", "Strength of the flowing distortion", "流动扭曲的强度", "Flow"),
}

zh_add, en_add = {}, {}
for key, (zh_label, en_desc, zh_desc, en_label) in generic.items():
    zh_add[f"Param.Generic.{key}"] = zh_label
    zh_add[f"Param.Generic.{key}.Description"] = zh_desc
    en_add[f"Param.Generic.{key}"] = en_label
    en_add[f"Param.Generic.{key}.Description"] = en_desc

for slug, zh_name, zh_desc, en_name, en_desc in styles:
    zh_add[f"Preset.{slug}.Name"] = zh_name
    zh_add[f"Preset.{slug}.Description"] = zh_desc
    en_add[f"Preset.{slug}.Name"] = en_name
    en_add[f"Preset.{slug}.Description"] = en_desc

for path, extra in [('src/Viora.Localization/Assets/zh-Hans.json', zh_add),
                    ('src/Viora.Localization/Assets/en.json', en_add)]:
    with io.open(path, encoding='utf-8') as f:
        data = json.load(f)
    data.update(extra)
    with io.open(path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(data, f, ensure_ascii=False, indent=2)
        f.write('\n')
    print(path, len(data))
