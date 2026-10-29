using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Domain.Auth;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// "Alterar dono" de uma ficha de NPC/Criatura do GM (Campanha, alterar a posse de ficha concedida): o GM a passa para
/// um membro ativo de uma campanha sua à qual ela está anexada, ou a devolve a si mesmo. A ficha é a mesma; o anexo
/// à campanha fica. A rota fica na subclasse de cada tipo.
/// </summary>
public abstract class DonoDaFichaController<TSheet>(RuinaRpgDbContext db, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha, IFichaConcedivel
{
    protected RuinaRpgDbContext Db { get; } = db;

    public const string RecusaDoDono = "O jogador informado não é membro ativo de uma campanha desta ficha.";

    /// <summary>Os anexos da ficha às campanhas (a coluna do anexo tem nome por tipo).</summary>
    protected abstract IQueryable<CampaignAttachment> Anexos(Guid sheetId);

    [HttpPut("dono")]
    [Authorize(Roles = "GM")]
    public async Task<IActionResult> AlterarDono(Guid sheetId, AlterarDonoRequest request)
    {
        var gmId = CurrentUserId();
        var ficha = await Tipo.CarregarAsync(sheetId);
        if (ficha is null || ficha.GmId != gmId)
            return NotFound();

        if (request.DonoId is null)
        {
            ficha.OwnerId = null;
        }
        else
        {
            if (!Guid.TryParse(request.DonoId, out var donoId) || !await MembroAtivoDeUmaCampanhaDaFichaAsync(sheetId, gmId, donoId))
                return BadRequest(RecusaDoDono);
            ficha.OwnerId = donoId;
            // Ficha concedida é privada (Campanha R0008/R0018/R0019): o Mestre não a vê em Anexos para voltar a publicar o nome ou a imagem.
            foreach (var anexo in await Anexos(sheetId).ToListAsync())
                anexo.IsPublic = anexo.NpcNomePublico = anexo.NpcImagemPublica = anexo.CreatureNomePublico = anexo.CreatureImagemPublica = false;
        }

        await Db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> MembroAtivoDeUmaCampanhaDaFichaAsync(Guid sheetId, Guid gmId, Guid donoId)
    {
        var campanhas = Anexos(sheetId)
            .Join(Db.Campaigns.Where(c => c.GmId == gmId), a => a.CampaignId, c => c.Id, (a, c) => c.Id);
        var ehMembro = await Db.CampaignMembers.AnyAsync(m => m.UserId == donoId && campanhas.Contains(m.CampaignId));
        return ehMembro && await Db.Users.AnyAsync(u => u.Id == donoId && u.Estado == EstadoDaConta.Ativa);
    }
}
