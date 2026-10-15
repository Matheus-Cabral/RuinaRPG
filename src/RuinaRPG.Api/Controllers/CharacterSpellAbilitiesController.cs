using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/spell-abilities")]
public class CharacterSpellAbilitiesController(RuinaRpgDbContext db, CharacterSheetStats stats, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, CondicoesAtivasDaFicha condicoes) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CharacterSpellAbilityResponse>> Add(Guid sheetId, AddCharacterSpellAbilityRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var fromScratch = request.Nome is not null && request.Tipo is not null && request.Grau is not null && request.Descricao is not null && request.Efeitos is not null;
        var fromBank = request.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // both or neither set
            return BadRequest("Informe exatamente um: os campos para montar do zero, ou SourceBankEntryId.");

        string nome; SpellAbilityTipo tipo; int grau; string descricao; List<SpellAbilityEffectRequest> efeitos;
        Guid? sourceBankEntryId = null;
        CategoriaDePassiva? categoria = null; RequisitosDePassiva? requisitos = null;

        if (fromBank)
        {
            if (!Guid.TryParse(request.SourceBankEntryId, out var bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            // Só entradas do banco do GM desta ficha; um jogador ainda precisa que o GM a tenha anexado
            // como pública à campanha da ficha (Requisitos - Ficha de Personagem R0003) — antes, qualquer
            // Guid de qualquer GM era aceito.
            var bankEntry = await db.SpellAbilityBankEntries.Include(e => e.Efeitos).FirstOrDefaultAsync(e => e.Id == bankEntryId && e.GmId == campaignGmId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            if (CurrentUserId() != campaignGmId
                && !await db.CampaignAttachments.AnyAsync(a => a.CampaignId == sheet.CampaignId && a.IsPublic && a.SpellAbilityBankEntryId == bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            // Depois das checagens de acesso: uma entrada inalcançável responde "não encontrada" e nunca
            // revela seus requisitos. O bloqueio vale para todos, GM incluído.
            if (bankEntry.Tipo == SpellAbilityTipo.Passiva)
            {
                // Uma Magia/Habilidade comum pode repetir na ficha (cópias independentes); uma Passiva não —
                // ela é um estado ligado/desligado por Categoria, então a mesma entrada do banco só pode
                // virar uma cópia por ficha.
                if (await db.CharacterSpellAbilities.AnyAsync(e => e.CharacterSheetId == sheetId && e.SourceBankEntryId == bankEntryId))
                    return BadRequest("Esta Passiva já está na ficha.");

                var pendencias = PassivaRequisitosEvaluator.Pendencias(bankEntry.Requisitos, await stats.FichaParaRequisitosAsync(sheet),
                    await RequisitosDePassivaMapper.NomeDoHistoricoAsync(db, bankEntry.Requisitos), RequisitosDePassivaMapper.NomeDaPericia(await pericias.PorIdAsync()));
                if (pendencias.Count > 0)
                    return BadRequest("Requisitos não cumpridos: " + string.Join(", ", pendencias));

                if (bankEntry.Categoria is { } cat)
                {
                    var naFicha = await db.CharacterSpellAbilities
                        .Where(e => e.CharacterSheetId == sheetId && e.Tipo == SpellAbilityTipo.Passiva && e.Categoria != null)
                        .Select(e => e.Categoria!.Value).ToListAsync();
                    if (LimitesDeNivel.Passivas(cat, naFicha, await tabelaDeNiveis.ObterAsync(), sheet.Nivel) is { } erroDeLimite)
                        return BadRequest(erroDeLimite);
                }
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

        // 4.b: o Gasto em PI sai do saldo da ficha; sem saldo, nem o GM cria. Passivas não têm PI.
        if (SaldoDePontosDeIgnicao.Consome(tipo))
        {
            if (SaldoDePontosDeIgnicao.ErroAoDebitar(sheet.PontosDeIgnicaoAtual, gastoEmPI) is { } semSaldo)
                return BadRequest(semSaldo);
            sheet.PontosDeIgnicaoAtual = SaldoDePontosDeIgnicao.Debitar(sheet.PontosDeIgnicaoAtual, gastoEmPI);
        }

        var sheetCopy = new CharacterSpellAbility
        {
            Id = Guid.NewGuid(), CharacterSheetId = sheetId, SourceBankEntryId = sourceBankEntryId,
            Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao,
            Categoria = categoria, Requisitos = requisitos
        };
        sheetCopy.Efeitos = efeitos.Select(e => new CharacterSpellAbilityEffect { Id = Guid.NewGuid(), CharacterSpellAbilityId = sheetCopy.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
        db.CharacterSpellAbilities.Add(sheetCopy);

        // R0001 (Banco de Magias): uma criação do zero também grava uma cópia independente no banco do GM,
        // e a ficha guarda o vínculo com ela. Partir de uma entrada do banco (R0003) só copia para a
        // ficha — reusa a entrada, sem duplicá-la; para um jogador ela já é pública na campanha.
        if (!fromBank)
        {
            sheetCopy.CriadaNaFicha = true;
            var bankCopy = new SpellAbilityBankEntry
            {
                Id = Guid.NewGuid(), GmId = campaignGmId, Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao
            };
            bankCopy.Efeitos = efeitos.Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = bankCopy.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
            db.SpellAbilityBankEntries.Add(bankCopy);
            sheetCopy.SourceBankEntryId = bankCopy.Id;

            // Requisitos - Banco de Magias e Habilidades R0007: when the creator is the owning
            // Jogador (not the GM managing the sheet), the bank copy also becomes a public campaign
            // attachment — no GM approval step, per Requisitos - Campanha R0012.
            if (CurrentUserId() != campaignGmId)
            {
                db.CampaignAttachments.Add(new CampaignAttachment
                {
                    Id = Guid.NewGuid(),
                    CampaignId = sheet.CampaignId,
                    SpellAbilityBankEntryId = bankCopy.Id,
                    IsPublic = true
                });
            }
        }

        await db.SaveChangesAsync();
        return Created(string.Empty, await ToResponseAsync(sheetCopy, sheetCopy.Tipo == SpellAbilityTipo.Passiva ? await stats.FichaParaRequisitosAsync(sheet) : null));
    }

    [HttpGet]
    public async Task<ActionResult<List<CharacterSpellAbilityResponse>>> List(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return NotFound(); // NotFound rather than Forbid — avoids confirming the sheet exists to a stranger

        var entries = await db.CharacterSpellAbilities.Include(e => e.Efeitos).Where(e => e.CharacterSheetId == sheetId).ToListAsync();
        // Uma só leitura da ficha, e só se houver Passivas; mapeamento sequencial (um único DbContext).
        var ficha = entries.Any(e => e.Tipo == SpellAbilityTipo.Passiva) ? await stats.FichaParaRequisitosAsync(sheet) : null;
        var result = new List<CharacterSpellAbilityResponse>();
        foreach (var e in entries)
            result.Add(await ToResponseAsync(e, ficha));
        return result;
    }

    [HttpGet("passivas-disponiveis")]
    public async Task<ActionResult<List<PassivaDisponivelResponse>>> PassivasDisponiveis(Guid sheetId)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return NotFound();

        // Mesmo alcance do Add: o GM vê todas as Passivas do próprio banco; o jogador, só as públicas na campanha.
        var query = db.SpellAbilityBankEntries.Where(e => e.GmId == campaignGmId && e.Tipo == SpellAbilityTipo.Passiva);
        if (CurrentUserId() != campaignGmId)
            query = query.Where(e => db.CampaignAttachments.Any(a => a.CampaignId == sheet.CampaignId && a.IsPublic && a.SpellAbilityBankEntryId == e.Id));

        return await PassivasDisponiveisAsync(query, await stats.FichaParaRequisitosAsync(sheet));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<CharacterSpellAbilityResponse>> Update(Guid sheetId, Guid id, UpdateSheetSpellAbilityRequest request)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var entry = await db.CharacterSpellAbilities.Include(e => e.Efeitos).FirstOrDefaultAsync(e => e.Id == id && e.CharacterSheetId == sheetId);
        if (entry is null)
            return NotFound();

        // Só o que foi montado na ficha: uma entrada vinda do banco segue somente leitura (4.b).
        // E só Magia ou Habilidade: uma Racial montada na ficha nunca foi debitada — convertê-la por aqui
        // sairia quase de graça e a remoção devolveria o gasto inteiro.
        if (!entry.CriadaNaFicha || !SaldoDePontosDeIgnicao.Consome(entry.Tipo))
            return BadRequest("Só Magias e Habilidades montadas na ficha podem ser editadas.");

        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo) || !SaldoDePontosDeIgnicao.Consome(tipo))
            return BadRequest("Tipo desconhecido. Use Magia ou Habilidade.");

        var validationError = await EfeitoValidationHelper.ValidarAsync(db, request.Grau, request.Efeitos);
        if (validationError is not null)
            return BadRequest(validationError);

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));

        // Só a diferença: subir o custo precisa caber no saldo; baixar devolve.
        var diferenca = gastoEmPI - entry.GastoEmPI;
        if (SaldoDePontosDeIgnicao.ErroAoDebitar(sheet.PontosDeIgnicaoAtual, diferenca) is { } semSaldo)
            return BadRequest(semSaldo);
        sheet.PontosDeIgnicaoAtual = SaldoDePontosDeIgnicao.Debitar(sheet.PontosDeIgnicaoAtual, diferenca);

        entry.Nome = request.Nome;
        entry.Tipo = tipo;
        entry.Grau = request.Grau;
        entry.Descricao = request.Descricao;
        entry.GastoEmPI = gastoEmPI;
        entry.Custo = SpellAbilityCostCalculator.Custo(gastoEmPI);

        // Troca os Efeitos por inteiro; a cópia que foi para o banco na criação não é tocada.
        db.CharacterSpellAbilityEffects.RemoveRange(entry.Efeitos);
        // Adicionados direto no DbSet (e não pela coleção rastreada): o EF os trataria como alterados e o
        // SaveChanges falharia por concorrência.
        var novos = request.Efeitos
            .Select(e => new CharacterSpellAbilityEffect { Id = Guid.NewGuid(), CharacterSpellAbilityId = entry.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI })
            .ToList();
        db.CharacterSpellAbilityEffects.AddRange(novos);

        await db.SaveChangesAsync();
        return await ToResponseAsync(entry, null);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        var sheet = await db.CharacterSheets.FindAsync(sheetId);
        if (sheet is null)
            return NotFound();

        var campaignGmId = await db.Campaigns.Where(c => c.Id == sheet.CampaignId).Select(c => c.GmId).SingleAsync();
        if (!CharacterSheetAuthorization.CanEdit(CurrentUserId(), sheet.OwnerId, campaignGmId))
            return Forbid();

        var entry = await db.CharacterSpellAbilities.FirstOrDefaultAsync(e => e.Id == id && e.CharacterSheetId == sheetId);
        if (entry is null)
            return NotFound();

        // Remover devolve o Gasto em PI, inclusive o de uma entrada anterior à 1.5.0 (nunca debitada).
        if (SaldoDePontosDeIgnicao.Consome(entry.Tipo))
            sheet.PontosDeIgnicaoAtual = SaldoDePontosDeIgnicao.Debitar(sheet.PontosDeIgnicaoAtual, -entry.GastoEmPI);
        db.CharacterSpellAbilities.Remove(entry); // the bank copy this creation also made is untouched — independent copies
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<CharacterSpellAbilityResponse> ToResponseAsync(CharacterSpellAbility e, FichaParaRequisitos? ficha)
    {
        List<string>? pendentes = null;
        if (e.Tipo == SpellAbilityTipo.Passiva && ficha is not null)
            pendentes = PassivaRequisitosEvaluator.Pendencias(e.Requisitos, ficha, await RequisitosDePassivaMapper.NomeDoHistoricoAsync(db, e.Requisitos), RequisitosDePassivaMapper.NomeDaPericia(await pericias.PorIdAsync())).ToList();
        return new(e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao,
            e.Efeitos.Select(ef => new SpellAbilityEffectResponse(ef.EfeitoNome, ef.Quantidade, ef.CustoPI)).ToList(),
            e.Categoria?.ToString(), RequisitosDePassivaMapper.ToDto(e.Requisitos, await pericias.PorIdAsync()), pendentes, CriadaNaFicha: e.CriadaNaFicha,
            DesabilitadoPor: await DesabilitadoPorAsync(e));
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

    // Condição ativa da ficha que desabilita a linha (null se nenhuma). Racial nunca é desabilitada.
    private async Task<string?> DesabilitadoPorAsync(CharacterSpellAbility e)
    {
        var categoria = e.Tipo switch
        {
            SpellAbilityTipo.Magia => CategoriaDesabilitavel.Magia,
            SpellAbilityTipo.Habilidade => CategoriaDesabilitavel.Habilidade,
            SpellAbilityTipo.Passiva => CategoriaDesabilitavel.Passiva,
            _ => (CategoriaDesabilitavel?)null
        };
        if (categoria is not { } c)
            return null;
        var sheetId = e.CharacterSheetId;
        return (await condicoes.ParaPersonagemAsync(sheetId)).DesabilitadoPor(c, e.Id);
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
