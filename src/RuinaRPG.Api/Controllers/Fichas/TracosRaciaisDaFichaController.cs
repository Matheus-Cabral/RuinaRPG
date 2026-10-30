using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// A escolha dos traços raciais da Variante (Personagem e NPC; a Criatura não tem Variante). A rota fica na subclasse
/// de cada tipo; os contratos são os mesmos para os dois.
/// </summary>
public abstract class TracosRaciaisDaFichaController<TSheet, TLinha>(RuinaRpgDbContext db, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFichaComVariante
    where TLinha : class, ICaracteristicaDaFicha, new()
{
    protected RuinaRpgDbContext Db { get; } = db;

    /// <summary>
    /// Se a Variante atual da ficha ainda tem uma escolha de traço racial pendente (Ruína RPG - Sistema Básico.md §7,
    /// "Característica Gratuita"/"Obrigatória"). Variante null (ainda não escolhida) nunca está pendente: não há o que resolver.
    /// </summary>
    [HttpGet("racial-traits/pending")]
    public async Task<ActionResult<PendingRacialTraitChoiceResponse>> PendingRacialTraitChoice(Guid sheetId)
    {
        if ((await FichaParaLeituraAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        if (sheet.Variante is null)
            return new PendingRacialTraitChoiceResponse(false, [], []);

        var variante = sheet.Variante.Value;
        var slots = await SlotsAsync(sheet, variante);

        var existingCount = await Db.Set<TLinha>().CountAsync(t => t.SheetId == sheetId && t.IsRacial && t.RacialVariante == variante);
        var resolved = RacialTraitChoiceResolver.IsResolved(slots, existingCount);
        var requerEspecificacaoByNome = await RequerEspecificacaoByNomeAsync(slots);

        return new PendingRacialTraitChoiceResponse(!resolved,
            slots.Gratuita.Select(o => new RacialTraitChoiceOptionResponse(o.TraitNome, o.Especificacao, requerEspecificacaoByNome.GetValueOrDefault(o.TraitNome))).ToList(),
            slots.Obrigatoria.Select(o => new RacialTraitChoiceOptionResponse(o.TraitNome, o.Especificacao, requerEspecificacaoByNome.GetValueOrDefault(o.TraitNome))).ToList());
    }

    [HttpPost("racial-traits/resolve")]
    public async Task<IActionResult> ResolveRacialTraitChoice(Guid sheetId, ResolveRacialTraitChoiceRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        if (sheet.Variante is null)
            return BadRequest("A ficha ainda não tem uma Variante escolhida.");

        var variante = sheet.Variante.Value;
        var slots = await SlotsAsync(sheet, variante);

        var resolution = RacialTraitChoiceResolver.Resolve(slots, request.GratuitaTraitNome, request.ObrigatoriaTraitNome);
        if (resolution.Error is not null)
            return BadRequest(resolution.Error);

        for (var i = 0; i < resolution.Grants!.Count; i++)
        {
            var grant = resolution.Grants[i];
            var trait = await Db.Traits.FirstOrDefaultAsync(t => t.Nome == grant.TraitNome && !t.IsDeleted);
            if (trait is null)
                return BadRequest($"Característica \"{grant.TraitNome}\" não encontrada no catálogo.");

            // grant.Especificacao é o que RacialTraitLookup (ou uma personalização do Mestre) fixa para esta opção
            // (ex.: a Desvantagem Elemental de Alóra -> "Fogo") — um valor fixado pela lore sempre vence. Só quando
            // nada está fixado vale o texto livre de quem escolhe (i == 0 é sempre a Gratuita, i == 1 a Obrigatória —
            // ver RacialTraitChoiceResolver.Resolve), com a mesma validação de RequerEspecificacao do AddTrait.
            var userEspecificacao = i == 0 ? request.GratuitaEspecificacao : request.ObrigatoriaEspecificacao;
            var especificacao = grant.Especificacao ?? userEspecificacao;
            if (trait.RequerEspecificacao && string.IsNullOrWhiteSpace(especificacao))
                return BadRequest($"A característica \"{grant.TraitNome}\" exige uma especificação.");

            Db.Set<TLinha>().Add(new TLinha
            {
                Id = Guid.NewGuid(), SheetId = sheetId, TraitId = trait.Id, Polaridade = trait.Polaridade,
                Especificacao = trait.RequerEspecificacao ? especificacao : null, IsRacial = true, RacialVariante = variante,
            });
        }

        await Db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<RacialTraitSlots> SlotsAsync(TSheet sheet, Variante variante)
    {
        var gmId = await Tipo.MestreAsync(sheet);
        var over = await Db.RacialTraitOverrides.FirstOrDefaultAsync(o => o.GmId == gmId && o.Variante == variante);
        return RacialTraitOverrideResolver.Resolve(over, variante);
    }

    private async Task<Dictionary<string, bool>> RequerEspecificacaoByNomeAsync(RacialTraitSlots slots)
    {
        var nomes = slots.Gratuita.Select(o => o.TraitNome).Concat(slots.Obrigatoria.Select(o => o.TraitNome)).Distinct().ToList();
        return await Db.Traits.Where(t => nomes.Contains(t.Nome) && !t.IsDeleted).ToDictionaryAsync(t => t.Nome, t => t.RequerEspecificacao);
    }
}
