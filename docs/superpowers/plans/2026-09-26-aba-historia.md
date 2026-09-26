# Aba "História" Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "História" tab to the Ficha de Personagem and Ficha de NPC holding Estrela, Histórico and a new sanitized rich-text backstory field edited with Jodit.

**Architecture:** A nullable `Historia` text column on `CharacterSheets`/`NpcSheets`, read through the existing sheet responses and written through dedicated `PUT …/historia` endpoints that run the HTML through an allowlist sanitizer (`HtmlSanitizer`). On the client, a vendored, lazily loaded Jodit is wrapped by a JS module + `RichTextEditor` Blazor component, and a `HistoriaEditor` component adds autosave; both sheets place Estrela, Histórico and `HistoriaEditor` in the new tab.

**Tech Stack:** .NET 8, ASP.NET Core, EF Core/Npgsql, Blazor WebAssembly, MudBlazor 9.9.0, HtmlSanitizer 9.2.1039 (NuGet, MIT), Jodit 4.15.14 (npm tarball, MIT), xUnit, FluentAssertions, bUnit 2.9.0, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-09-25-aba-historia-design.md`

## Global Constraints

- `dotnet build` must finish with **0 warnings, 0 errors**.
- TDD is mandatory: failing test first, see it fail for the right reason, then the minimum code.
- `dotnet test` needs Docker running. Before a full integration run check `df -h /` — if under ~2 GB free, stop and report (a full disk makes Postgres fail with `57P03`, not a code bug). The integration suite has a known ~2% flake: `HttpClient.Timeout of 100 seconds elapsing` in random classes — re-run just the failing classes; they must pass on re-run. Never "fix" that flake as part of this plan.
- Max História length: **200,000 characters** of raw input; 400 message exactly `A História pode ter no máximo 200.000 caracteres.`
- Sanitizer allowlist, verbatim from the spec — tags: `p div br hr h1 h2 h3 h4 h5 h6 strong b em i u s strike sub sup span blockquote ul ol li a table thead tbody tr th td`; attributes: `style href colspan rowspan`; CSS: `color background-color font-family font-size text-align text-decoration padding-left margin-left`; schemes: `http https mailto`; every `a` gets `target="_blank" rel="noopener noreferrer"`.
- Jodit toolbar disables image, video, file, source/HTML view, print, about.
- User-facing text is Brazilian Portuguese. Code comments follow the surrounding file's language.
- The Changelog (`ChangelogDialog.razor`, `AppVersionInfo`) is **not** touched.
- Commit after each task with a message ending in `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

- A player typing in the editor while another field's autosave reloads the sheet — the typed text must not be replaced by the older server copy (Task 4 pins: a re-render with a new `Html` does not call JS again; Task 5 pins: `_form.Historia` is only assigned on the sheet's first load).
- Leaving the tab or the page less than 1s after typing — the pending change must still be saved (Task 4 pins: dispose calls `ruinaRichText.destroy`, whose JS flushes the pending debounce; the browser check confirms it).
- Pasting from Word/Google Docs (`class="MsoNormal"`, `mso-*` styles, `<o:p>`) — keeps the text and allowed formatting, drops the junk (Task 1 test).
- Accents and emoji ("Coração 🌿", "ação") must round-trip unchanged, not become entities or `?` (Task 1 test).
- A story of exactly 200,000 characters must be accepted; 200,001 rejected (Task 2 test).

---

### Task 1: `HistoriaSanitizer`

**Files:**
- Modify: `src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj` (add package)
- Create: `src/RuinaRPG.Infrastructure/CharacterSheets/HistoriaSanitizer.cs`
- Test: `tests/RuinaRPG.Tests.Unit/CharacterSheets/HistoriaSanitizerTests.cs`

**Interfaces:**
- Produces: `RuinaRPG.Infrastructure.CharacterSheets.HistoriaSanitizer` (static) with `const int MaxLength = 200_000`, `const string MaxLengthMessage = "A História pode ter no máximo 200.000 caracteres."`, `static string? Sanitize(string? html)` — returns sanitized HTML, or `null` when the input is null/whitespace or has no visible content after sanitizing.

- [ ] **Step 1: Add the package**

```bash
dotnet add src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj package HtmlSanitizer --version 9.2.1039
```

- [ ] **Step 2: Write the failing tests**

`tests/RuinaRPG.Tests.Unit/CharacterSheets/HistoriaSanitizerTests.cs`:

