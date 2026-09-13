import json, io

# ---- en.json ----
with io.open('src/Viora.Localization/Assets/en.json', encoding='utf-8') as f:
    en = json.load(f)

en["Home.Feature.Convert.Body"] = "Turn photos and illustrations into stylized artwork — Anime Vector, mosaic, oil painting, sketch and watercolor presets built in."

en.update({
    "Param.TileSize": "Tile size",
    "Param.TileSize.Description": "Side length of each mosaic tile in pixels",
    "Param.Grout": "Grout strength",
    "Param.Grout.Description": "Darkening of the seams between tiles",
    "Param.StrokeSize": "Stroke size",
    "Param.StrokeSize.Description": "Brush patch size — larger strokes flatten detail",
    "Param.PaintColors": "Paint colors",
    "Param.PaintColors.Description": "Number of palette levels in the final painting",
    "Param.Texture": "Impasto texture",
    "Param.Texture.Description": "Depth of stroke relief and canvas weave",
    "Param.LineThreshold": "Line sensitivity",
    "Param.LineThreshold.Description": "How easily edges become ink lines",
    "Param.LineThickness": "Line thickness",
    "Param.LineThickness.Description": "Width of the drawn ink lines",
    "Param.Shading": "Pencil shading",
    "Param.Shading.Description": "Strength of the soft graphite tone",
    "Param.Wetness": "Wetness",
    "Param.Wetness.Description": "How strongly wet washes merge before settling",
    "Param.Pigments": "Pigment count",
    "Param.Pigments.Description": "Number of pigments kept in the palette",
    "Param.EdgePooling": "Edge pooling",
    "Param.EdgePooling.Description": "Pigment darkening gathered at wash boundaries",
    "Param.PaperGrain": "Paper grain",
    "Param.PaperGrain.Description": "Strength of the cold-press paper texture",
    "Preset.Mosaic.Name": "Mosaic",
    "Preset.Mosaic.Description": "Ceramic tile mosaic: flat tile colors, dark grout seams and a subtle glaze jitter.",
    "Preset.OilPainting.Name": "Van Gogh Oil",
    "Preset.OilPainting.Description": "Impasto oil painting: swirling brush dabs, bold limited palette and canvas texture.",
    "Preset.Sketch.Name": "Pencil Sketch",
    "Preset.Sketch.Description": "Minimal line drawing: ink outlines on paper with optional graphite shading.",
    "Preset.Watercolor.Name": "Watercolor",
    "Preset.Watercolor.Description": "Wet watercolor washes: soft pigment glazes, pooled dark edges and paper grain.",
    "Plugins.BuiltInPresets": "Built-in presets",
    "Plugins.BuiltInPresets.Hint": "Shipped with Viora — always available, nothing to install.",
    "Plugins.BuiltInTag": "Built-in",
    "Plugins.UsePreset": "Use",
    "Plugins.Installed": "Installed plugins",
})

with io.open('src/Viora.Localization/Assets/en.json', 'w', encoding='utf-8', newline='\n') as f:
    json.dump(en, f, ensure_ascii=False, indent=2)
    f.write('\n')

# ---- zh-Hans.json ----
with io.open('src/Viora.Localization/Assets/zh-Hans.json', encoding='utf-8') as f:
    zh = json.load(f)

zh["Home.Feature.Convert.Body"] = "用内置的多种风格预设把照片和插画变成作品——二次元矢量、马赛克、油画、简笔画、水彩。"

zh.update({
    "Param.TileSize": "砖块尺寸",
    "Param.TileSize.Description": "每块马赛克砖的边长(像素)",
    "Param.Grout": "砖缝强度",
    "Param.Grout.Description": "砖块之间接缝的加深程度",
    "Param.StrokeSize": "笔触尺寸",
    "Param.StrokeSize.Description": "笔触色块大小——越大细节越概括",
    "Param.PaintColors": "颜料数量",
    "Param.PaintColors.Description": "最终画面的调色板级数",
    "Param.Texture": "肌理强度",
    "Param.Texture.Description": "笔触起伏与画布纹理的深浅",
    "Param.LineThreshold": "线条敏感度",
    "Param.LineThreshold.Description": "边缘多容易被画成墨线",
    "Param.LineThickness": "线条粗细",
    "Param.LineThickness.Description": "墨线的绘制宽度",
    "Param.Shading": "铅笔调子",
    "Param.Shading.Description": "柔和石墨调子的强弱",
    "Param.Wetness": "湿润度",
    "Param.Wetness.Description": "湿画层互相融合的程度",
    "Param.Pigments": "颜料数量",
    "Param.Pigments.Description": "调色板中保留的颜料色数",
    "Param.EdgePooling": "边缘沉淀",
    "Param.EdgePooling.Description": "色块边界处颜料沉积加深的强度",
    "Param.PaperGrain": "纸纹",
    "Param.PaperGrain.Description": "水彩纸纹理的强弱",
    "Preset.Mosaic.Name": "马赛克",
    "Preset.Mosaic.Description": "瓷砖马赛克:分块平色、深色砖缝与轻微釉面抖动。",
    "Preset.OilPainting.Name": "梵高油画",
    "Preset.OilPainting.Description": "厚涂油画:流动的笔触色块、大胆的限定色板与画布肌理。",
    "Preset.Sketch.Name": "简笔画",
    "Preset.Sketch.Description": "极简线条画:纸面墨线勾勒,可叠加柔和的铅笔调子。",
    "Preset.Watercolor.Name": "水彩画",
    "Preset.Watercolor.Description": "湿画水彩:柔和颜料罩层、边缘沉淀与纸张纹理。",
    "Plugins.BuiltInPresets": "系统预设",
    "Plugins.BuiltInPresets.Hint": "随 Viora 内置提供,始终可用,无需安装。",
    "Plugins.BuiltInTag": "内置",
    "Plugins.UsePreset": "去转换",
    "Plugins.Installed": "已安装插件",
})

with io.open('src/Viora.Localization/Assets/zh-Hans.json', 'w', encoding='utf-8', newline='\n') as f:
    json.dump(zh, f, ensure_ascii=False, indent=2)
    f.write('\n')

print("en keys:", len(en), "zh keys:", len(zh))
