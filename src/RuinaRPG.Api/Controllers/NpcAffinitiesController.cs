using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Api.Controllers.Fichas;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/npc-sheets/{sheetId}/affinities")]
public sealed class NpcAffinitiesController(RuinaRpgDbContext db, ITipoDeFicha<NpcSheet> tipo)
    : AfinidadesDaFichaController<NpcSheet, NpcAffinity>(db, tipo)
{
    protected override PedidoDeAfinidade LerNova(AddAffinityRequest r) =>
        new(r.Elemento, r.ElementoValor, r.SegundaEssencia, r.SegundaEssenciaValor, r.SubElementoValor, r.Experiencia);

    protected override PedidoDeAfinidade LerAlteracao(UpdateAffinityRequest r) =>
        new(r.Elemento, r.ElementoValor, r.SegundaEssencia, r.SegundaEssenciaValor, r.SubElementoValor, r.Experiencia);

    protected override AffinityResponse Resposta(NpcAffinity a, int? elementoValorComCondicoes) =>
        new(a.Id.ToString(), a.Elemento?.ToString(), a.ElementoValor, a.SegundaEssencia?.ToString(), a.SegundaEssenciaValor, a.SubElemento?.ToString(), a.SubElementoValor, a.Experiencia,
            elementoValorComCondicoes);
}
