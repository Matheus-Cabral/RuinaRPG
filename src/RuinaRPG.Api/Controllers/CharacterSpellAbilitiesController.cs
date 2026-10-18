using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/character-sheets/{sheetId}/spell-abilities")]
public sealed class CharacterSpellAbilitiesController(RuinaRpgDbContext db, CharacterSheetStats stats, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<CharacterSheet> tipo)
    : MagiasDaFichaController<CharacterSheet, CharacterSpellAbility, CharacterSpellAbilityEffect, CharacterSpellAbilityResponse, AddCharacterSpellAbilityRequest>(db, pericias, tabelaDeNiveis, tipo)
{
    protected override (string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos) Ler(AddCharacterSpellAbilityRequest r) =>
        (r.SourceBankEntryId, r.Nome, r.Tipo, r.Grau, r.Descricao, r.Efeitos);

    protected override CharacterSpellAbility Nova(string nome, string descricao) => new() { Nome = nome, Descricao = descricao };

    protected override CharacterSpellAbilityEffect NovoEfeito(Guid magiaId, SpellAbilityEffectRequest e) =>
        new() { Id = Guid.NewGuid(), CharacterSpellAbilityId = magiaId, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI };

    protected override CharacterSpellAbilityResponse Resposta(CharacterSpellAbility e, List<SpellAbilityEffectResponse> efeitos, RequisitosDePassivaDto? requisitos, List<string>? pendentes, string? desabilitadoPor) =>
        new(e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao, efeitos, e.Categoria?.ToString(), requisitos, pendentes, CriadaNaFicha: e.CriadaNaFicha, DesabilitadoPor: desabilitadoPor);

    protected override Task<FichaParaRequisitos> FichaParaRequisitosAsync(CharacterSheet ficha) => stats.FichaParaRequisitosAsync(ficha);

    protected override async Task<Guid> MestreDaFichaAsync(CharacterSheet ficha) =>
        await Db.Campaigns.Where(c => c.Id == ficha.CampaignId).Select(c => c.GmId).SingleAsync();

    protected override Task<Guid?> CampanhaDaFichaAsync(CharacterSheet ficha) => Task.FromResult<Guid?>(ficha.CampaignId);

    protected override int PontosDeIgnicao(CharacterSheet ficha) => ficha.PontosDeIgnicaoAtual;

    protected override void DefinirPontosDeIgnicao(CharacterSheet ficha, int valor) => ficha.PontosDeIgnicaoAtual = valor;
}
