#!/usr/bin/env python3
"""Compares two data fingerprints (A before, B after) against an allowlist."""
import argparse
import json
import sys
from collections import Counter, defaultdict

import yaml

SAMPLE = 20
_MISSING = object()


class InfraError(Exception):
    """Problem with the inputs (not a data finding): exit code 2."""


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
                key = (o["table"], str(o["pk"]))
                if key in keyed:
                    raise InfraError(f"chave duplicada {key} em {path}")
                keyed[key] = row
    return keyed, multi


def validate(allow):
    for e in allow:
        c = e.get("columns", "*") if isinstance(e, dict) else None
        ok = (isinstance(e, dict) and isinstance(e.get("table"), str)
              and e.get("change") in ("altered", "lost")
              and (c == "*" or (isinstance(c, list) and all(isinstance(x, str) for x in c))))
        if not ok:
            raise InfraError(f"entrada inválida na allowlist: {e!r}")


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
    ap.add_argument("--markdown", help="relatório público (só contagens e nomes de colunas); também vai para a saída padrão")
    ap.add_argument("--full", help="relatório completo (com até SAMPLE pks por tabela); nunca publicar")
    args = ap.parse_args()

    try:
        ka, ma = load(args.a)
        kb, mb = load(args.b)
        with open(args.allowlist, encoding="utf-8") as f:
            allow = (yaml.safe_load(f) or {}).get("changes") or []
        validate(allow)
        if not ka and not ma:
            raise InfraError("snapshot A está vazio")
    except InfraError as e:
        print(f"ERRO: {e}", file=sys.stderr)
        return 2
    cols_a = defaultdict(set)
    for (t, _), r in ka.items():
        cols_a[t].update(r)
    for t, ca in ma.items():
        for k in ca:
            cols_a[t].update(json.loads(k))
    proj = defaultdict(Counter)
    for t, cb in mb.items():
        for k, n in cb.items():
            r = json.loads(k)
            proj[t][json.dumps({c: r[c] for c in r if c in cols_a[t]}, sort_keys=True)] += n
    mb = proj

    # table -> {"lost": [pk], "altered": [(pk, cols)]}
    found = defaultdict(lambda: {"lost": [], "altered": []})
    for (t, pk), ra in ka.items():
        rb = kb.get((t, pk))
        if rb is None:
            found[t]["lost"].append(pk)
            continue
        diff = sorted(c for c, v in ra.items() if rb.get(c, _MISSING) != v)
        if diff:
            found[t]["altered"].append((pk, diff))
    for t, ca in ma.items():
        missing = ca - mb.get(t, Counter())
        for k, n in missing.items():
            found[t]["lost"].extend(["(sem pk)"] * n)

    lines, public, count = [], [], 0
    for t in sorted(found):
        lost, alt = found[t]["lost"], found[t]["altered"]
        sec, pub = [], []
        if lost and not allowed(allow, t, "lost", []) :
            count += len(lost)
            sec.append(f"- lost: {len(lost)} linha(s) (pk: {', '.join(lost[:SAMPLE])})")
            pub.append(f"- lost: {len(lost)} linha(s)")
        un = [(pk, d) for pk, d in alt if not allowed(allow, t, "altered", d)]
        if un:
            count += len(un)
            cols = sorted({c for _, d in un for c in d})
            sec.append(f"- altered: {len(un)} linha(s), colunas: {', '.join(cols)} (pk: {', '.join(pk for pk, _ in un[:SAMPLE])})")
            pub.append(f"- altered: {len(un)} linha(s), colunas: {', '.join(cols)}")
        if sec:
            lines.append(f"### {t}")
            lines += sec
            public.append(f"### {t}")
            public += pub

    head = ["## Comparação de dados (clone)",
            "Resultado: OK" if count == 0 else f"Resultado: FALHOU — {count} achado(s) não previsto(s) em deploy/data-changes.yml"]
    report = "\n".join(head + lines) + "\n"
    # Standard output feeds logs that are uploaded to GitHub: it carries the public report only.
    public_report = "\n".join(head + public) + "\n"
    sys.stdout.write(public_report)
    for path, text in ((args.markdown, public_report), (args.full, report)):
        if path:
            with open(path, "w", encoding="utf-8") as f:
                f.write(text)
    return 0 if count == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
