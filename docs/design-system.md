# Viora Design System

The Viora Design System is encoded as WPF `ResourceDictionary` tokens (`src/Viora.UI/Themes/`)
derived from the NavigationBar extraction in `00-inspection-and-plan.md` §3. No page XAML
hardcodes colors, radii, or font sizes; every visual value is a named token.

## 1. Token source (from the extraction)

| Extraction observation | Viora token translation |
|---|---|
| Dark anchored window `#222222` + light floating bar `#DDDDDD` | Dark shell `Color.Background #1C1C24` with elevated surfaces; light theme provided as `Tokens.Light.xaml` |
| Rounded bar (`CornerRadius=10`), circles (r=40/34) | `Radius.Large=10` (cards/surfaces), `Radius.Medium=8` (controls), `Radius.Small=4` |
| Opacity-based text hierarchy (`#44333333` → `#333333`) | `Color.Text.Primary / .Secondary / .Disabled` ramps instead of alpha stacking |
| `CubicEaseInOut` 500 ms motion | `Motion.Quick 120ms` (hover), `Motion.Standard 200ms` (selection), `Motion.Slow 350ms` (panels), shared `CubicEase` |
| Traveling circle indicator | Selection pill in the sidebar (animated background swap) |
| Spacious 20-unit rhythm | Spacing scale 4/8/12/16/24/32 |
| Playful-minimal, flat, no shadows | Flat controls; two restrained elevation tokens (`Shadow.Card`, `Shadow.Flyout`) for popups only |

## 2. Token files

- `Themes/Tokens.xaml` — color roles, typography styles, spacing, radii, elevation, motion (dark default)
- `Themes/Tokens.Light.xaml` — light theme color roles (swapped at runtime by `ThemeManager`)
- `Themes/Controls.xaml` — `VioraButton`, `SecondaryButton`, `DangerButton`, `IconButton`,
  `VioraToggle`, `VioraSlider`, `VioraCombo`, `VioraTextBox`, `VioraCard`, `LinkButton`, `FormCheckBox`
- `Themes/Navigation.xaml` — sidebar list styles (`NavGroupHeader`, `NavListBox`, `NavListBoxItem`)
- `Themes/PageChrome.xaml` — shared page header/scroll template (`PageChrome` control)

## 3. Color roles

| Token | Dark | Light | Use |
|---|---|---|---|
| `Color.Background` | `#1C1C24` | `#F2F2F5` | Window base |
| `Color.Surface` | `#26262F` | `#FFFFFF` | Cards, sidebar |
| `Color.SurfaceElevated` | `#2F2F3A` | `#EDEDF2` | Inputs, hover fills, popups |
| `Color.Accent` | `#5AC8FA` | `#2E9FD8` | Primary actions, selection |
| `Color.Text.Primary/Secondary/Disabled` | `#F2F2F5` / `#A8A8B4` / `#5C5C68` | inverted ramps | Text hierarchy |
| `Color.Danger / Success / Warning` | semantic | semantic | Errors, confirmations, states |

## 4. Information architecture (§7.3 rationale)

Sidebar groups, ordered by product surface:

1. **Create** — Home, Convert. The primary loop of the product.
2. **System** — Plugins, Settings. Product infrastructure.
3. **Information** — About, Help, Feedback, Community, Sponsor, Legal. Secondary cluster kept
   visually grouped so it never competes with feature navigation; new features slot into
   *Create* while the cluster stays stable.

Group headers are localized; entries are data (`NavigationItem` records), not hard-coded XAML
per page, so future plugins adding panels extend the same IA.

## 5. Rules

1. No raw WPF chrome: every shipped control has a Viora style.
2. No literals: colors/radii/sizes come from tokens only (`DynamicResource` for theme swaps).
3. Hierarchy via typography weight/size/color first, borders/boxes second.
4. Every interactive element defines hover/pressed/disabled in its style — one definition, reused.
5. Motion is easing-consistent (`CubicEaseInOut` family), short on controls, longer on panels.
