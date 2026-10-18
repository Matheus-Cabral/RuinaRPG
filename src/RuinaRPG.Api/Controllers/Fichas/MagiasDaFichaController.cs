using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Rules.Niveis;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;
using RuinaRPG.Infrastructure.Rules.Niveis;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Api.Controllers.Fichas;

/// <summary>
/// Magias e Habilidades da ficha (incluindo Passivas e Raciais), para os três tipos. A rota
/// (<c>api/&lt;tipo&gt;-sheets/{sheetId}/spell-abilities</c>) fica na subclasse de cada tipo.
/// </summary>
public abstract class MagiasDaFichaController<TSheet, TLinha, TEfeito, TResponse, TAdd>(RuinaRpgDbContext db, IPericiaCatalogo pericias, ITabelaDeNiveis tabelaDeNiveis, ITipoDeFicha<TSheet> tipo)
    : FichaControllerBase<TSheet>(tipo)
    where TSheet : class, IFicha
    where TLinha : class, IMagiaDaFicha<TEfeito>
    where TEfeito : class, IEfeitoDaMagiaDaFicha
{
    protected RuinaRpgDbContext Db { get; } = db;
    protected IPericiaCatalogo Pericias { get; } = pericias;
    protected ITabelaDeNiveis TabelaDeNiveis { get; } = tabelaDeNiveis;

    private DbSet<TLinha> Linhas => Db.Set<TLinha>();

    protected abstract (string? SourceBankEntryId, string? Nome, string? Tipo, int? Grau, string? Descricao, List<SpellAbilityEffectRequest>? Efeitos) Ler(TAdd request);

    /// <summary>Uma linha nova (os membros <c>required</c>); o resto é preenchido pela base.</summary>
    protected abstract TLinha Nova(string nome, string descricao);

    /// <summary>Um Efeito novo da linha <paramref name="magiaId"/> (a FK tem nome por tipo).</summary>
    protected abstract TEfeito NovoEfeito(Guid magiaId, SpellAbilityEffectRequest efeito);

    protected abstract TResponse Resposta(TLinha linha, List<SpellAbilityEffectResponse> efeitos, RequisitosDePassivaDto? requisitos, List<string>? pendentes, string? desabilitadoPor);

    /// <summary>O retrato da ficha SEM penalidades de equipamento usado para conferir Requisitos (o Stats do tipo).</summary>
    protected abstract Task<FichaParaRequisitos> FichaParaRequisitosAsync(TSheet ficha);

    /// <summary>O saldo de Pontos de Ignição da ficha (PontosDeIgnicaoAtual; PontosDeIgnicao na Criatura).</summary>
    protected abstract int PontosDeIgnicao(TSheet ficha);

    protected abstract void DefinirPontosDeIgnicao(TSheet ficha, int valor);

    /// <summary>A cópia que uma criação do zero grava no banco já nasce marcada como de Criatura? Só na Ficha de Criatura.</summary>
    protected virtual bool CopiaNoBancoDeCriatura => false;

    [HttpPost]
    public async Task<ActionResult<TResponse>> Add(Guid sheetId, TAdd request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var pedido = Ler(request);
        var fromScratch = pedido.Nome is not null && pedido.Tipo is not null && pedido.Grau is not null && pedido.Descricao is not null && pedido.Efeitos is not null;
        var fromBank = pedido.SourceBankEntryId is not null;
        if (fromScratch == fromBank) // os dois ou nenhum
            return BadRequest("Informe exatamente um: os campos para montar do zero, ou SourceBankEntryId.");

        // Só o jogador (quem chama não é o Mestre) precisa da campanha — tanto para escolher do banco (só as
        // entradas públicas nela) quanto para publicar depois a cópia que vai para o banco.
        var gmId = await Tipo.MestreAsync(sheet);
        Guid? campaignId = null;
        if (CurrentUserId() != gmId)
            campaignId = await Tipo.CampanhaAsync(sheet);

        string nome; SpellAbilityTipo tipo; int grau; string descricao; List<SpellAbilityEffectRequest> efeitos;
        Guid? sourceBankEntryId = null;
        CategoriaDePassiva? categoria = null; RequisitosDePassiva? requisitos = null;

        if (fromBank)
        {
            if (!Guid.TryParse(pedido.SourceBankEntryId, out var bankEntryId))
                return BadRequest("Entrada do banco não encontrada.");

            // Só entradas do banco do GM desta ficha; um jogador ainda precisa que o GM a tenha anexado
            // como pública à campanha (Requisitos - Ficha de Personagem R0003) — antes, qualquer Guid de
            // qualquer GM era aceito.
            var bankEntry = await Db.SpellAbilityBankEntries.Include(e => e.Efeitos).FirstOrDefaultAsync(e => e.Id == bankEntryId && e.GmId == gmId);
            if (bankEntry is null)
                return BadRequest("Entrada do banco não encontrada.");

            if (CurrentUserId() != gmId
                && (campaignId is null
                    || !await Db.CampaignAttachments.AnyAsync(a => a.CampaignId == campaignId && a.IsPublic && a.SpellAbilityBankEntryId == bankEntryId)))
                return BadRequest("Entrada do banco não encontrada.");

            // Depois das checagens de acesso: uma entrada inalcançável responde "não encontrada" e nunca
            // revela seus requisitos. O bloqueio vale para todos, GM incluído.
            if (bankEntry.Tipo == SpellAbilityTipo.Passiva)
            {
                // Uma Magia/Habilidade comum pode repetir na ficha (cópias independentes); uma Passiva não —
                // ela é um estado ligado/desligado por Categoria, então a mesma entrada do banco só pode
                // virar uma cópia por ficha.
                if (await Linhas.AnyAsync(e => e.SheetId == sheetId && e.SourceBankEntryId == bankEntryId))
                    return BadRequest("Esta Passiva já está na ficha.");

                var pendencias = PassivaRequisitosEvaluator.Pendencias(bankEntry.Requisitos, await FichaParaRequisitosAsync(sheet),
                    await RequisitosDePassivaMapper.NomeDoHistoricoAsync(Db, bankEntry.Requisitos), RequisitosDePassivaMapper.NomeDaPericia(await Pericias.PorIdAsync()));
                if (pendencias.Count > 0)
                    return BadRequest("Requisitos não cumpridos: " + string.Join(", ", pendencias));

                // Limite de Passivas por Categoria do Nível: Criatura não tem Tabela de Níveis.
                if (Tipo.AplicaLimitesDeNivel && bankEntry.Categoria is { } cat)
                {
                    var naFicha = await Linhas
                        .Where(e => e.SheetId == sheetId && e.Tipo == SpellAbilityTipo.Passiva && e.Categoria != null)
                        .Select(e => e.Categoria!.Value).ToListAsync();
                    if (LimitesDeNivel.Passivas(cat, naFicha, await TabelaDeNiveis.ObterAsync(), sheet.Nivel) is { } erroDeLimite)
                        return BadRequest(erroDeLimite);
                }
            }
            categoria = bankEntry.Categoria; requisitos = bankEntry.Requisitos;

            nome = bankEntry.Nome; tipo = bankEntry.Tipo; grau = bankEntry.Grau; descricao = bankEntry.Descricao;
            efeitos = bankEntry.Efeitos.Select(e => new SpellAbilityEffectRequest(e.EfeitoNome, e.Quantidade, e.CustoPI)).ToList();
            sourceBankEntryId = bankEntryId;
        }
        else
        {
            if (!Enum.TryParse<SpellAbilityTipo>(pedido.Tipo, out tipo))
                return BadRequest("Tipo desconhecido. Use Magia, Habilidade ou Racial.");
            if (tipo == SpellAbilityTipo.Passiva)
                return BadRequest("Passivas só são cadastradas no Banco de Magias e Habilidades.");
            nome = pedido.Nome!; grau = pedido.Grau!.Value; descricao = pedido.Descricao!; efeitos = pedido.Efeitos!;

            var validationError = await EfeitoValidationHelper.ValidarAsync(Db, grau, efeitos);
            if (validationError is not null)
                return BadRequest(validationError);
        }

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(efeitos.Select(e => e.CustoPI));
        var custo = SpellAbilityCostCalculator.Custo(gastoEmPI);

        // 4.b: o Gasto em PI sai do saldo da ficha; sem saldo, nem o GM cria. Passivas não têm PI.
        if (SaldoDePontosDeIgnicao.Consome(tipo))
        {
            if (SaldoDePontosDeIgnicao.ErroAoDebitar(PontosDeIgnicao(sheet), gastoEmPI) is { } semSaldo)
                return BadRequest(semSaldo);
            DefinirPontosDeIgnicao(sheet, SaldoDePontosDeIgnicao.Debitar(PontosDeIgnicao(sheet), gastoEmPI));
        }

        var sheetCopy = Nova(nome, descricao);
        sheetCopy.Id = Guid.NewGuid(); sheetCopy.SheetId = sheetId; sheetCopy.SourceBankEntryId = sourceBankEntryId;
        sheetCopy.Tipo = tipo; sheetCopy.Grau = grau; sheetCopy.GastoEmPI = gastoEmPI; sheetCopy.Custo = custo;
        sheetCopy.Categoria = categoria; sheetCopy.Requisitos = requisitos;
        sheetCopy.Efeitos = efeitos.Select(e => NovoEfeito(sheetCopy.Id, e)).ToList();
        Linhas.Add(sheetCopy);

        // R0001 (Banco de Magias): uma criação do zero também grava uma cópia independente no banco do GM,
        // e a ficha guarda o vínculo com ela. Criada numa Ficha de Criatura, a entrada já nasce marcada como
        // Magia/Habilidade de Criatura. Partir de uma entrada do banco (R0003) só copia para a ficha — reusa
        // a entrada, sem duplicá-la; para um jogador ela já é pública na campanha.
        if (!fromBank)
        {
            sheetCopy.CriadaNaFicha = true;
            var bankCopy = new SpellAbilityBankEntry
            {
                Id = Guid.NewGuid(), GmId = gmId, Nome = nome, Tipo = tipo, Grau = grau, GastoEmPI = gastoEmPI, Custo = custo, Descricao = descricao, DeCriatura = CopiaNoBancoDeCriatura
            };
            bankCopy.Efeitos = efeitos.Select(e => new SpellAbilityBankEffect { Id = Guid.NewGuid(), SpellAbilityBankEntryId = bankCopy.Id, EfeitoNome = e.EfeitoNome, Quantidade = e.Quantidade, CustoPI = e.CustoPI }).ToList();
            Db.SpellAbilityBankEntries.Add(bankCopy);
            sheetCopy.SourceBankEntryId = bankCopy.Id;

            // Requisitos - Banco de Magias e Habilidades R0007: quando quem cria é o Jogador dono (e não o GM
            // que gerencia a ficha), a cópia no banco também vira um anexo público da campanha — sem aprovação
            // do GM, por Requisitos - Campanha R0012.
            if (CurrentUserId() != gmId && campaignId is not null)
            {
                Db.CampaignAttachments.Add(new CampaignAttachment
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaignId.Value,
                    SpellAbilityBankEntryId = bankCopy.Id,
                    IsPublic = true
                });
            }
        }

        await Db.SaveChangesAsync();
        return Created(string.Empty, await RespostaAsync(sheet, sheetCopy, sheetCopy.Tipo == SpellAbilityTipo.Passiva ? await FichaParaRequisitosAsync(sheet) : null));
    }

    [HttpGet]
    public async Task<ActionResult<List<TResponse>>> List(Guid sheetId)
    {
        if ((await FichaParaLeituraAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var entries = await Linhas.Include(e => e.Efeitos).Where(e => e.SheetId == sheetId).ToListAsync();
        // Uma só leitura da ficha, e só se houver Passivas; mapeamento sequencial (um único DbContext).
        var ficha = entries.Any(e => e.Tipo == SpellAbilityTipo.Passiva) ? await FichaParaRequisitosAsync(sheet) : null;
        var result = new List<TResponse>();
        foreach (var e in entries)
            result.Add(await RespostaAsync(sheet, e, ficha));
        return result;
    }

    [HttpGet("passivas-disponiveis")]
    public async Task<ActionResult<List<PassivaDisponivelResponse>>> PassivasDisponiveis(Guid sheetId)
    {
        if ((await FichaParaLeituraAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        // Mesmo alcance do Add: o GM vê todas as Passivas do próprio banco; o jogador, só as públicas na
        // campanha — sem campanha, nenhuma.
        var gmId = await Tipo.MestreAsync(sheet);
        var query = Db.SpellAbilityBankEntries.Where(e => e.GmId == gmId && e.Tipo == SpellAbilityTipo.Passiva);
        if (CurrentUserId() != gmId)
        {
            var campaignId = await Tipo.CampanhaAsync(sheet);
            if (campaignId is null)
                return new List<PassivaDisponivelResponse>();
            query = query.Where(e => Db.CampaignAttachments.Any(a => a.CampaignId == campaignId && a.IsPublic && a.SpellAbilityBankEntryId == e.Id));
        }

        return await PassivasDisponiveisAsync(query, await FichaParaRequisitosAsync(sheet));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TResponse>> Update(Guid sheetId, Guid id, UpdateSheetSpellAbilityRequest request)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var entry = await Linhas.Include(e => e.Efeitos).FirstOrDefaultAsync(e => e.Id == id && e.SheetId == sheetId);
        if (entry is null)
            return NotFound();

        // Só o que foi montado na ficha: uma entrada vinda do banco segue somente leitura (4.b).
        // E só Magia ou Habilidade: uma Racial montada na ficha nunca foi debitada — convertê-la por aqui
        // sairia quase de graça e a remoção devolveria o gasto inteiro.
        if (!entry.CriadaNaFicha || !SaldoDePontosDeIgnicao.Consome(entry.Tipo))
            return BadRequest("Só Magias e Habilidades montadas na ficha podem ser editadas.");

        if (!Enum.TryParse<SpellAbilityTipo>(request.Tipo, out var tipo) || !SaldoDePontosDeIgnicao.Consome(tipo))
            return BadRequest("Tipo desconhecido. Use Magia ou Habilidade.");

        var validationError = await EfeitoValidationHelper.ValidarAsync(Db, request.Grau, request.Efeitos);
        if (validationError is not null)
            return BadRequest(validationError);

        var gastoEmPI = SpellAbilityCostCalculator.GastoEmPI(request.Efeitos.Select(e => e.CustoPI));

        // Só a diferença: subir o custo precisa caber no saldo; baixar devolve.
        var diferenca = gastoEmPI - entry.GastoEmPI;
        if (SaldoDePontosDeIgnicao.ErroAoDebitar(PontosDeIgnicao(sheet), diferenca) is { } semSaldo)
            return BadRequest(semSaldo);
        DefinirPontosDeIgnicao(sheet, SaldoDePontosDeIgnicao.Debitar(PontosDeIgnicao(sheet), diferenca));

        entry.Nome = request.Nome;
        entry.Tipo = tipo;
        entry.Grau = request.Grau;
        entry.Descricao = request.Descricao;
        entry.GastoEmPI = gastoEmPI;
        entry.Custo = SpellAbilityCostCalculator.Custo(gastoEmPI);

        // Troca os Efeitos por inteiro; a cópia que foi para o banco na criação não é tocada.
        Db.Set<TEfeito>().RemoveRange(entry.Efeitos);
        // Adicionados direto no DbSet (e não pela coleção rastreada): o EF os trataria como alterados e o
        // SaveChanges falharia por concorrência.
        var novos = request.Efeitos.Select(e => NovoEfeito(entry.Id, e)).ToList();
        Db.Set<TEfeito>().AddRange(novos);

        await Db.SaveChangesAsync();
        return await RespostaAsync(sheet, entry, null);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid sheetId, Guid id)
    {
        if ((await FichaEditavelAsync(sheetId)).Negado(out var erro, out var sheet))
            return erro;

        var entry = await Linhas.FirstOrDefaultAsync(e => e.Id == id && e.SheetId == sheetId);
        if (entry is null)
            return NotFound();

        // Remover devolve o Gasto em PI, inclusive o de uma entrada anterior à 1.5.0 (nunca debitada).
        if (SaldoDePontosDeIgnicao.Consome(entry.Tipo))
            DefinirPontosDeIgnicao(sheet, SaldoDePontosDeIgnicao.Debitar(PontosDeIgnicao(sheet), -entry.GastoEmPI));
        Linhas.Remove(entry); // a cópia no banco feita na criação não é tocada — cópias independentes
        await Db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Como <see cref="FichaControllerBase{TSheet}.FichaEditavelAsync"/>, mas o acesso negado é 404 nos três tipos:
    /// os GET de Magias do Personagem negam com 404, e não com o 403 dos seus POST/PUT/DELETE (divergência
    /// acidental mantida — ver "Divergências encontradas" na spec D).
    /// </summary>
    private async Task<AcessoAFicha> FichaParaLeituraAsync(Guid sheetId)
    {
        var ficha = await Tipo.CarregarAsync(sheetId);
        if (ficha is null || !await Tipo.PodeEditarAsync(ficha, CurrentUserId()))
            return new AcessoAFicha(NotFound());
        return new AcessoAFicha(ficha);
    }

    private async Task<TResponse> RespostaAsync(TSheet sheet, TLinha e, FichaParaRequisitos? ficha)
    {
        List<string>? pendentes = null;
        if (e.Tipo == SpellAbilityTipo.Passiva && ficha is not null)
            pendentes = PassivaRequisitosEvaluator.Pendencias(e.Requisitos, ficha, await RequisitosDePassivaMapper.NomeDoHistoricoAsync(Db, e.Requisitos), RequisitosDePassivaMapper.NomeDaPericia(await Pericias.PorIdAsync())).ToList();
        var efeitos = e.Efeitos.Select(ef => new SpellAbilityEffectResponse(ef.EfeitoNome, ef.Quantidade, ef.CustoPI)).ToList();
        return Resposta(e, efeitos, RequisitosDePassivaMapper.ToDto(e.Requisitos, await Pericias.PorIdAsync()), pendentes, await DesabilitadoPorAsync(sheet, e));
    }

    private async Task<List<PassivaDisponivelResponse>> PassivasDisponiveisAsync(IQueryable<SpellAbilityBankEntry> query, FichaParaRequisitos ficha)
    {
        var entradas = await query.OrderBy(e => e.Nome).ToListAsync();
        var result = new List<PassivaDisponivelResponse>();
        foreach (var e in entradas)
        {
            var pendencias = PassivaRequisitosEvaluator.Pendencias(e.Requisitos, ficha, await RequisitosDePassivaMapper.NomeDoHistoricoAsync(Db, e.Requisitos), RequisitosDePassivaMapper.NomeDaPericia(await Pericias.PorIdAsync()));
            result.Add(new PassivaDisponivelResponse(
                new SpellAbilityEntryResponse(e.Id.ToString(), e.Nome, e.Tipo.ToString(), e.Grau, e.GastoEmPI, e.Custo, e.Descricao, [], e.DeCriatura,
                    e.Categoria?.ToString(), RequisitosDePassivaMapper.ToDto(e.Requisitos, await Pericias.PorIdAsync())),
                pendencias.ToList()));
        }
        return result;
    }

    // Condição ativa da ficha que desabilita a linha (null se nenhuma). Racial nunca é desabilitada.
    private async Task<string?> DesabilitadoPorAsync(TSheet sheet, TLinha e)
    {
        var categoria = e.Tipo switch
        {
            SpellAbilityTipo.Magia => CategoriaDesabilitavel.Magia,
            SpellAbilityTipo.Habilidade => CategoriaDesabilitavel.Habilidade,
            SpellAbilityTipo.Passiva => CategoriaDesabilitavel.Passiva,
            _ => (CategoriaDesabilitavel?)null
        };
        if (categoria is not { } c)
            return null;
        return (await Tipo.CondicoesAsync(sheet)).DesabilitadoPor(c, e.Id);
    }
}
