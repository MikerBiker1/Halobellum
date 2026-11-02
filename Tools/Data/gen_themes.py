# Writes the UI theme assets (Data/UI) and links the faction themes. First-pass looks (DECISIONS D28).
import hashlib, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SCRIPT = "03e86a01e404793f605d1567c10f7231"


def guid(key):
    return hashlib.md5(("halobellum-theme-" + key).encode()).hexdigest()


def art_guid(rel):
    return hashlib.md5(("halobellum-ui-art-" + rel).encode()).hexdigest()


def tex(rel):
    return f"{{fileID: 2800000, guid: {art_guid('Assets/_Project/Art/' + rel)}, type: 3}}" if rel else "{fileID: 0}"


def font(rel):
    return f"{{fileID: 12800000, guid: {art_guid('Assets/_Project/Art/Imported/Fonts/' + rel)}, type: 3}}" if rel else "{fileID: 0}"


def col(c):
    r, g, b, a = (list(c) + [1])[:4]
    return f"{{r: {r}, g: {g}, b: {b}, a: {a}}}"


THEMES = {
    "UNSC": dict(name="UNSC", fonts=["Franklin Gothic Medium", "Arial Narrow", "Arial"], upper=1,
                 shape=1, corner=8, border=1, glow=0,
                 panel=(0.07, 0.082, 0.09, 0.93), edge=(0.42, 0.5, 0.55), glowc=(0, 0, 0, 0),
                 font="ChakraPetch/ChakraPetch-SemiBold.ttf", title_font="SairaSemiCondensed/SairaSemiCondensed-Bold.ttf",
                 font_scale=1.0, title_scale=1.08, text_shift=0.1, title_shift=0.24, detail_alpha=0.8, art="UI/UNSC/UNSC_", frame_border=48, fill_inset=21, ornament_offset=-3,
                 row_border=20, bar_segment=14, bar="BarSegment_Tile", button_border=12,
                 text=(0.9, 0.92, 0.9), dim=(0.72, 0.78, 0.76), title=(1, 0.8, 0.36), accent=(1, 0.72, 0.2),
                 button=(0.14, 0.17, 0.19, 0.96), hover=(0.2, 0.25, 0.27, 0.98), pressed=(0.1, 0.12, 0.13, 1),
                 btext=(0.92, 0.94, 0.92), disabled=(0.45, 0.48, 0.47)),
    "Covenant": dict(name="Covenant", fonts=["Candara", "Segoe UI", "Arial"], upper=0,
                     shape=2, corner=11, border=1, glow=4,
                     panel=(0.094, 0.047, 0.165, 0.925), edge=(0.72, 0.38, 1), glowc=(0.62, 0.3, 1, 0.45),
                     font="Electrolize/Electrolize-Regular.ttf", title_font="Audiowide/Audiowide-Regular.ttf",
                     font_scale=1.0, title_scale=0.92, text_shift=0.05, title_shift=0.09, detail_alpha=0.9, art="UI/Covenant/Covenant_", frame_border=60, fill_inset=18, ornament_offset=-7,
                     row_border=22, bar_segment=0, bar="BarFill", button_border=14,
                     text=(0.93, 0.9, 1), dim=(0.8, 0.75, 0.92), title=(0.55, 0.98, 0.95), accent=(0.35, 0.95, 0.9),
                     button=(0.2, 0.09, 0.3, 0.95), hover=(0.3, 0.13, 0.44, 0.98), pressed=(0.14, 0.06, 0.22, 1),
                     btext=(0.95, 0.92, 1), disabled=(0.5, 0.45, 0.58)),
    "Flood": dict(name="Flood", fonts=["Georgia", "Arial"], upper=0,
                  shape=2, corner=10, border=1, glow=3,
                  panel=(0.06, 0.066, 0.03, 0.93), edge=(0.55, 0.62, 0.25), glowc=(0.55, 0.72, 0.12, 0.4),
                  font="ChakraPetch/ChakraPetch-SemiBold.ttf", title_font="Creepster/Creepster-Regular.ttf",
                  font_scale=1.0, title_scale=1.12, text_shift=0.1, title_shift=0.08, detail_alpha=0.9, art="UI/Flood/Flood_", frame_border=60, fill_inset=19,
                  row_border=18, bar_segment=0, bar="BarFill", button_border=14,
                  text=(0.92, 0.93, 0.8), dim=(0.74, 0.76, 0.6), title=(0.78, 0.92, 0.3), accent=(0.78, 0.95, 0.25),
                  button=(0.2, 0.22, 0.09, 0.95), hover=(0.3, 0.33, 0.13, 0.98), pressed=(0.12, 0.13, 0.05, 1),
                  btext=(0.95, 0.96, 0.82), disabled=(0.47, 0.49, 0.38)),
    "Neutral": dict(name="Neutral", fonts=["Arial"], upper=0,
                    shape=0, corner=3, border=1, glow=0,
                    panel=(0.08, 0.08, 0.09, 0.88), edge=(0.35, 0.36, 0.4), glowc=(0, 0, 0, 0),
                    text=(0.95, 0.95, 0.95), dim=(0.78, 0.78, 0.8), title=(1, 1, 1), accent=(0.45, 0.75, 1),
                    button=(0.2, 0.21, 0.24, 0.95), hover=(0.28, 0.3, 0.34, 0.98), pressed=(0.14, 0.15, 0.17, 1),
                    btext=(1, 1, 1), disabled=(0.55, 0.55, 0.55)),
}

