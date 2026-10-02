using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

/// <summary>
/// GM-editable overrides for RacialAbilityLookup's hardcoded defaults (Ruína RPG - Sistema
/// Básico.md §7), plus the "tabela de Arcas" that Sinir/Laonir's (Humano) racial ability
/// references by name ("Role 1d{dado} na tabela de Arcas") but that no doc actually defines — it's
/// free-form GM content, not a fixed system rule. Both curating and browsing this page are
/// GM-only — a Jogador never reaches these endpoints directly; they only see the already-resolved
/// Nome/Descrição/Arca on their own sheet, which CharacterSheetsController/NpcSheetsController
/// compute by querying RacialAbilityOverrides/ArcaEntries themselves, not through this controller.
/// </summary>
[ApiController]
[Authorize(Roles = "GM")]
public class RacialAbilitiesController(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis) : ControllerBase
{
    [HttpGet("api/racial-abilities")]
    public async Task<ActionResult<List<RacialAbilityEntryResponse>>> ListRacialAbilities()
    {
        var gmId = CurrentUserId();
        var overrides = await db.RacialAbilityOverrides.Where(o => o.GmId == gmId).ToListAsync();
        var dado = await db.DadoDeArcaDoGmAsync(gmId);

        var responses = new List<RacialAbilityEntryResponse>();
        foreach (var variante in Enum.GetValues<Variante>())
        {
            var over = overrides.FirstOrDefault(o => o.Variante == variante);
            if (over is not null)
            {
                responses.Add(new RacialAbilityEntryResponse(variante.ToString(), over.Nome, over.Descricao, false, over.NomeDaVariante));
            }
            else
            {
                var def = RacialAbilityLookup.For(variante, dado);
                responses.Add(new RacialAbilityEntryResponse(variante.ToString(), def.Nome, def.Descricao, true));
            }
        }
        return responses;
    }

