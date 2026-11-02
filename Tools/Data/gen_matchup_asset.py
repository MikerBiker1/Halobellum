# Writes Data/AI/MatchupTable.asset from a counter-web matrix CSV (row unit's win rate vs column unit).
import csv, hashlib, os, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
SCRIPT_GUID = "18f573ec65622716199bfdc1300f78c5"
src = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "Docs", "M4_counter_web_matrix.csv")
rows = list(csv.reader(open(src, encoding="utf-8")))
cols = rows[0][1:]


def q(s):
    return "'" + s.replace("'", "''") + "'" if any(ch in s for ch in "(),:#") else s


lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
         "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
         "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
         f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}", "  m_Name: MatchupTable",
         "  m_EditorClassIdentifier: ",
         "  source: 'Counter web, tuning console (same simulation code) 2026-11-01, about 1800 credits per army, 10 battles per pair'",
         "  entries:"]
for r in rows[1:]:
    for j, v in enumerate(r[1:]):
        if v == "":
            continue
        lines += [f"  - attacker: {q(r[0])}", f"    defender: {q(cols[j])}", f"    winRate: {float(v):g}"]
out = os.path.join(ROOT, "Assets", "_Project", "Data", "AI", "MatchupTable.asset")
open(out, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
meta = out + ".meta"
if not os.path.exists(meta):
    g = hashlib.md5(b"halobellum-m5-matchup-table").hexdigest()
    open(meta, "w", encoding="utf-8", newline="\n").write(
        f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
print("entries:", sum(1 for l in lines if l.startswith("  - attacker")))
