using Microsoft.AspNetCore.Mvc;
using RuinaRPG.Contracts.Condicoes;
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

    /// <summary>
    /// O Mestre da ficha — dono do banco, dos itens e das personalizações de traço racial que valem para ela:
    /// o da campanha (Personagem) ou o da própria ficha (NPC/Criatura).
    /// </summary>
    Task<Guid> MestreAsync(TSheet sheet);

    /// <summary>
    /// A campanha da ficha: a coluna CampaignId (Personagem) ou a da concessão (NPC/Criatura, que não têm a coluna):
    /// o CampaignAttachment de vínculo numa campanha da qual o dono atual é membro — null se não há (ficha do
    /// próprio Mestre, ainda não concedida).
    /// </summary>
    Task<Guid?> CampanhaAsync(TSheet sheet);

    /// <summary>Resposta para acesso negado: Personagem 403 (Forbid), NPC/Criatura 404 (NotFound).</summary>
    ActionResult Negado(ControllerBase controller);

    /// <summary>Condições ativas da ficha (CondicoesAtivasDaFicha.ParaPersonagemAsync / ParaNpcAsync / ParaCriaturaAsync).</summary>
    Task<EfeitosDeCondicao> CondicoesAsync(TSheet sheet);

    /// <summary>As Condições ativas listadas para a ficha (CondicoesAtivasDaFicha.ListarDoPersonagemAsync / ListarDoNpcAsync / ListarDaCriaturaAsync).</summary>
    Task<List<CondicaoAtivaNaFichaResponse>> ListarCondicoesAsync(TSheet sheet);

    /// <summary>Os Encontros com um participante vindo da ficha (a FK do participante tem nome por tipo), sem repetição.</summary>
    Task<List<Guid>> EncontrosAsync(Guid sheetId);

    /// <summary>O saldo de Pontos de Ignição da ficha (PontosDeIgnicaoAtual; PontosDeIgnicao na Criatura).</summary>
    int PontosDeIgnicao(TSheet sheet);

    void DefinirPontosDeIgnicao(TSheet sheet, int valor);

    /// <summary>
    /// O saldo de Pontos de Ignição acompanha a mudança de Nível (Tabela de Níveis, Ficha de Personagem 1.b)?
    /// Personagem e NPC sim; a Criatura não ganha PI por Nível.
    /// </summary>
    bool PontosDeIgnicaoAcompanhamONivel { get; }

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

    /// <summary>
    /// As regras das Afinidades (2.c e R0007: máximo, teto, requisito, visibilidade, baixar um requisito) recusam a
    /// edição deste usuário? Personagem sempre; NPC só para quem não é o GM da ficha — o jogador de um NPC concedido
    /// (Ficha de NPCs R0010: para o GM a ficha só mostra). A Criatura não tem Afinidades por pontos.
    /// </summary>
    bool AfinidadesRecusamForaDasRegras(TSheet sheet, Guid userId);
}