```csharp
using FluentAssertions;
using RuinaRPG.Infrastructure.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class HistoriaSanitizerTests
{
    [Fact]
    public void Keeps_headings_paragraphs_and_inline_formatting()
    {
        var result = HistoriaSanitizer.Sanitize("<h2>Origem</h2><p><strong>Nasceu</strong> em <em>Alkeria</em>, <u>filho</u> de <s>ninguém</s> H<sub>2</sub>O x<sup>2</sup></p>");

        result.Should().Contain("<h2>Origem</h2>")
            .And.Contain("<strong>Nasceu</strong>")
            .And.Contain("<em>Alkeria</em>")
            .And.Contain("<u>filho</u>")
            .And.Contain("<s>ninguém</s>")
            .And.Contain("<sub>2</sub>")
            .And.Contain("<sup>2</sup>");
    }

    [Fact]
    public void Keeps_allowed_css_properties_and_drops_the_rest()
    {
        var result = HistoriaSanitizer.Sanitize("<p style=\"color: red; background-color: yellow; text-align: center; position: fixed; font-size: 20px\">Texto</p>");

        result.Should().Contain("color: red")
            .And.Contain("background-color: yellow")
            .And.Contain("text-align: center")
            .And.Contain("font-size: 20px")
            .And.NotContain("position");
    }

    [Fact]
    public void Keeps_tables_with_colspan_and_rowspan()
    {
        var result = HistoriaSanitizer.Sanitize("<table><thead><tr><th colspan=\"2\">Aliados</th></tr></thead><tbody><tr><td rowspan=\"2\">Lira</td><td>Irmã</td></tr></tbody></table>");

        result.Should().Contain("<table>").And.Contain("colspan=\"2\"").And.Contain("rowspan=\"2\"").And.Contain("<td");
    }

    [Fact]
    public void Keeps_http_and_mailto_links_and_forces_target_blank_with_noopener()
    {
        var result = HistoriaSanitizer.Sanitize("<p><a href=\"https://exemplo.com\" target=\"_self\">site</a> <a href=\"mailto:mestre@exemplo.com\">email</a></p>");

        result.Should().Contain("href=\"https://exemplo.com\"")
            .And.Contain("href=\"mailto:mestre@exemplo.com\"")
            .And.Contain("target=\"_blank\"")
            .And.Contain("rel=\"noopener noreferrer\"")
            .And.NotContain("_self");
    }

    [Theory]
    [InlineData("<p>oi</p><script>alert(1)</script>", "script")]
    [InlineData("<p onclick=\"alert(1)\">oi</p>", "onclick")]
    [InlineData("<p><a href=\"javascript:alert(1)\">oi</a></p>", "javascript")]
    [InlineData("<p>oi</p><img src=\"x\" onerror=\"alert(1)\">", "img")]
    [InlineData("<p>oi</p><iframe src=\"https://mal.com\"></iframe>", "iframe")]
    [InlineData("<style>p{color:red}</style><p>oi</p>", "style>")]
    [InlineData("<p class=\"perigo\" id=\"x\">oi</p>", "class")]
    [InlineData("<p><a href=\"data:text/html;base64,PHNjcmlwdD4=\">oi</a></p>", "data:")]
    public void Strips_dangerous_or_disallowed_markup_but_keeps_the_text(string input, string forbidden)
    {
        var result = HistoriaSanitizer.Sanitize(input);

        result.Should().Contain("oi").And.NotContain(forbidden);
    }

    [Fact]
    public void Cleans_markup_pasted_from_word()
    {
        var result = HistoriaSanitizer.Sanitize("<p class=\"MsoNormal\" style=\"mso-line-height-alt: 12pt; color: blue\"><b>Capítulo 1</b><o:p></o:p></p>");

        result.Should().Contain("<b>Capítulo 1</b>")
            .And.Contain("color: blue")
            .And.NotContain("Mso")
            .And.NotContain("mso-")
            .And.NotContain("o:p");
    }

    [Fact]
    public void Preserves_accents_and_emoji()
    {
        var result = HistoriaSanitizer.Sanitize("<p>Coração 🌿 e ação</p>");

        result.Should().Contain("Coração 🌿 e ação");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p><br></p>")]
    [InlineData("<p>&nbsp;</p>")]
    [InlineData("<script>alert(1)</script>")]
    public void Returns_null_when_there_is_no_visible_content(string? input)
    {
        HistoriaSanitizer.Sanitize(input).Should().BeNull();
    }

    [Fact]
    public void A_lone_horizontal_rule_counts_as_content()
    {
        HistoriaSanitizer.Sanitize("<hr>").Should().NotBeNull();
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~HistoriaSanitizerTests`
Expected: build error `The name 'HistoriaSanitizer' does not exist in the current context`.

- [ ] **Step 4: Implement**

`src/RuinaRPG.Infrastructure/CharacterSheets/HistoriaSanitizer.cs`:

```csharp
using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Ganss.Xss;

namespace RuinaRPG.Infrastructure.CharacterSheets;

/// <summary>
/// Ficha de Personagem/NPC, aba História: the backstory is HTML written by a player in a rich-text
/// editor and later opened by the GM, so it's run through an allowlist before it's ever persisted —
/// only the tags/attributes/CSS the Jodit toolbar can produce survive. Anything else (script, event
/// handlers, iframes, images, javascript:/data: links, class/id) is dropped, whatever the client sent.
/// </summary>
public static partial class HistoriaSanitizer
{
    public const int MaxLength = 200_000;
    public const string MaxLengthMessage = "A História pode ter no máximo 200.000 caracteres.";

    private static readonly HtmlSanitizer Sanitizer = Build();

    public static string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var clean = Sanitizer.Sanitize(html);
        return HasVisibleContent(clean) ? clean : null;
    }

    private static HtmlSanitizer Build()
    {
        var options = new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "div", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6", "strong", "b", "em", "i", "u", "s", "strike",
                "sub", "sup", "span", "blockquote", "ul", "ol", "li", "a", "table", "thead", "tbody", "tr", "th", "td",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "style", "href", "colspan", "rowspan" },
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "color", "background-color", "font-family", "font-size", "text-align", "text-decoration", "padding-left", "margin-left",
            },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http", "https", "mailto" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href" },
        };

        var sanitizer = new HtmlSanitizer(options);
        // target/rel aren't accepted from input (not in AllowedAttributes) — set here on every surviving link.
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is IElement { NodeName: "A" } link)
            {
                link.SetAttribute("target", "_blank");
                link.SetAttribute("rel", "noopener noreferrer");
            }
        };
        return sanitizer;
    }

    // "<p><br></p>" is what an emptied Jodit editor holds — store it as NULL, not as a blank story.
    private static bool HasVisibleContent(string html) =>
        html.Contains("<hr", StringComparison.OrdinalIgnoreCase)
        || html.Contains("<table", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(TagRegex().Replace(html, "")).Replace(' ', ' '));

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}
```

