# -*- coding: utf-8 -*-
"""Regenerate the four non-default palettes with strongly differentiated tones."""
import re

TEMPLATE = open('src/Viora.UI/Themes/Tokens.xaml', encoding='utf-8').read()

def set_colors(s, mapping):
    for key, value in mapping.items():
        s = re.sub(rf'<Color x:Key="{key}">#[0-9A-Fa-f]+</Color>', f'<Color x:Key="{key}">{value}</Color>', s)
    return s

# accent hover/pressed derived per palette for punch
PALETTES = {
    'Tokens.Ocean': {
        'banner': 'Viora palette: Ocean (deep navy, dark)',
        'colors': {
            'Color.BackgroundColor': '#FF0A1120',
            'Color.SurfaceColor': '#FF101C33',
            'Color.SurfaceElevatedColor': '#FF172744',
            'Color.SurfaceHighColor': '#FF1F3258',
            'Color.PrimaryColor': '#FF4FC3F7',
            'Color.OnPrimaryColor': '#FF00344F',
            'Color.PrimaryContainerColor': '#FF0C5A86',
            'Color.OnPrimaryContainerColor': '#FFD6F3FF',
            'Color.SecondaryColor': '#FF80DEEA',
            'Color.SecondaryContainerColor': '#FF006978',
            'Color.TertiaryColor': '#FF82B1FF',
            'Color.TertiaryContainerColor': '#FF1E41AF',
            'Color.TextPrimaryColor': '#FFDFF2FF',
            'Color.TextSecondaryColor': '#FF93B5D4',
            'Color.TextDisabledColor': '#FF54718F',
            'Color.BorderColor': '#FF1D3352',
            'Color.OutlineColor': '#FF3A5A82',
            'Color.DividerColor': '#FF16243D',
            'Color.StateSelectedColor': '#2E4FC3F7',
            'Color.StateHoverColor': '#17FFFFFF',
            'Color.StatePressedColor': '#24FFFFFF',
        },
        'hover': '#FF7DD4FF', 'pressed': '#FF36A2D8',
    },
    'Tokens.Forest': {
        'banner': 'Viora palette: Forest (deep green, dark)',
        'colors': {
            'Color.BackgroundColor': '#FF0A1510',
            'Color.SurfaceColor': '#FF10241A',
            'Color.SurfaceElevatedColor': '#FF173426',
            'Color.SurfaceHighColor': '#FF1F4431',
            'Color.PrimaryColor': '#FF6EE7A0',
            'Color.OnPrimaryColor': '#FF00391E',
            'Color.PrimaryContainerColor': '#FF147A47',
            'Color.OnPrimaryContainerColor': '#FFDBFFE9',
            'Color.SecondaryColor': '#FFA3E635',
            'Color.SecondaryContainerColor': '#FF3F6212',
            'Color.TertiaryColor': '#FF5EEAD4',
            'Color.TertiaryContainerColor': '#FF115E59',
            'Color.TextPrimaryColor': '#FFE4FBEA',
            'Color.TextSecondaryColor': '#FF9CC3AC',
            'Color.TextDisabledColor': '#FF587A66',
            'Color.BorderColor': '#FF1C382A',
            'Color.OutlineColor': '#FF3A614B',
            'Color.DividerColor': '#FF16291E',
            'Color.StateSelectedColor': '#2E6EE7A0',
            'Color.StateHoverColor': '#17FFFFFF',
            'Color.StatePressedColor': '#24FFFFFF',
        },
        'hover': '#FF93F0B8', 'pressed': '#FF47C583',
    },
    'Tokens.Plum': {
        'banner': 'Viora palette: Plum (violet, dark)',
        'colors': {
            'Color.BackgroundColor': '#FF150E26',
            'Color.SurfaceColor': '#FF1F1638',
            'Color.SurfaceElevatedColor': '#FF2A1F4D',
            'Color.SurfaceHighColor': '#FF362866',
            'Color.PrimaryColor': '#FFC79BFF',
            'Color.OnPrimaryColor': '#FF3B1470',
            'Color.PrimaryContainerColor': '#FF6C3BD4',
            'Color.OnPrimaryContainerColor': '#FFF1E8FF',
            'Color.SecondaryColor': '#FFF0ABFF',
            'Color.SecondaryContainerColor': '#FF6B2188',
            'Color.TertiaryColor': '#FFFFB4C8',
            'Color.TertiaryContainerColor': '#FF8E3A63',
            'Color.TextPrimaryColor': '#FFF3EBFF',
            'Color.TextSecondaryColor': '#FFC0AEE0',
            'Color.TextDisabledColor': '#FF7A6C99',
            'Color.BorderColor': '#FF2E2249',
            'Color.OutlineColor': '#FF4D3D75',
            'Color.DividerColor': '#FF241A3D',
            'Color.StateSelectedColor': '#2EC79BFF',
            'Color.StateHoverColor': '#17FFFFFF',
            'Color.StatePressedColor': '#24FFFFFF',
        },
        'hover': '#FFD6B8FF', 'pressed': '#FFA878F0',
    },
    'Tokens.Sand': {
        'banner': 'Viora palette: Sand (warm cream, light)',
        'colors': {
            'Color.BackgroundColor': '#FFFBF3E4',
            'Color.SurfaceColor': '#FFFFFDF6',
            'Color.SurfaceElevatedColor': '#FFF4EAD8',
            'Color.SurfaceHighColor': '#FFEADDC5',
            'Color.PrimaryColor': '#FFC2410C',
            'Color.OnPrimaryColor': '#FFFFFFFF',
            'Color.PrimaryContainerColor': '#FFFFE0C2',
            'Color.OnPrimaryContainerColor': '#FF431B02',
            'Color.SecondaryColor': '#FFA16207',
            'Color.SecondaryContainerColor': '#FFFEF08A',
            'Color.TertiaryColor': '#FF9F1239',
            'Color.TertiaryContainerColor': '#FFFFD9E2',
            'Color.TextPrimaryColor': '#FF2D1B0E',
            'Color.TextSecondaryColor': '#FF78716C',
            'Color.TextDisabledColor': '#FFB8AFA3',
            'Color.BorderColor': '#FFE7DCC8',
            'Color.OutlineColor': '#FF8A7A5F',
            'Color.DividerColor': '#FFF2E9D8',
            'Color.StateSelectedColor': '#24C2410C',
            'Color.StateHoverColor': '#0E000000',
            'Color.StatePressedColor': '#16000000',
        },
        'hover': '#FFD95E1F', 'pressed': '#FF9A3408',
    },
}

for name, spec in PALETTES.items():
    s = TEMPLATE
    s = set_colors(s, spec['colors'])
    # banner comment
    s = s.replace('Viora Fluent Design Tokens (dark, default)', spec['banner'])
    # accent hover/pressed aliases are literal colors in the template
    s = re.sub(r'<SolidColorBrush x:Key="Color.AccentHover" Color="[^"]+" />',
               f'<SolidColorBrush x:Key="Color.AccentHover" Color="{spec["hover"]}" />', s)
    s = re.sub(r'<SolidColorBrush x:Key="Color.AccentPressed" Color="[^"]+" />',
               f'<SolidColorBrush x:Key="Color.AccentPressed" Color="{spec["pressed"]}" />', s)
    path = f'src/Viora.UI/Themes/{name}.xaml'
    open(path, 'w', encoding='utf-8').write(s)
    print(name, 'regenerated')
