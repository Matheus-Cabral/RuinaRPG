using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Endpoints da própria ficha (<c>api/&lt;tipo&gt;-sheets/{id}/…</c>) iguais nos três tipos (spec D.3): aviso de subida de
/// nível, valores calculados, Condições ativas e o saldo de Pontos de Ignição. A rota e o que é de um tipo só (criar,
/// ler, editar, listar) ficam na subclasse; o que só NPC e Criatura têm (apagar, Nível, Experiência Atual) fica em
/// ações da subclasse que chamam os métodos protegidos daqui.
/// </summary>
public abstract class FichasController<TSheet, TStats>(RuinaRpgDbContext db, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<TSheet> tipo, TStats stats, EquipmentPenaltyService penalidades, IHubContext<EncounterHub> hub, ILogger logger)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TStats : IFichaStats<TSheet>
{
    protected RuinaRpgDbContext Db { get; } = db;
    protected ITabelaDeNiveis TabelaDeNiveis { get; } = tabelaDeNiveis;
    protected TStats Stats { get; } = stats;

    [HttpGet("{id}/level-up-notice")]
    public async Task<ActionResult<LevelUpNoticeResponse>> LevelUpNotice(Guid id)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        var tabela = await TabelaDeNiveis.ObterAsync();
        return new LevelUpNoticeResponse(tabela.LinhasDeBonusAcumuladas(sheet.LastDismissedLevelUpLevel ?? 0, sheet.Nivel).ToList());
    }

    [HttpPost("{id}/dismiss-level-up-notice")]
    public async Task<IActionResult> DismissLevelUpNotice(Guid id)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        sheet.LastDismissedLevelUpLevel = sheet.Nivel;
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Somente leitura, tudo derivado ao vivo — nada aqui é persistido. O cálculo fica no Stats do tipo
    /// (SubAtributosAsync), o mesmo do retrato FichaParaRequisitos usado pelos requisitos de Passiva.
    /// </summary>
    [HttpGet("{id}/sub-attributes")]
    public async Task<ActionResult<SubAttributesResponse>> SubAttributes(Guid id)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        return await Stats.SubAtributosAsync(sheet, await Tipo.CondicoesAsync(sheet));
    }

    /// <summary>
    /// As Condições ativas na ficha (de todo encontro em andamento em que ela participa), com o que cada uma muda e
    /// os Turnos que faltam. Sempre vazia numa ficha de NPC/Criatura do GM (sem dono): as Condições de um participante
    /// dela só valem no resumo do encontro.
    /// </summary>
    [HttpGet("{id}/condicoes-ativas")]
    public async Task<ActionResult<List<CondicaoAtivaNaFichaResponse>>> CondicoesAtivas(Guid id)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        return await Tipo.ListarCondicoesAsync(sheet);
    }

    /// <summary>Equipamentos em uso cujos Requisitos a ficha não cumpre, com o que falta e a penalidade aplicada.</summary>
    [HttpGet("{id}/equipment-penalties")]
    public async Task<ActionResult<List<PenalidadeAtivaResponse>>> EquipmentPenalties(Guid id)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        return await penalidades.ComoRespostaAsync(await Stats.PenalidadesAtivasAsync(sheet, await Tipo.CondicoesAsync(sheet)));
    }

    /// <summary>
    /// Ficha de Personagem 3.f (herdada por NPC e Criatura): um valor somente leitura por Tipo de Dano, a soma dos
    /// Artefatos equipados cujo Tipo de alvo é Dano e cujo Alvo é aquele Tipo de Dano — "Modificador de dano [tipo] =
    /// Artefato" em Formulas.md.
    /// </summary>
    [HttpGet("{id}/modificador-de-dano")]
    public async Task<ActionResult<ModificadorDeDanoResponse>> ModificadorDeDano(Guid id)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        var efeitos = await Tipo.CondicoesAsync(sheet);
        return efeitos.NoModificadorDeDano(await Stats.ModificadoresAsync(sheet, efeitos));
    }

    /// <summary>
    /// Edição manual do saldo de Pontos de Ignição (1.b) — fora do Update geral de propósito: o formulário inteiro não
    /// diz qual campo o usuário de fato mexeu, e um saldo desatualizado nele apagaria o que o servidor debitou ou
    /// devolveu. Não avisa os Encontros: eles não exibem Pontos de Ignição.
    /// </summary>
    [HttpPut("{id}/pontos-de-ignicao")]
    public async Task<IActionResult> UpdatePontosDeIgnicao(Guid id, [FromBody] int pontos)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        if (pontos < 0)
            return BadRequest("Os Pontos de Ignição não podem ser negativos.");

        Tipo.DefinirPontosDeIgnicao(sheet, pontos);
        await Db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>Apaga a ficha (NPC e Criatura: quem pode editá-la; o Personagem tem o seu, só do Mestre da campanha). Só para NPC/Criatura: não usar no Personagem (o delete dele é só do Mestre).</summary>
    protected async Task<IActionResult> ApagarAsync(Guid id)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        Db.Set<TSheet>().Remove(sheet);
        await Db.SaveChangesAsync();
        return NoContent();
    }

    // NPC e Criatura (ao contrário da Ficha de Personagem, em que o Nível só é calculado da Experiência Atual) mantêm o
    // Nível editável pelo GM — mas os dois campos ainda precisam cair no mesmo par que a tabela de XP daria a partir de
    // qualquer um deles. O Update geral não consegue aplicar essa regra: recebe sempre os dois campos no formulário
    // inteiro, sem como distinguir "o GM acabou de editar o Nível" de "o Nível não foi tocado". Por isso o cliente chama
    // um endpoint ou o outro, dizendo qual campo de fato quer (FichaDeNpc.razor / FichaDeCriatura.razor).

    /// <summary>Define o Nível e leva a Experiência Atual ao mínimo dele. Só para NPC/Criatura: o Nível do Personagem é calculado, não usar nele.</summary>
    protected async Task<IActionResult> DefinirNivelAsync(Guid id, int nivel)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        var tabela = await TabelaDeNiveis.ObterAsync();
        if (nivel < 1 || nivel > tabela.UltimoNivel)
            return BadRequest($"O nível deve estar entre 1 e {tabela.UltimoNivel}.");

        AcompanharNivel(sheet, nivel, tabela);
        sheet.Nivel = nivel;
        sheet.ExperienciaAtual = NivelCalculator.MinXpParaNivel(nivel, tabela.ComoXpPorNivel());
        await Db.SaveChangesAsync();
        await NotificarEncontrosAsync(id);

        return NoContent();
    }

    /// <summary>Define a Experiência Atual e recalcula o Nível por ela. Só para NPC/Criatura: o Nível do Personagem é calculado, não usar nele.</summary>
    protected async Task<IActionResult> DefinirExperienciaAtualAsync(Guid id, int experienciaAtual)
    {
        if ((await FichaEditavelAsync(id)).Negado(out var erro, out var sheet))
            return erro;

        sheet.ExperienciaAtual = experienciaAtual;
        var tabela = await TabelaDeNiveis.ObterAsync();
        var nivel = NivelCalculator.Compute(experienciaAtual, tabela.ComoXpPorNivel());
        AcompanharNivel(sheet, nivel, tabela);
        sheet.Nivel = nivel;
        await Db.SaveChangesAsync();
        await NotificarEncontrosAsync(id);

        return NoContent();
    }

    /// <summary>Avisa (ParticipantsChanged) todo Encontro com um participante vindo da ficha.</summary>
    protected async Task NotificarEncontrosAsync(Guid sheetId)
    {
        foreach (var encounterId in await Tipo.EncontrosAsync(sheetId))
        {
            // Item 3 da auditoria de lacunas: a ficha já está salva aqui — uma falha do hub (ex.: o backplane do
            // SignalR fora do ar por um instante) não pode virar 500 para quem salvou de fato. A sincronização ao
            // vivo só fica para trás até a próxima mudança; nada se perde.
            try
            {
                await hub.Clients.Group($"encounter-{encounterId}").SendAsync("ParticipantsChanged");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify encounter {EncounterId} of a ParticipantsChanged update.", encounterId);
            }
        }
    }

    /// <summary>
    /// Um valor nulo no pedido quer dizer "não definido" e vira null na entidade. Um valor não nulo que não converte é
    /// entrada malformada, não ausente — quem chama devolve 400 em vez de gravar null em silêncio (ex.: uma Variante
    /// inválida pulando LinhagemVarianteValidator).
    /// </summary>
    protected static bool TryParseEnum<TEnum>(string? value, out TEnum? parsed) where TEnum : struct, Enum
    {
        if (value is null)
        {
            parsed = null;
            return true;
        }

        if (Enum.TryParse<TEnum>(value, out var result))
        {
            parsed = result;
            return true;
        }

        parsed = null;
        return false;
    }

    /// <summary>
    /// Nulo, vazio ou só espaços é "sem imagem" (true com imageId nulo). Um texto que não é um Guid válido é recusado
    /// (false) em vez de lançar exceção. Como o TryParseImageId de ItemsController.
    /// </summary>
    protected static bool TryParseImageId(string? raw, out Guid? imageId)
    {
        imageId = null;
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        if (!Guid.TryParse(raw, out var parsed))
            return false;

        imageId = parsed;
        return true;
    }

    /// <summary>O saldo de Pontos de Ignição acompanha a mudança de Nível (sobe e desce, piso 0), onde o tipo ganha PI por Nível.</summary>
    private void AcompanharNivel(TSheet sheet, int nivel, ProgressaoDeNivel tabela)
    {
        if (Tipo.PontosDeIgnicaoAcompanhamONivel)
            Tipo.DefinirPontosDeIgnicao(sheet, PontosDeIgnicaoPorNivel.Aplicar(Tipo.PontosDeIgnicao(sheet), PontosDeIgnicaoPorNivel.Delta(sheet.Nivel, nivel, tabela)));
    }
}