If `Preserves_accents_and_emoji` fails because the output contains entities (e.g. `&#x1F33F;`), set the output formatter so non-ASCII stays literal: `sanitizer.OutputFormatter = AngleSharp.Html.HtmlMarkupFormatter.Instance;` in `Build()` (that formatter only escapes `&`, `<`, `>`, `"`), then re-run.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/RuinaRPG.Tests.Unit --filter FullyQualifiedName~HistoriaSanitizerTests`
Expected: all pass. Then `dotnet build` → 0 warnings, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/RuinaRPG.Infrastructure/RuinaRPG.Infrastructure.csproj src/RuinaRPG.Infrastructure/CharacterSheets/HistoriaSanitizer.cs tests/RuinaRPG.Tests.Unit/CharacterSheets/HistoriaSanitizerTests.cs
git commit -m "feat: adiciona HistoriaSanitizer (allowlist de HTML da aba História)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `Historia` column + Personagem endpoint

**Files:**
- Create: `src/RuinaRPG.Contracts/CharacterSheets/UpdateHistoriaRequest.cs`
- Modify: `src/RuinaRPG.Infrastructure/CharacterSheets/CharacterSheet.cs` (property after `EquipmentKitId`, line ~21)
- Modify: `src/RuinaRPG.Infrastructure/NpcSheets/NpcSheet.cs` (property after `EquipmentKitId`, line ~21)
- Create: migration `AddSheetHistoria` under `src/RuinaRPG.Infrastructure/Persistence/Migrations/` (generated)
- Modify: `src/RuinaRPG.Contracts/CharacterSheets/CharacterSheetResponse.cs` (append `string? Historia`)
- Modify: `src/RuinaRPG.Contracts/NpcSheets/NpcSheetResponse.cs` (append `string? Historia`)
- Modify: `src/RuinaRPG.Api/Controllers/CharacterSheetsController.cs` (new action after `Update`, line ~182; response constructor at line ~567)
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs` (response constructor at line ~576 only — its endpoint is Task 3)
- Modify: `Docs/Requisitos/Requisitos - Modelo de Dados.md` (§6.1 table)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs`

**Interfaces:**
- Consumes: `HistoriaSanitizer.Sanitize`, `HistoriaSanitizer.MaxLength`, `HistoriaSanitizer.MaxLengthMessage` (Task 1).
- Produces: `RuinaRPG.Contracts.CharacterSheets.UpdateHistoriaRequest(string? Historia)`; `CharacterSheet.Historia`/`NpcSheet.Historia` (`string?`); `CharacterSheetResponse.Historia`/`NpcSheetResponse.Historia` (`string?`, last positional member); `PUT api/character-sheets/{id}/historia` → 204 / 400 / 403 / 404.

- [ ] **Step 1: Write the failing integration tests**

Append to `CharacterSheetsControllerTests` (it already has `RegisterGmAndGetTokenAsync`, `RegisterJogadorLinkedToAsync`, `CreateCampaignAsync`, `CreateSheetForMemberAsync`, `AuthedRequest`, `ValidUpdate()`; add `using RuinaRPG.Contracts.CharacterSheets;` if missing). Every test uses unique nicknames/emails — the Postgres container is shared.

```csharp
    private async Task<(string GmToken, string PlayerToken, string SheetId)> CreateOwnedSheetAsync(string suffix)
    {
        var gmToken = await RegisterGmAndGetTokenAsync($"HistGm{suffix}", $"histgm{suffix}@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, $"HistPlayer{suffix}", $"histplayer{suffix}@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, $"Campanha História {suffix}");
        await _client.SendAsync(AuthedRequest(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gmToken, new AddCampaignMemberRequest(playerId)));
        var sheetId = await CreateSheetForMemberAsync(gmToken, campaignId, playerId);
        return (gmToken, playerToken, sheetId);
    }

    private async Task<CharacterSheetResponse> GetSheetAsync(string token, string sheetId) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/character-sheets/{sheetId}", token)))
            .Content.ReadFromJsonAsync<CharacterSheetResponse>())!;

    [Fact]
    public async Task UpdateHistoria_by_the_owner_persists_sanitized_html_and_Get_returns_it()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("C1");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken,
            new UpdateHistoriaRequest("<h2>Origem</h2><p style=\"color: red\">Nasceu em Alkeria</p><script>alert(1)</script>")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var sheet = await GetSheetAsync(playerToken, sheetId);
        sheet.Historia.Should().Contain("<h2>Origem</h2>").And.Contain("Nasceu em Alkeria").And.Contain("color: red").And.NotContain("script");
    }

    [Fact]
    public async Task UpdateHistoria_by_the_campaign_gm_is_allowed()
    {
        var (gmToken, _, sheetId) = await CreateOwnedSheetAsync("C2");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", gmToken,
            new UpdateHistoriaRequest("<p>Nota do mestre</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateHistoria_by_an_unrelated_jogador_returns_403()
    {
        var (gmToken, _, sheetId) = await CreateOwnedSheetAsync("C3");
        var (_, otherToken) = await RegisterJogadorLinkedToAsync(gmToken, "HistPlayerC3b", "histplayerc3b@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", otherToken,
            new UpdateHistoriaRequest("<p>invasão</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateHistoria_on_a_nonexistent_sheet_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("HistGmC4", "histgmc4@teste.com");

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{Guid.NewGuid()}/historia", gmToken,
            new UpdateHistoriaRequest("<p>x</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateHistoria_accepts_exactly_200000_characters_and_rejects_200001()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("C5");
        var noLimite = "<p>" + new string('a', 200_000 - 7) + "</p>";

        var ok = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest(noLimite)));
        var tooLong = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest(noLimite + "a")));

        noLimite.Length.Should().Be(200_000);
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
        tooLong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooLong.Content.ReadAsStringAsync()).Should().Contain("A História pode ter no máximo 200.000 caracteres.");
    }

    [Fact]
    public async Task UpdateHistoria_with_only_empty_markup_stores_null()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("C6");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest("<p>algo</p>")));

        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest("<p><br></p>")));

        (await GetSheetAsync(playerToken, sheetId)).Historia.Should().BeNull();
    }

    [Fact]
    public async Task The_general_sheet_Update_leaves_Historia_untouched()
    {
        var (_, playerToken, sheetId) = await CreateOwnedSheetAsync("C7");
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}/historia", playerToken, new UpdateHistoriaRequest("<p>Minha história</p>")));

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/character-sheets/{sheetId}", playerToken, ValidUpdate()));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetSheetAsync(playerToken, sheetId)).Historia.Should().Contain("Minha história");
    }
```

If `ValidUpdate()` is rejected for a player-owned sheet in this class (check how `Update_by_the_owner_returns_204_and_persists_every_field` calls it and copy its token/setup), adapt only the setup, not the assertion.

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~CharacterSheetsControllerTests.UpdateHistoria|FullyQualifiedName~CharacterSheetsControllerTests.The_general_sheet_Update_leaves_Historia_untouched"`
Expected: build errors — `UpdateHistoriaRequest` not found and `CharacterSheetResponse` has no `Historia`.

- [ ] **Step 3: Contract, entities, responses**

`src/RuinaRPG.Contracts/CharacterSheets/UpdateHistoriaRequest.cs`:

```csharp
namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>Aba História (Personagem e NPC): the rich-text backstory as HTML; the API sanitizes it.</summary>
public record UpdateHistoriaRequest(string? Historia);
```

In both `CharacterSheet.cs` and `NpcSheet.cs`, after `public Guid? EquipmentKitId { get; set; }`:

```csharp
    /// <summary>Aba História: sanitized HTML (see HistoriaSanitizer). NULL = empty editor.</summary>
    public string? Historia { get; set; }
```

`CharacterSheetResponse.cs`: change the last line `string? EquipmentKitId);` to

```csharp
    string? EquipmentKitId,
    string? Historia);
```

`NpcSheetResponse.cs`: same change.

In `CharacterSheetsController.cs` (~line 577) change the end of the `new CharacterSheetResponse(` call from
`s.Estrela?.ToString(), s.SinaAtual, s.HistoricoId?.ToString(), s.EquipmentKitId?.ToString());` to
`s.Estrela?.ToString(), s.SinaAtual, s.HistoricoId?.ToString(), s.EquipmentKitId?.ToString(), s.Historia);`

In `NpcSheetsController.cs` (~line 587) make the identical change to the end of `new NpcSheetResponse(`.

Run `grep -rn "new CharacterSheetResponse(\|new NpcSheetResponse(" src tests` — if anything else constructs them, append `null`/the value there too.

- [ ] **Step 4: Endpoint**

In `CharacterSheetsController.cs`, directly after the `Update` action (the one under `[HttpPut("api/character-sheets/{id}")]`), add (add `using RuinaRPG.Infrastructure.CharacterSheets;` if missing):

