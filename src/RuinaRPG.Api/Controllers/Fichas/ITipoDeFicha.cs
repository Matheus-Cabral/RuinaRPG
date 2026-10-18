using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Infrastructure.Fichas;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>O que muda entre Personagem, NPC e Criatura para o código genérico das fichas (spec D.2).</summary>
public interface ITipoDeFicha<TSheet> where TSheet : class, IFicha
{
    /// <summary>A ficha, ou null se não existe.</summary>
    Task<TSheet?> CarregarAsync(Guid sheetId);

    /// <summary>Se o usuário pode editar a ficha (Personagem: dono ou Mestre da campanha; NPC/Criatura: GrantedSheetAuthorization).</summary>
    Task<bool> PodeEditarAsync(TSheet sheet, Guid userId);

    /// <summary>Resposta para acesso negado: Personagem 403 (Forbid), NPC/Criatura 404 (NotFound).</summary>
    ActionResult Negado(ControllerBase controller);

    /// <summary>Condições ativas da ficha (CondicoesAtivasDaFicha.ParaPersonagemAsync / ParaNpcAsync / ParaCriaturaAsync).</summary>
    Task<EfeitosDeCondicao> CondicoesAsync(TSheet sheet);

    /// <summary>O Histórico da ficha, que dá o bônus +6/+3 de Perícia. Criatura não tem Histórico: sempre null.</summary>
    Guid? HistoricoId(TSheet sheet);

    /// <summary>Limites de Nível (Tabela de Níveis da Auditoria) valem para este tipo? Personagem e NPC sim, Criatura não.</summary>
    bool AplicaLimitesDeNivel { get; }

    /// <summary>Só Perícias com DisponivelParaCriaturas (Auditoria de Regras R0005)? Só Criatura.</summary>
    bool SoPericiasDeCriatura { get; }

    /// <summary>
    /// Subir o Gasto de um Atributo além dos pontos que o Nível já deu é recusado (Ficha de Personagem 2.a)?
    /// Só Personagem: no NPC e na Criatura o orçamento só informa (Ficha de NPCs R0007).
    /// </summary>
    bool RecusaAtributosAcimaDoOrcamento { get; }
}
