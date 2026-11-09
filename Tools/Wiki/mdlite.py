# mdlite.py
# Turns the wiki's written pages (Tools/Wiki/content/*.md) into HTML, with only the Python standard library.
# Reads a small Markdown subset: "---" front matter (key: value lines), # to #### headings (## and ### make the page's
# contents list), paragraphs, - and 1. lists (one level of nesting by indenting two or more spaces), | tables |,
# > quotes (a quote starting "**Tip:**", "**Note:**" or "**Warning:**" becomes a coloured box), ``` code blocks, ---
# rules, and **bold**, *italic*, `code`, [text](link). A line that is only {{name}} becomes a live widget the wiki
# draws from the game's data (see WIDGETS in Tools/Wiki/site/wiki.js), e.g. {{damage-table}}.
# Depends on: nothing.

import html
import re


def front_matter(text):
    meta = {}
    if text.startswith("---"):
        end = text.find("\n---", 3)
        if end > 0:
            for line in text[3:end].strip().splitlines():
                if ":" in line:
                    k, v = line.split(":", 1)
                    meta[k.strip()] = v.strip()
            text = text[end + 4:]
    return meta, text


def slug(text):
    return re.sub(r"[^a-z0-9]+", "-", re.sub(r"<[^>]+>", "", text).lower()).strip("-")


def inline(text):
    text = html.escape(text, quote=False)
    codes = []

    def keep(m):
        codes.append("<code>%s</code>" % m.group(1))
        return "\x00%d\x00" % (len(codes) - 1)

    text = re.sub(r"`([^`]+)`", keep, text)
    text = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)
    text = re.sub(r"(?<![\w*])\*(?!\s)(.+?)(?<!\s)\*(?![\w*])", r"<em>\1</em>", text)
    text = re.sub(r"\[([^\]]+)\]\(([^)\s]+)\)", lambda m: '<a href="%s">%s</a>' % (m.group(2), m.group(1)), text)
    text = re.sub(r"\x00(\d+)\x00", lambda m: codes[int(m.group(1))], text)
    return text


def _table(rows):
    def cells(row):
        row = row.strip()
        if row.startswith("|"):
            row = row[1:]
        if row.endswith("|"):
            row = row[:-1]
        return [c.strip() for c in row.split("|")]

    head = cells(rows[0])
    aligns = []
    body = rows[1:]
    if body and re.match(r"^\s*\|?\s*:?-{2,}", body[0]):
        for c in cells(body[0]):
            aligns.append("right" if c.endswith(":") and not c.startswith(":") else ("center" if c.startswith(":") and c.endswith(":") else ""))
        body = body[1:]
    out = ['<div class="table-wrap"><table><thead><tr>']
    for i, h in enumerate(head):
        a = ' style="text-align:%s"' % aligns[i] if i < len(aligns) and aligns[i] else ""
        out.append("<th%s>%s</th>" % (a, inline(h)))
    out.append("</tr></thead><tbody>")
    for r in body:
        out.append("<tr>")
        for i, c in enumerate(cells(r)):
            a = ' style="text-align:%s"' % aligns[i] if i < len(aligns) and aligns[i] else ""
            out.append("<td%s>%s</td>" % (a, inline(c)))
        out.append("</tr>")
    out.append("</tbody></table></div>")
    return "".join(out)


def _list(lines):
    """Lines of one list (with indented children) as HTML."""
    ordered = bool(re.match(r"^\s*\d+[.)]\s", lines[0]))
    tag = "ol" if ordered else "ul"
    out = ["<%s>" % tag]
    item, children = None, []

    def flush():
        if item is None:
            return
        out.append("<li>%s" % inline(item))
        if children:
            pad = min(len(c) - len(c.lstrip(" ")) for c in children)
            out.append(_list([c[pad:] for c in children]))
        out.append("</li>")

    for line in lines:
        m = re.match(r"^(\d+[.)]|[-*+])\s+(.*)$", line)
        if m and not line.startswith(" "):
            flush()
            item, children = m.group(2), []
        elif line.startswith("  ") and re.match(r"^\s+(\d+[.)]|[-*+])\s", line):
            children.append(line)
        elif item is not None:
            item += " " + line.strip()
    flush()
    out.append("</%s>" % tag)
    return "".join(out)


def render(text):
    """HTML for a page and its contents list [{level, id, text}]."""
    lines = text.replace("\r\n", "\n").split("\n")
    out, toc, i = [], [], 0
    used = set()
    while i < len(lines):
        line = lines[i]
        s = line.strip()
        if not s:
            i += 1
            continue
        if s.startswith("```"):
            j = i + 1
            code = []
            while j < len(lines) and not lines[j].strip().startswith("```"):
                code.append(lines[j])
                j += 1
            out.append("<pre><code>%s</code></pre>" % html.escape("\n".join(code)))
            i = j + 1
            continue
        m = re.match(r"^\{\{\s*([\w-]+)\s*\}\}$", s)
        if m:
            out.append('<div class="widget" data-widget="%s"></div>' % m.group(1))
            i += 1
            continue
        m = re.match(r"^(#{1,4})\s+(.*)$", s)
        if m:
            level = len(m.group(1))
            title = inline(m.group(2))
            hid = slug(m.group(2))
            base, n = hid, 2
            while hid in used:
                hid, n = "%s-%d" % (base, n), n + 1
            used.add(hid)
            if level in (2, 3):
                toc.append({"level": level, "id": hid, "text": re.sub(r"<[^>]+>", "", title)})
            out.append('<h%d id="%s">%s</h%d>' % (level, hid, title, level))
            i += 1
            continue
        if re.match(r"^(-{3,}|\*{3,})$", s):
            out.append("<hr>")
            i += 1
            continue
        if s.startswith("|"):
            rows = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                rows.append(lines[i])
                i += 1
            out.append(_table(rows))
            continue
        if s.startswith(">"):
            quote = []
            while i < len(lines) and lines[i].strip().startswith(">"):
                quote.append(lines[i].strip()[1:].strip())
                i += 1
            body = " ".join(quote)
            kind = "quote"
            km = re.match(r"^\*\*(Tip|Note|Warning):\*\*\s*(.*)$", body)
            if km:
                kind, body = km.group(1).lower(), km.group(2)
                out.append('<div class="callout callout-%s"><span class="callout-label">%s</span><p>%s</p></div>' % (kind, km.group(1), inline(body)))
            else:
                out.append("<blockquote><p>%s</p></blockquote>" % inline(body))
            continue
        if re.match(r"^(\d+[.)]|[-*+])\s", s):
            block = []
            while i < len(lines) and (re.match(r"^(\d+[.)]|[-*+])\s", lines[i]) or
                                      (lines[i].startswith("  ") and lines[i].strip()) ):
                block.append(lines[i])
                i += 1
            out.append(_list(block))
            continue
        para = [s]
        i += 1
        while i < len(lines) and lines[i].strip() and not re.match(r"^(#{1,4}\s|\||>|```|(\d+[.)]|[-*+])\s|\{\{)", lines[i].strip()):
            para.append(lines[i].strip())
            i += 1
        out.append("<p>%s</p>" % inline(" ".join(para)))
    return "\n".join(out), toc