```csharp
    /// <summary>
    /// Aba História — kept out of UpdateCharacterSheetRequest on purpose, so the general autosave
    /// (fired by any other field) can never overwrite the backstory with a stale copy, and vice versa.
    /// Same authorization as Update.
    /// </summary>
    [HttpPut("api/character-sheets/{id}/historia")]
    public async Task<IActionResult> UpdateHistoria(Guid id, UpdateHistoriaRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        if (request.Historia is { Length: > HistoriaSanitizer.MaxLength })
            return BadRequest(HistoriaSanitizer.MaxLengthMessage);

        sheet.Historia = HistoriaSanitizer.Sanitize(request.Historia);
        await db.SaveChangesAsync();
        return NoContent();
    }
```

- [ ] **Step 5: Migration**

```bash
dotnet ef migrations add AddSheetHistoria --project src/RuinaRPG.Infrastructure --startup-project src/RuinaRPG.Api --output-dir Persistence/Migrations
```

Open the generated `*_AddSheetHistoria.cs`: `Up` must only `AddColumn<string>(name: "Historia", table: "CharacterSheets", type: "text", nullable: true)` and the same for `"NpcSheets"`; `Down` drops both. Nothing else.

- [ ] **Step 6: Run the tests to verify they pass**

Run the Step 2 command. Expected: all pass. Then `dotnet build` → 0 warnings, 0 errors.

- [ ] **Step 7: Docs**

In `Docs/Requisitos/Requisitos - Modelo de Dados.md`, §6.1 CharacterSheets table, add after the `HistoricoId` row:

```markdown
| Historia | text, nullable | História — HTML do texto rico, sanitizado no servidor por uma allowlist antes de gravar; NULL = vazio (ver "[[Requisitos - Ficha de Personagem]]", aba História) |
```

(NpcSheets, §6.2, inherits it — §6.2 lists only differences, so nothing to add there.)

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Contracts src/RuinaRPG.Infrastructure src/RuinaRPG.Api tests/RuinaRPG.Tests.Integration/Controllers/CharacterSheetsControllerTests.cs "Docs/Requisitos/Requisitos - Modelo de Dados.md"
git commit -m "feat: coluna Historia nas fichas e PUT da História do Personagem

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: NPC endpoint + deep copy on grant

**Files:**
- Modify: `src/RuinaRPG.Api/Controllers/NpcSheetsController.cs` (new action after `Update`, line ~59)
- Modify: `src/RuinaRPG.Api/Controllers/CampaignGrantsController.cs` (`DeepCopyNpcAsync` initializer, line ~168-177)
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs`
- Test: `tests/RuinaRPG.Tests.Integration/Controllers/CampaignGrantsControllerTests.cs`

**Interfaces:**
- Consumes: `UpdateHistoriaRequest`, `NpcSheet.Historia`, `NpcSheetResponse.Historia` (Task 2); `HistoriaSanitizer` (Task 1).
- Produces: `PUT api/npc-sheets/{id}/historia` → 204 / 400 / 404.

- [ ] **Step 1: Write the failing tests**

Append to `NpcSheetsControllerTests` (has `RegisterGmAndGetTokenAsync`, `CreateSheetAsync(gmToken)`, `RegisterJogadorLinkedToAsync`, `CreateCampaignAsync`, `AddMemberAsync`, `GrantBlankNpcAsync`, `AuthedRequest`, `ValidUpdate()`; add `using RuinaRPG.Contracts.CharacterSheets;`):

```csharp
    private async Task<NpcSheetResponse> GetNpcAsync(string token, string sheetId) =>
        (await (await _client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{sheetId}", token)))
            .Content.ReadFromJsonAsync<NpcSheetResponse>())!;

    [Fact]
    public async Task UpdateHistoria_by_the_gm_persists_sanitized_html()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm1", "npchistgm1@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", gmToken,
            new UpdateHistoriaRequest("<p>Guardiã do portal</p><img src=x onerror=alert(1)>")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetNpcAsync(gmToken, sheetId)).Historia.Should().Contain("Guardiã do portal").And.NotContain("img");
    }

    [Fact]
    public async Task UpdateHistoria_by_a_different_gm_returns_404()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm2", "npchistgm2@teste.com");
        var otherGmToken = await RegisterGmAndGetTokenAsync("NpcHistGm2b", "npchistgm2b@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", otherGmToken,
            new UpdateHistoriaRequest("<p>x</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateHistoria_by_the_player_the_npc_was_granted_to_is_allowed()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm3", "npchistgm3@teste.com");
        var (playerId, playerToken) = await RegisterJogadorLinkedToAsync(gmToken, "NpcHistPlayer3", "npchistplayer3@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha História NPC");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var sheetId = await GrantBlankNpcAsync(gmToken, campaignId, playerId);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", playerToken,
            new UpdateHistoriaRequest("<p>Meu companheiro</p>")));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpdateHistoria_longer_than_200000_characters_returns_400()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm4", "npchistgm4@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);

        var response = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", gmToken,
            new UpdateHistoriaRequest(new string('a', 200_001))));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_general_npc_Update_leaves_Historia_untouched()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("NpcHistGm5", "npchistgm5@teste.com");
        var sheetId = await CreateSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}/historia", gmToken, new UpdateHistoriaRequest("<p>Passado sombrio</p>")));

        var update = await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sheetId}", gmToken, ValidUpdate()));

        update.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetNpcAsync(gmToken, sheetId)).Historia.Should().Contain("Passado sombrio");
    }
```

Append to `CampaignGrantsControllerTests` (has `RegisterGmAndGetTokenAsync`, `RegisterJogadorLinkedToAsync`, `CreateCampaignAsync`, `AddMemberAsync`, `CreateNpcSheetAsync`, `GetNpcSheetAsync`, `GrantAsync`, `AuthedRequest`; add `using RuinaRPG.Contracts.CharacterSheets;`):

```csharp
    [Fact]
    public async Task Grant_from_an_existing_Npc_copies_the_Historia()
    {
        var gmToken = await RegisterGmAndGetTokenAsync("GrantGmHist", "granthist@teste.com");
        var (playerId, _) = await RegisterJogadorLinkedToAsync(gmToken, "GrantPlayerHist", "grantplayerhist@teste.com");
        var campaignId = await CreateCampaignAsync(gmToken, "Campanha Grant História");
        await AddMemberAsync(gmToken, campaignId, playerId);
        var sourceId = await CreateNpcSheetAsync(gmToken);
        await _client.SendAsync(AuthedRequest(HttpMethod.Put, $"/api/npc-sheets/{sourceId}/historia", gmToken, new UpdateHistoriaRequest("<p>Criado nas ruínas</p>")));

        var response = await GrantAsync(gmToken, campaignId, new GrantSheetRequest(playerId, "Npc", sourceId));
        var body = await response.Content.ReadFromJsonAsync<GrantSheetResponse>();

        (await GetNpcSheetAsync(gmToken, body!.SheetId)).Historia.Should().Contain("Criado nas ruínas");
    }
