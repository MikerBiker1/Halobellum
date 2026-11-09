// wiki.js: the Halobellum Field Manual's pages, drawn in the browser from window.WIKI (data.js, built by
// Tools/Wiki/build_wiki.py from the game's own data). One page at a time, chosen by the address after "#":
//   #/  home   #/faction/<id>   #/units   #/unit/<id>   #/techs   #/spells   #/cards   #/counters   #/compare?u=a,b
//   #/page/<id>  a written page from Tools/Wiki/content (its {{widgets}} are drawn by WIDGETS below).
// Edit this copy in Tools/Wiki/site, not the one in Wiki/. Plain browser JavaScript, no libraries, works from file://.
(() => {
  "use strict";
  const W = window.WIKI;
  const main = document.getElementById("main");
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const esc = (s) => String(s == null ? "" : s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  const num = (n, d = 2) => (n == null || isNaN(n) ? "–" : Number(n).toLocaleString("en-GB", { maximumFractionDigits: d }));
  const pct = (x) => Math.round(x * 100) + "%";
  const FACTION_VAR = { UNSC: "var(--unsc)", Covenant: "var(--covenant)", Flood: "var(--flood)" };

  // ------------------------------------------------------------------ icons (drawn here, no outside marks)
  const I = {
    home: '<path d="M3 11l9-8 9 8"/><path d="M5 10v10h14V10"/><path d="M10 20v-6h4v6"/>',
    units: '<circle cx="9" cy="7" r="3"/><path d="M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6"/><circle cx="17" cy="9" r="2.5"/><path d="M15 14.5c3 0 6 2 6 5.5"/>',
    flag: '<path d="M5 21V4"/><path d="M5 4h11l-2 4 2 4H5"/>',
    tech: '<path d="M9 3h6"/><path d="M10 3v6L4 19a1.5 1.5 0 0 0 1.3 2h13.4a1.5 1.5 0 0 0 1.3-2L14 9V3"/><path d="M7 15h10"/>',
    spell: '<path d="M13 2L4 14h7l-1 8 9-12h-7z"/>',
    item: '<path d="M12 2l9 5v10l-9 5-9-5V7z"/><path d="M12 22V12"/><path d="M21 7l-9 5-9-5"/>',
    tower: '<path d="M6 21V8l6-5 6 5v13"/><path d="M4 21h16"/><path d="M10 21v-5h4v5"/><path d="M9 10h6"/>',
    web: '<circle cx="12" cy="12" r="2"/><circle cx="5" cy="5" r="2"/><circle cx="19" cy="5" r="2"/><circle cx="5" cy="19" r="2"/><circle cx="19" cy="19" r="2"/><path d="M7 7l3.5 3.5M17 7l-3.5 3.5M7 17l3.5-3.5M17 17l-3.5-3.5M7 5h10M7 19h10"/>',
    compare: '<path d="M4 4h6v16H4zM14 8h6v12h-6z"/>',
    book: '<path d="M4 4h6a2 2 0 0 1 2 2v14a2 2 0 0 0-2-2H4z"/><path d="M20 4h-6a2 2 0 0 0-2 2v14a2 2 0 0 1 2-2h6z"/>',
    swords: '<path d="M14.5 17.5L3 6V3h3l11.5 11.5"/><path d="M13 19l6-6M16 16l4 4M19 21l2-2"/><path d="M9.5 6.5L13 3h3v3l-3.5 3.5"/>',
    shield: '<path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z"/>',
    drop: '<path d="M12 3v12"/><path d="M7 10l5 5 5-5"/><path d="M5 21h14"/>',
    skull: '<path d="M12 3a8 8 0 0 0-8 8c0 3 1.5 4.5 3 5.5V20h10v-3.5c1.5-1 3-2.5 3-5.5a8 8 0 0 0-8-8z"/><circle cx="9" cy="11" r="1.5"/><circle cx="15" cy="11" r="1.5"/>',
    spawn: '<circle cx="12" cy="12" r="3"/><circle cx="5" cy="6" r="1.5"/><circle cx="19" cy="6" r="1.5"/><circle cx="6" cy="19" r="1.5"/><circle cx="18" cy="18" r="1.5"/><path d="M9.5 10L6 7M14.5 10L18 7M10 14.5L7 18M14 14.5l3 2.5"/>',
    aura: '<circle cx="12" cy="12" r="3"/><circle cx="12" cy="12" r="7" stroke-dasharray="2 3"/><circle cx="12" cy="12" r="10.5" stroke-dasharray="1 4"/>',
    leap: '<path d="M3 19c4-12 14-12 18 0"/><path d="M17 15l4 4-5 1"/>',
    heart: '<path d="M12 20s-7-4.5-7-10a4 4 0 0 1 7-2.5A4 4 0 0 1 19 10c0 5.5-7 10-7 10z"/>',
    biohazard: '<circle cx="12" cy="12" r="2"/><path d="M12 10V4M10.3 13L5 16M13.7 13l5.3 3"/><circle cx="12" cy="4" r="2"/><circle cx="5" cy="16" r="2"/><circle cx="19" cy="16" r="2"/>',
    wind: '<path d="M3 8h11a3 3 0 1 0-3-3"/><path d="M3 12h15a3 3 0 1 1-3 3"/><path d="M3 16h7"/>',
    target: '<circle cx="12" cy="12" r="8"/><circle cx="12" cy="12" r="4"/><path d="M12 2v4M12 18v4M2 12h4M18 12h4"/>',
    plane: '<path d="M21 15l-8-4V5a1.5 1.5 0 0 0-3 0v6l-8 4v2l8-2v4l-2 1.5V22l3.5-1 3.5 1v-1.5L13 19v-4l8 2z"/>',
    flank: '<path d="M4 12h12"/><path d="M12 6l6 6-6 6"/><path d="M4 4v16"/>',
    bomb: '<circle cx="11" cy="14" r="7"/><path d="M15 8l3-3M18 5l2 1M18 5l-1-2"/>',
    mask: '<path d="M4 6c5-2 11-2 16 0v5c0 5-4 9-8 9s-8-4-8-9z"/><path d="M8 11h3M13 11h3"/>',
    grid: '<path d="M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z"/>',
    list: '<path d="M8 6h13M8 12h13M8 18h13M3 6h1M3 12h1M3 18h1"/>',
    dice: '<rect x="4" y="4" width="16" height="16" rx="3"/><circle cx="9" cy="9" r="1"/><circle cx="15" cy="15" r="1"/><circle cx="15" cy="9" r="1"/><circle cx="9" cy="15" r="1"/>',
    coins: '<ellipse cx="12" cy="6" rx="7" ry="3"/><path d="M5 6v6c0 1.7 3.1 3 7 3s7-1.3 7-3V6"/><path d="M5 12v6c0 1.7 3.1 3 7 3s7-1.3 7-3v-6"/>',
    map: '<path d="M9 4L3 6v14l6-2 6 2 6-2V4l-6 2z"/><path d="M9 4v14M15 6v14"/>',
    cpu: '<rect x="6" y="6" width="12" height="12" rx="1"/><path d="M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4"/><path d="M10 10h4v4h-4z"/>',
    play: '<path d="M6 4l14 8-14 8z"/>',
    star: '<path d="M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1L3.2 9.5l6.1-.9z"/>',
    clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  };
  const icon = (name) => `<svg viewBox="0 0 24 24" aria-hidden="true">${I[name] || I.book}</svg>`;

  // ------------------------------------------------------------------ data helpers
  const UNITS = W.units;
  const ORDER = [];
  const seen = new Set();
  W.factions.forEach((f) => {
    f.base.forEach((id) => { if (!seen.has(id)) { seen.add(id); ORDER.push(id); } });
    f.subFactions.forEach((s) => s.units.forEach((id) => { if (!seen.has(id)) { seen.add(id); ORDER.push(id); } }));
  });
  Object.keys(UNITS).forEach((id) => { if (!seen.has(id)) ORDER.push(id); });
  // Who makes each unit appear: drops, spawns, death bursts, seeds and spells.
  Object.values(UNITS).forEach((u) => {
    const mark = (target, how) => { if (UNITS[target]) (UNITS[target].madeBy = UNITS[target].madeBy || []).push({ unit: u.id, how }); };
    if (u.reinforcement) mark(u.reinforcement.unit, "dropped by");
    if (u.spawns) { mark(u.spawns.unit, "released by"); if (u.spawns.alternate) mark(u.spawns.alternate.unit, "released by"); }
    if (u.deathBurst) u.deathBurst.spawns.forEach((s) => mark(s.unit, "bursts from"));
    if (u.seedForms) u.seedForms.forEach((s) => mark(s.becomes, "grown by the seeds of"));
  });
  const factionOf = (u) => (u.homes && u.homes[0] ? u.homes[0].faction : u.summonedBy ? W.spells[u.summonedBy[0]].faction : "");
  const subOf = (u) => (u.homes && u.homes[0] && u.homes[0].sub ? u.homes[0].sub : u.homes && u.homes.length ? "Base roster" : u.summonedBy ? "Spell unit" : "");
  const fc = (faction) => FACTION_VAR[faction] || "var(--accent)";
  const fstyle = (faction) => `style="--fc:${fc(faction)}"`;
  const factionByName = (name) => W.factions.find((f) => f.name === name);
  const playable = ORDER.filter((id) => UNITS[id].homes && UNITS[id].homes.length);
  const maxOf = (key, ids = playable) => Math.max(...ids.map((id) => +UNITS[id][key] || 0));
  const unitLink = (id, label) => (UNITS[id] ? `<a href="#/unit/${id}">${esc(label || UNITS[id].name)}</a>` : esc(id));
  const tierPips = (t, max = 4) => `<span class="tier" title="Tier ${t}">${Array.from({ length: max }, (_, i) => `<i class="${i < t ? "on" : ""}"></i>`).join("")}</span>`;
  const credits = (n) => `<span class="credits">${num(n, 0)}</span>`;
  const img = (src, alt, cls = "") => (src ? `<img src="${esc(src)}" alt="${esc(alt)}" loading="lazy" class="${cls}">` : "");

  // ------------------------------------------------------------------ chrome: sidebar, version, menu
  const PAGES = W.pages || [];
  const pageGroups = {};
  PAGES.forEach((p) => (pageGroups[p.group] = pageGroups[p.group] || []).push(p));

  function sidebar() {
    const link = (href, label, ic, extra = "", cls = "") => `<a class="nav-link ${cls}" href="${href}">${ic}<span>${esc(label)}</span>${extra}</a>`;
    const count = (n) => `<span class="count">${n}</span>`;
    let html = `<div class="nav-group"><div class="nav-title">Start</div>${link("#/", "Home", icon("home"))}`;
    (pageGroups["Start"] || []).forEach((p) => (html += link(`#/page/${p.id}`, p.title, icon(p.icon || "book"))));
    html += `</div><div class="nav-group"><div class="nav-title">Database</div>`;
    W.factions.forEach((f) => (html += link(`#/faction/${f.id}`, f.name, `<span class="dot" ${fstyle(f.name)}></span>`, count(new Set([...f.base, ...f.subFactions.flatMap((s) => s.units)]).size))));
    html += link("#/units", "All units", icon("units"), count(playable.length));
    html += link("#/techs", "Techs", icon("tech"), count(Object.keys(W.techs).length));
    html += link("#/spells", "Spells", icon("spell"), count(Object.keys(W.spells).length));
    html += link("#/cards", "Items & cards", icon("item"), count(Object.keys(W.cards).length));
    html += link("#/counters", "Counter web", icon("web"));
    html += link("#/compare", "Compare units", icon("compare"));
    html += `</div>`;
    Object.keys(pageGroups).filter((g) => g !== "Start").forEach((g) => {
      html += `<div class="nav-group"><div class="nav-title">${esc(g)}</div>`;
      pageGroups[g].forEach((p) => (html += link(`#/page/${p.id}`, p.title, icon(p.icon || "book"))));
      html += `</div>`;
    });
    $("#sidebar").innerHTML = html;
  }
  function markNav() {
    const h = location.hash || "#/";
    $$(".nav-link").forEach((a) => {
      const href = a.getAttribute("href");
      a.classList.toggle("active", href === "#/" ? h === "#/" || h === "" : h === href || h.startsWith(href + "?") || h.startsWith(href + "/"));
    });
    if (h.startsWith("#/unit/")) {
      const u = UNITS[h.slice(7)];
      const f = u && factionByName(factionOf(u));
      if (f) $$(`.nav-link[href="#/faction/${f.id}"]`).forEach((a) => a.classList.add("active"));
    }
  }

  // ------------------------------------------------------------------ shared pieces
  const crumbs = (...parts) => `<div class="crumbs">${parts.map((p) => (Array.isArray(p) ? `<a href="${p[1]}">${esc(p[0])}</a>` : `<span>${esc(p)}</span>`)).join('<span class="sep">/</span>')}</div>`;
  const sectionHead = (title, hint = "") => `<div class="section-head"><h2>${esc(title)}</h2>${hint ? `<span class="hint">${hint}</span>` : ""}</div>`;
  const footer = () => `<div class="foot"><span>Halobellum Field Manual · game version ${esc(W.version)}</span><span>Built ${esc(W.built)} from the game's own data</span><span>A private, non-commercial fan project.</span></div>`;

  function unitCard(id) {
    const u = UNITS[id];
    const f = factionOf(u);
    const badges = [u.air ? '<span class="mini-badge air">Air</span>' : "", u.isFlood ? '<span class="mini-badge flood">Flood</span>' : ""].join("");
    return `<a class="unit-card" href="#/unit/${id}" ${fstyle(f)}>
      <div class="pic">${img(u.icon, u.name)}<span class="tl">${tierPips(u.tier)}</span><span class="tr">${badges}</span></div>
      <div class="body"><div class="name">${esc(u.name)}</div><div class="sub">${esc(u.role)} · ${esc(subOf(u) === "Base roster" ? f : subOf(u))}</div>
      <div class="mini"><span><small>Cost</small>${num(u.cost, 0)}</span><span><small>Squad</small>×${u.squadSize}</span><span><small>Health</small>${num(u.squadHealth, 0)}</span><span><small>DPS</small>${num(u.squadDps, 0)}</span></div></div></a>`;
  }
  const unitCell = (id, extra = "") => {
    const u = UNITS[id];
    return u ? `<a class="unit-cell" href="#/unit/${id}" ${fstyle(factionOf(u))}>${img(u.icon, u.name)}<span>${esc(u.name)}${extra}</span></a>` : esc(id);
  };
  const effectChips = (effects) => `<div class="chips">${effects.map((e) => `<span class="chip ${e.good ? "good" : "bad"}">${esc(e.label)} <b>${esc(e.text)}</b></span>`).join("")}</div>`;

  function bar(value, max, cls = "") {
    const w = max > 0 ? Math.max(2, Math.min(100, (value / max) * 100)) : 0;
    return `<div class="bar ${cls}"><i style="width:${w}%"></i></div>`;
  }
  function rankOf(id, key) {
    const vals = playable.map((x) => +UNITS[x][key] || 0).sort((a, b) => b - a);
    return vals.indexOf(+UNITS[id][key] || 0) + 1;
  }

  // Sortable tables: any <table data-sort> gets clickable headers with data-key (cells carry data-v).
  function bindSort(root) {
    $$("table[data-sort]", root).forEach((table) => {
      $$("th.sortable", table).forEach((th, colIndex) => {
        th.addEventListener("click", () => {
          const idx = Array.from(th.parentNode.children).indexOf(th);
          const asc = !th.classList.contains("asc");
          $$("th", table).forEach((t) => t.classList.remove("asc", "desc"));
          th.classList.add(asc ? "asc" : "desc");
          const rows = $$("tbody tr", table);
          rows.sort((a, b) => {
            const av = a.children[idx].dataset.v, bv = b.children[idx].dataset.v;
            const an = parseFloat(av), bn = parseFloat(bv);
            const c = !isNaN(an) && !isNaN(bn) ? an - bn : String(av).localeCompare(String(bv));
            return asc ? c : -c;
          });
          const body = $("tbody", table);
          rows.forEach((r) => body.appendChild(r));
        });
      });
    });
  }

  // ------------------------------------------------------------------ home
  function home() {
    const pick = (ids) => ids.map((id) => UNITS[id]).filter((u) => u && u.icon);
    const heroUnits = pick(["Spartan", "Scarab", "Abomination"]);
    const counts = [
      [playable.length, "Units"], [Object.keys(W.techs).length, "Techs"], [Object.keys(W.spells).length, "Spells"],
      [Object.keys(W.cards).length, "Items & cards"], [W.factions.reduce((n, f) => n + f.subFactions.length, 0), "Sub-factions"], [W.towers.levels.length, "Tower levels"],
    ];
    const tileArt = { UNSC: "Scorpion", Covenant: "Wraith", Flood: "PureFormTank" };
    const spotlight = playable[Math.floor(Math.random() * playable.length)];
    const sections = [
      ["#/units", "units", "Units", "Every unit with its picture, size, stats, weapons, techs and counters."],
      ["#/techs", "tech", "Techs", "All tech upgrades and exactly what each one changes."],
      ["#/spells", "spell", "Spells", "Each faction's spell cards: damage, areas, timings and cooldowns."],
      ["#/cards", "item", "Items & cards", "Items your squads carry, economy cards, army buffs and training."],
      ["#/counters", "web", "Counter web", "Who beats whom: measured win rates for every pair of units."],
      ["#/compare", "compare", "Compare", "Put up to four units side by side."],
    ];
    PAGES.forEach((p) => sections.push([`#/page/${p.id}`, p.icon || "book", p.title, p.blurb || ""]));
    return `<div class="page">
      <section class="hero">
        <div class="kicker">Field manual · v${esc(W.version)}</div>
        <h1>Halobellum</h1>
        <p>Draft an army, deploy your squads, then watch them fight. This manual covers every unit, tech, spell and item, and how a match plays out. Every number comes straight from the game's data.</p>
        <div class="hero-actions"><a class="btn" href="#/units">${icon("units")} Browse units</a>${PAGES[0] ? `<a class="btn ghost" href="#/page/${PAGES[0].id}">${icon("play")} ${esc(PAGES[0].title)}</a>` : ""}<a class="btn ghost" href="#/unit/${spotlight}">${icon("dice")} Random unit</a></div>
        <div class="hero-units">${heroUnits.map((u) => img(u.icon, u.name)).join("")}</div>
      </section>
      <div class="stats-strip">${counts.map(([n, l]) => `<div class="stat-tile"><b>${n}</b><span>${l}</span></div>`).join("")}</div>
      ${sectionHead("Factions", "Pick one, then one of its sub-factions")}
      <div class="faction-tiles">${W.factions.map((f) => {
        const art = UNITS[tileArt[f.name]];
        return `<a class="faction-tile" href="#/faction/${f.id}" ${fstyle(f.name)}>${img(f.emblem, f.name, "emblem")}<h3>${esc(f.name)}</h3><p>${f.subFactions.map((s) => esc(s.name)).join(" · ")}</p><div class="chips"><span class="chip fc">${new Set([...f.base, ...f.subFactions.flatMap((s) => s.units)]).size} units</span><span class="chip fc">${f.spells.length} spells</span></div>${art ? img(art.icon, art.name, "art") : ""}</a>`;
      }).join("")}</div>
      <div class="section">${sectionHead("Records", "The top five in each")}<div class="grid cols-3">${[["Toughest squad", "squadHealth", 0, ""], ["Most damage per second", "squadDps", 0, ""], ["Longest range", "maxRange", 0, " m"], ["Fastest", "speed", 2, " m/s"], ["Most health per credit", "healthPerCredit", 2, ""], ["Biggest footprint", "radius", 2, " m"]].map(([t, k, d, unit]) => {
        const top = playable.slice().sort((a, b) => UNITS[b][k] - UNITS[a][k]).slice(0, 5);
        const max = UNITS[top[0]][k];
        return `<div class="panel"><div class="panel-title">${icon("star")} ${t}</div><div class="match-list">${top.map((id, i) => { const u = UNITS[id]; const v = k === "radius" ? u.radius * 2 : u[k]; return `<a class="match-item" href="#/unit/${id}" ${fstyle(factionOf(u))}>${img(u.icon, u.name)}<span><b>${i + 1}. ${esc(u.name)}</b><small>${esc(factionOf(u))}</small></span><span class="rate">${num(v, d)}${unit}${bar(u[k], max)}</span></a>`; }).join("")}</div></div>`;
      }).join("")}</div></div>
      <div class="section">${sectionHead("Explore")}<div class="section-cards">${sections.map(([h, ic, t, d]) => `<a class="section-card" href="${h}"><span class="ico">${icon(ic)}</span><span><b>${esc(t)}</b><span>${esc(d)}</span></span></a>`).join("")}</div></div>
      ${footer()}</div>`;
  }

  // ------------------------------------------------------------------ faction
  function factionPage(id) {
    const f = W.factions.find((x) => x.id === id);
    if (!f) return notFound();
    const art = [...f.base].map((x) => UNITS[x]).filter((u) => u.icon).sort((a, b) => b.cost - a.cost).slice(0, 3);
    const spells = f.spells.map((s) => W.spells[s]);
    return `<div class="page" ${fstyle(f.name)}>
      ${crumbs(["Home", "#/"], "Factions", f.name)}
      <section class="faction-hero">${img(f.emblem, f.name, "emblem")}<h1><span class="accent">${esc(f.name)}</span></h1>
        <p class="lede">${f.base.length} units in the base roster, open to every ${esc(f.name)} player, and ${f.subFactions.length} sub-factions, each adding units of its own.</p>
        <div class="chips" style="margin-top:14px">${f.subFactions.map((s) => `<a class="chip fc" href="#sub-${s.id}">${esc(s.name)}</a>`).join("")}<a class="chip" href="#/spells?f=${encodeURIComponent(f.name)}">${f.spells.length} spells</a></div>
        <div class="art">${art.map((u) => img(u.icon, u.name)).join("")}</div></section>
      <div class="section">${sectionHead("Base roster", "Every " + esc(f.name) + " player can unlock these")}<div class="unit-grid">${f.base.map(unitCard).join("")}</div></div>
      ${f.subFactions.map((s) => `<div class="sub-block" id="sub-${s.id}"><div class="sub-head"><h3>${esc(s.name)}</h3><span class="chip fc">${s.units.length} exclusive units</span><p>${esc(s.description)}</p></div><div class="unit-grid">${s.units.map(unitCard).join("")}</div></div>`).join("")}
      <div class="section">${sectionHead("Spells")}<div class="card-grid">${spells.map(spellCard).join("")}</div></div>
      ${footer()}</div>`;
  }

  // ------------------------------------------------------------------ units list
  const uf = { faction: "All", sub: "All", role: "All", tier: "All", domain: "All", armor: "All", q: "", sort: "order", view: "cards" };
  function unitsPage(query) {
    if (query.f) uf.faction = query.f;
    if (query.role) uf.role = query.role;
    const seg = (key, values) => `<div class="seg" data-key="${key}">${values.map((v) => `<button class="${uf[key] === v ? "on" : ""}" data-v="${esc(v)}">${esc(v)}</button>`).join("")}</div>`;
    return `<div class="page">
      ${crumbs(["Home", "#/"], "Units")}
      <div class="page-head"><h1>Units</h1><p class="lede">All ${playable.length} units you can field, plus the ones that only appear in battle. Filter, sort, or switch to the table to compare numbers at a glance.</p></div>
      <div class="toolbar">
        ${seg("faction", ["All", ...W.factions.map((f) => f.name)])}
        <label>Sub-faction <select data-key="sub"></select></label>
        <label>Role <select data-key="role">${["All", ...W.enums.roles].map((r) => `<option ${uf.role === r ? "selected" : ""}>${esc(r)}</option>`).join("")}</select></label>
        <label>Armor <select data-key="armor">${["All", ...W.enums.armor].map((r) => `<option ${uf.armor === r ? "selected" : ""}>${esc(r)}</option>`).join("")}</select></label>
        ${seg("tier", ["All", "1", "2", "3", "4"])}
        ${seg("domain", ["All", "Ground", "Air"])}
        <input type="search" class="filter-input grow" data-key="q" placeholder="Filter by name or weapon" value="${esc(uf.q)}">
        <label>Sort <select data-key="sort">${[["order", "Faction order"], ["name", "Name"], ["cost", "Cost"], ["tier", "Tier"], ["squadHealth", "Squad health"], ["squadDps", "Squad DPS"], ["maxRange", "Range"], ["speed", "Speed"], ["healthPerCredit", "Health per credit"], ["dpsPerCredit", "DPS per 100 credits"]].map(([v, l]) => `<option value="${v}" ${uf.sort === v ? "selected" : ""}>${l}</option>`).join("")}</select></label>
        ${seg("view", ["cards", "table", "sizes"])}
        <span class="result-count" id="resultCount"></span>
      </div>
      <div id="unitResults"></div>${footer()}</div>`;
  }
  function bindUnits() {
    const subSel = $('select[data-key="sub"]');
    const fillSubs = () => {
      const fs = uf.faction === "All" ? W.factions : [factionByName(uf.faction)];
      const opts = ["All", "Base roster", ...fs.flatMap((f) => f.subFactions.map((s) => s.name)), "Battle only"];
      if (!opts.includes(uf.sub)) uf.sub = "All";
      subSel.innerHTML = opts.map((o) => `<option ${uf.sub === o ? "selected" : ""}>${esc(o)}</option>`).join("");
    };
    const draw = () => {
      let ids = ORDER.slice();
      ids = ids.filter((id) => {
        const u = UNITS[id];
        const homes = u.homes || [];
        if (uf.faction !== "All" && factionOf(u) !== uf.faction && !homes.some((h) => h.faction === uf.faction)) return false;
        if (uf.sub === "Battle only" && homes.length) return false;
        if (uf.sub === "All" && !homes.length && uf.q === "" ) return false;
        if (uf.sub === "Base roster" && !homes.some((h) => !h.sub)) return false;
        if (!["All", "Base roster", "Battle only"].includes(uf.sub) && !homes.some((h) => h.sub === uf.sub)) return false;
        if (uf.role !== "All" && u.role !== uf.role) return false;
        if (uf.armor !== "All" && u.armor !== uf.armor) return false;
        if (uf.tier !== "All" && String(u.tier) !== uf.tier) return false;
        if (uf.domain === "Air" && !u.air) return false;
        if (uf.domain === "Ground" && u.air) return false;
        if (uf.q) {
          const hay = (u.name + " " + u.role + " " + u.weapons.map((w) => w.name).join(" ")).toLowerCase();
          if (!hay.includes(uf.q.toLowerCase())) return false;
        }
        return true;
      });
      if (uf.sort !== "order") {
        ids.sort((a, b) => {
          const A = UNITS[a], B = UNITS[b];
          if (uf.sort === "name") return A.name.localeCompare(B.name);
          return (B[uf.sort] || 0) - (A[uf.sort] || 0) || A.name.localeCompare(B.name);
        });
      }
      $("#resultCount").textContent = ids.length + " shown";
      const box = $("#unitResults");
      if (!ids.length) { box.innerHTML = '<div class="empty">No unit matches these filters.</div>'; return; }
      if (uf.view === "cards") { box.innerHTML = `<div class="unit-grid">${ids.map(unitCard).join("")}</div>`; return; }
      if (uf.view === "sizes") {
        // Pictures drawn to one scale: each one as wide as the unit's footprint.
        const sorted = ids.slice().sort((a, b) => UNITS[a].radius - UNITS[b].radius);
        const maxD = Math.max(...sorted.map((id) => UNITS[id].radius * 2));
        const px = Math.min(36, 420 / maxD);
        box.innerHTML = `<div class="panel"><div class="panel-title">${icon("map")} Footprints to scale · one square is ${W.combat.squareSize} m</div><div class="size-chart">${sorted.map((id) => {
          const u = UNITS[id], w = Math.max(22, u.radius * 2 * px);
          return `<a class="size-item" href="#/unit/${id}" ${fstyle(factionOf(u))} title="${esc(u.name)}: ${num(u.radius * 2)} m"><span class="size-pic" style="width:${w}px;height:${w}px">${img(u.icon, u.name)}</span><span class="size-ring" style="width:${w}px"></span><b>${esc(u.name)}</b><small>${num(u.radius * 2)} m</small></a>`;
        }).join("")}</div></div>`;
        return;
      }
      const cols = [["Unit", (u) => u.name, (u) => unitCell(u.id)], ["Faction", (u) => factionOf(u), (u) => esc(subOf(u) === "Base roster" ? factionOf(u) : subOf(u))], ["Role", (u) => u.role, (u) => esc(u.role)],
        ["Tier", (u) => u.tier, (u) => u.tier, 1], ["Cost", (u) => u.cost, (u) => num(u.cost, 0), 1], ["Unlock", (u) => u.unlockCost, (u) => num(u.unlockCost, 0), 1], ["Squad", (u) => u.squadSize, (u) => u.squadSize, 1],
        ["Health", (u) => u.health, (u) => num(u.health), 1], ["Shield", (u) => u.shield, (u) => num(u.shield), 1], ["Armor", (u) => u.armor, (u) => esc(u.armor)], ["Speed", (u) => u.speed, (u) => num(u.speed), 1],
        ["Range", (u) => u.maxRange, (u) => num(u.maxRange), 1], ["Squad HP", (u) => u.squadHealth, (u) => num(u.squadHealth, 0), 1], ["Squad DPS", (u) => u.squadDps, (u) => num(u.squadDps, 0), 1],
        ["Air", (u) => (u.air ? 1 : 0), (u) => (u.air ? "Yes" : ""), 0]];
      box.innerHTML = `<div class="table-wrap"><table data-sort><thead><tr>${cols.map((c) => `<th class="sortable ${c[3] ? "num" : ""}">${c[0]}</th>`).join("")}</tr></thead><tbody>${ids.map((id) => {
        const u = UNITS[id];
        return `<tr>${cols.map((c) => `<td class="${c[3] ? "num" : ""}" data-v="${esc(c[1](u))}">${c[2](u)}</td>`).join("")}</tr>`;
      }).join("")}</tbody></table></div>`;
      bindSort(box);
    };
    $$(".toolbar .seg").forEach((s) => s.addEventListener("click", (e) => {
      const b = e.target.closest("button"); if (!b) return;
      uf[s.dataset.key] = b.dataset.v;
      $$("button", s).forEach((x) => x.classList.toggle("on", x === b));
      if (s.dataset.key === "faction") fillSubs();
      draw();
    }));
    $$(".toolbar select, .toolbar input").forEach((el) => el.addEventListener("input", () => { uf[el.dataset.key] = el.value; draw(); }));
    fillSubs();
    draw();
  }

  // ------------------------------------------------------------------ unit page
  function footprintSvg(u) {
    const sq = W.combat.squareSize;
    const span = Math.max(4, Math.ceil((u.radius * 2) / sq) + 2);
    const size = 160, cell = size / span, c = size / 2, r = (u.radius / sq) * cell;
    let lines = "";
    for (let i = 0; i <= span; i++) {
      lines += `<line x1="${i * cell}" y1="0" x2="${i * cell}" y2="${size}" stroke="rgba(120,190,255,0.18)"/><line x1="0" y1="${i * cell}" x2="${size}" y2="${i * cell}" stroke="rgba(120,190,255,0.18)"/>`;
    }
    return `<svg viewBox="-5 -5 ${size + 10} ${size + 10}" role="img" aria-label="Footprint on the ${sq} m grid">${lines}<circle cx="${c}" cy="${c}" r="${Math.max(r, 1.5)}" fill="color-mix(in srgb, ${fc(factionOf(u))} 35%, transparent)" stroke="${fc(factionOf(u))}" stroke-width="1.5"/><line x1="${c - r}" y1="${c}" x2="${c + r}" y2="${c}" stroke="#fff" stroke-dasharray="2 2"/></svg>`;
  }
  function abilities(u) {
    const out = [];
    const add = (ic, title, text) => out.push(`<div class="ability"><span class="ico">${icon(ic)}</span><div><b>${title}</b><p>${text}</p></div></div>`);
    const c = W.combat;
    if (u.air) add("plane", "Flies", "An air unit: it ignores ground collision and flies straight to its target. Only weapons that can hit air can touch it.");
    if (u.canFlank) add("flank", "Flanker deployment", `Can be deployed in the flank zones beside the enemy's area. It arrives ${num(c.flankArrivalDelaySeconds)} s into the battle.`);
    if (u.defenseArc) {
      const a = u.defenseArc;
      add("shield", "Defense arc", `Across its front ${a.degrees}°, it takes ${pct(a.front)} damage${a.blockChance ? ` and blocks ${pct(a.blockChance)} of blockable shots${a.blocks.length ? ` (${a.blocks.join(", ")})` : ""}` : ""}. From behind it takes ${pct(a.rear)}.`);
    }
    if (u.reinforcement) {
      const r = u.reinforcement, d = UNITS[r.unit];
      add("drop", r.needsTech ? "Troop drop (needs its tech)" : "Troop drop", `Drops ${r.bodies} × ${unitLink(r.unit, d ? d.name : r.unit)}${r.drops > 1 ? `, up to ${r.drops} times` : ""}, ${num(r.delay)} s apart${r.firstDrop ? `, the first ${num(r.firstDrop)} s in` : ""}.${r.landSeconds ? ` It lands behind the enemy's lines to do it (${num(r.landSeconds)} s down, as long back up).` : ""} Dropped troops fight only in that battle.`);
    }
    if (u.spawns) {
      const s = u.spawns;
      add("spawn", "Spawner", `Releases ${s.bodies} × ${unitLink(s.unit)} every ${num(s.interval)} s${s.alternate ? `, taking turns with ${s.alternate.bodies} × ${unitLink(s.alternate.unit)}` : ""}.`);
    }
    if (u.deathBurst) {
      const b = u.deathBurst;
      const parts = [];
      if (b.damage) parts.push(`explodes for ${num(b.damage)} ${b.antiArmor ? "anti-armor " : ""}${b.type.toLowerCase()} damage within ${num(b.radius)} m`);
      if (b.spawns.length) parts.push(`releases ${b.spawns.map((s) => `${s.bodies} × ${unitLink(s.unit)}`).join(" and ")}`);
      add("bomb", u.selfDestructs ? "Self-destructs" : "Death burst", `When it dies it ${parts.join(" and ")}.${u.selfDestructs ? " It sets this off on purpose when its main weapon fires." : ""}`);
    }
    if (u.infects) add("biohazard", "Infects", `Unarmored enemies that die within ${num(c.infectionRange)} m of it rise ${num(c.infectionDelaySeconds)} s later as infected units on the Flood's side, with ${pct(c.infectedHealthShare)} of their health and ${num(c.infectedSpeedMultiplier)}× speed.`);
    if (u.seedForms) add("biohazard", "Seeds grow into Flood", `Units that die seeded rise as the Flood's own version of them: ${u.seedForms.map((s) => `${esc(s.from)} → ${unitLink(s.becomes)}`).join(", ")}. Anything else stays dead.`);
    if (u.isFlood) add("mask", "Flood", "A Flood unit: it can't be infected or seeded.");
    if (u.immuneToInfection) add("shield", "Can't be infected", "Infection forms can't take this unit over, even though its body is unarmored.");
    if (u.formsPerBody > 1) add("spawn", "Swarm", `Each body is ${u.formsPerBody} tiny forms acting as one. An area hit damages every form inside it, and the pack deals less damage as its forms die.`);
    if (u.seeksDensest) add("target", "Seeks the crowd", "It heads for the thickest group of enemies instead of the closest one.");
    if (u.aura) add("aura", "Aura", `Friendly units within ${num(u.aura.radius)} m${u.aura.speed !== 1 ? ` move ${num(u.aura.speed)}× as fast` : ""}${u.aura.heal ? `${u.aura.speed !== 1 ? " and" : ""} regain ${num(u.aura.heal * 100)}% of their health a second` : ""}.`);
    if (u.lifesteal) add("heart", "Lifesteal", `${pct(u.lifesteal)} of the damage it deals comes back to it as health.`);
    if (u.leap) add("leap", u.leap.charge ? "Charge" : "Leap", u.leap.charge ? `Charges along the ground at a target within ${num(u.leap.range)} m at ${num(u.leap.speed)} m/s${u.leap.cooldown ? `, every ${num(u.leap.cooldown)} s at most` : ""}.` : `Pounces on a target within ${num(u.leap.range)} m at ${num(u.leap.speed)} m/s, and jumps over anything that holds it up${u.leap.cooldown ? `, every ${num(u.leap.cooldown)} s at most` : ""}.`);
    if (u.madeBy) {
      const groups = {};
      u.madeBy.forEach((m) => (groups[m.how] = groups[m.how] || []).push(m.unit));
      add("spawn", "Appears in battle", Object.entries(groups).map(([how, ids]) => `${how[0].toUpperCase() + how.slice(1)} ${[...new Set(ids)].map((x) => unitLink(x)).join(", ")}`).join(". ") + ".");
    }
    if (u.summonedBy) add("spell", "Called in by a spell", u.summonedBy.map((s) => `<a href="#/spells?f=${encodeURIComponent(W.spells[s].faction)}">${esc(W.spells[s].name)}</a> (${esc(W.spells[s].faction)})`).join(", ") + ".");
    return out;
  }
  function weaponBlock(w, i) {
    const tags = [];
    tags.push(`<span class="chip">${esc(w.type)}</span>`);
    if (w.antiArmor) tags.push('<span class="chip warn">Anti-armor</span>');
    if (w.splash) tags.push(`<span class="chip">Splash ${num(w.splash)} m</span>`);
    if (w.cone) tags.push(`<span class="chip">Cone ${num(w.cone)}°</span>`);
    if (w.lobbed) tags.push('<span class="chip">Lobbed, can miss</span>');
    if (w.minRange) tags.push(`<span class="chip">Min range ${num(w.minRange)} m</span>`);
    if (w.targets) tags.push(`<span class="chip">Hits ${esc(w.targets.toLowerCase())}</span>`);
    if (w.targetClass !== "Any") tags.push(`<span class="chip">Used on ${esc(w.targetClass.toLowerCase())}</span>`);
    if (w.airMultiplier !== 1) tags.push(`<span class="chip good">×${num(w.airMultiplier)} vs air</span>`);
    if (!w.blockable) tags.push('<span class="chip">Can\'t be blocked</span>');
    if (w.beam) tags.push(`<span class="chip">Beam ${num(w.beam.seconds)} s (charge ${num(w.beam.charge)} s, rest ${num(w.beam.rest)} s)</span>`);
    if (w.seed) tags.push(`<span class="chip good">Seeds for ${num(w.seed.seconds)} s: −${num(w.seed.damageShare * 100)}% health/s, ${pct(w.seed.slow)} slower</span>`);
    if (w.hitSlow) tags.push(`<span class="chip">Slows ${pct(w.hitSlow.slow)} for ${num(w.hitSlow.seconds)} s</span>`);
    if (w.windUp) tags.push(`<span class="chip">Wind-up ${num(w.windUp)} s</span>`);
    return `<div class="weapon"><div class="weapon-head"><h3>${esc(w.name)}</h3>${i === 0 ? '<span class="main-tag">Main weapon</span>' : ""}</div>
      <div class="weapon-stats"><div><small>Damage</small><b>${num(w.damage)}</b></div><div><small>Every</small><b>${num(w.interval, 3)} s</b></div><div><small>DPS / body</small><b>${num(w.dps)}</b></div><div><small>Range</small><b>${num(w.range)} m</b></div><div><small>Projectile</small><b>${w.projectileSpeed ? num(w.projectileSpeed) + " m/s" : "Instant"}</b></div></div>
      <div class="chips" style="margin-top:10px">${tags.join("")}</div></div>`;
  }
  function matchList(list, good) {
    if (!list || !list.length) return '<div class="empty">No clear results in the measured table.</div>';
    return `<div class="match-list">${list.map((m) => {
      const o = UNITS[m.unit];
      return `<a class="match-item" href="#/unit/${m.unit}" ${fstyle(factionOf(o))}>${img(o.icon, o.name)}<span><b>${esc(o.name)}</b><small>${esc(o.role)} · ${esc(factionOf(o))}</small></span><span class="rate">${good ? "Wins" : "Wins"} ${pct(m.rate)}${bar(good ? m.rate : 1 - m.rate, 1, good ? "good" : "bad")}</span></a>`;
    }).join("")}</div>`;
  }
  function unitPage(id) {
    const u = UNITS[id];
    if (!u) return notFound();
    const f = factionOf(u);
    const fac = factionByName(f);
    const homes = (u.homes || []).map((h) => (h.sub ? `<a class="chip fc" href="#/faction/${factionByName(h.faction).id}">${esc(h.faction)} · ${esc(h.sub)}</a>` : `<a class="chip fc" href="#/faction/${factionByName(h.faction).id}">${esc(h.faction)} base roster</a>`));
    if (!homes.length) homes.push('<span class="chip warn">Battle only</span>');
    const ab = abilities(u);
    const idx = ORDER.indexOf(id);
    const prev = UNITS[ORDER[(idx - 1 + ORDER.length) % ORDER.length]], next = UNITS[ORDER[(idx + 1) % ORDER.length]];
    const profile = [["Squad health", "squadHealth", 0], ["Squad DPS", "squadDps", 0], ["Health per body", "health", 0], ["Speed", "speed", 2, " m/s"], ["Longest range", "maxRange", 1, " m"], ["Health per credit", "healthPerCredit", 2], ["DPS per 100 credits", "dpsPerCredit", 2]];
    const vsMax = Math.max(...Object.values(u.dpsVs), 1);
    const techs = u.techs.map((t) => W.techs[t]).filter(Boolean);
    const shownName = u.name;
    return `<div class="page" ${fstyle(f)}>
      ${crumbs(["Home", "#/"], ["Units", "#/units"], fac ? [f, "#/faction/" + fac.id] : "Battle only", shownName)}
      <section class="unit-hero">
        <div class="unit-portrait">${img(u.icon, u.name)}</div>
        <div class="unit-title">
          <div class="role-line">${esc(u.role)} ${tierPips(u.tier)} <span>Tier ${u.tier}</span></div>
          <h1>${esc(shownName)}</h1>
          <div class="chips">${homes.join("")}<span class="chip">${u.air ? "Air" : "Ground"}</span><span class="chip">${esc(u.armor)} armor</span><span class="chip">Attacks ${esc(u.targets.toLowerCase())}</span></div>
          <div class="keystats">
            <div class="keystat"><small>Squad cost</small><b>${num(u.cost, 0)}<em>cr</em></b></div>
            <div class="keystat"><small>Unlock</small><b>${u.unlockCost ? num(u.unlockCost, 0) + "<em>cr</em>" : "Free"}</b></div>
            <div class="keystat"><small>Squad size</small><b>×${u.squadSize}</b></div>
            <div class="keystat"><small>Health / body</small><b>${num(u.health)}</b></div>
            <div class="keystat"><small>Shield / body</small><b>${u.shield ? num(u.shield) : "–"}</b></div>
            <div class="keystat"><small>Speed</small><b>${num(u.speed)}<em>m/s</em></b></div>
            <div class="keystat"><small>Range</small><b>${num(u.maxRange)}<em>m</em></b></div>
            <div class="keystat"><small>Base damage</small><b>${num(u.baseDamage)}</b></div>
          </div>
          ${u.strongAgainst || u.weakAgainst ? `<div class="counter-text"><div><small>Designed to beat</small>${esc(u.strongAgainst || "–")}</div><div class="weak"><small>Weak to</small>${esc(u.weakAgainst || "–")}</div></div>` : ""}
        </div>
      </section>

      <div class="section grid cols-2">
        <div class="panel"><div class="panel-title">${icon("map")} Size and squad</div>
          <div class="footprint">${footprintSvg(u)}<dl class="kv">
            <dt>Footprint</dt><dd>${num(u.radius * 2)} m across (${num(u.footprint)} squares)</dd>
            <dt>Collision radius</dt><dd>${num(u.radius)} m</dd>
            <dt>Grid square</dt><dd>${num(W.combat.squareSize)} m</dd>
            <dt>Bodies per squad</dt><dd>${u.squadSize}${u.formsPerBody > 1 ? ` (${u.formsPerBody} forms each)` : ""}</dd>
            <dt>Turn 180°</dt><dd>${u.turn180 ? num(u.turn180) + " s" : "Instant"}</dd>
            <dt>Squad health</dt><dd>${num(u.squadHealth, 0)}</dd>
            ${u.shield ? `<dt>Shield regen</dt><dd>${num(u.shieldRegen)}/s after ${num(W.combat.shieldRegenDelaySeconds)} s</dd>` : ""}
          </dl></div></div>
        <div class="panel"><div class="panel-title">${icon("star")} Combat profile <span style="margin-left:auto;font-size:10px">Rank among ${playable.length} units</span></div>
          <div class="bars">${profile.map(([l, k, d, unit]) => `<div class="bar-row"><span class="lbl">${l}</span>${bar(u[k], maxOf(k))}<span class="val">${num(u[k], d)}${unit || ""} <span class="rank">#${rankOf(id, k)}</span></span></div>`).join("")}</div></div>
      </div>

      <div class="section grid cols-2">
        <div>${sectionHead("Weapons", `${u.weapons.length} ${u.weapons.length === 1 ? "weapon" : "weapons"} on each body`)}${u.weapons.map(weaponBlock).join("") || '<div class="empty">No weapons.</div>'}</div>
        <div>${sectionHead("Squad damage per second", "after armor and shields")}<div class="panel"><div class="bars">${["Unarmored", "Light", "Heavy", "Shields"].map((k) => `<div class="bar-row"><span class="lbl">vs ${k}</span>${bar(u.dpsVs[k], vsMax)}<span class="val">${num(u.dpsVs[k], 0)}</span></div>`).join("")}</div>
          <p style="color:var(--text-3);font-size:12.5px;margin:12px 0 0">The whole squad's damage each second against bodies of each armor, and against energy shields, using the <a href="#/page/${pageIdFor("damage")}">damage table</a>. Splash, misses and travel time are not counted.</p></div></div>
      </div>

      ${ab.length ? `<div class="section">${sectionHead("Abilities and traits")}<div class="abilities grid cols-2">${ab.join("")}</div></div>` : ""}

      <div class="section">${sectionHead("Techs", techs.length ? `Bought once per match, they apply to every ${esc(u.name)} squad` : "")}
        ${techs.length ? `<div class="tech-grid">${techs.map((t, i) => `<div class="tech"><span class="slot">T${i + 1}</span><h3>${esc(t.name)}</h3><p>${esc(t.description)}</p>${effectChips(t.effects)}<span class="cost credits">${num(t.cost, 0)}</span></div>`).join("")}</div>` : '<div class="empty">This unit has no techs.</div>'}</div>

      ${u.goodAgainst || u.badAgainst ? `<div class="section">${sectionHead("Good against · bad against", "Measured win rates against rival units at equal credits")}
        <div class="matchups"><div><h4 style="margin-bottom:10px;color:var(--good)">Good against</h4>${matchList(u.goodAgainst, true)}</div><div><h4 style="margin-bottom:10px;color:var(--bad)">Bad against</h4>${matchList(u.badAgainst, false)}</div></div>
        <p style="color:var(--text-3);font-size:12.5px">${u.winAverage != null ? `Wins ${pct(u.winAverage)} of its pairings overall. ` : ""}See the full <a href="#/counters">counter web</a>. Source: ${esc(W.counters.source)}.</p></div>` : ""}

      <div class="section"><details class="panel"><summary class="panel-title" style="cursor:pointer;margin:0">${icon("list")} Every number</summary>
        <div class="table-wrap"><table><tbody>${rawRows(u)}</tbody></table></div></details></div>

      <div class="pager"><a href="#/unit/${prev.id}">${img(prev.icon, prev.name)}<span><small>Previous</small>${esc(prev.name)}</span></a><a class="next" href="#/unit/${next.id}"><span><small>Next</small>${esc(next.name)}</span>${img(next.icon, next.name)}</a></div>
      ${footer()}</div>`;
  }
  function rawRows(u) {
    const rows = [["Role", u.role], ["Tier", u.tier], ["Squad cost", u.cost], ["Unlock cost", u.unlockCost], ["Squad size", u.squadSize], ["Base damage per surviving body", u.baseDamage],
      ["Health", u.health], ["Armor", u.armor], ["Shield", u.shield], ["Shield regen per second", u.shieldRegen], ["Move speed (m/s)", u.speed], ["Collision radius (m)", u.radius],
      ["Turn 180° (s)", u.turn180], ["Air", u.air ? "Yes" : "No"], ["Attacks", u.targets], ["Can flank", u.canFlank ? "Yes" : "No"], ["Flood", u.isFlood ? "Yes" : "No"], ["Infects", u.infects ? "Yes" : "No"],
      ["Immune to infection", u.immuneToInfection ? "Yes" : "No"], ["Forms per body", u.formsPerBody], ["Squad health", u.squadHealth], ["Squad DPS", u.squadDps], ["DPS per body", u.bodyDps]];
    return rows.map(([k, v]) => `<tr><td>${esc(k)}</td><td class="num">${esc(typeof v === "number" ? num(v, 3) : v)}</td></tr>`).join("");
  }

  // ------------------------------------------------------------------ techs
  const tf = { faction: "All", q: "" };
  function techsPage() {
    return `<div class="page">${crumbs(["Home", "#/"], "Techs")}
      <div class="page-head"><h1>Techs</h1><p class="lede">Each unit type has its own techs. A tech is researched once per match and from then on applies to every squad of that unit, old and new.</p></div>
      <div class="toolbar"><div class="seg" data-key="faction">${["All", ...W.factions.map((f) => f.name)].map((v) => `<button class="${tf.faction === v ? "on" : ""}" data-v="${esc(v)}">${esc(v)}</button>`).join("")}</div>
      <input type="search" class="filter-input grow" placeholder="Filter by tech, unit or effect" value="${esc(tf.q)}"><span class="result-count" id="resultCount"></span></div>
      <div id="techResults"></div>${footer()}</div>`;
  }
  function bindTechs() {
    const draw = () => {
      const rows = [];
      ORDER.forEach((uid) => {
        const u = UNITS[uid];
        if (tf.faction !== "All" && factionOf(u) !== tf.faction) return;
        u.techs.forEach((tid, i) => {
          const t = W.techs[tid];
          const hay = (t.name + " " + u.name + " " + t.description + " " + t.effects.map((e) => e.label + " " + e.text).join(" ")).toLowerCase();
          if (tf.q && !hay.includes(tf.q.toLowerCase())) return;
          rows.push(`<tr><td data-v="${esc(u.name)}">${unitCell(uid)}</td><td data-v="${esc(t.name)}"><b>${esc(t.name)}</b><div style="color:var(--text-3);font-size:13px">${esc(t.description)}</div></td><td data-v="${t.effects.length}">${effectChips(t.effects)}</td><td class="num" data-v="${t.cost}">${credits(t.cost)}</td></tr>`);
        });
      });
      $("#resultCount").textContent = rows.length + " techs";
      $("#techResults").innerHTML = rows.length ? `<div class="table-wrap"><table data-sort><thead><tr><th class="sortable">Unit</th><th class="sortable">Tech</th><th>Effects</th><th class="sortable num">Cost</th></tr></thead><tbody>${rows.join("")}</tbody></table></div>` : '<div class="empty">No tech matches.</div>';
      bindSort($("#techResults"));
    };
    $(".toolbar .seg").addEventListener("click", (e) => { const b = e.target.closest("button"); if (!b) return; tf.faction = b.dataset.v; $$(".toolbar .seg button").forEach((x) => x.classList.toggle("on", x === b)); draw(); });
    $(".toolbar input").addEventListener("input", (e) => { tf.q = e.target.value; draw(); });
    draw();
  }

  // ------------------------------------------------------------------ spells
  function spellCard(s) {
    const kv = [];
    kv.push(["Cost", credits(s.cost)], ["Cooldown", s.cooldown ? `${s.cooldown} round${s.cooldown > 1 ? "s" : ""}` : "None"], ["Copies", s.copies]);
    if (s.minRound) kv.push(["From round", s.minRound]);
    if (s.line) kv.push(["Area", `Line ${num(s.line.length)} × ${num(s.line.width)} m`]); else if (s.radius) kv.push(["Radius", `${num(s.radius)} m`]);
    if (s.delay) kv.push(["Lands", `${num(s.delay)} s into the battle`]);
    if (s.damage) {
      const d = s.damage;
      kv.push(["Damage", `${num(d.perTick)} ${d.antiArmor ? "anti-armor " : ""}${esc(d.type.toLowerCase())}${d.ticks > 1 ? ` × ${d.ticks} (every ${num(d.interval)} s)` : ""}`]);
      if (d.strikeRadius) kv.push(["Strikes", `${d.strikesPerTick} per hit, ${num(d.strikeRadius)} m each, at random spots`]);
      kv.push(["Hits", esc(d.targets)]);
      if (d.infects) kv.push(["Infects", "Unarmored kills rise as Flood"]);
    }
    if (s.buff) { if (s.buff.shieldShare) kv.push(["Shield", `+${pct(s.buff.shieldShare)} of health`]); if (s.buff.damage !== 1) kv.push(["Damage", `×${num(s.buff.damage)} for the battle`]); }
    if (s.drop) kv.push(["Drops", `${s.drop.squads} squad${s.drop.squads > 1 ? "s" : ""} of ${unitLink(s.drop.unit)}`]);
    if (s.disrupt) {
      if (s.disrupt.shieldDamage) kv.push(["Shields", `−${num(s.disrupt.shieldDamage)}`]);
      if (s.disrupt.techDisable) kv.push(["Techs off", `${num(s.disrupt.techDisable)} s`]);
      if (s.disrupt.slow) kv.push(["Armored slowed", `${pct(s.disrupt.slow)} for ${num(s.disrupt.slowSeconds)} s`]);
    }
    if (s.smoke) kv.push(["Smoke", `${s.smoke.duration ? num(s.smoke.duration) + " s" : "Whole battle"}, range ×${num(s.smoke.range)}`]);
    return `<div class="game-card ${s.offensive ? "offensive" : "defensive"}">
      <div class="art">${img(s.emblem, s.name)}<span class="corner chip">${s.offensive ? "Offensive" : "Defensive"} · ${esc(s.effect)}</span></div>
      <div class="body"><h3>${esc(s.name)}</h3><p class="desc">${esc(s.description)}</p><dl class="kv">${kv.map(([k, v]) => `<dt>${k}</dt><dd>${v}</dd>`).join("")}</dl>
      <div class="chips"><span class="chip" ${fstyle(s.faction)}><span style="color:${fc(s.faction)}">●</span> ${esc(s.faction)}</span></div></div></div>`;
  }
  function spellsPage(query) {
    const only = query.f && factionByName(query.f) ? query.f : null;
    return `<div class="page">${crumbs(["Home", "#/"], "Spells")}
      <div class="page-head"><h1>Spells</h1><p class="lede">Spell cards are offered between rounds. Offensive spells (red) can be placed anywhere on the field. Defensive spells and buffs (blue) go only on your own side. After use, a spell recharges for its cooldown.</p></div>
      <div class="toolbar"><div class="seg">${["All", ...W.factions.map((f) => f.name)].map((v) => `<a class="btn ghost" style="padding:6px 12px;${(only || "All") === v ? "background:var(--accent);color:#021018" : ""}" href="#/spells${v === "All" ? "" : "?f=" + encodeURIComponent(v)}">${esc(v)}</a>`).join("")}</div></div>
      ${W.factions.filter((f) => !only || f.name === only).map((f) => `<div class="section" ${fstyle(f.name)}>${sectionHead(f.name + " spells")}<div class="card-grid">${f.spells.map((s) => spellCard(W.spells[s])).join("")}</div></div>`).join("")}
      ${footer()}</div>`;
  }

  // ------------------------------------------------------------------ cards
  function cardCard(c) {
    const kv = [["Cost", credits(c.cost)], ["Copies", c.copies >= 99 ? "Unlimited" : c.copies]];
    if (c.minRound) kv.push(["From round", c.minRound]);
    if (c.cooldown) kv.push(["Cooldown", `${c.cooldown} rounds`]);
    const allFactions = c.factions.length === W.factions.length;
    return `<div class="game-card"><div class="art">${img(c.emblem, c.name)}<span class="corner chip">${esc(c.kind)}</span></div>
      <div class="body"><h3>${esc(c.name)}</h3><p class="desc">${esc(c.description)}</p>${c.effects.length ? effectChips(c.effects) : ""}<dl class="kv">${kv.map(([k, v]) => `<dt>${k}</dt><dd>${v}</dd>`).join("")}</dl>
      <div class="chips">${allFactions ? '<span class="chip">Every faction</span>' : c.factions.map((f) => `<span class="chip"><span style="color:${fc(f)}">●</span> ${esc(f)}</span>`).join("")}</div></div></div>`;
  }
  function cardsPage() {
    const kinds = [["Item", "Items", "An item is carried by one squad. Its bonus goes with that squad from round to round, and the squad shows it in the Inventory."], ["Economy", "Economy cards", "More credits every round for the rest of the match."], ["Buff", "Army buffs", "Bonuses for your whole army, or more squads deployed each round."], ["Training", "Training", "Spent on your squads; it recharges between uses."]];
    const m = W.match;
    return `<div class="page">${crumbs(["Home", "#/"], "Items & cards")}
      <div class="page-head"><h1>Items &amp; cards</h1><p class="lede">From round ${m.firstCardRound}, you're offered ${m.cardsPerOffer} cards after each battle. Take one and pay its cost, or skip and get ${m.spellSkipBonus} credits instead. Spells have <a href="#/spells">their own page</a>.</p></div>
      ${kinds.map(([k, title, text]) => {
        const list = Object.values(W.cards).filter((c) => c.kind === k);
        return list.length ? `<div class="section">${sectionHead(title, text)}<div class="card-grid">${list.map(cardCard).join("")}</div></div>` : "";
      }).join("")}
      <div class="section">${sectionHead("Reinforcement cards", "Free squads offered between rounds")}<div class="panel"><dl class="kv">
        <dt>First offered</dt><dd>Round ${m.reinforcementFirstRound}</dd><dt>Budget</dt><dd>${m.reinforcementBudgetBase} credits + ${m.reinforcementBudgetPerRound} per round</dd>
        <dt>Highest tier</dt><dd>${m.reinforcementMaxTier}</dd><dt>Highest level</dt><dd>${m.reinforcementMaxLevel} (one level every ${m.reinforcementLevelEveryRounds} rounds)</dd><dt>Skip bonus</dt><dd>${m.reinforcementSkipBonus} credits</dd></dl></div></div>
      ${footer()}</div>`;
  }

  // ------------------------------------------------------------------ counter web
  const cw = { rows: "All", cols: "All" };
  function countersPage() {
    const seg = (key) => `<div class="seg" data-key="${key}">${["All", ...W.factions.map((f) => f.name)].map((v) => `<button class="${cw[key] === v ? "on" : ""}" data-v="${esc(v)}">${esc(v)}</button>`).join("")}</div>`;
    return `<div class="page">${crumbs(["Home", "#/"], "Counter web")}
      <div class="page-head"><h1>Counter web</h1><p class="lede">Each cell is a measured battle result: an army of the row's unit against an army of the column's unit, at equal credits. Green means the row wins, red means it loses. Hover over a cell for details; click a name to open that unit.</p></div>
      <div class="toolbar"><label>Rows</label>${seg("rows")}<label>Columns</label>${seg("cols")}<span class="grow"></span><span class="legend">Row loses <span class="ramp"></span> Row wins</span></div>
      <div class="matrix-wrap" id="matrix"></div>
      <p style="color:var(--text-3);font-size:12.5px">Source: ${esc(W.counters.source)}. These are single-unit armies; mixed armies, techs, levels, items and spells change things.</p>
      ${footer()}</div>`;
  }
  function heat(rate) {
    // Red (0) through slate (0.5) to green (1).
    const lerp = (a, b, t) => Math.round(a + (b - a) * t);
    const [r1, g1, b1] = rate < 0.5 ? [255, 90, 79] : [58, 70, 82];
    const [r2, g2, b2] = rate < 0.5 ? [58, 70, 82] : [79, 224, 143];
    const t = rate < 0.5 ? rate / 0.5 : (rate - 0.5) / 0.5;
    return `rgb(${lerp(r1, r2, t)},${lerp(g1, g2, t)},${lerp(b1, b2, t)})`;
  }
  function bindCounters() {
    const tip = $("#tooltip");
    const draw = () => {
      const rates = W.counters.rates;
      const pick = (sel) => ORDER.filter((id) => rates[id] && (sel === "All" || factionOf(UNITS[id]) === sel));
      const rows = pick(cw.rows), cols = pick(cw.cols);
      let html = `<table class="matrix"><thead><tr><th class="corner"></th>${cols.map((c) => `<th class="col"><div><a href="#/unit/${c}" style="color:${fc(factionOf(UNITS[c]))}">${esc(UNITS[c].name)}</a></div></th>`).join("")}</tr></thead><tbody>`;
      rows.forEach((r) => {
        html += `<tr><th class="row"><a href="#/unit/${r}" style="color:${fc(factionOf(UNITS[r]))}">${esc(UNITS[r].name)}</a></th>`;
        cols.forEach((c) => {
          const v = rates[r][c];
          html += r === c || v == null ? `<td class="m" style="background:rgba(255,255,255,0.03)"></td>` : `<td class="m" data-r="${r}" data-c="${c}" style="background:${heat(v)}"></td>`;
        });
        html += "</tr>";
      });
      $("#matrix").innerHTML = html + "</tbody></table>";
    };
    $$(".toolbar .seg").forEach((s) => s.addEventListener("click", (e) => { const b = e.target.closest("button"); if (!b) return; cw[s.dataset.key] = b.dataset.v; $$("button", s).forEach((x) => x.classList.toggle("on", x === b)); draw(); }));
    $("#matrix").addEventListener("mousemove", (e) => {
      const td = e.target.closest("td[data-r]");
      if (!td) { tip.hidden = true; return; }
      const a = UNITS[td.dataset.r], b = UNITS[td.dataset.c], v = W.counters.rates[td.dataset.r][td.dataset.c];
      tip.innerHTML = `<b>${esc(a.name)}</b> vs <b>${esc(b.name)}</b><br>${esc(a.name)} wins ${pct(v)}`;
      tip.hidden = false;
      tip.style.left = Math.min(e.clientX + 14, innerWidth - 300) + "px";
      tip.style.top = e.clientY + 14 + "px";
    });
    $("#matrix").addEventListener("mouseleave", () => (tip.hidden = true));
    $("#matrix").addEventListener("click", (e) => { const td = e.target.closest("td[data-r]"); if (td) location.hash = `#/compare?u=${td.dataset.r},${td.dataset.c}`; });
    draw();
  }

  // ------------------------------------------------------------------ compare
  function comparePage(query) {
    const ids = (query.u || "").split(",").filter((x) => UNITS[x]).slice(0, 4);
    const options = W.factions.map((f) => `<optgroup label="${esc(f.name)}">${ORDER.filter((id) => factionOf(UNITS[id]) === f.name).map((id) => `<option value="${id}">${esc(UNITS[id].name)}</option>`).join("")}</optgroup>`).join("");
    const rows = [["Faction", (u) => factionOf(u)], ["Role", (u) => u.role], ["Tier", (u) => u.tier, 1], ["Squad cost", (u) => u.cost, -1], ["Unlock cost", (u) => u.unlockCost, -1], ["Squad size", (u) => u.squadSize, 0],
      ["Health / body", (u) => u.health, 1], ["Shield / body", (u) => u.shield, 1], ["Armor", (u) => u.armor], ["Air", (u) => (u.air ? "Yes" : "No")], ["Speed (m/s)", (u) => u.speed, 1], ["Longest range (m)", (u) => u.maxRange, 1],
      ["Footprint (m)", (u) => u.radius * 2, 0], ["Squad health", (u) => u.squadHealth, 1], ["Squad DPS", (u) => u.squadDps, 1], ["DPS vs unarmored", (u) => u.dpsVs.Unarmored, 1], ["DPS vs light", (u) => u.dpsVs.Light, 1],
      ["DPS vs heavy", (u) => u.dpsVs.Heavy, 1], ["DPS vs shields", (u) => u.dpsVs.Shields, 1], ["Health per credit", (u) => u.healthPerCredit, 1], ["DPS per 100 credits", (u) => u.dpsPerCredit, 1], ["Base damage", (u) => u.baseDamage, 1], ["Techs", (u) => u.techs.length, 0]];
    const units = ids.map((id) => UNITS[id]);
    const pairRate = ids.length === 2 && W.counters.rates[ids[0]] ? W.counters.rates[ids[0]][ids[1]] : null;
    return `<div class="page">${crumbs(["Home", "#/"], "Compare units")}
      <div class="page-head"><h1>Compare units</h1><p class="lede">Pick up to four units. In each row the best value is green and the worst is red.</p></div>
      <div class="toolbar"><label>Add a unit <select id="addUnit"><option value="">Choose…</option>${options}</select></label><span class="grow"></span>${ids.length ? '<a class="btn ghost" href="#/compare" style="padding:6px 12px">Clear</a>' : ""}</div>
      ${units.length ? `<div class="compare-head" style="grid-template-columns:repeat(${units.length},1fr)">${units.map((u) => `<div class="compare-slot" ${fstyle(factionOf(u))}><button class="x" data-rm="${u.id}" aria-label="Remove ${esc(u.name)}">×</button>${img(u.icon, u.name)}<div><a href="#/unit/${u.id}" style="font:700 18px var(--font-head)">${esc(u.name)}</a></div></div>`).join("")}</div>
      ${pairRate != null ? `<div class="callout callout-note" style="margin-top:14px"><span class="callout-label">Head to head</span><p>${esc(units[0].name)} wins <b>${pct(pairRate)}</b> of measured battles against ${esc(units[1].name)} at equal credits.</p></div>` : ""}
      <div class="table-wrap"><table><thead><tr><th>Stat</th>${units.map((u) => `<th class="num">${esc(u.name)}</th>`).join("")}</tr></thead><tbody>${rows.map(([label, get, better]) => {
        const vals = units.map(get);
        const numeric = typeof vals[0] === "number" && better;
        const max = Math.max(...vals), min = Math.min(...vals);
        return `<tr><td>${label}</td>${vals.map((v) => {
          let cls = "num";
          if (numeric && units.length > 1 && max !== min) { const best = better > 0 ? max : min, worst = better > 0 ? min : max; if (v === best) cls += " best"; else if (v === worst) cls += " worst"; }
          return `<td class="${cls}">${typeof v === "number" ? num(v) : esc(v)}</td>`;
        }).join("")}</tr>`;
      }).join("")}</tbody></table></div>` : '<div class="empty">Choose a unit above to start. Tip: click any cell in the counter web to compare that pair.</div>'}
      ${footer()}</div>`;
  }
  function bindCompare(query) {
    const ids = (query.u || "").split(",").filter((x) => UNITS[x]).slice(0, 4);
    $("#addUnit").addEventListener("change", (e) => { if (!e.target.value) return; const next = [...ids.filter((x) => x !== e.target.value), e.target.value].slice(-4); location.hash = "#/compare?u=" + next.join(","); });
    $$("[data-rm]").forEach((b) => b.addEventListener("click", () => { location.hash = "#/compare?u=" + ids.filter((x) => x !== b.dataset.rm).join(","); }));
  }

  // ------------------------------------------------------------------ written pages and their widgets
  function pageIdFor(word) { const p = PAGES.find((x) => x.id.includes(word) || x.html.includes(`data-widget="damage-table"`)); return p ? p.id : ""; }
  const WIDGETS = {
    "damage-table": () => {
      const c = W.combat, types = [["Ballistic", "ballistic"], ["Explosive", "explosive"], ["Energy", "energy"], ["Fire", "fire"], ["Melee", "melee"], ["Anti-armor", "antiArmor"]];
      const cell = (v) => `<td class="num cell" style="background:${heat(Math.max(0, Math.min(1, v / 2)))};color:#fff;text-shadow:0 1px 2px rgba(0,0,0,0.7);font-weight:700">×${num(v)}</td>`;
      return `<div class="table-wrap"><table class="heat"><thead><tr><th>Damage type</th><th class="num">Unarmored</th><th class="num">Light</th><th class="num">Heavy</th><th class="num">Energy shield</th></tr></thead><tbody>${types.map(([l, k]) => `<tr><td><b>${l}</b></td>${cell(c[k].unarmored)}${cell(c[k].light)}${cell(c[k].heavy)}${cell(c.shieldMultipliers[k])}</tr>`).join("")}</tbody></table></div>`;
    },
    "income-table": () => {
      const m = W.match, rows = [];
      for (let r = 1; r <= 10; r++) rows.push([r, Math.min(m.incomeCap, m.baseIncome + m.incomeIncreasePerRound * (r - 1))]);
      let total = 0;
      return `<div class="table-wrap"><table><thead><tr><th>Round</th><th class="num">Income</th><th class="num">Total so far</th><th style="width:45%"></th></tr></thead><tbody>${rows.map(([r, v]) => { total += v; return `<tr><td>Round ${r}</td><td class="num">${credits(v)}</td><td class="num">${num(total, 0)}</td><td>${bar(v, m.incomeCap)}</td></tr>`; }).join("")}</tbody></table></div>`;
    },
    "tower-levels": () => {
      const t = W.towers, maxH = Math.max(...t.levels.map((l) => l.health));
      return `<div class="grid cols-2"><div class="panel"><div class="panel-title">${icon("tower")} Tower health by level</div><div class="tower-visual">${t.levels.map((l) => `<div style="height:${(l.health / maxH) * 100}%"><span>${num(l.health, 0)}</span><em>LV ${l.level}</em></div>`).join("")}</div></div>
        <div class="table-wrap" style="margin:0"><table><thead><tr><th>Level</th><th class="num">Health</th><th class="num">Shockwave</th><th class="num">XP for destroying</th></tr></thead><tbody>${t.levels.map((l) => `<tr><td style="white-space:nowrap">Level ${l.level}</td><td class="num">${num(l.health, 0)}</td><td class="num">${l.shockwave} s</td><td class="num">${l.xp != null ? num(l.xp, 0) : "–"}</td></tr>`).join("")}</tbody></table></div></div>`;
    },
    "shockwave": () => {
      const s = W.towers.shockwave;
      return `<div class="chips"><span class="chip bad">Speed <b>×${num(s.speed)}</b></span><span class="chip bad">Damage <b>×${num(s.damage)}</b></span><span class="chip bad">Fire rate <b>×${num(s.fireRate)}</b></span><span class="chip bad">Damage taken <b>×${num(s.damageTaken)}</b></span></div>`;
    },
    "match-numbers": () => {
      const m = W.match, c = W.combat;
      const items = [["Base health", num(m.baseHealth, 0)], ["Starting credits", num(m.startingCredits, 0)], ["Income", `${m.baseIncome} + ${m.incomeIncreasePerRound}/round, max ${m.incomeCap}`], ["New squads per round", m.newSquadsPerRound], ["Unlocks per round", m.unlocksPerRound],
        ["Refund before battle", pct(m.refundBeforeBattle)], ["Refund after battle", pct(m.refundAfterBattle)], ["Battle time limit", `${c.battleTimeLimitSeconds} s`], ["Arena", `${c.arenaWidthSquares} × ${c.deploymentDepthSquares * 2 + c.noMansLandSquares} squares of ${c.squareSize} m`],
        ["Your zone", `${c.arenaWidthSquares} × ${c.deploymentDepthSquares} squares`], ["Cards from round", m.firstCardRound], ["Cards per offer", m.cardsPerOffer]];
      return `<div class="stats-strip" style="grid-template-columns:repeat(auto-fill,minmax(170px,1fr))">${items.map(([l, v]) => `<div class="stat-tile"><b style="font-size:22px">${esc(v)}</b><span>${l}</span></div>`).join("")}</div>`;
    },
    "roles": () => {
      return `<div class="grid auto-fill" style="--min:250px">${W.enums.roles.map((r) => {
        const ids = playable.filter((id) => UNITS[id].role === r);
        return `<a class="section-card" href="#/units?role=${encodeURIComponent(r)}"><span><b>${esc(r)}</b><span>${ids.length} units</span><span class="chips" style="margin-top:8px">${ids.slice(0, 6).map((id) => img(UNITS[id].icon, UNITS[id].name, "")).join("").replace(/<img /g, '<img style="width:34px;height:34px;object-fit:contain" ')}</span></span></a>`;
      }).join("")}</div>`;
    },
    "factions": () => `<div class="faction-tiles">${W.factions.map((f) => `<a class="faction-tile" style="min-height:150px;--fc:${fc(f.name)}" href="#/faction/${f.id}">${img(f.emblem, f.name, "emblem")}<h3>${esc(f.name)}</h3><p>${f.subFactions.map((s) => esc(s.name)).join(" · ")}</p></a>`).join("")}</div>`,
    "subfactions": () => `<div class="table-wrap"><table><thead><tr><th>Faction</th><th>Sub-faction</th><th>What it's about</th><th>Its units</th></tr></thead><tbody>${W.factions.flatMap((f) => f.subFactions.map((s) => `<tr ${fstyle(f.name)}><td style="color:${fc(f.name)};font-weight:700">${esc(f.name)}</td><td><a href="#/faction/${f.id}">${esc(s.name)}</a></td><td>${esc(s.description)}</td><td>${s.units.map((id) => unitLink(id)).join(", ")}</td></tr>`)).join("")}</tbody></table></div>`,
    "flood-units": () => `<div class="unit-grid">${playable.filter((id) => UNITS[id].infects || UNITS[id].seedForms || UNITS[id].spawns || (UNITS[id].deathBurst && UNITS[id].deathBurst.spawns.length)).map(unitCard).join("")}</div>`,
    "air-units": () => `<div class="unit-grid">${playable.filter((id) => UNITS[id].air).map(unitCard).join("")}</div>`,
    "flankers": () => `<div class="unit-grid">${playable.filter((id) => UNITS[id].canFlank).map(unitCard).join("")}</div>`,
    "titans": () => `<div class="unit-grid">${playable.filter((id) => UNITS[id].tier === 4).map(unitCard).join("")}</div>`,
    "armor-counts": () => `<div class="chips">${W.enums.armor.map((a) => `<a class="chip" href="#/units" data-armor="${a}">${a} <b>${playable.filter((id) => UNITS[id].armor === a).length}</b></a>`).join("")}<span class="chip">With shields <b>${playable.filter((id) => UNITS[id].shield > 0).length}</b></span></div>`,
  };
  function docPage(id) {
    const p = PAGES.find((x) => x.id === id);
    if (!p) return notFound();
    const i = PAGES.indexOf(p), prev = PAGES[i - 1], next = PAGES[i + 1];
    return `<div class="page">${crumbs(["Home", "#/"], p.group, p.title)}
      <div class="page-head"><h1>${esc(p.title)}</h1>${p.blurb ? `<p class="lede">${esc(p.blurb)}</p>` : ""}</div>
      <div class="doc"><article class="doc-body">${p.html}
        <div class="doc-pager pager">${prev ? `<a href="#/page/${prev.id}"><span><small>Previous</small>${esc(prev.title)}</span></a>` : "<span></span>"}${next ? `<a class="next" href="#/page/${next.id}"><span><small>Next</small>${esc(next.title)}</span></a>` : "<span></span>"}</div></article>
        ${p.toc.length > 1 ? `<aside class="toc"><b>On this page</b>${p.toc.map((t) => `<a class="l${t.level}" href="#/page/${p.id}#${t.id}" data-target="${t.id}">${esc(t.text)}</a>`).join("")}</aside>` : ""}</div>
      ${footer()}</div>`;
  }
  function bindDoc() {
    $$(".widget").forEach((el) => { const f = WIDGETS[el.dataset.widget]; el.innerHTML = f ? f() : `<div class="empty">Unknown widget ${esc(el.dataset.widget)}</div>`; });
    $$(".toc a").forEach((a) => a.addEventListener("click", (e) => { e.preventDefault(); const t = document.getElementById(a.dataset.target); if (t) t.scrollIntoView({ behavior: "smooth" }); }));
    // Links inside written pages may say "#/unit/Marine" or "#some-heading".
    $$(".doc-body a[href^='#']").forEach((a) => {
      const h = a.getAttribute("href");
      if (!h.startsWith("#/")) a.addEventListener("click", (e) => { e.preventDefault(); const t = document.getElementById(h.slice(1)); if (t) t.scrollIntoView({ behavior: "smooth" }); });
    });
    const heads = $$(".doc-body h2, .doc-body h3");
    if (heads.length && "IntersectionObserver" in window) {
      const obs = new IntersectionObserver((entries) => entries.forEach((en) => { if (en.isIntersecting) $$(".toc a").forEach((a) => a.classList.toggle("on", a.dataset.target === en.target.id)); }), { rootMargin: "-80px 0px -70% 0px" });
      heads.forEach((h) => obs.observe(h));
    }
  }

  function notFound() { return `<div class="page"><div class="page-head"><h1>Not found</h1><p class="lede">That page isn't in the manual. <a href="#/">Back to the start</a>.</p></div></div>`; }

  // ------------------------------------------------------------------ search
  const INDEX = [];
  ORDER.forEach((id) => { const u = UNITS[id]; INDEX.push({ t: u.name, s: `${u.role} · ${subOf(u) === "Base roster" ? factionOf(u) : subOf(u) || factionOf(u)}`, k: "Unit", h: `#/unit/${id}`, img: u.icon, extra: u.weapons.map((w) => w.name).join(" ") }); });
  Object.values(W.techs).forEach((t) => INDEX.push({ t: t.name, s: `Tech for ${t.units.map((x) => UNITS[x].name).join(", ")} · ${t.description}`, k: "Tech", h: `#/unit/${t.units[0]}`, ic: "tech" }));
  Object.values(W.spells).forEach((s) => INDEX.push({ t: s.name, s: `${s.faction} spell · ${s.description}`, k: "Spell", h: `#/spells?f=${encodeURIComponent(s.faction)}`, img: s.emblem, dark: true }));
  Object.values(W.cards).forEach((c) => INDEX.push({ t: c.name, s: `${c.kind} · ${c.description}`, k: c.kind, h: "#/cards", img: c.emblem, dark: true }));
  W.factions.forEach((f) => { INDEX.push({ t: f.name, s: "Faction", k: "Faction", h: `#/faction/${f.id}`, img: f.emblem }); f.subFactions.forEach((s) => INDEX.push({ t: s.name, s: `${f.name} sub-faction · ${s.description}`, k: "Sub-faction", h: `#/faction/${f.id}`, ic: "flag" })); });
  PAGES.forEach((p) => { INDEX.push({ t: p.title, s: p.blurb || p.group, k: "Page", h: `#/page/${p.id}`, ic: p.icon || "book" }); p.toc.forEach((t) => INDEX.push({ t: t.text, s: `In ${p.title}`, k: "Section", h: `#/page/${p.id}#${t.id}`, ic: "book" })); });
  [["Counter web", "#/counters", "web"], ["Compare units", "#/compare", "compare"], ["All units", "#/units", "units"], ["Techs", "#/techs", "tech"], ["Spells", "#/spells", "spell"], ["Items & cards", "#/cards", "item"]].forEach(([t, h, ic]) => INDEX.push({ t, s: "Section", k: "Page", h, ic }));
  function score(entry, q) {
    const t = entry.t.toLowerCase();
    if (t === q) return 100;
    if (t.startsWith(q)) return 80 - t.length / 100;
    if (t.split(/[\s(\-/]+/).some((w) => w.startsWith(q))) return 60;
    if (t.includes(q)) return 45;
    if ((entry.extra || "").toLowerCase().includes(q)) return 30;
    if (entry.s.toLowerCase().includes(q)) return 20;
    let i = 0; for (const ch of t) if (ch === q[i]) i++;
    return i === q.length ? 10 : 0;
  }
  let sel = 0, results = [];
  function openPalette() { $("#palette").hidden = false; const inp = $("#paletteInput"); inp.value = ""; drawResults(""); setTimeout(() => inp.focus(), 10); }
  function closePalette() { $("#palette").hidden = true; }
  function drawResults(q) {
    q = q.trim().toLowerCase();
    results = q ? INDEX.map((e) => [score(e, q), e]).filter((x) => x[0] > 0).sort((a, b) => b[0] - a[0]).slice(0, 40).map((x) => x[1]) : INDEX.filter((e) => e.k === "Page" || e.k === "Faction").slice(0, 14);
    sel = 0;
    $("#paletteResults").innerHTML = results.length ? results.map((e, i) => `<a class="p-item ${i === 0 ? "sel" : ""}" href="${e.h}" data-i="${i}">${e.img ? `<img src="${esc(e.img)}" alt="" ${e.dark ? 'style="background:#1a2633;padding:4px"' : ""}>` : `<span class="p-ico">${icon(e.ic)}</span>`}<span style="min-width:0"><b>${esc(e.t)}</b><small style="display:block;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${esc(e.s)}</small></span><span class="p-kind">${esc(e.k)}</span></a>`).join("") : '<div class="p-empty">Nothing found.</div>';
  }
  function moveSel(d) {
    const items = $$(".p-item");
    if (!items.length) return;
    sel = (sel + d + items.length) % items.length;
    items.forEach((x, i) => x.classList.toggle("sel", i === sel));
    items[sel].scrollIntoView({ block: "nearest" });
  }
  $("#searchBtn").addEventListener("click", openPalette);
  $("#palette").addEventListener("click", (e) => { if (e.target.id === "palette") closePalette(); if (e.target.closest(".p-item")) closePalette(); });
  $("#paletteInput").addEventListener("input", (e) => drawResults(e.target.value));
  $("#paletteInput").addEventListener("keydown", (e) => {
    if (e.key === "ArrowDown") { e.preventDefault(); moveSel(1); }
    else if (e.key === "ArrowUp") { e.preventDefault(); moveSel(-1); }
    else if (e.key === "Enter") { const r = results[sel]; if (r) { location.hash = r.h; closePalette(); } }
    else if (e.key === "Escape") closePalette();
  });
  document.addEventListener("keydown", (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") { e.preventDefault(); openPalette(); }
    else if (e.key === "/" && !/input|select|textarea/i.test(document.activeElement.tagName) && $("#palette").hidden) { e.preventDefault(); openPalette(); }
    else if (e.key === "Escape" && !$("#palette").hidden) closePalette();
  });

  // ------------------------------------------------------------------ router
  function parse() {
    let h = location.hash.replace(/^#/, "") || "/";
    let anchor = "";
    const second = h.indexOf("#");
    if (second >= 0) { anchor = h.slice(second + 1); h = h.slice(0, second); }
    const [path, qs] = h.split("?");
    const query = {};
    (qs || "").split("&").filter(Boolean).forEach((kv) => { const [k, v] = kv.split("="); query[decodeURIComponent(k)] = decodeURIComponent(v || ""); });
    const parts = path.split("/").filter(Boolean);
    return { parts, query, anchor };
  }
  let lastPath = null;
  function route() {
    const { parts, query, anchor } = parse();
    const pathKey = parts.join("/") + "?" + JSON.stringify(query);
    if (pathKey === lastPath && anchor) { const t = document.getElementById(anchor); if (t) t.scrollIntoView({ behavior: "smooth" }); return; }
    lastPath = pathKey;
    const [a, b] = parts;
    let html, after = null, title = "";
    if (!a) html = home();
    else if (a === "faction") { html = factionPage(b); const f = W.factions.find((x) => x.id === b); title = f && f.name; }
    else if (a === "units") { html = unitsPage(query); after = bindUnits; title = "Units"; }
    else if (a === "unit") { html = unitPage(b); title = UNITS[b] && UNITS[b].name; }
    else if (a === "techs") { html = techsPage(); after = bindTechs; title = "Techs"; }
    else if (a === "spells") { html = spellsPage(query); title = "Spells"; }
    else if (a === "cards") { html = cardsPage(); title = "Items & cards"; }
    else if (a === "counters") { html = countersPage(); after = bindCounters; title = "Counter web"; }
    else if (a === "compare") { html = comparePage(query); after = () => bindCompare(query); title = "Compare"; }
    else if (a === "page") { html = docPage(b); after = bindDoc; const p = PAGES.find((x) => x.id === b); title = p && p.title; }
    else html = notFound();
    main.innerHTML = html;
    if (after) after();
    bindSort(main);
    document.title = (title ? title + " · " : "") + "Halobellum Field Manual";
    markNav();
    document.body.classList.remove("nav-open");
    if (anchor) { const t = document.getElementById(anchor); if (t) { setTimeout(() => t.scrollIntoView(), 30); return; } }
    window.scrollTo(0, 0);
  }

  sidebar();
  $("#version").textContent = "v" + W.version;
  $("#menuBtn").addEventListener("click", () => document.body.classList.toggle("nav-open"));
  $("#scrim").addEventListener("click", () => document.body.classList.remove("nav-open"));
  window.addEventListener("hashchange", route);
  route();
})();
