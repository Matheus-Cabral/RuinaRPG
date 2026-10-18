using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/creature-sheets/{sheetId}/spell-abilities")]
public sealed class CreatureSpellAbilitiesController(RuinaRpgDbContext db, CreatureSheetStats stats, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CreatureSheet> tipo)
    : MagiasDaFichaController<CreatureSheet, CreatureSpellAbility, CreatureSpellAbilityEffect, CreatureSpellAbilityResponse, AddCreatureSpellAbilityRequest>(db, pericias, tabelaDeNiveis, tipo)
{
    protected override (string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos) Ler(AddCreatureSpellAbilityRequest r) =>
        (r.SourceBankEntryId, r.Nome, r.Tipo, r.Grau, r.Descricao, r.Efeitos);

    protected override CreatureSpellAbility Nova(string nome, string descricao) => new() { Nome = nome, Descricao = descricao };

    protected override CreatureSpellAbilityEffect NovoEfeito(Guid magiaId, SpellAbilityEffectRequest e) =>
        new() { Id = Guid.NewGuid(), CreatureSpellAbilityId = magiaId, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI };

    protected override CreatureSpellAbilityResponse Resposta(CreatureSpellAbility e, List<SpellAbilityEffectResponse> efeitos, RequisitosDePassivaDto? requisitos, List<string>? pendentes, string? desabilitadoPor) =>
        new(e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao, efeitos, e.Categoria?.ToString(), requisitos, pendentes, CriadaNaFicha: e.CriadaNaFicha, DesabilitadoPor: desabilitadoPor);

    protected override Task<FichaParaRequisitos> FichaParaRequisitosAsync(CreatureSheet ficha) => stats.FichaParaRequisitosAsync(ficha);

    // Criada numa Ficha de Criatura, a cópia no banco já nasce marcada como Magia/Habilidade de Criatura.
    protected override bool CopiaNoBancoDeCriatura => true;
}
