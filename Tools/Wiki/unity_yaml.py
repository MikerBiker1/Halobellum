# unity_yaml.py
# A small reader for the Unity YAML that Halobellum's data assets are saved in (ScriptableObjects: units, techs, spells,
# cards, factions, rules), so the wiki builder needs only the Python standard library. It reads the subset those files use:
# block mappings, lists (also at the same indent as their key, as Unity writes them), flow mappings such as
# {fileID: 0, guid: ..., type: 2}, quoted and plain scalars, and scalars that wrap onto the next lines.
# Not a general YAML reader. Depends on: nothing.

import os
import re

_NUMBER = re.compile(r"^-?(\d+\.?\d*|\.\d+)([eE][-+]?\d+)?$")
# Keys whose values look like numbers but are byte strings (Unity writes small enum lists as hex).
RAW_KEYS = {"blockedDamageTypes"}


def _scalar(text, key=None):
    text = text.strip()
    if key in RAW_KEYS:
        return text
    if text == "" or text == "~" or text == "null":
        return None
    if text == "[]":
        return []
    if text == "{}":
        return {}
    if text.startswith("{") and text.endswith("}"):
        return _flow_map(text[1:-1])
    if text.startswith("'") and text.endswith("'") and len(text) >= 2:
        return text[1:-1].replace("''", "'")
    if text.startswith('"') and text.endswith('"') and len(text) >= 2:
        return bytes(text[1:-1], "utf-8").decode("unicode_escape")
    if _NUMBER.match(text):
        number = float(text)
        return int(number) if re.match(r"^-?\d+$", text) else number
    return text


def _flow_map(body):
    result = {}
    for part in _split_flow(body):
        if ":" not in part:
            continue
        key, value = part.split(":", 1)
        result[key.strip()] = _scalar(value)
    return result


def _split_flow(body):
    parts, depth, current = [], 0, ""
    for c in body:
        if c in "{[":
            depth += 1
        elif c in "}]":
            depth -= 1
        if c == "," and depth == 0:
            parts.append(current)
            current = ""
        else:
            current += c
    if current.strip():
        parts.append(current)
    return parts


def _indent(line):
    return len(line) - len(line.lstrip(" "))


class _Reader:
    def __init__(self, lines):
        self.lines = lines
        self.i = 0

    def peek(self):
        while self.i < len(self.lines) and not self.lines[self.i].strip():
            self.i += 1
        return self.lines[self.i] if self.i < len(self.lines) else None

    def block(self, indent):
        line = self.peek()
        if line is None:
            return None
        if line.lstrip().startswith("- ") or line.strip() == "-":
            return self.sequence(_indent(line))
        return self.mapping(_indent(line))

    def sequence(self, indent):
        items = []
        while True:
            line = self.peek()
            if line is None or _indent(line) != indent or not (line.lstrip().startswith("- ") or line.strip() == "-"):
                return items
            rest = line.lstrip()[2:] if line.strip() != "-" else ""
            if rest.strip() == "":
                self.i += 1
                items.append(self.block(indent + 2))
            elif re.match(r"^[^'\"{\[][^:]*:( |$)", rest):
                # "- key: value" opens a mapping indented two past the dash.
                self.lines[self.i] = " " * (indent + 2) + rest
                items.append(self.mapping(indent + 2))
            else:
                self.i += 1
                items.append(self._continued(rest, indent, None))
        return items

    def mapping(self, indent):
        result = {}
        while True:
            line = self.peek()
            if line is None or _indent(line) != indent:
                return result
            stripped = line.strip()
            if stripped.startswith("- "):
                return result
            if ":" not in stripped:
                self.i += 1
                continue
            key, value = stripped.split(":", 1)
            key = key.strip()
            self.i += 1
            if value.strip() == "":
                nxt = self.peek()
                if nxt is not None and (_indent(nxt) > indent or (_indent(nxt) == indent and nxt.lstrip().startswith("- "))):
                    result[key] = self.block(indent)
                else:
                    result[key] = "" if key in RAW_KEYS else None
            else:
                result[key] = self._continued(value.strip(), indent, key)

    def _continued(self, value, indent, key):
        # A quoted string can wrap until its closing quote; a plain one wraps onto deeper-indented lines.
        if value[:1] in ("'", '"'):
            quote = value[0]
            while not _closed(value, quote):
                nxt = self.peek()
                if nxt is None:
                    break
                self.i += 1
                value += ("\n" if nxt.strip() == "" else " ") + nxt.strip()
            return _scalar(value, key)
        if value.startswith("{") and not value.endswith("}"):
            while not value.endswith("}"):
                nxt = self.peek()
                if nxt is None:
                    break
                self.i += 1
                value += " " + nxt.strip()
            return _scalar(value, key)
        while True:
            nxt = self.peek()
            if nxt is None or _indent(nxt) <= indent or nxt.lstrip().startswith("- "):
                break
            self.i += 1
            value += " " + nxt.strip()
        return _scalar(value, key)


def _closed(value, quote):
    body = value[1:]
    if quote == "'":
        body = body.replace("''", "")
    else:
        body = body.replace('\\"', "")
    return quote in body


def load_asset(path):
    """The fields of the first MonoBehaviour in a Unity .asset file, as a dict (m_ fields included)."""
    with open(path, encoding="utf-8") as f:
        text = f.read()
    lines = text.splitlines()
    start = next((n for n, l in enumerate(lines) if l.startswith("MonoBehaviour:")), None)
    if start is None:
        return {}
    body = []
    for line in lines[start + 1:]:
        if line.startswith("--- "):
            break
        body.append(line)
    return _Reader(body).block(2) or {}


def guid_index(root):
    """Every asset's GUID under root (from its .meta file), mapped to its path."""
    index = {}
    for folder, _, files in os.walk(root):
        for name in files:
            if not name.endswith(".meta"):
                continue
            path = os.path.join(folder, name)
            with open(path, encoding="utf-8", errors="ignore") as f:
                for line in f:
                    if line.startswith("guid:"):
                        index[line.split(":", 1)[1].strip()] = path[:-5]
                        break
    return index
