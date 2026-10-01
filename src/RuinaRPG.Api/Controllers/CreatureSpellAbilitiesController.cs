using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/spell-abilities")]
public class CreatureSpellAbilitiesController(RuinaRpgDbContext db, CreatureSheetStats stats, IPericiaCatalogo pericias) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreatureSpellAbilityResponse>> Add(Guid sheetId, AddCreatureSpellAbilityRequest request)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var fromScratch = request.Nome is not null && request.Tipo is not null && request.Grau is not null && request.Descricao is not null && request.Efeitos is not null;
        var fromBank = request.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // both or neither set
            return BadRequest("Informe exatamente um: os campos para montar do zero, ou SourceBankEntryId.");

        // A Criatura sheet has no CampaignId column: a granted sheet's campaign is resolved through the
        // grant-link CampaignAttachment. Only a Jogador (caller != GM) needs it — both to pick from the bank
        // (public entries of that campaign only) and to publish the bank copy afterwards.
        Guid? campaignId = null;
        if (CurrentUserId() != sheet.GmId)
        {
            campaignId = await db.CampaignAttachments
                .Where(a => a.CreatureSheetId == sheetId && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == sheet.OwnerId))
                .Select(a => (Guid?)a.CampaignId)
                .FirstOrDefaultAsync();
        }

        string nome; SpellAbilityTipo tipo; int grau; string descricao; List<SpellAbilityEffectRequest> efeitos;
        Guid? sourceBankEntryId = null;
        CategoriaDePassiva? categoria = null; RequisitosDePassiva? requisitos = null;

        if (fromBank)
        {
            if (!Guid.TryParse(request.SourceBankEntryId, out var bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            // Só entradas do banco do GM desta ficha; um jogador ainda precisa que o GM a tenha anexado
            // como pública à campanha da concessão — antes, qualquer Guid de qualquer GM era aceito.
            var bankEntry = await db.SpellAbilityBankEntries.Include(e => e.Efeitos).FirstOrDefaultAsync(e => e.Id == bankEntryId && e.GmId == sheet.GmId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            if (CurrentUserId() != sheet.GmId
                && (campaignId is null
                    || !await db.CampaignAttachments.AnyAsync(a => a.CampaignId == campaignId && a.IsPublic && a.SpellAbilityBankEntryId == bankEntryId)))
                return BadRequest("Entrada do banco não encontrada.");

            // Depois das checagens de acesso: uma entrada inalcançável responde "não encontrada" e nunca
            // revela seus requisitos. O bloqueio vale para todos, GM incluído.
            if (bankEntry.Tipo == SpellAbilityTipo.Passiva)
            {
                // Uma Magia/Habilidade comum pode repetir na ficha (cópias independentes); uma Passiva não —
                // ela é um estado ligado/desligado por Categoria, então a mesma entrada do banco só pode
                // virar uma cópia por ficha.
                if (await db.CreatureSpellAbilities.AnyAsync(e => e.CreatureSheetId == sheetId && e.SourceBankEntryId == bankEntryId))
                    return BadRequest("Esta Passiva já está na ficha.");

                var pendencias = PassivaRequisitosEvaluator.Pendencias(bankEntry.Requisitos, await stats.FichaParaRequisitosAsync(sheet),
                    await RequisitosDePassivaMapper.NomeDoHistoricoAsync(db, bankEntry.Requisitos), RequisitosDePassivaMapper.NomeDaPericia(await pericias.PorIdAsync()));
                if (pendencias.Count > 0)
                    return BadRequest("Requisitos não cumpridos: " + string.Join(", ", pendencias));
            }
            categoria = bankEntry.Categoria; requisitos = bankEntry.Requisitos;

            nome = bankEntry.Nome; tipo = bankEntry.Tipo; grau = bankEntry.Grau; descricao = bankEntry.Descricao;
            efeitos = bankEntry.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();
            sourceBankEntryId = bankEntryId;
        }
        else
        {
            if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out tipo))
                return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");
            if (tipo == SpellAbilityTipo.Passiva)
                return BadRequest("Passivas só são cadastradas no Banco de Magias e Habilidades.");
            nome = request.Nome!; grau = request.Grau!.Value; descricao = request.Descricao!; efeitos = request.Efeitos!;

            var validationError = await EfeitoValidationHelper.ValidarAsync(db, grau, efeitos);
            if (validationError is not null)
                return BadRequest(validationError);
        }

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(efeitos.Select(e => e.CustoPI));
        var custo = SpellAbilityCostCalculator.Custo(gastoEmPI);

        var sheetCopy = new CreatureSpellAbility
        {
            Id = Guid.NewGuid(), CreatureSheetId = sheetId, SourceBankEntryId = sourceBankEntryId,
            Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao,
            Categoria = categoria, Requisitos = requisitos
        };
        sheetCopy.Efeitos = efeitos.Select(e => new CreatureSpellAbilityEffect { Id = Guid.NewGuid(), CreatureSpellAbilityId = sheetCopy.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
        db.CreatureSpellAbilities.Add(sheetCopy);

        // R0001 (Banco de Magias): uma criação do zero também grava uma cópia independente no banco do GM,
        // e a ficha guarda o vínculo com ela. Criada numa Ficha de Criatura, a entrada já nasce marcada como
        // Magia/Habilidade de Criatura. Partir de uma entrada do banco (R0003) só copia para a ficha — reusa
        // a entrada, sem duplicá-la; para um jogador ela já é pública na campanha.
        if (!fromBank)
        {
            var bankCopy = new SpellAbilityBankEntry
            {
                Id = Guid.NewGuid(), GmId = sheet.GmId, Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao, DeCriatura = true
            };
            bankCopy.Efeitos = efeitos.Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = bankCopy.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
            db.SpellAbilityBankEntries.Add(bankCopy);
            sheetCopy.SourceBankEntryId = bankCopy.Id;

            if (CurrentUserId() != sheet.GmId && campaignId is not null)
            {
                db.CampaignAttachments.Add(new CampaignAttachment
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId.Value,
                    SpellAbilityBankEntryId = bankCopy.Id,
                    IsPublic = true
                });
            }
        }

        await db.SaveChangesAsync();
        return Created(string.Empty, await ToResponseAsync(sheetCopy, sheetCopy.Tipo == SpellAbilityTipo.Passiva ? await stats.FichaParaRequisitosAsync(sheet) : null));
    }

    [HttpGet]
    public async Task<ActionResult<List<CreatureSpellAbilityResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var entries = await db.CreatureSpellAbilities.Include(e => e.Efeitos).Where(e => e.CreatureSheetId == sheetId).ToListAsync();
        // Uma só leitura da ficha, e só se houver Passivas; mapeamento sequencial (um único DbContext).
        var ficha = entries.Any(e => e.Tipo == SpellAbilityTipo.Passiva) ? await stats.FichaParaRequisitosAsync(sheet) : null;
        var result = new List<CreatureSpellAbilityResponse>();
        foreach (var e in entries)
            result.Add(await ToResponseAsync(e, ficha));
        return result;
    }

    [HttpGet("passivas-disponiveis")]
    public async Task<ActionResult<List<PassivaDisponivelResponse>>> PassivasDisponiveis(Guid sheetId)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        // Mesmo alcance do Add: o GM vê todas as Passivas do próprio banco; o jogador, só as públicas na
        // campanha da concessão (resolvida pelo CampaignAttachment de vínculo) — sem campanha, nenhuma.
        var query = db.SpellAbilityBankEntries.Where(e => e.GmId == sheet.GmId && e.Tipo == SpellAbilityTipo.Passiva);
        if (CurrentUserId() != sheet.GmId)
        {
            var campaignId = await db.CampaignAttachments
                .Where(a => a.CreatureSheetId == sheetId && db.CampaignMembers.Any(m => m.CampaignId == a.CampaignId && m.UserId == sheet.OwnerId))
                .Select(a => (Guid?)a.CampaignId)
                .FirstOrDefaultAsync();
            if (campaignId is null)
                return new List<PassivaDisponivelResponse>();
            query = query.Where(e => db.CampaignAttachments.Any(a => a.CampaignId == campaignId && a.IsPublic && a.SpellAbilityBankEntryId == e.Id));
        }

        return await PassivasDisponiveisAsync(query, await stats.FichaParaRequisitosAsync(sheet));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.CreatureSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        if (!GrantedSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, sheet.GmId))
            return NotFound();

        var entry = await db.CreatureSpellAbilities.FirstOrDefaultAsync(e => e.Id == id && e.CreatureSheetId == sheetId);
        if (entry is null)
            return NotFound();

        db.CreatureSpellAbilities.Remove(entry); // the bank copy this creation also made is untouched — independent copies
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<CreatureSpellAbilityResponse> ToResponseAsync(CreatureSpellAbility e, FichaParaRequisitos? ficha)
    {
        List<string>? pendentes = null;
        if (e.Tipo == SpellAbilityTipo.Passiva && ficha is not null)
            pendentes = PassivaRequisitosEvaluator.Pendencias(e.Requisitos, ficha, await RequisitosDePassivaMapper.NomeDoHistoricoAsync(db, e.Requisitos), RequisitosDePassivaMapper.NomeDaPericia(await pericias.PorIdAsync())).ToList();
        return new(e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao,
            e.Efeitos.Select(ef => new SpellAbilityEffectResponse(ef.EfeitoNome, ef.Quantidade, ef.CustoPI)).ToList(),
            e.Categoria?.ToString(), RequisitosDePassivaMapper.ToDto(e.Requisitos, await pericias.PorIdAsync()), pendentes);
    }

    private async Task<List<PassivaDisponivelResponse>> PassivasDisponiveisAsync(IQueryable<SpellAbilityBankEntry> query, FichaParaRequisitos ficha)
    {
        var entradas = await query.OrderBy(e => e.Nome).ToListAsync();
        var result = new List<PassivaDisponivelResponse>();
        foreach (var e in entradas)
        {
            var pendencias = PassivaRequisitosEvaluator.Pendencias(e.Requisitos, ficha, await RequisitosDePassivaMapper.NomeDoHistoricoAsync(db, e.Requisitos), RequisitosDePassivaMapper.NomeDaPericia(await pericias.PorIdAsync()));
            result.Add(new PassivaDisponivelResponse(
                new SpellAbilityEntryResponse(e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao, [], e.DeCriatura,
                    e.Categoria?.ToString(), RequisitosDePassivaMapper.ToDto(e.Requisitos, await pericias.PorIdAsync())),
                pendencias.ToList()));
        }
        return result;
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
