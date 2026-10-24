# Efeitos no Livro de Regras — Design

**Status:** approved by user 2026-09-29 (three choices answered in chat); queued to run after the Durabilidade por Rank plan.

## Problem

An Efeito created on the Auditoria de Efeitos page is saved to the `Efeitos` catalog (and can be bought in Magias/Habilidades), but the Livro de Regras' **Graus & Círculos** tab never shows it: that tab renders Markdown (the Auditor's `RulebookDocumentOverride` for slug `graus-e-circulos`, or the embedded `GRAUS & CÍRCULOS.md`), which nothing updates when the catalog changes.

## Decisions

| Question | Decision |
|---|---|
| Mechanism | **Written into the Livro's text**: the Efeito's `## Nome` block is inserted into the Graus & Círculos Markdown (the override row, created from the embedded .md if none exists yet), so it also shows in the Auditoria's Livro de Regras editor and can be hand-edited afterwards. |
| Block content | Same shape as the existing Efeitos: `Gasto:` line built from the cost fields, the per-Grau max line when there is one, the Descrição, and "Obrigatória a compra de X" for pré-requisitos. |
| Edit / delete of an Efeito on the Auditoria | **Full sync**: editing an Efeito rewrites its block (and moves it if the Grau changed / renames the heading if the Nome changed); deleting removes its block — for original Efeitos too. |
| Existing data | On startup of the new version (and on `--migrate`), a sync pass **adds** every catalog Efeito missing from the Livro text. It never rewrites or removes existing blocks. |

## Rules

**Where a block lives.** Graus & Círculos is split by `#` headings, one per Grau (`# 1º GRAU / CÍRCULO I`, … `# 9º GRAU / CÍRCULO IX`). An Efeito of Grau N belongs in the Nth `#` section; a new block is appended at the end of that section (before the next `#`). If the section for its Grau doesn't exist, the block is appended at the end of the document under a new `# Nº GRAU / CÍRCULO <roman>` heading.

**Matching an Efeito to a block** (normalized: trimmed, case-insensitive, accent-insensitive) — a `##` heading matches the Efeito when:
- it equals the Efeito's Nome; or
- it equals the Nome with a trailing parenthetical removed (the catalog's `Libra (Arcana)` / `Libra (Vitalidade)` share the `## Libra` block).

**Shared blocks are never rewritten or removed by the sync**: the three basic Efeitos (`Dano`, `Alcance`, `Duração`) live together under `## Efeitos Básicos`, and the `Libra (…)` variants share `## Libra`. For these, "present" means the shared heading exists; edit/delete on the Auditoria leaves the Livro text untouched.

**Generated block** (Markdown):

```
## <Nome>

**Gasto:** <gasto>.

Max. <MaxUnidades> <UnidadeLabel> por Grau/Círculo.      ← only when MaxUnidades is set and MaxEscalaPorGrau; "Max. <MaxUnidades> <UnidadeLabel>." when not per-Grau

<Descricao>

Obrigatória a compra de <A>.                              ← one line per AND-group; an OR-group reads "<A> ou <B>"
```

`<gasto>` by `TipoDeCusto`: Fixo → `<CustoFixo> PI`; PorUnidade → `<CustoPorUnidade> PI por <UnidadeLabel>`; Manual → `X PI`; ManualPorUnidade → `X PI por <UnidadeLabel>`; DerivadoDeOutroEfeito → `igual à Quantidade de <QuantidadeDerivadaDeEfeito>`. When `CustoAlternativo` is set, append `(<CustoAlternativo> PI a partir do <CustoAlternativoAPartirDoGrau>º Grau)`.

**When it runs**
- `POST /api/efeitos` (create): insert the block if missing.
- `PUT /api/efeitos/{id}` (edit): if the Efeito's block exists (matched by its **old** Nome), replace it with the regenerated block in the section of its (possibly new) Grau; if it doesn't exist, insert it. Shared blocks: no change.
- `DELETE /api/efeitos/{id}`: remove its block (heading through the line before the next `##`/`#`). Shared blocks: no change.
- `DELETE /api/rulebook-documents/graus-e-circulos` (Auditor's "Restaurar padrão"): after deleting the override, run the add-missing pass, so the restored text still contains every catalog Efeito.
- Startup (Development branch) and `--migrate`: add-missing pass.
- All of these write the `graus-e-circulos` override in the same transaction as the Efeito change (the Efeito save and the Livro save succeed or fail together).

**Author of system writes.** `RulebookDocumentOverride.UpdatedByUserId` becomes nullable: an Auditoria action records the Auditor; the startup pass records `null`.

## Components

- Domain (`RuinaRPG.Domain.Rules`), pure and unit-tested:
  - `EfeitoMarkdownBlock.Gerar(EfeitoParaLivro efeito) → string` — the generated block.
  - `GrausECirculosMarkdown` — operations on the Markdown text: `Contem(md, nome)`, `Inserir(md, grau, bloco)`, `Substituir(md, nomeAntigo, grau, bloco)`, `Remover(md, nome)`, with the matching and shared-block rules above.
  - `EfeitoParaLivro` record — the Efeito fields the block needs (Nome, Grau, Descricao, TipoDeCusto, custos, UnidadeLabel, QuantidadeDerivadaDeEfeito, MaxUnidades, MaxEscalaPorGrau, CustoAlternativo, CustoAlternativoAPartirDoGrau, PreRequisitos as `IReadOnlyList<IReadOnlyList<string>>`).
- Infrastructure: `LivroDeRegrasEfeitosSync` (Scoped) — loads/creates the override, applies the domain operations, and `SincronizarFaltantesAsync()` for the startup/restore pass.
- Api: `EfeitosController` create/update/delete and `RulebookDocumentsController` delete call the sync; `Program.cs` runs `SincronizarFaltantesAsync` after `EfeitoSeeder` in both seed blocks.

## Docs

- `Requisitos - Auditoria de Regras.md`: R0006 (Efeitos CRUD) gains: creating/editing/deleting an Efeito updates its block in the Graus & Círculos document of the Livro de Regras (shared blocks excepted); the Livro always contains every catalog Efeito (checked at startup and after "Restaurar padrão").
- `Requisitos - Modelo de Dados.md`: `RulebookDocumentOverrides.UpdatedByUserId` nullable.

## Testing

- Unit: block generation for each TipoDeCusto, max lines, alternativo, pré-requisitos (AND + OR); Markdown operations: insert in the right Grau section, insert when the Grau section is missing, replace (same Grau, new Grau, renamed), remove, matching with accents/case, `Libra (X)` → `## Libra`, basics → `## Efeitos Básicos`, shared blocks untouched by replace/remove.
- Integration: create Efeito → `GET /api/rulebook/graus-e-circulos` (the Livro endpoint) shows it in the right Grau section; edit (rename + Grau change) moves/renames; delete removes; an existing hand-edited override keeps its other text; startup pass on an override missing an Efeito adds it and is idempotent (second run changes nothing); restore-default keeps the new Efeito.

## Out of scope

- Rewriting the original Efeitos' hand-authored prose at startup (only edit on the Auditoria regenerates a block).
- Any other Livro tab.
