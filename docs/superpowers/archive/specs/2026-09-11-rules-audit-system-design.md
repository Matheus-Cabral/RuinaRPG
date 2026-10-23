# Rules Audit System — Design Spec

Date: 2026-09-11
Branch: `feature/caracteristicas-raciais`
Status: Approved by user, ready for `writing-plans`.

## 1. Problem

"Livro de Regras" and "Características" are currently **fixed, read-only content**
baked into the Docker image (`Docs/Sistema RPG/*.md`, embedded build resources,
parsed at startup/request time by `IRulesDataProvider` and `RulebookRenderer`).
No GM can correct a typo, tweak a Característica's cost, or fix a rules description
without a code change + redeploy.

The user wants:
1. A way to **designate one GM, by email, via a server-console `make` command**, as
   a "Rules Auditor" — a role separate from GM/Jogador that grants access to new
   admin-only pages.
2. Those pages let the Rules Auditor **edit the Livro de Regras' displayed text**
   (Sistema Básico, Graus & Círculos, Tabela de Níveis) and **fully manage the
   Características catalog** (add/edit/delete).
3. Edits are **global** — they affect what every GM/Jogador on the server sees,
   not a per-GM scoped catalog like Itens/Banco de Magias.

## 2. Explicit scope decisions (confirmed with the user)