for key, t in THEMES.items():
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
             "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
             f"  m_Script: {{fileID: 11500000, guid: {SCRIPT}, type: 3}}", f"  m_Name: {key}_Theme", "  m_EditorClassIdentifier: ",
             f"  themeName: {t['name']}", f"  fontAsset: {font(t.get('font'))}", f"  titleFontAsset: {font(t.get('title_font'))}", "  osFontNames:"]
    lines += [f"  - {f}" for f in t["fonts"]]
    art = t.get("art")
    lines += [f"  uppercaseTitles: {t['upper']}", f"  fontScale: {t.get('font_scale', 1)}", f"  titleFontScale: {t.get('title_scale', 1)}",
              f"  textShift: {t.get('text_shift', 0)}", f"  titleTextShift: {t.get('title_shift', 0)}",
              "  textureScale: 0.5",
              f"  panelFrame: {tex(art + 'PanelFrame.png' if art else None)}", f"  panelFrameBorder: {t.get('frame_border', 48)}",
              f"  panelFillInset: {t.get('fill_inset', 15)}",
              f"  panelDetail: {tex(art + 'Hull_Tile.png' if art else None)}", f"  panelDetailTint: {{r: 1, g: 1, b: 1, a: {t.get('detail_alpha', 1)}}}",
              f"  panelOrnament: {tex(art + 'Ornament.png' if art else None)}", f"  ornamentOffset: {t.get('ornament_offset', -3)}",
              f"  rowFrame: {tex(art + 'Row.png' if art else None)}", f"  rowFrameBorder: {t.get('row_border', 20)}",
              f"  barFill: {tex(art + t['bar'] + '.png' if art else None)}", f"  barSegmentWidth: {t.get('bar_segment', 0)}",
              f"  buttonNormal: {tex(art + 'Button.png' if art else None)}", f"  buttonHover: {tex(art + 'ButtonHover.png' if art else None)}",
              f"  buttonPressed: {tex(art + 'ButtonPressed.png' if art else None)}", f"  buttonBorder: {t.get('button_border', 12)}",
              f"  hexEmpty: {tex((art or 'UI/UNSC/UNSC_') + 'HexEmpty.png')}", f"  hexFilled: {tex((art or 'UI/UNSC/UNSC_') + 'HexFilled.png')}"]
    lines += [f"  panelShape: {t['shape']}", f"  cornerSize: {t['corner']}",
              f"  borderThickness: {t['border']}", f"  glowSize: {t['glow']}",
              f"  panelColor: {col(t['panel'])}", f"  borderColor: {col(t['edge'])}", f"  glowColor: {col(t['glowc'])}",
              f"  textColor: {col(t['text'])}", f"  dimTextColor: {col(t['dim'])}", f"  titleColor: {col(t['title'])}",
              f"  accentColor: {col(t['accent'])}", f"  buttonColor: {col(t['button'])}", f"  buttonHoverColor: {col(t['hover'])}",
              f"  buttonPressedColor: {col(t['pressed'])}", f"  buttonTextColor: {col(t['btext'])}",
              f"  disabledTextColor: {col(t['disabled'])}"]
    path = os.path.join(ROOT, "Assets", "_Project", "Data", "UI", f"{key}_Theme.asset")
    open(path, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
    if not os.path.exists(path + ".meta"):
        open(path + ".meta", "w", encoding="utf-8", newline="\n").write(
            f"fileFormatVersion: 2\nguid: {guid(key)}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

for faction, asset in (("UNSC", "UNSC_Standard.asset"), ("Covenant", "Covenant_Standard.asset"), ("Flood", "Flood_Standard.asset")):
    path = os.path.join(ROOT, "Assets", "_Project", "Data", "Factions", asset)
    s = open(path, encoding="utf-8").read()
    s = re.sub(r"  uiTheme: .*\n", "", s)
    s = re.sub(r"(  color: \{.*\}\n)", r"\1" + f"  uiTheme: {{fileID: 11400000, guid: {guid(faction)}, type: 2}}\n", s)
    open(path, "w", encoding="utf-8", newline="\n").write(s)
print("ok")
