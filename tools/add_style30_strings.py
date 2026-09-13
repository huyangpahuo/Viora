import json, io

styles = [
    ("Comic", "漫画", "粗黑描边、漫画阴影、动态分镜感", "Comic Book", "Bold ink outlines, comic shading, dynamic panel energy."),
    ("PixelArt", "像素艺术", "像素块、有限色板、低分辨率重构", "Pixel Art", "Chunky pixels, limited palette, low-res rebuild."),
    ("LowPoly", "低多边形", "三角面切割、几何光影、立体重构", "Low Poly", "Triangular facets with geometric shading."),
    ("Clay", "黏土雕塑", "黏土材质、圆润体积、手工模型感", "Clay Sculpture", "Soft rounded clay volumes, hand-modeled look."),
    ("PaperCut", "剪纸", "多层纸片、阴影叠层、平面剪裁", "Paper Cut", "Layered paper sheets with stacked shadows."),
    ("Woodcut", "木刻版画", "雕刻线条、高反差、粗粝纹理", "Woodcut", "Carved hatch lines and raw high contrast."),
    ("Risograph", "孔版印刷", "少色印刷、套色错位、颗粒噪点", "Risograph", "Two-ink riso print with misregistration and grain."),
    ("Halftone", "半色调", "印刷网点、点阵明暗、复古印刷感", "Halftone", "Classic print dots shaping light and shade."),
    ("Newspaper", "报纸印刷", "新闻纸、黑白油墨、旧报纸质感", "Newspaper", "Black-and-white ink on aged newsprint."),
    ("NeonCyberpunk", "霓虹赛博", "霓虹光源、夜景、未来科技感", "Neon Cyberpunk", "Neon glow light sources, futuristic night mood."),
    ("Glitch", "故障艺术", "RGB分离、数字撕裂、信号错误", "Glitch Art", "RGB split, digital tearing, signal errors."),
    ("CRT", "CRT复古", "扫描线、屏幕弯曲、荧光像素", "CRT Retro", "Scanlines, tube curvature, phosphor glow."),
    ("Holographic", "全息", "彩虹折射、金属光泽、虹彩渐变", "Holographic", "Iridescent rainbow refraction with metallic sheen."),
    ("Blueprint", "蓝图", "工程线稿、网格底、技术制图感", "Blueprint", "White drafting lines on blueprint grid."),
    ("XRay", "X光", "半透明结构、冷色医学成像", "X-Ray", "Translucent structures in cold medical imaging."),
    ("Thermal", "热成像", "温度伪彩、热源高亮、监控感", "Thermal Camera", "Ironbow false color with hot highlights."),
    ("Infrared", "红外摄影", "红外植物、异常色彩、梦幻摄影", "Infrared", "Dreamlike infrared false-color photography."),
    ("DoubleExposure", "双重曝光", "两幅影像融合、轮廓叠加", "Double Exposure", "Two images fused with screen-blended silhouettes."),
    ("FilmNegative", "胶片负片", "负片色彩、胶片颗粒、暗房感", "Film Negative", "Inverted negative with film grain, darkroom mood."),
    ("Polaroid", "拍立得", "即时成像、褪色、相纸边框", "Polaroid", "Faded instant photo with a paper frame."),
    ("Collage", "拼贴艺术", "素材撕裂、重叠、剪贴组合", "Collage", "Torn, overlapping paper collage."),
    ("Embroidery", "刺绣", "线迹、布料纤维、针脚图像", "Embroidery", "Thread stitches forming the image on fabric."),
    ("Knitted", "针织", "毛线纹理、编织结构、柔软立体", "Knitted", "Wool stitches in a soft knitted structure."),
    ("StainedGlass", "彩色玻璃", "彩色玻璃块、铅条分割、透光", "Stained Glass", "Glass panes separated by lead lines."),
    ("Porcelain", "瓷器", "光滑陶瓷、釉面反光、精致感", "Porcelain", "Glazed ceramic with delicate specular sheen."),
    ("Origami", "折纸", "折痕、纸张结构、几何立体", "Origami", "Creased paper facets, geometric folds."),
    ("Chalkboard", "黑板粉笔", "粉笔颗粒、黑板底、手写感", "Chalkboard", "Chalk strokes and dust on a slate board."),
    ("WaxCrayon", "蜡笔", "蜡质笔触、粗糙涂抹、童趣", "Wax Crayon", "Waxy crayon strokes on rough paper."),
    ("AsciiArt", "ASCII字符画", "字符密度形成明暗和轮廓", "ASCII Art", "Luminance rendered as character density."),
    ("IsometricDiorama", "等距微缩场景", "等距视角、微缩模型、游戏地图感", "Isometric Diorama", "Miniature isometric diorama scene."),
]

generic = {
    "Size": ("尺寸", "Side length / scale of the effect's building block",
             "效果基本单元的边长或尺度", "Size"),
    "Colors": ("色彩数", "Colors kept in the stylized palette", "风格化调色板中保留的色数", "Colors"),
    "Intensity": ("强度", "Overall strength of the effect", "该效果的整体强度", "Intensity"),
    "Contrast": ("对比度", "Tonal contrast of the result", "结果的明暗对比强度", "Contrast"),
    "Grain": ("颗粒", "Amount of photographic/print grain", "胶片或印刷颗粒的多少", "Grain"),
    "Glow": ("光晕", "Strength of the bloom/glow pass", "辉光效果的强度", "Glow"),
    "Offset": ("偏移", "Displacement used by the effect", "效果使用的位移量", "Offset"),
    "Density": ("密度", "How dense the pattern/texture is", "图案或纹理的密集程度", "Density"),
    "Thickness": ("线宽", "Width of the drawn lines", "描画线条的宽度", "Line width"),
    "Detail": ("细节", "How much edge detail is kept", "保留的边缘细节量", "Detail"),
    "Texture": ("纹理", "Strength of the material texture", "材质纹理的强度", "Texture"),
}

zh_add = {}
en_add = {}
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