- **Livro de Regras editing is display-only.** It changes the rendered prose on
  the Livro de Regras page. It does **not** change any of the structured tables
  `IRulesDataProvider` feeds into gameplay calculators (Tabela de Níveis'
  XP/EAP/PI-per-level numbers, Tabela de Vocação/Classes/Arquetipos, Graus &
  Círculos' Efeitos catalog, Circulo/Grau-per-EAP). Those keep parsing the
  embedded resources exactly as today — unaffected by this feature. The edit
  page must say this explicitly, in the UI, so the Rules Auditor doesn't assume
  editing "Tabela de Níveis" changes anyone's computed Vitalidade.
- **Características editing is full CRUD**, including renaming, re-costing, or
  re-polarizing an existing (originally-seeded) trait, and deleting one. This
  conflicts with the existing `TraitSeeder` (re-syncs from `Características.md`
  on every startup/migrate) unless the seeder is taught to leave manually-touched
  rows alone — see §4.2.
- **Unification**: the Livro de Regras "Características" tab currently renders
  the raw `Caracteristicas.md` prose (via `RulebookRenderer.BuildCaracteristicas`),
  completely disconnected from the `Traits` DB table the character/NPC sheets'
  5.d actually uses. This spec **unifies** them: the "Características" tab is
  rebuilt from the live `Traits` table (grouped by Polaridade), so editing a
  trait via the new admin page is immediately visible both on sheets and on the
  Livro de Regras page, with one edit flow instead of two disconnected ones.
  Confirmed with the user.
- **Grant mechanism reflects immediately, no relogin needed** — checked directly
  against the DB per request, not baked into the JWT as a claim (a claim would
  only take effect on the auditor's next login/token refresh).

## 3. New concept: Rules Auditor

- `ApplicationUser.IsRulesAuditor` (bool, default false). Anyone can hold it in
  principle, but in practice it's only ever granted to a GM (the pages this
  gates are GM-panel pages); granting a Jogador account is rejected by the CLI
  command with a clear error, not silently allowed.
- Exactly one designated auditor at a time is the expected usage (per the user's
  "o email de um GM" framing), but nothing in the data model enforces
  single-auditor — granting a second email just adds a second auditor. Revoking
  is explicit and separate from granting a different email.

### 3.1 CLI (mirrors the existing `--migrate` one-shot pattern in `Program.cs`)

```csharp
if (args.Contains("--grant-rules-auditor") || args.Contains("--revoke-rules-auditor"))
{
    var grant = args.Contains("--grant-rules-auditor");
    var flag = grant ? "--grant-rules-auditor" : "--revoke-rules-auditor";
    var email = args.ElementAtOrDefault(Array.IndexOf(args, flag) + 1);
    if (string.IsNullOrWhiteSpace(email)) { log error, return with non-zero-ish log, return; }

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
    var user = await db.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == email.ToUpperInvariant());
    if (user is null) { log "no user with that email"; return; }
    if (grant && user.Role != UserRole.GM) { log "only a GM can be a Rules Auditor"; return; }

    user.IsRulesAuditor = grant;
    await db.SaveChangesAsync();
    log confirmation (nickname, email, new state);
    return;
}
```

Placed alongside the existing `--migrate` block, before `app.Run()` — same
one-shot-then-exit shape, no web server started.

### 3.2 Makefile

```makefile
grant-rules-auditor:
	docker compose exec api dotnet RuinaRPG.Api.dll --grant-rules-auditor $(EMAIL)

revoke-rules-auditor:
	docker compose exec api dotnet RuinaRPG.Api.dll --revoke-rules-auditor $(EMAIL)
```

Usage: `make grant-rules-auditor EMAIL=gm@example.com`. Documented in
`Requisitos - Técnico.md` next to the other `make` targets.

### 3.3 Authorization

New endpoints check `IsRulesAuditor` by loading the current user row directly
(same shape as existing `CurrentUserId()` + a DB lookup already used elsewhere
for GM-scoped checks) — not a new ASP.NET Core policy/claim, to guarantee the
grant takes effect on the auditor's very next request, no relogin. A non-auditor
(including a plain GM) gets 403 from these endpoints. `MeResponse` gains
`IsRulesAuditor` so the client can decide whether to show the new nav entries.

## 4. Características (Trait) — full CRUD

### 4.1 New `Trait` columns

- `IsCustomized` (bool, default false) — true once a row has been created or
  edited through the new admin endpoints. Protects the row from `TraitSeeder`.
- `IsDeleted` (bool, default false) — soft delete. Hidden from every existing
  read path (`TraitsController.List`, the Compêndio search, the 5.d picker's
  underlying query) via an added `!IsDeleted` filter. Kept as a row (not a hard
  delete) specifically so `TraitSeeder` still recognizes it as "already present"
  and never resurrects it — see §4.2. A soft-deleted Trait already referenced by
  a `CharacterTrait`/`NpcTrait` row is blocked from deletion up front (same
  spirit as the existing FK `DeleteBehavior.Restrict`, checked in the endpoint
  before flipping the flag, with a clear error) — deleting a trait that's in use
  would silently orphan sheets that already picked it.
- `UpdatedByUserId` (Guid?, nullable, FK to Users) and `UpdatedAt` (DateTime?,
  nullable) — light audit trail: who last created/edited/deleted this row.

Migration: `AddTraitAuditFields` (or similar), adding these 4 columns to
`Traits`.

### 4.2 `TraitSeeder.SeedAsync` changes

Current behavior (see file's own doc comment): matches existing rows by
`(Nome, Custo, Polaridade)`, inserts what's missing, and syncs only
`RequerEspecificacao` on a match. This spec changes it to:

- Build `existingByKey` from **all** rows regardless of `IsDeleted` (so a
  soft-deleted originally-seeded row still counts as "present" and is never
  re-inserted).
- Skip a matched row entirely (no `RequerEspecificacao` sync either) when
  `existingTrait.IsCustomized` is true — a manual edit wins forever, the seeder
  never touches that row again.
- Never insert against a row that's `IsDeleted` (already covered by the
  broadened `existingByKey`, called out here for clarity).

Everything else about the seeder (parsing `Características.md`, the insert path
for genuinely new upstream traits) is unchanged.

### 4.3 API — extend `TraitsController`

Currently GM/Jogador-readable, GET-only. Add, all `[Authorize]` + an inline
`IsRulesAuditor` check (403 otherwise):

- `POST api/traits` — `CreateTraitRequest(Nome, Descricao, Custo, Polaridade,
  RequerEspecificacao)`. Sets `IsCustomized = true`, `UpdatedByUserId`/`UpdatedAt`.
  Rejects a duplicate `(Nome, Custo, Polaridade)` against a non-deleted row
  (400) — same identity rule the seeder itself uses. Also rejects (400) a sign
  mismatch between `Custo` and `Polaridade` — every existing Positiva has
  `Custo >= 0` and every Negativa has `Custo <= 0` (Custo stored negative for
  Negativas, per "Requisitos - Ficha de Personagem" 5.d's budget math, which
  reads the two lists' signs to separate "spent" from "gained"); this endpoint
  enforces the same rule rather than silently accepting a trait that would
  break that math.
- `PUT api/traits/{id}` — same fields and the same sign validation, full
  replace. Sets `IsCustomized = true`, refreshes `UpdatedByUserId`/`UpdatedAt`.
  404 if the id doesn't exist or is already soft-deleted.
- `DELETE api/traits/{id}` — soft delete (`IsDeleted = true`,
  `UpdatedByUserId`/`UpdatedAt` refreshed). 409 (not 500) if any
  `CharacterTrait`/`NpcTrait` row still references it — checked explicitly
  before flipping the flag, with a message naming the conflict.
- `List` (existing `GET`) gains `.Where(t => !t.IsDeleted)`.

Every other reader of `Traits` (Compêndio search, character/NPC possessions'
add-trait flow) gets the same `!IsDeleted` filter — an explicit file-by-file
pass, not a global query filter (EF global filters interact awkwardly with the
soft-delete-but-still-matchable-by-seeder requirement above, so this spec keeps
the filter explicit per query instead).

## 5. Livro de Regras — display-only markdown override

### 5.1 New table `RulebookDocumentOverride`

| Coluna | Tipo |
|---|---|
| Id | PK |
| Slug | string, unique — one of `sistema-basico`, `graus-e-circulos`, `tabela-de-niveis` (never `caracteristicas`, see §2/§6) |
| MarkdownText | text |
| UpdatedByUserId | FK → Users |
| UpdatedAt | DateTime |

One row per overridden slug; absence of a row = use the embedded `.md` resource,
exactly as today.

### 5.2 API

New controller `RulebookDocumentsController` (or added to `RacialAbilitiesController`'s
sibling admin-controller pattern — this spec puts it in its own controller,
`api/rulebook-documents`, since it's a distinct concern from racial data),
`[Authorize]` + inline `IsRulesAuditor` check:

- `GET api/rulebook-documents` — the 3 slugs, each with its current effective
  `MarkdownText` (override or embedded default) and an `IsDefault` flag, same
  shape as `RacialAbilityEntryResponse`/`RacialTraitSlotsEntryResponse`.
- `PUT api/rulebook-documents/{slug}` — `UpdateRulebookDocumentRequest(MarkdownText)`.
  400 for an unknown slug (including `caracteristicas`, which this endpoint
  explicitly rejects — that document has no free-text override, see §6).
- `DELETE api/rulebook-documents/{slug}` — clears the override, reverting to
  the embedded default.

### 5.3 `RulebookRenderer` changes

`IRulebookRenderer.GetDocuments()` becomes `Task<IReadOnlyList<RulebookDocument>>`
(currently a sync `Lazy<>` with zero dependencies) and the class takes a
`RuinaRpgDbContext` (or a small dedicated reader) in its constructor.

- `BuildSistemaBasico`/`BuildGrausECirculos`/`BuildTabelaDeNiveis`: before
  reading the embedded resource, check `RulebookDocumentOverride` for that
  slug; use its `MarkdownText` if present, the embedded resource text
  otherwise. Same `SplitIntoSections` pipeline either way — nothing about
  rendering/section-splitting changes, only where the raw markdown string
  comes from.
- No more `Lazy<>` caching across requests (an override can change between
  requests and must be reflected immediately) — rendering the 3 documents from
  whatever markdown is current on every call is cheap enough (this is already
  what happens once per process lifetime today; it's a page nobody hits at high
  frequency).
- `RulebookController.Get()` becomes `async`, awaiting `renderer.GetDocuments()`.

## 6. Características tab — now DB-driven, not markdown-driven

`RulebookRenderer.BuildCaracteristicas()` is replaced with a query against the
live `Traits` table (`!IsDeleted`, ordered by Polaridade then Nome), building
one `RulebookSection` per trait (`Titulo` = Nome, `Html` = a small rendered
block of Descricao + Custo, `Grupo` = "Positivas"/"Negativas") — reproducing
the shape the client's existing filterable-list UI
(`LivroDeRegras.razor`'s `document.Slug == "caracteristicas"` branch) already
expects, so **no client change is needed on that page** beyond it now reading
live data. `IntroHtml` stays `null` (matches today).

## 7. Client

### 7.1 Nav / access

`NavMenu.razor`: a new "Auditoria de Regras" entry (or two entries) inside the
existing GM `<AuthorizeView Roles="GM">` block, shown only when
`MeResponse.IsRulesAuditor` is true (fetched once, same place `_isGmCaller` is
already resolved on pages that need it).

### 7.2 New page: Livro de Regras (edição)

Route e.g. `/auditoria/livro-de-regras`. Three sections (one per slug), each a
`MudTextField Lines="20"` (or similar) bound to that document's `MarkdownText`,
autosaved on blur (existing `AutoSaveCoordinator` pattern), a "Restaurar padrão"
button per section, and a persistent `MudAlert` at the top stating plainly:
editing here only changes what's displayed on the Livro de Regras page, not any
calculation used by character/NPC/creature sheets.

### 7.3 New page: Características (edição)

Route e.g. `/auditoria/caracteristicas`. A table (Positivas/Negativas, same
grouping as the sheet's own 5.d) with inline-editable Nome/Custo/Polaridade/
Descricao/RequerEspecificacao per row, a delete button per row (surfacing the
409-conflict message if the trait is in use), and an "Adicionar característica"
form/row.

## 8. Docs

- New `Docs/Requisitos/Requisitos - Auditoria de Regras.md` — the requirements
  doc for this whole subsystem (grant model, the two pages, the display-only
  guarantee, the Características unification), following the same preamble
  convention as `Requisitos - Habilidades Raciais.md` ("não corresponde a
  nenhum bloco do canvas ainda").
- `Requisitos - Técnico.md` — document `IsRulesAuditor`, the two new `make`
  targets, and that the grant is DB-checked per-request (no relogin needed).
- `Requisitos - Compêndio de Regras.md` / `Requisitos - Livro de Regras.md` —
  a short note that the Características tab is now sourced from the live
  catalog, and that GM-facing editing of both exists (link to the new doc).
- `Requisitos - Modelo de Dados.md` — new `RulebookDocumentOverrides` table,
  `Traits`' 4 new columns, `ApplicationUser.IsRulesAuditor`.

## 9. Testing strategy

- Domain: none of this introduces new pure-domain logic beyond the seeder's
  changed matching rule, which is exercised via `TraitSeederTests`
  (unit, in-memory, no DB) — add cases for "customized row survives a reseed
  unchanged" and "soft-deleted row is never reinserted."
- Integration: CLI grant/revoke (a small test harness invoking the same code
  path the `--grant-rules-auditor` branch runs, or a thin service extracted so
  it's testable without spinning up the whole `Program.cs` args pipeline — the
  plan should decide which), Trait CRUD (create/update/soft-delete, 403 for a
  non-auditor GM, 409 for deleting an in-use trait, reseed-survives-edit via a
  real `TraitSeeder.SeedAsync` call after an edit), RulebookDocument override
  CRUD (get/put/delete, 400 for `caracteristicas`), and
  `GET /api/rulebook` reflecting an override immediately after it's saved (no
  restart) plus the Características tab reflecting a live Trait edit.
- Client: none planned beyond what's already established for this codebase's
  large sheet/admin pages (no bUnit coverage precedent for pages this shape;
  consistent with e.g. `HabilidadesRaciais.razor` having none either).

## 10. Out of scope (explicitly, per §2)

- Editing Tabela de Vocação/Classes/Arquetipos, Tabela de Circulo e Grau por
  EAP, XP/EAP-per-level numbers, or Graus & Círculos' Efeitos catalog. These
  keep parsing the embedded resources unchanged; `IRulesDataProvider`'s
  interface and every calculator that consumes it are untouched by this task.
- Multi-auditor workflows, an edit-history/diff view, or notifying other users
  when a document changes — a flat "last edited by/at" per row/document is all
  this spec provides.
