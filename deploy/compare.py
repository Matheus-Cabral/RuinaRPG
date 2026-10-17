#!/usr/bin/env python3
"""Compares two data fingerprints (A before, B after) against an allowlist."""
import argparse
import json
import sys
from collections import Counter, defaultdict

import yaml

SAMPLE = 20


def load(path):
    keyed, multi = {}, defaultdict(Counter)
    with open(path, encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            o = json.loads(line)
            row = {k: (None if v is None else str(v)) for k, v in o["row"].items()}
            if o["pk"] is None:
                multi[o["table"]][json.dumps(row, sort_keys=True)] += 1
            else:
                keyed[(o["table"], str(o["pk"]))] = row
    return keyed, multi


def allowed(allow, table, change, cols):
    for e in allow:
        if e.get("table") != table or e.get("change") != change:
            continue
        c = e.get("columns", "*")
        if c == "*" or set(cols) <= set(c):
            return True
    return False


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("a"); ap.add_argument("b"); ap.add_argument("allowlist")
    ap.add_argument("--markdown")
    args = ap.parse_args()

    ka, ma = load(args.a)
    kb, mb = load(args.b)
    with open(args.allowlist, encoding="utf-8") as f:
        allow = (yaml.safe_load(f) or {}).get("changes") or []

    # table -> {"lost": [pk], "altered": [(pk, cols)]}
    found = defaultdict(lambda: {"lost": [], "altered": []})
    for (t, pk), ra in ka.items():
        rb = kb.get((t, pk))
        if rb is None:
            found[t]["lost"].append(pk)
            continue
        diff = sorted(c for c, v in ra.items() if c in rb and rb[c] != v)
        if diff:
            found[t]["altered"].append((pk, diff))
    for t, ca in ma.items():
        missing = ca - mb.get(t, Counter())
        for k, n in missing.items():
            found[t]["lost"].extend(["(sem pk)"] * n)

    lines, count = [], 0
    for t in sorted(found):
        lost, alt = found[t]["lost"], found[t]["altered"]
        sec = []
        if lost and not allowed(allow, t, "lost", []) :
            count += len(lost)
            sec.append(f"- lost: {len(lost)} linha(s) (pk: {', '.join(lost[:SAMPLE])})")
        un = [(pk, d) for pk, d in alt if not allowed(allow, t, "altered", d)]
        if un:
            count += len(un)
            cols = sorted({c for _, d in un for c in d})
            sec.append(f"- altered: {len(un)} linha(s), colunas: {', '.join(cols)} (pk: {', '.join(pk for pk, _ in un[:SAMPLE])})")
        if sec:
            lines.append(f"### {t}")
            lines += sec

    head = ["## Comparação de dados (clone)",
            "Resultado: OK" if count == 0 else f"Resultado: FALHOU — {count} achado(s) não previsto(s) em deploy/data-changes.yml"]
    report = "\n".join(head + lines) + "\n"
    sys.stdout.write(report)
    if args.markdown:
        with open(args.markdown, "w", encoding="utf-8") as f:
            f.write(report)
    return 0 if count == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
