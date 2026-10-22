using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/spell-abilities")]
public sealed class NpcSpellAbilitiesController(RuinaRpgDbContext db, NpcSheetStats stats, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<NpcSheet> tipo)
    : MagiasDaFichaController<NpcSheet, NpcSpellAbility, NpcSpellAbilityEffect>(db, pericias, tabelaDeNiveis, tipo)
{
    protected override (string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos) Ler(AddSpellAbilityRequest r) =>
        (r.SourceBankEntryId, r.Nome, r.Tipo, r.Grau, r.Descricao, r.Efeitos);

    protected override NpcSpellAbility Nova(string nome, string descricao) => new() { Nome = nome, Descricao = descricao };

    protected override NpcSpellAbilityEffect NovoEfeito(Guid magiaId, SpellAbilityEffectRequest e) =>
        new() { Id = Guid.NewGuid(), NpcSpellAbilityId = magiaId, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI };

    protected override SpellAbilityResponse Resposta(NpcSpellAbility e, List<SpellAbilityEffectResponse> efeitos, RequisitosDePassivaDto? requisitos, List<string>? pendentes, string? desabilitadoPor) =>
        new(e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao, efeitos, e.Categoria?.ToString(), requisitos, pendentes, CriadaNaFicha: e.CriadaNaFicha, DesabilitadoPor: desabilitadoPor);

    protected override Task<FichaParaRequisitos> FichaParaRequisitosAsync(NpcSheet ficha) => stats.FichaParaRequisitosAsync(ficha);
}