```

(If `AddMemberAsync`/`AuthedRequest` have a different name in `CampaignGrantsControllerTests`, use the class's own helper — check lines 36-130.)

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Integration --filter "FullyQualifiedName~NpcSheetsControllerTests.UpdateHistoria|FullyQualifiedName~NpcSheetsControllerTests.The_general_npc_Update_leaves_Historia_untouched|FullyQualifiedName~CampaignGrantsControllerTests.Grant_from_an_existing_Npc_copies_the_Historia"`
Expected: FAIL — the endpoint returns 404/405 and the copy has `Historia == null`.

- [ ] **Step 3: Implement**

In `NpcSheetsController.cs`, directly after the `Update` action (add `using RuinaRPG.Contracts.CharacterSheets;` and `using RuinaRPG.Infrastructure.CharacterSheets;` if missing):

```csharp
    /// <summary>
    /// Aba História — separate from UpdateNpcSheetRequest for the same reason as
    /// CharacterSheetsController.UpdateHistoria. Same authorization as Update (NotFound for strangers).
    /// </summary>
    [HttpPut("{id}/historia")]
    public async Task<IActionResult> UpdateHistoria(Guid id, UpdateHistoriaRequest request)
    {
        var sheet = await db.NpcSheets.FindAsync(id);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        if (request.Historia is { Length: > HistoriaSanitizer.MaxLength })
            return BadRequest(HistoriaSanitizer.MaxLengthMessage);

        sheet.Historia = HistoriaSanitizer.Sanitize(request.Historia);
        await db.SaveChangesAsync();
        return NoContent();
    }
```

In `CampaignGrantsController.DeepCopyNpcAsync`, change the initializer's last line
`EstresseAtual = source.EstresseAtual, Cobertura = source.Cobertura, Ciclos = source.Ciclos`
to
`EstresseAtual = source.EstresseAtual, Cobertura = source.Cobertura, Ciclos = source.Ciclos, Historia = source.Historia`

- [ ] **Step 4: Run the tests to verify they pass**

Run the Step 2 command → all pass. `dotnet build` → 0 warnings, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/RuinaRPG.Api/Controllers/NpcSheetsController.cs src/RuinaRPG.Api/Controllers/CampaignGrantsController.cs tests/RuinaRPG.Tests.Integration/Controllers/NpcSheetsControllerTests.cs tests/RuinaRPG.Tests.Integration/Controllers/CampaignGrantsControllerTests.cs
git commit -m "feat: PUT da História do NPC e cópia da História ao conceder NPC

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Vendored Jodit + `ruinaRichText` JS + `RichTextEditor` component

**Files:**
- Create: `src/RuinaRPG.Client/wwwroot/lib/jodit/jodit.min.js`, `jodit.min.css`, `LICENSE.txt` (vendored)
- Create: `src/RuinaRPG.Client/wwwroot/js/richTextEditor.js`
- Modify: `src/RuinaRPG.Client/wwwroot/index.html` (script tag next to `js/clipboard.js`, line ~24)
- Create: `src/RuinaRPG.Client/Shared/Fields/RichTextEditor.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Fields/RichTextEditorTests.cs`

**Interfaces:**
- Produces:
  - JS `window.ruinaRichText.create(element, dotNetRef, initialHtml)` (returns a Promise) and `window.ruinaRichText.destroy(element)`; calls back `dotNetRef.invokeMethodAsync('OnEditorChanged', html)`.
  - Blazor `RuinaRPG.Client.Shared.Fields.RichTextEditor` with `[Parameter] string? Html`, `[Parameter] EventCallback<string?> HtmlChanged`, `[Parameter] EventCallback<string?> OnCommit`, `[JSInvokable] public Task OnEditorChanged(string html)`; implements `IAsyncDisposable`.

- [ ] **Step 1: Vendor Jodit**

```bash
S=/tmp/jodit-vendor && rm -rf $S && mkdir -p $S
curl -sSf -o $S/jodit.tgz https://registry.npmjs.org/jodit/-/jodit-4.15.14.tgz
tar xzf $S/jodit.tgz -C $S
mkdir -p src/RuinaRPG.Client/wwwroot/lib/jodit
cp $S/package/es2021/jodit.min.js $S/package/es2021/jodit.min.css $S/package/LICENSE.txt src/RuinaRPG.Client/wwwroot/lib/jodit/
grep -c "Negrito" src/RuinaRPG.Client/wwwroot/lib/jodit/jodit.min.js   # must be >= 1: the pt_br locale is bundled
git check-ignore -v src/RuinaRPG.Client/wwwroot/lib/jodit/jodit.min.js || echo "not ignored"   # must print "not ignored"
```

- [ ] **Step 2: Write the failing bUnit tests**

`tests/RuinaRPG.Tests.Client/Shared/Fields/RichTextEditorTests.cs`:

```csharp
using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class RichTextEditorTests : MudBunitContext
{
    [Fact]
    public void First_render_creates_the_js_editor_with_the_initial_html()
    {
        Render<RichTextEditor>(p => p.Add(x => x.Html, "<p>Era uma vez</p>"));

        var create = JSInterop.VerifyInvoke("ruinaRichText.create");
        create.Arguments[2].Should().Be("<p>Era uma vez</p>");
    }

    [Fact]
    public void A_null_Html_creates_an_empty_editor()
    {
        Render<RichTextEditor>(p => p.Add(x => x.Html, (string?)null));

        JSInterop.VerifyInvoke("ruinaRichText.create").Arguments[2].Should().Be("");
    }

    [Fact]
    public async Task A_change_reported_by_js_raises_HtmlChanged_then_OnCommit()
    {
        var calls = new List<string>();
        var cut = Render<RichTextEditor>(p => p
            .Add(x => x.Html, "<p>a</p>")
            .Add(x => x.HtmlChanged, (string? v) => calls.Add($"changed:{v}"))
            .Add(x => x.OnCommit, (string? v) => calls.Add($"commit:{v}")));

        await cut.InvokeAsync(() => cut.Instance.OnEditorChanged("<p>ab</p>"));

        calls.Should().Equal("changed:<p>ab</p>", "commit:<p>ab</p>");
    }

    [Fact]
    public void Re_rendering_with_a_different_Html_does_not_touch_the_js_editor()
    {
        // Review Focus: a sheet reload (autosave of another field) must never push older text into
        // an editor the player is typing in — the editor only takes content when it is created.
        var cut = Render<RichTextEditor>(p => p.Add(x => x.Html, "<p>a</p>"));

        cut.Render(p => p.Add(x => x.Html, "<p>versão antiga do servidor</p>"));

        JSInterop.Invocations.Should().ContainSingle(i => i.Identifier.StartsWith("ruinaRichText."));
    }

    [Fact]
    public async Task Disposing_destroys_the_js_editor()
    {
        var cut = Render<RichTextEditor>();

        await DisposeComponentsAsync();

        JSInterop.VerifyInvoke("ruinaRichText.destroy");
        _ = cut;
    }
}
```

