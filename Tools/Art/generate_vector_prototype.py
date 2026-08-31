from __future__ import annotations

import json
import struct
import subprocess
import tempfile
import xml.etree.ElementTree as ET
import zlib
from dataclasses import dataclass
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[2]
ART_ROOT = PROJECT_ROOT / "Assets" / "Game" / "Art" / "VectorPrototype"
SVG_ROOT = ART_ROOT / "SVG"
PNG_ROOT = ART_ROOT / "PNG"
EDGE = Path(r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe")

INK = "#3B2B28"
INK_SOFT = "#5A4137"
GRASS = "#94BE72"
GRASS_LIGHT = "#B8D58A"
GRASS_DARK = "#628D55"
DIRT = "#D5AD70"
DIRT_LIGHT = "#E6CA91"
DIRT_DARK = "#A67D4D"
WOOD = "#9A643F"
WOOD_LIGHT = "#C88A55"
WOOD_DARK = "#68412F"
STONE = "#D7D1B8"
STONE_LIGHT = "#EFE8CE"
STONE_DARK = "#8C8A78"
FRIENDLY = "#E87332"
FRIENDLY_LIGHT = "#F6B647"
ENEMY = "#765187"
ENEMY_LIGHT = "#A67AB4"
DANGER = "#A83E39"
CREAM = "#F5E9C8"
SHADOW = "#1D1715"


@dataclass(frozen=True)
class Asset:
    name: str
    width: int
    height: int
    body: str


def svg(asset: Asset) -> str:
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{asset.width}" height="{asset.height}" viewBox="0 0 {asset.width} {asset.height}">
<defs>
  <filter id="softShadow" x="-30%" y="-30%" width="160%" height="170%">
    <feDropShadow dx="0" dy="7" stdDeviation="5" flood-color="{SHADOW}" flood-opacity="0.28"/>
  </filter>
  <linearGradient id="grassWash" x1="0" y1="0" x2="0" y2="1"><stop stop-color="{GRASS_LIGHT}"/><stop offset="1" stop-color="{GRASS}"/></linearGradient>
  <linearGradient id="dirtWash" x1="0" y1="0" x2="0" y2="1"><stop stop-color="{DIRT_LIGHT}"/><stop offset="1" stop-color="{DIRT}"/></linearGradient>
  <linearGradient id="friendlyWash" x1="0" y1="0" x2="0" y2="1"><stop stop-color="{FRIENDLY_LIGHT}"/><stop offset="1" stop-color="{FRIENDLY}"/></linearGradient>
</defs>
{asset.body}
</svg>'''


def outline(width: int = 8) -> str:
    return f'stroke="{INK}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"'


def double_stroke(path_data: str, color: str, width: int, extra: int = 7) -> str:
    return (
        f'<path d="{path_data}" fill="none" stroke="{INK}" stroke-width="{width + extra}" stroke-linecap="round" stroke-linejoin="round"/>'
        f'<path d="{path_data}" fill="none" stroke="{color}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"/>'
    )


def tree_group(x: int, y: int, scale: float = 1.0) -> str:
    return f'''<g transform="translate({x} {y}) scale({scale})">
  <ellipse cx="0" cy="55" rx="43" ry="13" fill="{SHADOW}" opacity=".18"/>
  <path d="M-10 48 L-8 10 L8 10 L12 49 Z" fill="{WOOD}" {outline(5)}/>
  <path d="M0 -62 C-35 -55 -47 -28 -29 -9 C-55 -3 -50 31 -19 31 C-6 49 22 45 28 27 C57 25 59 -12 35 -21 C43 -48 20 -66 0 -62Z" fill="{GRASS_DARK}" {outline(6)}/>
  <path d="M-17 -43 C-32 -25 -25 -10 -6 -7 C-14 10 3 20 18 11 C36 3 31 -23 16 -28 C15 -43 -2 -49 -17 -43Z" fill="{GRASS}" opacity=".92"/>
</g>'''


def stone_group(x: int, y: int, scale: float = 1.0) -> str:
    return f'''<g transform="translate({x} {y}) scale({scale})">
  <ellipse cx="0" cy="34" rx="45" ry="12" fill="{SHADOW}" opacity=".18"/>
  <path d="M-43 27 L-33 -15 L-8 -32 L22 -24 L43 5 L31 31 L-10 38 Z" fill="{STONE}" {outline(6)}/>
  <path d="M-31 -12 L-8 -25 L4 -3 L-16 13 Z" fill="{STONE_LIGHT}" opacity=".82"/>
  <path d="M7 -4 L24 -18 L36 4 L27 22 Z" fill="{STONE_DARK}" opacity=".65"/>
</g>'''


def berry_group(x: int, y: int, scale: float = 1.0) -> str:
    berries = ''.join(
        f'<circle cx="{bx}" cy="{by}" r="8" fill="{DANGER}" {outline(3)}/>'
        for bx, by in [(-26, 4), (-8, -15), (13, 1), (28, -11), (-1, 17)]
    )
    return f'''<g transform="translate({x} {y}) scale({scale})">
  <ellipse cx="0" cy="27" rx="44" ry="12" fill="{SHADOW}" opacity=".16"/>
  <path d="M-46 15 C-51 -14 -24 -34 -3 -19 C13 -39 48 -20 42 8 C35 32 -28 39 -46 15Z" fill="{GRASS_DARK}" {outline(6)}/>
  <path d="M-31 4 C-22 -10 -6 -2 0 11 M4 7 C10 -9 27 -7 34 6" fill="none" stroke="{GRASS_LIGHT}" stroke-width="7" stroke-linecap="round"/>
  {berries}
</g>'''


def building_slot() -> Asset:
    body = f'''<g filter="url(#softShadow)">
  <path d="M28 40 Q28 24 44 24 H212 Q228 24 228 40 V216 Q228 232 212 232 H44 Q28 232 28 216Z" fill="{WOOD_DARK}" {outline(8)}/>
  <path d="M43 51 Q43 40 55 40 H201 Q213 40 213 51 V204 Q213 216 201 216 H55 Q43 216 43 204Z" fill="url(#dirtWash)" {outline(5)}/>
  <path d="M56 74 C91 58 143 66 199 52 M52 132 C99 118 152 126 204 108 M56 190 C102 177 151 188 201 168" fill="none" stroke="{DIRT_DARK}" stroke-width="8" opacity=".45" stroke-linecap="round"/>
  <circle cx="51" cy="48" r="7" fill="{STONE_LIGHT}" {outline(3)}/><circle cx="205" cy="48" r="7" fill="{STONE_LIGHT}" {outline(3)}/>
  <circle cx="51" cy="208" r="7" fill="{STONE_LIGHT}" {outline(3)}/><circle cx="205" cy="208" r="7" fill="{STONE_LIGHT}" {outline(3)}/>
</g>'''
    return Asset("prop_building_slot", 256, 256, body)


def lumber_camp() -> Asset:
    body = f'''<ellipse cx="128" cy="220" rx="92" ry="19" fill="{SHADOW}" opacity=".2"/>
<g filter="url(#softShadow)">
  <path d="M45 102 L128 38 L214 102 V211 H45Z" fill="{WOOD_LIGHT}" {outline(9)}/>
  <path d="M31 105 L128 25 L228 105 L208 121 L128 61 L49 122Z" fill="{FRIENDLY}" {outline(9)}/>
  <path d="M71 113 H185 V211 H71Z" fill="{WOOD}" {outline(7)}/>
  <rect x="105" y="139" width="48" height="72" rx="5" fill="{WOOD_DARK}" {outline(6)}/>
  <rect x="82" y="128" width="30" height="30" rx="4" fill="{CREAM}" {outline(5)}/>
  <path d="M35 191 H88 M38 177 H91 M43 163 H95" stroke="{WOOD_DARK}" stroke-width="13" stroke-linecap="round"/>
  {double_stroke("M183 160 L211 190 M202 151 L178 198", STONE_LIGHT, 8, 5)}
</g>'''
    return Asset("building_lumber_camp", 256, 256, body)


def barracks() -> Asset:
    body = f'''<ellipse cx="128" cy="222" rx="94" ry="18" fill="{SHADOW}" opacity=".2"/>
<g filter="url(#softShadow)">
  <path d="M43 103 L128 31 L217 103 V214 H43Z" fill="{STONE}" {outline(9)}/>
  <path d="M29 106 L128 21 L230 106 L210 124 L128 61 L48 125Z" fill="{DANGER}" {outline(9)}/>
  <path d="M62 122 H194 V214 H62Z" fill="{STONE_LIGHT}" {outline(7)}/>
  <path d="M104 214 V143 Q128 119 152 143 V214" fill="{WOOD_DARK}" {outline(7)}/>
  <circle cx="181" cy="145" r="27" fill="url(#friendlyWash)" {outline(7)}/>
  <path d="M181 124 V166 M160 145 H202" stroke="{CREAM}" stroke-width="7" stroke-linecap="round"/>
  <path d="M72 80 L72 26 M184 80 L184 26" stroke="{INK}" stroke-width="7"/>
  <path d="M72 31 L97 43 L72 55Z M184 31 L159 43 L184 55Z" fill="{FRIENDLY}" {outline(4)}/>
</g>'''
    return Asset("building_barracks", 256, 256, body)


def engineer_yard() -> Asset:
    body = f'''<ellipse cx="128" cy="223" rx="96" ry="18" fill="{SHADOW}" opacity=".2"/>
<g filter="url(#softShadow)">
  <path d="M35 106 L128 42 L222 106 V214 H35Z" fill="{WOOD_LIGHT}" {outline(9)}/>
  <path d="M23 111 L128 31 L234 111 L213 130 L128 70 L44 130Z" fill="{FRIENDLY_LIGHT}" {outline(9)}/>
  <path d="M55 130 H201 V214 H55Z" fill="{WOOD}" {outline(7)}/>
  <rect x="78" y="158" width="100" height="56" rx="5" fill="{WOOD_DARK}" {outline(6)}/>
  <circle cx="128" cy="143" r="34" fill="{STONE}" {outline(7)}/>
  <circle cx="128" cy="143" r="13" fill="{WOOD_DARK}" {outline(5)}/>
  <path d="M128 101 V115 M128 171 V185 M86 143 H100 M156 143 H170 M98 113 L108 123 M148 163 L158 173 M158 113 L148 123 M108 163 L98 173" stroke="{INK}" stroke-width="9" stroke-linecap="round"/>
  {double_stroke("M45 185 L79 151 M48 153 L80 185", STONE_LIGHT, 9, 5)}
</g>'''
    return Asset("building_engineer_yard", 256, 256, body)


def worker() -> Asset:
    body = f'''<ellipse cx="128" cy="226" rx="61" ry="15" fill="{SHADOW}" opacity=".22"/>
<g filter="url(#softShadow)">
  <path d="M92 121 Q128 95 164 121 L174 198 Q128 224 82 198Z" fill="{DIRT}" {outline(8)}/>
  <circle cx="128" cy="82" r="39" fill="{CREAM}" {outline(8)}/>
  <path d="M91 75 Q99 35 128 35 Q163 36 169 76Z" fill="{FRIENDLY_LIGHT}" {outline(8)}/>
  <path d="M103 85 Q128 99 153 85" fill="none" stroke="{INK_SOFT}" stroke-width="5"/>
  <circle cx="115" cy="78" r="4" fill="{INK}"/><circle cx="142" cy="78" r="4" fill="{INK}"/>
  {double_stroke("M92 130 L62 177 M165 132 L194 169", DIRT, 22, 8)}
  {double_stroke("M99 195 L90 224 M157 195 L167 224", WOOD_DARK, 22, 8)}
  <path d="M164 121 Q202 118 210 155 V190 H171Z" fill="{WOOD}" {outline(7)}/>
  <path d="M180 126 V188 M196 136 V188" stroke="{WOOD_LIGHT}" stroke-width="6"/>
  {double_stroke("M58 174 L80 197", STONE_LIGHT, 9, 5)}
</g>'''
    return Asset("unit_worker", 256, 256, body)


def quarry() -> Asset:
    body = f'''<ellipse cx="128" cy="226" rx="98" ry="17" fill="{SHADOW}" opacity=".2"/>
<g filter="url(#softShadow)"><path d="M31 205 L58 112 L108 75 L157 96 L201 70 L229 205Z" fill="{STONE_DARK}" {outline(9)}/>
<path d="M48 198 L72 126 L111 99 L145 119 L198 91 L214 198Z" fill="{STONE}" {outline(6)}/>
<path d="M74 143 L113 122 L145 141 M161 116 L191 101" stroke="{STONE_LIGHT}" stroke-width="8" stroke-linecap="round"/>
{double_stroke("M55 92 L116 188", WOOD_DARK, 12, 6)}<path d="M40 76 L75 68 L93 101 L61 115Z" fill="{STONE_LIGHT}" {outline(6)}/></g>'''
    return Asset("building_quarry", 256, 256, body)


def farm() -> Asset:
    body = f'''<ellipse cx="128" cy="226" rx="103" ry="17" fill="{SHADOW}" opacity=".2"/>
<g filter="url(#softShadow)"><path d="M30 117 L128 48 L226 117 V214 H30Z" fill="{CREAM}" {outline(9)}/>
<path d="M19 122 L128 34 L237 122 L215 140 L128 76 L41 140Z" fill="{FRIENDLY_LIGHT}" {outline(9)}/>
<path d="M58 139 H198 V214 H58Z" fill="{DIRT_LIGHT}" {outline(7)}/>
<path d="M105 214 V161 H151 V214" fill="{WOOD_DARK}" {outline(6)}/>
<path d="M42 203 H87 M169 203 H214" stroke="{GRASS_DARK}" stroke-width="13" stroke-linecap="round"/>
<path d="M55 203 V174 M72 203 V165 M184 203 V168 M201 203 V177" stroke="{FRIENDLY_LIGHT}" stroke-width="7"/></g>'''
    return Asset("building_farm", 256, 256, body)


def archer_range() -> Asset:
    body = f'''<ellipse cx="128" cy="226" rx="99" ry="17" fill="{SHADOW}" opacity=".2"/>
<g filter="url(#softShadow)"><path d="M35 113 L128 47 L221 113 V214 H35Z" fill="{WOOD}" {outline(9)}/>
<path d="M24 116 L128 34 L232 116 L211 135 L128 74 L45 135Z" fill="{FRIENDLY}" {outline(9)}/>
<circle cx="128" cy="153" r="45" fill="{CREAM}" {outline(7)}/><circle cx="128" cy="153" r="27" fill="{DANGER}"/><circle cx="128" cy="153" r="10" fill="{CREAM}"/>
{double_stroke("M72 204 L190 91", WOOD_DARK, 10, 5)}<path d="M190 91 L177 122 L207 109Z" fill="{STONE_LIGHT}" {outline(5)}/></g>'''
    return Asset("building_archer_range", 256, 256, body)


def archer() -> Asset:
    body = f'''<ellipse cx="128" cy="228" rx="63" ry="15" fill="{SHADOW}" opacity=".22"/>
<g filter="url(#softShadow)"><circle cx="126" cy="78" r="38" fill="{CREAM}" {outline(8)}/>
<path d="M88 78 Q96 34 127 34 Q158 36 166 78Z" fill="{FRIENDLY}" {outline(8)}/>
<path d="M91 121 Q127 98 163 121 L171 199 Q128 220 84 199Z" fill="{FRIENDLY_LIGHT}" {outline(8)}/>
{double_stroke("M105 198 L96 226 M150 198 L161 226", WOOD_DARK, 22, 8)}
<path d="M58 75 Q24 129 58 188 M58 75 Q94 129 58 188" fill="none" stroke="{WOOD_DARK}" stroke-width="9"/>
<path d="M52 131 H205" stroke="{CREAM}" stroke-width="4"/><path d="M205 131 L181 116 V146Z" fill="{STONE_LIGHT}" {outline(4)}/></g>'''
    return Asset("unit_archer", 256, 256, body)


def shield_soldier() -> Asset:
    body = f'''<ellipse cx="128" cy="227" rx="64" ry="15" fill="{SHADOW}" opacity=".22"/>
<g filter="url(#softShadow)">
  <circle cx="126" cy="77" r="38" fill="{CREAM}" {outline(8)}/>
  <path d="M89 72 Q95 34 126 34 Q160 36 165 73Z" fill="{STONE_DARK}" {outline(8)}/>
  <path d="M91 119 Q127 96 164 119 L171 199 Q128 219 84 198Z" fill="url(#friendlyWash)" {outline(8)}/>
  {double_stroke("M105 197 L96 225 M151 197 L160 225", WOOD_DARK, 22, 8)}
  {double_stroke("M179 54 L193 218", WOOD_DARK, 9, 5)}<path d="M179 48 L196 79 L177 76Z" fill="{STONE_LIGHT}" {outline(5)}/>
  <path d="M51 116 Q89 91 123 119 V180 Q90 214 51 180Z" fill="{FRIENDLY}" {outline(9)}/>
  <path d="M87 113 V194 M57 151 H116" stroke="{CREAM}" stroke-width="7" stroke-linecap="round"/>
</g>'''
    return Asset("unit_shield_soldier", 256, 256, body)


def raider() -> Asset:
    body = f'''<ellipse cx="128" cy="228" rx="63" ry="15" fill="{SHADOW}" opacity=".22"/>
<g filter="url(#softShadow)">
  <circle cx="128" cy="79" r="38" fill="{STONE_LIGHT}" {outline(8)}/>
  <path d="M86 85 Q91 34 128 30 Q168 36 171 90 L151 74 L128 89 L104 72Z" fill="{ENEMY}" {outline(8)}/>
  <path d="M92 120 Q127 96 164 121 L174 199 Q128 220 82 199Z" fill="{ENEMY}" {outline(8)}/>
  {double_stroke("M104 198 L94 226 M151 198 L162 226", WOOD_DARK, 22, 8)}
  <circle cx="115" cy="81" r="5" fill="{DANGER}"/><circle cx="142" cy="81" r="5" fill="{DANGER}"/>
  {double_stroke("M79 128 L47 174 M166 127 L195 159", ENEMY_LIGHT, 20, 8)}
  <path d="M190 151 Q221 166 197 205 Q180 211 169 194 Q196 181 190 151Z" fill="{STONE_LIGHT}" {outline(7)}/>
  {double_stroke("M45 168 L72 190", STONE_LIGHT, 10, 5)}
</g>'''
    return Asset("unit_raider", 256, 256, body)


def castle() -> Asset:
    body = f'''<ellipse cx="128" cy="230" rx="111" ry="16" fill="{SHADOW}" opacity=".22"/>
<g filter="url(#softShadow)">
  <path d="M28 55 H72 V34 H103 V55 H153 V34 H184 V55 H228 V220 H28Z" fill="{STONE}" {outline(9)}/>
  <path d="M42 75 H214 V218 H42Z" fill="{STONE_LIGHT}" {outline(6)}/>
  <path d="M67 218 V139 Q89 113 111 139 V218 M145 218 V139 Q167 113 189 139 V218" fill="{WOOD_DARK}" {outline(7)}/>
  <path d="M66 159 H112 M144 159 H190" stroke="{STONE_DARK}" stroke-width="7"/>
  <path d="M43 95 H214 M44 121 H214 M44 181 H214" stroke="{STONE_DARK}" stroke-width="5" opacity=".55"/>
  <path d="M85 35 V18 M171 35 V18" stroke="{INK}" stroke-width="6"/><path d="M85 18 L111 31 L85 44Z M171 18 L145 31 L171 44Z" fill="{FRIENDLY}" {outline(4)}/>
</g>'''
    return Asset("prop_castle", 256, 256, body)


def supply_node() -> Asset:
    body = tree_group(92, 116, .88) + berry_group(170, 164, .82) + f'''<g transform="translate(133 184)" filter="url(#softShadow)">
  <path d="M-42 -19 H42 V44 H-42Z" fill="{WOOD_LIGHT}" {outline(7)}/><path d="M-42 -19 L0 -43 L42 -19 L0 3Z" fill="{DIRT_LIGHT}" {outline(7)}/>
  <path d="M0 3 V44 M-42 -19 L0 3 L42 -19" fill="none" stroke="{WOOD_DARK}" stroke-width="6"/>
</g>'''
    return Asset("prop_supply_node", 256, 256, body)


def terrain_asset(name: str, kind: str) -> Asset:
    if kind == "grass":
        body = f'''<rect width="256" height="256" fill="url(#grassWash)"/><path d="M18 61 L30 42 M61 211 L73 189 M188 53 L201 32 M221 184 L235 164 M112 123 L123 104" stroke="{GRASS_DARK}" stroke-width="7" stroke-linecap="round" opacity=".55"/><circle cx="53" cy="100" r="6" fill="{DIRT_LIGHT}"/><circle cx="174" cy="202" r="5" fill="{DIRT_LIGHT}"/>'''
    elif kind == "dirt":
        body = f'''<rect width="256" height="256" fill="url(#dirtWash)"/><path d="M21 58 C62 72 81 41 121 59 S191 77 236 52 M12 172 C51 155 83 184 126 166 S198 147 247 177" fill="none" stroke="{DIRT_DARK}" stroke-width="9" opacity=".4"/><circle cx="70" cy="121" r="7" fill="{STONE_LIGHT}"/><circle cx="190" cy="113" r="5" fill="{STONE_DARK}"/>'''
    else:
        body = f'''<path d="M128 8 V248" stroke="{CREAM}" stroke-width="10" stroke-dasharray="18 15" opacity=".8"/><path d="M98 128 L158 88 V112 H214 V144 H158 V168Z" fill="{FRIENDLY}" {outline(7)}/><circle cx="128" cy="128" r="23" fill="{FRIENDLY_LIGHT}" {outline(7)}/>'''
    return Asset(name, 256, 256, body)


def ui_asset(name: str, kind: str, width: int = 512, height: int = 256) -> Asset:
    if kind == "panel":
        body = f'''<rect x="12" y="12" width="{width-24}" height="{height-24}" rx="30" fill="{WOOD_DARK}" {outline(10)}/><rect x="27" y="27" width="{width-54}" height="{height-54}" rx="20" fill="{CREAM}" stroke="{WOOD_LIGHT}" stroke-width="7"/><path d="M45 52 H{width-45} M45 {height-52} H{width-45}" stroke="{DIRT_DARK}" stroke-width="5" opacity=".55"/>'''
    elif kind == "danger_panel":
        body = f'''<rect x="12" y="12" width="{width-24}" height="{height-24}" rx="30" fill="{INK}" {outline(10)}/><rect x="27" y="27" width="{width-54}" height="{height-54}" rx="20" fill="{DANGER}" stroke="{FRIENDLY_LIGHT}" stroke-width="7"/><path d="M45 52 H{width-45} M45 {height-52} H{width-45}" stroke="{CREAM}" stroke-width="5" opacity=".35"/>'''
    elif kind == "topbar":
        body = f'''<path d="M8 8 H{width-8} V{height-30} Q{width//2} {height+6} 8 {height-30}Z" fill="{WOOD_DARK}" {outline(8)}/><path d="M28 22 H{width-28} V{height-42} H28Z" fill="{INK}" stroke="{WOOD_LIGHT}" stroke-width="5"/><circle cx="48" cy="{height//2}" r="13" fill="{FRIENDLY_LIGHT}"/><circle cx="{width-48}" cy="{height//2}" r="13" fill="{FRIENDLY_LIGHT}"/>'''
    elif kind == "slot":
        return building_slot().__class__(name, 256, 160, f'''<rect x="10" y="10" width="236" height="140" rx="20" fill="{WOOD_DARK}" {outline(8)}/><rect x="24" y="24" width="208" height="112" rx="14" fill="{DIRT_LIGHT}" stroke="{WOOD_LIGHT}" stroke-width="6"/><path d="M42 112 C83 90 143 111 214 78" fill="none" stroke="{DIRT_DARK}" stroke-width="8" opacity=".45"/>''')
    else:
        fill = {"primary": FRIENDLY, "secondary": WOOD, "danger": DANGER}.get(kind, WOOD)
        hi = FRIENDLY_LIGHT if kind == "primary" else WOOD_LIGHT
        body = f'''<rect x="10" y="10" width="{width-20}" height="{height-20}" rx="24" fill="{WOOD_DARK}" {outline(8)}/><rect x="22" y="20" width="{width-44}" height="{height-46}" rx="16" fill="{fill}" stroke="{hi}" stroke-width="6"/><path d="M36 {height-37} H{width-36}" stroke="{INK}" stroke-width="7" opacity=".35"/>'''
    return Asset(name, width, height, body)


def screen_backdrop(name: str, variant: str) -> Asset:
    if variant == "boot":
        body = f'''<rect width="1920" height="1080" fill="#BDD6DD"/><path d="M0 0 H1920 V430 Q1450 260 1040 390 T0 350Z" fill="#F4C477"/><circle cx="270" cy="245" r="92" fill="#FFE6A3"/><path d="M0 590 Q320 420 650 590 T1240 555 T1920 520 V1080 H0Z" fill="#9BB876"/><path d="M0 710 Q430 550 840 700 T1500 650 T1920 640 V1080 H0Z" fill="#789A62"/><path d="M0 1080 V580 H130 V420 H260 V580 H390 V1080Z" fill="#C69C69" stroke="{INK}" stroke-width="16"/><path d="M1510 1080 V500 H1620 V320 H1745 V500 H1860 V1080Z" fill="#565169" stroke="{INK}" stroke-width="16"/><path d="M500 860 C850 700 1160 770 1470 680" fill="none" stroke="#DAB178" stroke-width="110" stroke-linecap="round"/>'''
    elif variant == "selection":
        body = f'''<rect width="1920" height="1080" fill="#211B18"/><rect x="22" y="98" width="1876" height="956" rx="28" fill="#4B3425" stroke="{INK}" stroke-width="18"/><path d="M0 0 H1920 V96 H0Z" fill="#171311"/><path d="M72 104 V1050 M1210 104 V1050" stroke="#A56E3D" stroke-width="12"/><path d="M0 820 Q190 700 390 850 V1080 H0Z M1920 800 Q1720 690 1580 860 V1080 H1920Z" fill="#2E4250" opacity=".6"/>'''
    else:
        body = f'''<rect width="1920" height="1080" fill="#729758"/><path d="M0 0 H422 V1080 H0Z" fill="#8BAE63"/><path d="M422 0 H518 V1080 H422Z" fill="#C9B990"/><path d="M518 0 H1824 V1080 H518Z" fill="#83A65F"/><path d="M1824 0 H1920 V1080 H1824Z" fill="#555066"/><path d="M518 455 C780 350 980 510 1230 430 S1580 350 1824 485 V700 C1590 570 1410 690 1190 615 S790 570 518 700Z" fill="#C9A56A"/><path d="M540 575 C820 480 1030 630 1280 535 S1590 500 1800 600" fill="none" stroke="#9E7749" stroke-width="14" opacity=".42"/><g opacity=".32"><path d="M660 80 V980 M930 80 V980 M1200 80 V980 M1470 80 V980 M1740 80 V980" stroke="#F0E2B6" stroke-width="5" stroke-dasharray="15 14"/><path d="M535 340 H1810 M535 700 H1810" stroke="#F0E2B6" stroke-width="5" stroke-dasharray="15 14"/></g>'''
    return Asset(name, 1920, 1080, body)


def wall_asset(name: str, enemy: bool) -> Asset:
    base = ENEMY if enemy else STONE
    light = ENEMY_LIGHT if enemy else STONE_LIGHT
    blocks = ''.join(f'<rect x="8" y="{y}" width="80" height="52" rx="8" fill="{light if y % 104 == 0 else base}" stroke="{INK_SOFT}" stroke-width="5"/>' for y in range(0, 1080, 52))
    return Asset(name, 96, 1080, f'<rect width="96" height="1080" fill="{base}"/>{blocks}<path d="M4 0 V1080 M92 0 V1080" stroke="{INK}" stroke-width="9"/>')


def emblem(name: str, symbol: str, fill: str) -> Asset:
    body = f'''<circle cx="128" cy="128" r="108" fill="{WOOD_DARK}" {outline(9)}/><circle cx="128" cy="128" r="88" fill="{fill}" stroke="{WOOD_LIGHT}" stroke-width="8"/><text x="128" y="151" text-anchor="middle" font-family="Arial" font-size="66" font-weight="bold" fill="{CREAM}" stroke="{INK}" stroke-width="3">{symbol}</text>'''
    return Asset(name, 256, 256, body)


def card_frame() -> Asset:
    body = f'''<rect x="12" y="12" width="296" height="416" rx="24" fill="{WOOD_DARK}" {outline(10)}/><rect x="28" y="28" width="264" height="380" rx="17" fill="{CREAM}" stroke="{WOOD_LIGHT}" stroke-width="7"/><rect x="44" y="52" width="232" height="210" rx="14" fill="#B8C89B" stroke="{DIRT_DARK}" stroke-width="6"/><path d="M44 286 H276 M44 338 H276" stroke="{DIRT_DARK}" stroke-width="5" opacity=".55"/>'''
    return Asset("ui_card_frame", 320, 440, body)


def boss_golem() -> Asset:
    body = f'''<ellipse cx="256" cy="456" rx="166" ry="28" fill="{SHADOW}" opacity=".28"/><g filter="url(#softShadow)"><path d="M142 182 L216 92 L312 106 L374 206 L344 390 L256 444 L160 394Z" fill="#8F704A" {outline(14)}/><path d="M96 218 L151 190 L174 334 L111 382 L68 319Z M413 216 L362 190 L338 334 L403 382 L447 316Z" fill="#A27B48" {outline(13)}/><path d="M201 176 L254 138 L313 178 L294 244 L220 244Z" fill="#C39448" {outline(10)}/><circle cx="232" cy="202" r="10" fill="#FFD357"/><circle cx="278" cy="202" r="10" fill="#FFD357"/></g>'''
    return Asset("boss_stone_golem", 512, 512, body)


def blueprint_asset() -> Asset:
    body = f'''<rect x="12" y="12" width="232" height="232" rx="20" fill="#2DAA91" fill-opacity=".42" stroke="#86F1D0" stroke-width="8"/><path d="M32 32 H224 V224 H32Z M32 96 H224 M32 160 H224 M96 32 V224 M160 32 V224" fill="none" stroke="#B6FFE8" stroke-width="4" opacity=".72"/><path d="M64 190 V110 L128 62 L194 110 V190Z" fill="none" stroke="{CREAM}" stroke-width="10" stroke-dasharray="15 10"/>'''
    return Asset("ui_blueprint", 256, 256, body)


def tower_site() -> Asset:
    body = f'''<ellipse cx="128" cy="222" rx="96" ry="18" fill="{SHADOW}" opacity=".2"/><path d="M54 212 L74 82 H182 L202 212Z" fill="{WOOD_LIGHT}" fill-opacity=".5" {outline(8)}/><path d="M74 82 L182 212 M182 82 L74 212 M48 148 H208" stroke="{WOOD_DARK}" stroke-width="13"/><rect x="47" y="63" width="162" height="24" rx="8" fill="{FRIENDLY_LIGHT}" {outline(6)}/>'''
    return Asset("prop_arrow_tower_site", 256, 256, body)


def sawmill() -> Asset:
    body = f'''<ellipse cx="128" cy="224" rx="98" ry="18" fill="{SHADOW}" opacity=".2"/>
<g filter="url(#softShadow)"><path d="M30 112 L128 42 L226 112 V214 H30Z" fill="{WOOD_LIGHT}" {outline(9)}/>
<path d="M19 116 L128 30 L237 116 L214 136 L128 72 L42 136Z" fill="{FRIENDLY}" {outline(9)}/>
<rect x="52" y="132" width="152" height="82" rx="8" fill="{WOOD}" {outline(7)}/>
<circle cx="128" cy="163" r="42" fill="{STONE_LIGHT}" {outline(8)}/><circle cx="128" cy="163" r="10" fill="{WOOD_DARK}" {outline(5)}/>
<path d="M128 121 L137 151 L168 142 L144 163 L168 184 L137 175 L128 205 L119 175 L88 184 L112 163 L88 142 L119 151Z" fill="{STONE_DARK}"/>
<path d="M45 207 H92 M164 207 H211" stroke="{WOOD_DARK}" stroke-width="13" stroke-linecap="round"/></g>'''
    return Asset("building_sawmill", 256, 256, body)


def shield_camp() -> Asset:
    base = barracks()
    return Asset("building_shield_camp", base.width, base.height, base.body)


def p0_state_asset(name: str, kind: str) -> Asset:
    colors = {"locked": STONE_DARK, "blocked": DANGER, "ready": GRASS_DARK, "active": FRIENDLY, "done": "#2DAA91"}
    fill = colors.get(kind, WOOD)
    ring = f'''<circle cx="128" cy="128" r="105" fill="{WOOD_DARK}" {outline(8)}/><circle cx="128" cy="128" r="84" fill="{fill}" stroke="{CREAM}" stroke-width="7"/>'''
    symbols = {
        "outbound": double_stroke("M70 142 H176 M145 108 L180 142 L145 176", CREAM, 13, 7),
        "gather": double_stroke("M82 180 L169 82 M105 76 L178 149", CREAM, 13, 7),
        "return": double_stroke("M186 142 H80 M111 108 L76 142 L111 176", CREAM, 13, 7),
        "blocked": f'<path d="M128 67 L194 186 H62Z" fill="{CREAM}" {outline(7)}/><path d="M128 104 V148" stroke="{DANGER}" stroke-width="14"/><circle cx="128" cy="169" r="8" fill="{DANGER}"/>',
        "paused": f'<rect x="91" y="82" width="25" height="92" rx="8" fill="{CREAM}"/><rect x="140" y="82" width="25" height="92" rx="8" fill="{CREAM}"/>',
        "hidden": f'<path d="M61 128 Q128 67 195 128 Q128 189 61 128Z" fill="none" stroke="{CREAM}" stroke-width="10"/><path d="M64 192 L192 64" stroke="{DANGER}" stroke-width="14"/>',
        "locked": f'<rect x="78" y="119" width="100" height="76" rx="14" fill="{CREAM}" {outline(7)}/><path d="M95 119 V96 Q95 62 128 62 Q161 62 161 96 V119" fill="none" stroke="{CREAM}" stroke-width="15"/>',
        "ready": f'<path d="M67 132 L109 174 L191 84" fill="none" stroke="{CREAM}" stroke-width="18" stroke-linecap="round" stroke-linejoin="round"/>',
        "active": f'<path d="M128 65 A63 63 0 1 1 81 86" fill="none" stroke="{CREAM}" stroke-width="15"/><path d="M64 64 L107 70 L76 102Z" fill="{CREAM}"/>',
        "max": f'<path d="M128 58 L148 105 L199 110 L160 143 L172 194 L128 167 L84 194 L96 143 L57 110 L108 105Z" fill="{CREAM}" {outline(5)}/>',
        "waiting": f'<circle cx="128" cy="128" r="54" fill="none" stroke="{CREAM}" stroke-width="12"/><path d="M128 128 V89 M128 128 L160 148" stroke="{CREAM}" stroke-width="12" stroke-linecap="round"/>',
        "training": f'<path d="M82 182 L174 74 M101 71 L185 155" stroke="{CREAM}" stroke-width="15" stroke-linecap="round"/><circle cx="91" cy="173" r="17" fill="{CREAM}"/><circle cx="176" cy="82" r="17" fill="{CREAM}"/>',
        "done": f'<path d="M66 132 L108 176 L194 80" fill="none" stroke="{CREAM}" stroke-width="19" stroke-linecap="round"/>',
    }
    return Asset(name, 256, 256, ring + symbols[kind])


def p1_unit(name: str, role: str, enemy: bool = False) -> Asset:
    color = ENEMY if enemy else FRIENDLY
    symbol = {"archer": "A", "ram": "R", "builder": "B"}[role]
    body = f'''<ellipse cx="128" cy="220" rx="76" ry="16" fill="{SHADOW}" opacity=".2"/>
<path d="M66 205 L82 102 L128 58 L176 102 L192 205Z" fill="{color}" {outline(9)}/>
<circle cx="128" cy="88" r="34" fill="{CREAM}" {outline(7)}/>
<text x="128" y="163" text-anchor="middle" font-family="Arial" font-size="62" font-weight="bold" fill="{CREAM}" stroke="{INK}" stroke-width="3">{symbol}</text>'''
    return Asset(name, 256, 256, body)


def p1_tower(name: str, enemy: bool, site: bool) -> Asset:
    color = ENEMY if enemy else FRIENDLY
    if site:
        body = f'''<ellipse cx="128" cy="222" rx="96" ry="18" fill="{SHADOW}" opacity=".2"/><path d="M54 212 L74 82 H182 L202 212Z" fill="{WOOD_LIGHT}" fill-opacity=".5" {outline(8)}/><path d="M74 82 L182 212 M182 82 L74 212 M48 148 H208" stroke="{WOOD_DARK}" stroke-width="13"/><rect x="47" y="63" width="162" height="24" rx="8" fill="{color}" {outline(6)}/>'''
    else:
        body = f'''<ellipse cx="128" cy="222" rx="92" ry="18" fill="{SHADOW}" opacity=".2"/><path d="M70 216 L84 94 H172 L188 216Z" fill="{STONE}" {outline(9)}/><path d="M55 104 L74 52 H182 L201 104Z" fill="{color}" {outline(9)}/><path d="M93 52 V92 M128 52 V92 M163 52 V92" stroke="{CREAM}" stroke-width="11"/><path d="M128 97 L128 154" stroke="{WOOD_DARK}" stroke-width="12"/><path d="M128 105 L205 78" stroke="{INK}" stroke-width="9"/>'''
    return Asset(name, 256, 256, body)


def wall_crack(name: str, level: int) -> Asset:
    paths = ["M128 22 L115 91 L145 126 L119 222", "M32 82 L94 111 L72 178 M224 55 L166 113 L189 205", "M20 210 L78 155 L42 98 M236 214 L177 159 L218 91"]
    body = ''.join(double_stroke(path, DANGER, 7 + level * 2, 5) for path in paths[:level])
    return Asset(name, 256, 256, body)


def all_assets() -> list[Asset]:
    return [
        screen_backdrop("backdrop_boot", "boot"), screen_backdrop("backdrop_selection", "selection"), screen_backdrop("backdrop_gameplay", "gameplay"),
        wall_asset("prop_wall_friendly", False), wall_asset("prop_wall_enemy", True), boss_golem(), blueprint_asset(), tower_site(),
        sawmill(), shield_camp(),
        p0_state_asset("state_worker_outbound", "outbound"), p0_state_asset("state_worker_gathering", "gather"),
        p0_state_asset("state_worker_returning", "return"), p0_state_asset("state_missing_input", "blocked"),
        p0_state_asset("state_paused", "paused"), p0_state_asset("state_upgrade_hidden", "hidden"),
        p0_state_asset("state_upgrade_locked", "locked"), p0_state_asset("state_upgrade_ready", "ready"),
        p0_state_asset("state_upgrade_upgrading", "active"), p0_state_asset("state_upgrade_max", "max"),
        p0_state_asset("state_training_waiting", "waiting"), p0_state_asset("state_training_active", "training"),
        p0_state_asset("state_training_deployed", "done"),
        terrain_asset("terrain_grass", "grass"), terrain_asset("terrain_dirt", "dirt"), terrain_asset("terrain_frontline", "frontline"),
        Asset("prop_tree", 256, 256, tree_group(128, 136, 1.45)),
        Asset("prop_stone", 256, 256, stone_group(128, 140, 1.55)),
        Asset("prop_berry", 256, 256, berry_group(128, 140, 1.55)),
        building_slot(), lumber_camp(), quarry(), farm(), barracks(), archer_range(), engineer_yard(),
        worker(), shield_soldier(), archer(), raider(),
        supply_node(), castle(),
        emblem("icon_food", "食", "#B55A3C"), emblem("icon_meat", "肉", "#C87052"), emblem("icon_wine", "酒", "#B98345"),
        emblem("icon_wood", "木", "#97633E"), emblem("icon_plank", "板", "#BE8655"), emblem("icon_ore", "矿", "#88877D"),
        emblem("icon_stone", "石", "#A5A394"), emblem("icon_iron", "铁", "#526D7C"), emblem("icon_ingot", "锭", "#77746B"),
        emblem("icon_reward_core", "✦", FRIENDLY), card_frame(),
        ui_asset("ui_panel", "panel"), ui_asset("ui_panel_danger", "danger_panel"),
        ui_asset("ui_topbar", "topbar", 1024, 128), ui_asset("ui_slot", "slot", 256, 160),
        ui_asset("ui_button_primary", "primary", 256, 96), ui_asset("ui_button_secondary", "secondary", 256, 96),
        ui_asset("ui_button_danger", "danger", 256, 96),
        p1_unit("unit_archer_friendly", "archer"), p1_unit("unit_archer_enemy", "archer", True),
        p1_unit("unit_siege_ram_friendly", "ram"), p1_unit("unit_siege_ram_enemy", "ram", True),
        p1_unit("unit_builder_friendly", "builder"), p1_unit("unit_builder_enemy", "builder", True),
        p1_tower("building_arrow_tower_site_friendly", False, True), p1_tower("building_arrow_tower_site_enemy", True, True),
        p1_tower("building_arrow_tower_friendly", False, False), p1_tower("building_arrow_tower_enemy", True, False),
        wall_crack("overlay_wall_crack_01", 1), wall_crack("overlay_wall_crack_02", 2), wall_crack("overlay_wall_crack_03", 3),
        emblem("card_research_attack", "ATK", DANGER), emblem("card_research_defense", "DEF", STONE_DARK), emblem("card_research_tactics", "TAC", "#2DAA91"),
        emblem("icon_tab_soldier", "S", FRIENDLY), emblem("icon_tab_item", "I", WOOD),
        emblem("card_arrow_tower", "T", STONE_DARK), emblem("card_arrow_rain", "AR", ENEMY),
        emblem("card_field_rations", "F", GRASS_DARK), emblem("card_emergency_supplies", "+", FRIENDLY),
        emblem("pickup_boss_reward_core", "*", FRIENDLY),
    ]


def render_with_edge(svg_path: Path, png_path: Path, width: int, height: int) -> None:
    if not EDGE.exists():
        raise FileNotFoundError(f"Microsoft Edge not found: {EDGE}")
    with tempfile.TemporaryDirectory(prefix="fortress-vector-") as profile:
        overscan_path = Path(profile) / "render.png"
        command = [
            str(EDGE), "--headless=new", "--disable-gpu", "--hide-scrollbars",
            "--force-device-scale-factor=1", "--default-background-color=00000000",
            # Chromium's outer-window size can reserve a title-bar-height strip even in
            # headless mode. Render with vertical overscan and crop the actual PNG so
            # the SVG viewBox always occupies the complete requested output.
            f"--window-size={width},{height + 128}", f"--screenshot={overscan_path}",
            f"--user-data-dir={profile}", svg_path.resolve().as_uri(),
        ]
        completed = subprocess.run(command, capture_output=True, text=True, timeout=45)
        if completed.returncode != 0 or not overscan_path.exists():
            raise RuntimeError(f"Edge failed for {svg_path.name}: {completed.stderr.strip()}")
        crop_png_top_left(overscan_path, png_path, width, height)


def crop_png_top_left(source: Path, destination: Path, crop_width: int, crop_height: int) -> None:
    payload = source.read_bytes()
    if payload[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"Invalid PNG signature: {source}")

    position = 8
    header = None
    compressed_parts = []
    while position < len(payload):
        length = struct.unpack(">I", payload[position:position + 4])[0]
        chunk_type = payload[position + 4:position + 8]
        chunk_data = payload[position + 8:position + 8 + length]
        position += 12 + length
        if chunk_type == b"IHDR":
            header = struct.unpack(">IIBBBBB", chunk_data)
        elif chunk_type == b"IDAT":
            compressed_parts.append(chunk_data)
        elif chunk_type == b"IEND":
            break

    if header is None:
        raise ValueError(f"Missing PNG header: {source}")
    width, height, bit_depth, color_type, compression, filtering, interlace = header
    if bit_depth != 8 or color_type not in (4, 6) or interlace != 0:
        raise ValueError(f"Unsupported Edge PNG format: bit={bit_depth}, color={color_type}, interlace={interlace}")
    if crop_width > width or crop_height > height:
        raise ValueError(f"Crop {(crop_width, crop_height)} exceeds source {(width, height)}")

    bytes_per_pixel = 4 if color_type == 6 else 2
    stride = width * bytes_per_pixel
    raw = zlib.decompress(b"".join(compressed_parts))
    rows = []
    previous = bytearray(stride)
    offset = 0
    for _ in range(height):
        filter_type = raw[offset]
        encoded = raw[offset + 1:offset + 1 + stride]
        offset += stride + 1
        decoded = bytearray(stride)
        for index, value in enumerate(encoded):
            left = decoded[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            up = previous[index]
            up_left = previous[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            if filter_type == 0:
                decoded[index] = value
            elif filter_type == 1:
                decoded[index] = (value + left) & 0xFF
            elif filter_type == 2:
                decoded[index] = (value + up) & 0xFF
            elif filter_type == 3:
                decoded[index] = (value + ((left + up) // 2)) & 0xFF
            elif filter_type == 4:
                estimate = left + up - up_left
                pa = abs(estimate - left)
                pb = abs(estimate - up)
                pc = abs(estimate - up_left)
                predictor = left if pa <= pb and pa <= pc else up if pb <= pc else up_left
                decoded[index] = (value + predictor) & 0xFF
            else:
                raise ValueError(f"Unsupported PNG filter {filter_type}: {source}")
        rows.append(decoded)
        previous = decoded

    cropped_stride = crop_width * bytes_per_pixel
    cropped_raw = b"".join(b"\x00" + bytes(row[:cropped_stride]) for row in rows[:crop_height])

    def chunk(chunk_type: bytes, chunk_data: bytes) -> bytes:
        checksum = zlib.crc32(chunk_type + chunk_data) & 0xFFFFFFFF
        return struct.pack(">I", len(chunk_data)) + chunk_type + chunk_data + struct.pack(">I", checksum)

    new_header = struct.pack(">IIBBBBB", crop_width, crop_height, bit_depth, color_type, compression, filtering, interlace)
    destination.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", new_header)
        + chunk(b"IDAT", zlib.compress(cropped_raw, level=9))
        + chunk(b"IEND", b"")
    )


def validate_png(png_path: Path, expected_width: int, expected_height: int) -> None:
    with png_path.open("rb") as stream:
        if stream.read(8) != b"\x89PNG\r\n\x1a\n":
            raise ValueError(f"Invalid PNG signature: {png_path}")
        length = struct.unpack(">I", stream.read(4))[0]
        chunk_type = stream.read(4)
        if chunk_type != b"IHDR" or length != 13:
            raise ValueError(f"Missing PNG IHDR: {png_path}")
        width, height, _bit_depth, color_type, _compression, _filter, _interlace = struct.unpack(">IIBBBBB", stream.read(13))
        if (width, height) != (expected_width, expected_height):
            raise ValueError(f"Unexpected PNG dimensions for {png_path.name}: {(width, height)}")
        if color_type not in (4, 6):
            raise ValueError(f"PNG does not contain alpha: {png_path.name}, color type {color_type}")


def main() -> None:
    SVG_ROOT.mkdir(parents=True, exist_ok=True)
    PNG_ROOT.mkdir(parents=True, exist_ok=True)
    manifest = []
    for asset in all_assets():
        svg_path = SVG_ROOT / f"{asset.name}.svg"
        png_path = PNG_ROOT / f"{asset.name}.png"
        svg_path.write_text(svg(asset), encoding="utf-8")
        ET.parse(svg_path)
        render_with_edge(svg_path, png_path, asset.width, asset.height)
        validate_png(png_path, asset.width, asset.height)
        manifest.append({"name": asset.name, "width": asset.width, "height": asset.height, "svg": str(svg_path.relative_to(PROJECT_ROOT)).replace("\\", "/"), "png": str(png_path.relative_to(PROJECT_ROOT)).replace("\\", "/")})
    (ART_ROOT / "manifest.json").write_text(json.dumps({"assets": manifest}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Generated {len(manifest)} SVG masters and transparent PNG sprites in {ART_ROOT}")


if __name__ == "__main__":
    main()