    [HttpPut("api/racial-abilities/{variante}")]
    public async Task<IActionResult> UpdateRacialAbility(string variante, UpdateRacialAbilityRequest request)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante) || !Enum.IsDefined(parsedVariante))
            return BadRequest("Variante desconhecida.");

        var gmId = CurrentUserId();
        var existing = await db.RacialAbilityOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == parsedVariante);
        if (existing is null)
        {
            db.RacialAbilityOverrides.Add(new RacialAbilityOverride { Id = Guid.NewGuid(), GmId = gmId, Variante = parsedVariante, Nome = request.Nome, Descricao = request.Descricao });
        }
        else
        {
            existing.Nome = request.Nome;
            existing.Descricao = request.Descricao;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Dá (ou tira) o nome da variante solar de Alóra. Preencher libera a variante nas fichas do GM;
    /// vazio a bloqueia de novo. Só vale para Variante.AloraSolar — as demais têm nome fixo.
    /// </summary>
    [HttpPut("api/racial-abilities/{variante}/nome-da-variante")]
    public async Task<IActionResult> UpdateNomeDaVariante(string variante, UpdateNomeDaVarianteRequest request)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante) || parsedVariante != VarianteLiberadaResolver.Personalizada)
            return BadRequest("Só a variante solar de Alóra tem nome editável.");

        var nome = string.IsNullOrWhiteSpace(request.Nome) ? null : request.Nome.Trim();
        if (nome is { Length: > 60 })
            return BadRequest("O nome da variante pode ter no máximo 60 caracteres.");

        var gmId = CurrentUserId();
        var existing = await db.RacialAbilityOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == parsedVariante);
        if (existing is null)
        {
            if (nome is not null)
                db.RacialAbilityOverrides.Add(new RacialAbilityOverride { Id = Guid.NewGuid(), GmId = gmId, Variante = parsedVariante, Nome = "", Descricao = "", NomeDaVariante = nome });
        }
        else
        {
            existing.NomeDaVariante = nome;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("api/racial-abilities/{variante}")]
    public async Task<IActionResult> DeleteRacialAbilityOverride(string variante)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante) || !Enum.IsDefined(parsedVariante))
            return BadRequest("Variante desconhecida.");

        var gmId = CurrentUserId();
        var existing = await db.RacialAbilityOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == parsedVariante);
        if (existing is not null)
        {
            db.RacialAbilityOverrides.Remove(existing);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpGet("api/arcas/dado")]
    public async Task<ActionResult<ArcaDadoResponse>> GetDado() =>
        new ArcaDadoResponse(await db.DadoDeArcaDoGmAsync(CurrentUserId()));

    /// <summary>
    /// Escolhe o dado da Tabela de Arcas do GM. Diminuir o dado só ESCONDE as Arcas acima dele (e as
    /// suas evoluções): nada é apagado, e elas voltam se o GM escolher um dado maior de novo.
    /// </summary>
    [HttpPut("api/arcas/dado")]
    public async Task<IActionResult> SetDado(ArcaDadoRequest request)
    {
        if (!DadoDeArca.EhValido(request.Dado))
            return BadRequest("Dado inválido. Use D6, D8, D10, D12, D20 ou D100.");

        var gmId = CurrentUserId();
        var existing = await db.ArcaTabelas.FirstOrDefaultAsync(t => t.GmId == gmId);
        if (existing is null)
            db.ArcaTabelas.Add(new ArcaTabela { GmId = gmId, Dado = request.Dado });
        else
            existing.Dado = request.Dado;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("api/arcas")]
    public async Task<ActionResult<List<ArcaEntryResponse>>> ListArcas()
    {
        var gmId = CurrentUserId();
        var dado = await db.DadoDeArcaDoGmAsync(gmId);
        var entries = await db.ArcaEntries.Include(a => a.Evolucoes).Where(a => a.GmId == gmId && a.Roll <= dado).ToListAsync();

        var responses = new List<ArcaEntryResponse>();
        for (var roll = 1; roll <= dado; roll++)
        {
            var entry = entries.FirstOrDefault(a => a.Roll == roll);
            var evolucoes = entry is null
                ? new List<ArcaEvolucaoResponse>()
                : ArcaEvolucaoRules.Desbloqueadas(entry.Evolucoes, e => e.Nivel, e => e.CriadaEm, int.MaxValue).Select(ToResponse).ToList();
            responses.Add(new ArcaEntryResponse(roll, entry?.Nome, entry?.Descricao, evolucoes));
        }
        return responses;
    }

    [HttpPut("api/arcas/{roll:int}")]
    public async Task<IActionResult> UpdateArca(int roll, UpdateArcaEntryRequest request)
    {
        var gmId = CurrentUserId();
        if (await ValidateRollAsync(roll) is { } invalidRoll)
            return invalidRoll;

        var existing = await db.ArcaEntries.FirstOrDefaultAsync(a => a.GmId == gmId && a.Roll == roll);
        if (existing is null)
        {
            db.ArcaEntries.Add(new ArcaEntry { Id = Guid.NewGuid(), GmId = gmId, Roll = roll, Nome = request.Nome, Descricao = request.Descricao });
        }
        else
        {
            existing.Nome = request.Nome;
            existing.Descricao = request.Descricao;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("api/arcas/{roll:int}/evolucoes")]
    public async Task<ActionResult<ArcaEvolucaoResponse>> AddEvolucao(int roll, ArcaEvolucaoRequest request)
    {
        if (await ValidateEvolucaoAsync(roll, request) is { } invalid)
            return invalid;

        var gmId = CurrentUserId();
        var arca = await db.ArcaEntries.FirstOrDefaultAsync(a => a.GmId == gmId && a.Roll == roll);
        if (arca is null)
        {
            arca = new ArcaEntry { Id = Guid.NewGuid(), GmId = gmId, Roll = roll, Nome = "", Descricao = "" };
            db.ArcaEntries.Add(arca);
        }

        var evolucao = new ArcaEvolucao { Id = Guid.NewGuid(), ArcaEntryId = arca.Id, Nivel = request.Nivel, Descricao = request.Descricao.Trim(), CriadaEm = DateTimeOffset.UtcNow };
        db.ArcaEvolucoes.Add(evolucao);
        await db.SaveChangesAsync();
        return Created($"/api/arcas/{roll}/evolucoes/{evolucao.Id}", ToResponse(evolucao));
    }

    [HttpPut("api/arcas/{roll:int}/evolucoes/{id:guid}")]
    public async Task<ActionResult<ArcaEvolucaoResponse>> UpdateEvolucao(int roll, Guid id, ArcaEvolucaoRequest request)
    {
        if (await ValidateEvolucaoAsync(roll, request) is { } invalid)
            return invalid;

        var evolucao = await FindOwnEvolucaoAsync(roll, id);
        if (evolucao is null)
            return NotFound();

        evolucao.Nivel = request.Nivel;
        evolucao.Descricao = request.Descricao.Trim();
        await db.SaveChangesAsync();
        return ToResponse(evolucao);
    }

    [HttpDelete("api/arcas/{roll:int}/evolucoes/{id:guid}")]
    public async Task<IActionResult> DeleteEvolucao(int roll, Guid id)
    {
        var evolucao = await FindOwnEvolucaoAsync(roll, id);
        if (evolucao is null)
            return NotFound();

        db.ArcaEvolucoes.Remove(evolucao);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ActionResult?> ValidateRollAsync(int roll)
    {
        var dado = await db.DadoDeArcaDoGmAsync(CurrentUserId());
        return roll < 1 || roll > dado ? BadRequest($"Roll deve estar entre 1 e {dado}.") : null;
    }

    private async Task<ActionResult?> ValidateEvolucaoAsync(int roll, ArcaEvolucaoRequest request)
    {
        if (await ValidateRollAsync(roll) is { } invalidRoll)
            return invalidRoll;
        var ultimo = (await tabelaDeNiveis.ObterAsync()).UltimoNivel;
        if (!ArcaEvolucaoRules.NivelValido(request.Nivel, ultimo))
            return BadRequest($"O nível da evolução deve estar entre 1 e {ultimo}.");
        if (string.IsNullOrWhiteSpace(request.Descricao))
            return BadRequest("Descreva a evolução.");
        return null;
    }

    // Scoped by GM *and* roll: an id from another GM, or from another roll of the same GM, is a 404.
    private Task<ArcaEvolucao?> FindOwnEvolucaoAsync(int roll, Guid id)
    {
        var gmId = CurrentUserId();
        return db.ArcaEvolucoes
            .Where(e => e.Id == id && db.ArcaEntries.Any(a => a.Id == e.ArcaEntryId && a.GmId == gmId && a.Roll == roll))
            .FirstOrDefaultAsync();
    }

    private static ArcaEvolucaoResponse ToResponse(ArcaEvolucao e) => new(e.Id, e.Nivel, e.Descricao);

    /// <summary>
    /// GM-editable overrides for RacialTraitLookup's hardcoded Característica Gratuita/Obrigatória
    /// defaults (Ruína RPG - Sistema Básico.md §7). Same shape as ListRacialAbilities above.
    /// </summary>
    [HttpGet("api/racial-traits")]
    public async Task<ActionResult<List<RacialTraitSlotsEntryResponse>>> ListRacialTraits()
    {
        var gmId = CurrentUserId();
        var overrides = await db.RacialTraitOverrides.Where(o => o.GmId == gmId).ToListAsync();

        var responses = new List<RacialTraitSlotsEntryResponse>();
        foreach (var variante in Enum.GetValues<Variante>())
        {
            var over = overrides.FirstOrDefault(o => o.Variante == variante);
            if (over is not null)
            {
                responses.Add(new RacialTraitSlotsEntryResponse(variante.ToString(),
                    DeserializeOptions(over.GratuitaOptionsJson), DeserializeOptions(over.ObrigatoriaOptionsJson), false));
            }
            else
            {
                var def = RacialTraitLookup.For(variante);
                responses.Add(new RacialTraitSlotsEntryResponse(variante.ToString(), ToResponseOptions(def.Gratuita), ToResponseOptions(def.Obrigatoria), true));
            }
        }
        return responses;
    }

    [HttpPut("api/racial-traits/{variante}")]
    public async Task<IActionResult> UpdateRacialTraitSlots(string variante, UpdateRacialTraitSlotsRequest request)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante) || !Enum.IsDefined(parsedVariante))
            return BadRequest("Variante desconhecida.");

        if (request.Gratuita.Count == 0)
            return BadRequest("A Característica Gratuita precisa de ao menos uma opção.");

        var gmId = CurrentUserId();
        var gratuitaJson = JsonSerializer.Serialize(request.Gratuita.Select(o => new RacialTraitOptionResponse(o.TraitNome, o.Especificacao)));
        var obrigatoriaJson = JsonSerializer.Serialize(request.Obrigatoria.Select(o => new RacialTraitOptionResponse(o.TraitNome, o.Especificacao)));

        var existing = await db.RacialTraitOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == parsedVariante);
        if (existing is null)
        {
            db.RacialTraitOverrides.Add(new RacialTraitOverride
            {
                Id = Guid.NewGuid(), GmId = gmId, Variante = parsedVariante,
                GratuitaOptionsJson = gratuitaJson, ObrigatoriaOptionsJson = obrigatoriaJson,
            });
        }
        else
        {
            existing.GratuitaOptionsJson = gratuitaJson;
            existing.ObrigatoriaOptionsJson = obrigatoriaJson;
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("api/racial-traits/{variante}")]
    public async Task<IActionResult> DeleteRacialTraitOverride(string variante)
    {
        if (!Enum.TryParse<Variante>(variante, out var parsedVariante) || !Enum.IsDefined(parsedVariante))
            return BadRequest("Variante desconhecida.");

        var gmId = CurrentUserId();
        var existing = await db.RacialTraitOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == parsedVariante);
        if (existing is not null)
        {
            db.RacialTraitOverrides.Remove(existing);
            await db.SaveChangesAsync();
        }

        return NoContent();
    }

    private static List<RacialTraitOptionResponse> DeserializeOptions(string json) =>
        JsonSerializer.Deserialize<List<RacialTraitOptionResponse>>(json) ?? [];

    private static List<RacialTraitOptionResponse> ToResponseOptions(List<RacialTraitOption> options) =>
        options.Select(o => new RacialTraitOptionResponse(o.TraitNome, o.Especificacao)).ToList();

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