(If `DisposeComponentsAsync` doesn't exist in bUnit 2.9, use `DisposeComponents()`.)

- [ ] **Step 3: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~RichTextEditorTests`
Expected: build error `The type or namespace name 'RichTextEditor' could not be found`.

- [ ] **Step 4: Component**

`src/RuinaRPG.Client/Shared/Fields/RichTextEditor.razor`:

```razor
@inject IJSRuntime JS
@implements IAsyncDisposable

@* Aba História — hosts a Jodit editor through wwwroot/js/richTextEditor.js. Html is read ONLY when
   the editor is created: later parameter changes (e.g. the sheet reloading after another field's
   autosave) are ignored on purpose, so they can never overwrite what the player is typing. *@
<div @ref="_host" class="rr-rich-text-editor"></div>

@code {
    [Parameter] public string? Html { get; set; }
    [Parameter] public EventCallback<string?> HtmlChanged { get; set; }
    /// <summary>Fired after HtmlChanged for every change the editor reports (debounced ~1s in JS, flushed on blur/destroy).</summary>
    [Parameter] public EventCallback<string?> OnCommit { get; set; }

    private ElementReference _host;
    private DotNetObjectReference<RichTextEditor>? _selfRef;
    private bool _created;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        _selfRef = DotNetObjectReference.Create(this);
        await JS.InvokeVoidAsync("ruinaRichText.create", _host, _selfRef, Html ?? "");
        _created = true;
    }

    [JSInvokable]
    public async Task OnEditorChanged(string html)
    {
        Html = html;
        await HtmlChanged.InvokeAsync(html);
        await OnCommit.InvokeAsync(html);
    }

    public async ValueTask DisposeAsync()
    {
        if (_created)
        {
            try
            {
                await JS.InvokeVoidAsync("ruinaRichText.destroy", _host);
            }
            catch (JSDisconnectedException)
            {
                // Page already torn down — nothing left to destroy.
            }
        }
        _selfRef?.Dispose();
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run the Step 3 command → all pass.

- [ ] **Step 6: JS module**

`src/RuinaRPG.Client/wwwroot/js/richTextEditor.js`:

```javascript
// Aba História — Jodit (wwwroot/lib/jodit, MIT) behind a tiny API for RichTextEditor.razor.
// Jodit itself (~900 KB) is only fetched the first time an editor is created.
window.ruinaRichText = {
    _loading: null,
    _instances: new Map(),
    DEBOUNCE_MS: 1000,

    _ensureLoaded: function () {
        if (window.Jodit) return Promise.resolve();
        if (!this._loading) {
            this._loading = new Promise(function (resolve, reject) {
                var css = document.createElement('link');
                css.rel = 'stylesheet';
                css.href = 'lib/jodit/jodit.min.css';
                document.head.appendChild(css);
                var script = document.createElement('script');
                script.src = 'lib/jodit/jodit.min.js';
                script.onload = resolve;
                script.onerror = reject;
                document.head.appendChild(script);
            });
        }
        return this._loading;
    },

    _isDark: function () {
        return document.documentElement.getAttribute('data-theme') === 'dark';
    },

    create: async function (element, dotNetRef, initialHtml) {
        await this._ensureLoaded();
        var self = this;
        var editor = Jodit.make(element, {
            language: 'pt_br',
            theme: this._isDark() ? 'dark' : 'default',
            minHeight: 500,
            height: 'auto',
            toolbarAdaptive: false,
            showCharsCounter: false,
            showWordsCounter: false,
            showXPathInStatusbar: false,
            askBeforePasteHTML: false,
            askBeforePasteFromWord: false,
            defaultActionOnPaste: 'insert_clear_html',
            disablePlugins: ['image', 'video', 'file', 'media', 'source', 'print', 'about', 'speech-recognize', 'ai-assistant'],
            buttons: [
                'undo', 'redo', '|',
                'paragraph', 'font', 'fontsize', '|',
                'bold', 'italic', 'underline', 'strikethrough', 'superscript', 'subscript', '|',
                'brush', '|',
                'ul', 'ol', 'outdent', 'indent', 'align', '|',
                'link', 'table', 'hr', '|',
                'find', 'eraser', 'fullsize'
            ]
        });
        editor.value = initialHtml || '';

        var state = { editor: editor, timer: null, observer: null };

        var send = function () {
            state.timer = null;
            dotNetRef.invokeMethodAsync('OnEditorChanged', editor.value);
        };
        state.flush = function () {
            if (state.timer !== null) {
                clearTimeout(state.timer);
                send();
            }
        };

        editor.events.on('change', function () {
            if (state.timer !== null) clearTimeout(state.timer);
            state.timer = setTimeout(send, self.DEBOUNCE_MS);
        });
        editor.events.on('blur', function () { state.flush(); });

        // Follow the app's light/dark toggle (theme.js sets data-theme on <html>).
        state.observer = new MutationObserver(function () {
            var dark = self._isDark();
            editor.container.classList.toggle('jodit_theme_dark', dark);
            editor.container.classList.toggle('jodit_theme_default', !dark);
        });
        state.observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });

        this._instances.set(element, state);
    },

    destroy: function (element) {
        var state = this._instances.get(element);
        if (!state) return;
        state.flush();
        state.observer.disconnect();
        state.editor.destruct();
        this._instances.delete(element);
    }
};
```

In `src/RuinaRPG.Client/wwwroot/index.html`, after `<script src="js/clipboard.js"></script>` add:

```html
    <script src="js/richTextEditor.js"></script>
```

Run `dotnet build` → 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/RuinaRPG.Client/wwwroot/lib/jodit src/RuinaRPG.Client/wwwroot/js/richTextEditor.js src/RuinaRPG.Client/wwwroot/index.html src/RuinaRPG.Client/Shared/Fields/RichTextEditor.razor tests/RuinaRPG.Tests.Client/Shared/Fields/RichTextEditorTests.cs
git commit -m "feat: editor de texto rico (Jodit 4.15.14 vendorizado) e componente RichTextEditor

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `HistoriaEditor` + História tab on both sheets + requirement docs

**Files:**
- Create: `src/RuinaRPG.Client/Shared/Fields/HistoriaEditor.razor`
- Test: `tests/RuinaRPG.Tests.Client/Shared/Fields/HistoriaEditorTests.cs`
- Modify: `src/RuinaRPG.Client/Pages/FichaDePersonagem.razor` (remove lines ~145-146 from Informações Básicas; new tab before `<MudTabPanel Text="Diário">` ~line 773; `SheetFormModel` ~line 1733; sheet load mapping ~line 1029)
- Modify: `src/RuinaRPG.Client/Pages/FichaDeNpc.razor` (remove lines ~144-145; new tab before `</MudTabs>` ~line 719; `SheetFormModel` ~line 1532; load mapping ~line 926)
- Modify: `Docs/Requisitos/Requisitos - Ficha de Personagem.md`, `Docs/Requisitos/Requisitos - Ficha de NPCs.md`

**Interfaces:**
- Consumes: `RichTextEditor` (`Html`, `HtmlChanged`, `OnCommit`) from Task 4; `UpdateHistoriaRequest` and `…Response.Historia` from Task 2; `PUT character-sheets/{id}/historia` (Task 2) and `PUT npc-sheets/{id}/historia` (Task 3); existing `AutoSaveCoordinator`, `AutoSaveIndicator`, `HttpContentExtensions.ReadErrorMessageAsync`.
- Produces: `RuinaRPG.Client.Shared.Fields.HistoriaEditor` with `[Parameter, EditorRequired] string SaveUrl` (relative to the HttpClient base, e.g. `character-sheets/{id}/historia`), `[Parameter] string? Html`, `[Parameter] EventCallback<string?> HtmlChanged`, `[Parameter] EventCallback<string> OnError`.

- [ ] **Step 1: Write the failing bUnit tests**

`tests/RuinaRPG.Tests.Client/Shared/Fields/HistoriaEditorTests.cs`:

```csharp
using System.Net;
using System.Text;
using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class HistoriaEditorTests : MudBunitContext
{
    private readonly List<(string Method, string Path, string Body)> _requests = new();

    private void RegisterApi(HttpStatusCode status, string responseBody = "") =>
        Services.AddScoped(_ => FakeHttpMessageHandler.CreateClient(request =>
        {
            var body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            lock (_requests) _requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, body));
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "text/plain") };
        }));

    [Fact]
    public async Task A_change_in_the_editor_is_saved_to_SaveUrl()
    {
        RegisterApi(HttpStatusCode.NoContent);
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.Html, "<p>a</p>"));

        await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.OnEditorChanged("<p>Nasceu em Alkeria</p>"));

        cut.WaitForAssertion(() =>
        {
            lock (_requests) _requests.Should().ContainSingle();
        }, TimeSpan.FromSeconds(3));
        var request = _requests.Single();
        request.Method.Should().Be("PUT");
        request.Path.Should().Be("/api/character-sheets/abc/historia");
        request.Body.Should().Contain("Nasceu em Alkeria");
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Salvo às"), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task A_change_updates_the_bound_Html_immediately()
    {
        RegisterApi(HttpStatusCode.NoContent);
        string? bound = null;
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "npc-sheets/abc/historia")
            .Add(x => x.HtmlChanged, (string? v) => bound = v));

        await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.OnEditorChanged("<p>novo</p>"));

        bound.Should().Be("<p>novo</p>");
    }

    [Fact]
    public async Task A_400_from_the_api_is_reported_through_OnError_and_the_indicator_shows_the_error()
    {
        RegisterApi(HttpStatusCode.BadRequest, "A História pode ter no máximo 200.000 caracteres.");
        string? error = null;
        var cut = Render<HistoriaEditor>(p => p
            .Add(x => x.SaveUrl, "character-sheets/abc/historia")
            .Add(x => x.OnError, (string e) => error = e));

        await cut.InvokeAsync(() => cut.FindComponent<RichTextEditor>().Instance.OnEditorChanged("<p>longo demais</p>"));

        cut.WaitForAssertion(() => error.Should().Be("A História pode ter no máximo 200.000 caracteres."), TimeSpan.FromSeconds(3));
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Erro ao salvar"), TimeSpan.FromSeconds(3));
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet test tests/RuinaRPG.Tests.Client --filter FullyQualifiedName~HistoriaEditorTests`
Expected: build error `The type or namespace name 'HistoriaEditor' could not be found`.

- [ ] **Step 3: Implement `HistoriaEditor`**

`src/RuinaRPG.Client/Shared/Fields/HistoriaEditor.razor`:

```razor
@using System.Net
@using MudBlazor
@using RuinaRPG.Client.Services
@using RuinaRPG.Contracts.CharacterSheets
@inject HttpClient Http
@implements IDisposable

@* Aba História (Personagem e NPC): the rich-text editor plus its own autosave — the História has a
   dedicated endpoint (PUT …/historia), so it doesn't ride the sheet's general autosave. *@
<div class="d-flex align-center justify-space-between mb-2">
    <MudText Typo="Typo.h6">História do personagem</MudText>
    <AutoSaveIndicator State="_autoSave.State" LastSavedAt="_autoSave.LastSavedAt" />
</div>
<RichTextEditor Html="@Html" HtmlChanged="OnHtmlChangedAsync" OnCommit="OnCommit" />

@code {
    [Parameter, EditorRequired] public string SaveUrl { get; set; } = "";
    [Parameter] public string? Html { get; set; }
    [Parameter] public EventCallback<string?> HtmlChanged { get; set; }
    [Parameter] public EventCallback<string> OnError { get; set; }

    private readonly AutoSaveCoordinator _autoSave = new();

    protected override void OnInitialized() => _autoSave.StateChanged += Refresh;

    private void Refresh() => InvokeAsync(StateHasChanged);

    public void Dispose() => _autoSave.StateChanged -= Refresh;

    private async Task OnHtmlChangedAsync(string? html)
    {
        Html = html;
        await HtmlChanged.InvokeAsync(html);
    }

    private Task OnCommit(string? html)
    {
        _autoSave.NotifyChanged(() => SaveAsync(html));
        return Task.CompletedTask;
    }

    private async Task<bool> SaveAsync(string? html)
    {
        var response = await Http.PutAsJsonAsync(SaveUrl, new UpdateHistoriaRequest(html));
        if (response.IsSuccessStatusCode)
            return true;

        var message = response.StatusCode == HttpStatusCode.BadRequest
            ? await response.Content.ReadErrorMessageAsync() ?? "Não foi possível salvar a História."
            : "Não foi possível salvar a História.";
        await InvokeAsync(() => OnError.InvokeAsync(message));
        // Throwing (not returning false) is what makes AutoSaveCoordinator show "Erro ao salvar".
        throw new InvalidOperationException(message);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run the Step 2 command → all pass.

- [ ] **Step 5: Wire the Ficha de Personagem**

In `FichaDePersonagem.razor`:

1. In the "Informações Básicas" identity `Section`, delete these two lines:
   ```razor
                   <EstrelaSelect @bind-Value="_form.Estrela" @bind-Value:after="NotifySavedAsync" />
                   <HistoricoSelect @bind-Value="_form.HistoricoId" @bind-Value:after="NotifySavedAsync" />
   ```
2. Immediately before `    <MudTabPanel Text="Diário">`, insert:
   ```razor
       <MudTabPanel Text="História">
           <Section Title="Estrela e Histórico">
               <EstrelaSelect @bind-Value="_form.Estrela" @bind-Value:after="NotifySavedAsync" />
               <HistoricoSelect @bind-Value="_form.HistoricoId" @bind-Value:after="NotifySavedAsync" />
           </Section>
           <Section>
               <HistoriaEditor SaveUrl="@($"character-sheets/{SheetId}/historia")" @bind-Html="_form.Historia" OnError="@(e => _errorMessage = e)" />
           </Section>
       </MudTabPanel>

   ```
3. In `private class SheetFormModel`, after `public string? Estrela { get; set; }`, add `public string? Historia { get; set; }`.
4. Add a field next to the other private fields near `_autoSave` (~line 851):
   ```csharp
       // Aba História: _form.Historia comes from the API only on the first load — later reloads (after
       // any other field's autosave) must not overwrite what HistoriaEditor is holding.
       private bool _historiaLoaded;
   ```
5. In the load method, right after `_form.HistoricoId = sheet.HistoricoId;`, add:
   ```csharp
           if (!_historiaLoaded)
           {
               _form.Historia = sheet.Historia;
               _historiaLoaded = true;
           }
   ```

- [ ] **Step 6: Wire the Ficha de NPC**

Same five edits in `FichaDeNpc.razor`, except:
- the tab goes immediately before `</MudTabs>` (the NPC has no Diário, so História is the last tab);
- `SaveUrl="@($"npc-sheets/{SheetId}/historia")"`.

Run `dotnet build` → 0 warnings, 0 errors; run `dotnet test tests/RuinaRPG.Tests.Client` → all pass (the known intermittent `AutoSaveCoordinatorTests.A_delegate_returning_true_sets_state_Saved_and_LastSavedAt` timing flake passes on re-run; anything else is real).

- [ ] **Step 7: Requirement docs**

`Docs/Requisitos/Requisitos - Ficha de Personagem.md`:

1. In R0001's tab list, change `6. **Diário**: anotações pessoais do jogador sobre a campanha. Ver detalhes abaixo.` to
   ```markdown
   6. **História**: Estrela, Histórico e a história do personagem em texto formatado. Ver R0006.

   7. **Diário**: anotações pessoais do jogador sobre a campanha. Ver detalhes abaixo.
   ```
   and the heading `## 6. Diário` to `## 7. Diário`; in the preamble line mentioning `6. Diário` (~line 429), change it to `7. Diário`.
2. In 1.a, replace the whole *Estrela* bullet and the whole *Histórico* bullet with one line:
   ```markdown
   - *Estrela* e *Histórico*: ficam na aba **História** (ver R0006).
   ```
3. Append a new requirement at the end of the file, moving the removed Estrela/Histórico bullet texts into it verbatim where marked:
   ```markdown
     

   # **R0006** - A aba História reúne Estrela, Histórico e a história do personagem.

   **Descrição**: Aba entre Posses e Diário, com, de cima para baixo:

   - *Estrela*: <texto integral do bullet *Estrela* removido de 1.a>
   - *Histórico*: <texto integral do bullet *Histórico* removido de 1.a>
   - *História do personagem*: campo de texto rico, editado com uma barra de formatação completa — desfazer/refazer, parágrafo/títulos, fonte e tamanho, negrito, itálico, sublinhado, tachado, sobrescrito, subscrito, cor do texto e do fundo, listas com marcador e numeradas, recuo, alinhamento, citação, link, tabela (inserir, adicionar/remover linhas e colunas, mesclar células), linha horizontal, localizar e substituir, limpar formatação e tela cheia. Não aceita imagens, vídeos ou arquivos. Salva automaticamente cerca de 1 segundo após o jogador parar de digitar e ao sair do campo, com o indicador de salvamento automático próprio da aba. Quando NULL, o editor aparece vazio. Máximo de 200.000 caracteres; acima disso, o salvamento é recusado com aviso. O texto é gravado como HTML e o servidor remove tudo o que a barra de formatação não produz (scripts, eventos, imagens, iframes, links que não sejam http/https/mailto) antes de gravar (ver "[[Requisitos - Modelo de Dados]]").
   ```

`Docs/Requisitos/Requisitos - Ficha de NPCs.md`:

1. In the opening preamble, change `todas as 5 abas (Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses)` to `todas as 6 abas (Informações Básicas, Atributos & Perícias, Combate, Magias & Habilidades, Posses, História)`.
2. Append:
   ```markdown
     

   # **R0009** - A aba História do NPC é a última aba.

   **Descrição**: A aba História segue "[[Requisitos - Ficha de Personagem]]" R0006 sem alteração (Estrela, Histórico e a história em texto formatado, com o mesmo salvamento automático e a mesma limpeza de HTML). Como a Ficha de NPC não tem Diário, a História é a última aba. Quando o GM concede a um jogador a cópia de um NPC existente ("[[Requisitos - Campanha]]" R0010), a História é copiada junto.
   ```

- [ ] **Step 8: Commit**

```bash
git add src/RuinaRPG.Client/Shared/Fields/HistoriaEditor.razor tests/RuinaRPG.Tests.Client/Shared/Fields/HistoriaEditorTests.cs src/RuinaRPG.Client/Pages/FichaDePersonagem.razor src/RuinaRPG.Client/Pages/FichaDeNpc.razor "Docs/Requisitos/Requisitos - Ficha de Personagem.md" "Docs/Requisitos/Requisitos - Ficha de NPCs.md"
git commit -m "feat: aba História nas fichas de Personagem e NPC

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

## After all tasks (controller, not a task subagent)

1. Full `dotnet build` (0/0) and `dotnet test` (see Global Constraints for disk check and the known flake).
2. Whole-branch review.
3. Browser check (bUnit can't run Jodit): run the API + client locally in Development, open a Ficha de Personagem and a Ficha de NPC, and confirm: tab order (História before Diário / last on NPC); Estrela + Histórico moved out of Informações Básicas; toolbar present and in pt-BR; no image/video/source buttons; dark theme follows the app toggle; typing → "Salvo às…"; switching tabs within 1s of typing still saves; reload → text persists; a table and a colored heading survive the round trip; pasting a `<script>` via devtools-edited content never reaches the saved HTML.
