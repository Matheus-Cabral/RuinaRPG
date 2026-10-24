using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/affinities")]
public sealed class CharacterAffinitiesController(RuinaRpgDbContext db, IRulesDataProvider rules, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CharacterSheet> tipo)
    : AfinidadesDaFichaController<CharacterSheet, CharacterAffinity>(db, tipo)
{
    protected override PedidoDeAfinidade LerNova(AddAffinityRequest r) =>
        new(r.Elemento, r.ElementoValor, r.SegundaEssencia, r.SegundaEssenciaValor, r.SubElementoValor, r.Experiencia);

    protected override PedidoDeAfinidade LerAlteracao(UpdateAffinityRequest r) =>
        new(r.Elemento, r.ElementoValor, r.SegundaEssencia, r.SegundaEssenciaValor, r.SubElementoValor, r.Experiencia);

    protected override AffinityResponse Resposta(CharacterAffinity a, int? elementoValorComCondicoes) =>
        new(a.Id.ToString(), a.Elemento?.ToString(), a.ElementoValor, a.SegundaEssencia?.ToString(), a.SegundaEssenciaValor, a.SubElemento?.ToString(), a.SubElementoValor, a.Experiencia,
            elementoValorComCondicoes);

    // Vocação Arcana (2.c): só bloqueia a alteração que faz o gasto subir acima do máximo — uma
    // ficha que já está acima (o máximo caiu depois) ainda pode baixar o gasto ou mexer em campos
    // que não custam pontos.
    protected override async Task<string?> ExcedeVocacaoArcanaAsync(CharacterSheet sheet, LinhaDeAfinidade novaLinha, Guid? substituindoId)
    {
        var existentes = await Db.CharacterAffinities.Where(a => a.SheetId == sheet.Id)
            .Select(a => new { a.Id, Linha = new LinhaDeAfinidade(a.Elemento, a.ElementoValor, a.SubElemento, a.SubElementoValor, a.SegundaEssencia, a.SegundaEssenciaValor) })
            .ToListAsync();

        var gastoAntes = VocacaoArcanaCalculator.Gasto(existentes.Select(e => e.Linha));
        var gastoDepois = VocacaoArcanaCalculator.Gasto(existentes.Where(e => e.Id != substituindoId).Select(e => e.Linha).Append(novaLinha));

        var tabela = await tabelaDeNiveis.ObterAsync();
        var eapAtual = EapCalculator.Compute(sheet.Nivel, sheet.NucleosRankF, sheet.NucleosRankE, sheet.NucleosRankD, sheet.NucleosRankC, sheet.NucleosRankB, sheet.NucleosRankA, sheet.NucleosRankS, tabela.ComoEapPorNivel());
        var graduacao = sheet.Vocacao is { } vocacao ? GraduacaoCalculator.Compute(vocacao, eapAtual, sheet.PossuiCoracaoDeMana, rules.CirculoGrauPorEap) : 0;
        var maxima = VocacaoArcanaCalculator.Maxima(sheet.Vocacao, graduacao, sheet.AfinidadeAdicional, rules.CirculoGrauPorEap);

        return gastoDepois > gastoAntes && gastoDepois > maxima
            ? $"Pontos de Vocação Arcana insuficientes: essa alteração gastaria {gastoDepois} de {maxima}."
            : null;
    }
}
