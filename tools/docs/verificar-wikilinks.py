#!/usr/bin/env python3
"""Verifica os [[wikilinks]] de Docs/ (.md e .canvas) como o Obsidian: o nome casa com um arquivo
de Docs/, com ou sem extensão. Aceita [[nome|apelido]] e [[nome#secao]]. Sai com 1 se houver link sem destino."""
import json
import re
import sys
import unicodedata
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[2] / "Docs"
LINK = re.compile(r"\[\[([^\]\n]+?)\]\]")


def norm(s):
    return unicodedata.normalize("NFC", s).strip().lower()


def destinos():
    nomes = set()
    for p in RAIZ.rglob("*"):
        if p.is_file():
            nomes.add(norm(p.name))
            nomes.add(norm(p.stem))
            nomes.add(norm(p.relative_to(RAIZ).as_posix()))
            nomes.add(norm(p.relative_to(RAIZ).with_suffix("").as_posix()))
    return nomes


def textos(arquivo):
    bruto = arquivo.read_text(encoding="utf-8")
    if arquivo.suffix != ".canvas":
        return [bruto]
    saida = []
    def varrer(o):
        if isinstance(o, dict):
            for k, v in o.items():
                if k in ("text", "file") and isinstance(v, str):
                    # nó "file" do canvas: caminho a partir da raiz do cofre antigo (ex.: RuínaRPG/Docs/...)
                    saida.append(v if k == "text" else "[[" + v.split("Docs/", 1)[-1] + "]]")
                else:
                    varrer(v)
        elif isinstance(o, list):
            for v in o:
                varrer(v)
    varrer(json.loads(bruto))
    return saida


def main():
    existentes = destinos()
    quebrados = []
    for arq in sorted(list(RAIZ.rglob("*.md")) + list(RAIZ.rglob("*.canvas"))):
        for texto in textos(arq):
            for m in LINK.finditer(texto):
                alvo = m.group(1).split("|")[0].split("#")[0].strip()
                if alvo == "":  # [[#secao]]: link para o próprio arquivo
                    continue
                if norm(alvo) not in existentes:
                    quebrados.append(f"{arq.relative_to(RAIZ.parent)}: [[{m.group(1)}]]")
    for q in quebrados:
        print(q)
    print(f"{len(quebrados)} wikilink(s) sem destino")
    return 1 if quebrados else 0


if __name__ == "__main__":
    sys.exit(main())
