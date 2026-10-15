using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Infrastructure.SheetLogs;

/// <summary>
/// Grava o log de ficha (Campanha R0016, Técnico R0012): a cada salvamento, lê do ChangeTracker o que mudou nas
/// tabelas de ficha (SheetLogMap) e acrescenta as linhas de SheetLogEntry ao MESMO salvamento — log e alteração
/// são gravados ou falham juntos, e nenhum controller de ficha precisa saber do log. Um defeito ao MONTAR as
/// linhas é registrado em log de aplicação e o salvamento segue sem elas: perder uma linha do log é aceitável,
/// derrubar o salvamento de uma ficha não. Sem usuário autenticado (seeders, inicialização, CLIs) nada é registrado. ExecuteUpdate/ExecuteDelete e SQL direto não passam por aqui.
/// As mudanças de Vitalidade, Foco e Adrenalina vão também, pelo mesmo caminho, para o log dos encontros em
/// andamento de que a ficha participa (EncounterLogDaFicha). O que o servidor escreve por conta própria dentro da
/// ação (SheetLogDoServidor) entra no mesmo salvamento: as escritas marcadas saem com Automatico, e as linhas
/// explícitas ("Condição X aplicada") são assinadas como as outras.
/// </summary>
public sealed class SheetLogInterceptor(ICurrentActor currentActor, SheetLogDoServidor doServidor, ILogger<SheetLogInterceptor> logger) : SaveChangesInterceptor
{
    private const string CampoEfeitos = "Efeitos";

    // Interceptor com escopo de requisição = um contexto: as linhas que ESTE salvamento acrescentou (do log de
    // ficha e do log do encontro), para soltá-las se ele falhar.
    private List<object> _acrescentadas = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        RegistrarAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await RegistrarAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => SoltarAcrescentadas(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        SoltarAcrescentadas(eventData.Context);
        return Task.CompletedTask;
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        _acrescentadas = [];
        doServidor.Limpar();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        _acrescentadas = [];
        doServidor.Limpar();
        return ValueTask.FromResult(result);
    }

    /// <summary>Salvamento que falhou: as linhas do log não podem ficar no contexto, ou uma nova tentativa as gravaria em dobro.</summary>
    private void SoltarAcrescentadas(DbContext? context)
    {
        foreach (var linha in _acrescentadas)
            if (context?.Entry(linha) is { State: EntityState.Added } entry)
                entry.State = EntityState.Detached;
        _acrescentadas = [];
    }

    /// <summary>A ficha de uma linha do log, já resolvida: de que tipo é, qual é e se quem salva é o GM dela.</summary>
    private sealed record Ficha(SheetLogKind Kind, Guid Id, bool ActorIsGm);

    private async Task RegistrarAsync(DbContext? context, CancellationToken ct)
    {
        _acrescentadas = [];
        var linhas = await SemDerrubarOSalvamentoAsync("sheet log", () => MontarLinhasAsync(context, ct));
        if (linhas.Count == 0)
            return;
        var db = (RuinaRpgDbContext)context!;
        // Guarda própria: um defeito ao montar as linhas do encontro não leva junto as do log de ficha.
        var doEncontro = await SemDerrubarOSalvamentoAsync("encounter log", () => EncounterLogDaFicha.MontarAsync(db, linhas, ct));
        // A inserção em si fica no MESMO SaveChanges do usuário: log e alteração são gravados ou falham juntos.
        db.SheetLogEntries.AddRange(linhas);
        db.EncounterLogEntries.AddRange(doEncontro);
        _acrescentadas = [.. linhas, .. doEncontro];
    }

    /// <summary>Monta linhas de log sem nunca derrubar o salvamento: um defeito vai para o log de aplicação e nenhuma linha é acrescentada.</summary>
    private async Task<List<T>> SemDerrubarOSalvamentoAsync<T>(string log, Func<Task<List<T>>> montar)
    {
        try
        {
            return await montar();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build the {Log} entries; saving without them", log);
            return [];
        }
    }

    /// <summary>
    /// O que as etapas de um salvamento compartilham: as entidades de ficha rastreadas (por chave), as fichas
    /// criadas ou apagadas nele e as já resolvidas — cada ficha é resolvida uma vez só por salvamento.
    /// </summary>
    private sealed class Salvamento(RuinaRpgDbContext db, Guid actorId, List<EntityEntry> rastreadas, List<EntityEntry> alteradas, SheetLogDoServidor doServidor, CancellationToken ct)
    {
        private readonly Dictionary<(SheetLogKind, Guid), Ficha?> _fichas = [];
        private readonly HashSet<Guid> _fichasNovas = FichasEm(alteradas, EntityState.Added);
        private readonly HashSet<Guid> _fichasApagadas = FichasEm(alteradas, EntityState.Deleted);

        public CancellationToken Ct => ct;
        public List<EntityEntry> Alteradas => alteradas;
        public Dictionary<(Type, Guid), EntityEntry> PorChave { get; } = rastreadas.ToDictionary(e => (e.Metadata.ClrType, Id(e)));
        public SheetLogNames Nomes { get; } = new(db);
        public SheetLogDoServidor DoServidor => doServidor;

        public bool CriadaOuApagada(Guid sheetId) => _fichasNovas.Contains(sheetId) || _fichasApagadas.Contains(sheetId);

        public async Task<Ficha?> FichaAsync(SheetLogKind kind, Guid sheetId)
        {
            if (!_fichas.TryGetValue((kind, sheetId), out var ficha))
                _fichas[(kind, sheetId)] = ficha = await ResolverFichaAsync(db, PorChave, kind, sheetId, actorId, ct);
            return ficha;
        }
    }

    private async Task<List<SheetLogEntry>> MontarLinhasAsync(DbContext? context, CancellationToken ct)
    {
        // Este método roda em todo salvamento da aplicação: sai o quanto antes quando não há o que registrar.
        if (currentActor.UserId is not { } actorId || context is not RuinaRpgDbContext db)
            return [];
        var (rastreadas, alteradas) = EntidadesDeFicha(db);
        if (alteradas.Count == 0 && doServidor.Explicitas.Count == 0)
            return [];

        var salvamento = new Salvamento(db, actorId, rastreadas, alteradas, doServidor, ct);
        var (linhas, efeitos) = await LinhasDasEntidadesAsync(salvamento);
        linhas.AddRange(await LinhasDosEfeitosAsync(salvamento, efeitos));
        linhas.AddRange(await LinhasExplicitasAsync(salvamento));
        if (linhas.Count > 0)
            await AssinarAsync(db, linhas, actorId, ct);
        return linhas;
    }

    /// <summary>
    /// Fotografia do ChangeTracker tirada ANTES de acrescentar as linhas do log: as entidades de ficha rastreadas
    /// e, delas, as alteradas. As não alteradas ficam: servem para achar a ficha ou a Magia já carregada sem consultar o banco.
    /// </summary>
    private static (List<EntityEntry> Rastreadas, List<EntityEntry> Alteradas) EntidadesDeFicha(RuinaRpgDbContext db)
    {
        var rastreadas = db.ChangeTracker.Entries().Where(e => SheetLogMap.Entidades.ContainsKey(e.Metadata.ClrType)).ToList();
        var alteradas = rastreadas.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        return (rastreadas, alteradas);
    }

    /// <summary>As linhas de cada entidade alterada. Os Efeitos de Magia/Habilidade saem à parte, agrupados pela Magia, para LinhasDosEfeitosAsync.</summary>
    private static async Task<(List<SheetLogEntry> Linhas, Dictionary<(Type Pai, Guid PaiId), List<EntityEntry>> Efeitos)> LinhasDasEntidadesAsync(Salvamento salvamento)
    {
        var linhas = new List<SheetLogEntry>();
        var efeitos = new Dictionary<(Type Pai, Guid PaiId), List<EntityEntry>>();
        foreach (var entry in salvamento.Alteradas)
        {
            var map = SheetLogMap.Entidades[entry.Metadata.ClrType];
            if (map.ParentType is not null)
            {
                // Efeitos de Magia/Habilidade nunca viram linha própria: entram na linha da Magia (LinhasDosEfeitosAsync).
                var chave = (map.ParentType, (Guid)Valor(entry, map.ParentIdProperty!)!);
                if (!efeitos.TryGetValue(chave, out var grupo))
                    efeitos[chave] = grupo = [];
                grupo.Add(entry);
                continue;
            }

            var sheetId = map.IsSheet ? Id(entry) : (Guid)Valor(entry, map.SheetIdProperty!)!;
            // Criar a ficha é uma linha só (as filhas semeadas ou copiadas com ela não contam); apagar não é
            // nenhuma — a FK em cascata leva o log junto, e uma linha nova apontaria para uma ficha que some.
            if (map.IsSheet ? entry.State == EntityState.Deleted : salvamento.CriadaOuApagada(sheetId))
                continue;
            if (await salvamento.FichaAsync(map.Kind, sheetId) is not { } ficha)
                continue;

            if (entry.State == EntityState.Modified)
                linhas.AddRange(await AlteracoesAsync(entry, map, ficha, salvamento.Nomes, salvamento.DoServidor, salvamento.Ct));
            else if (map.IsSheet)
                linhas.Add(Linha(ficha, map.Secao, SheetLogAcao.Criou));
            else
                linhas.Add(Linha(ficha, map.Secao, entry.State == EntityState.Added ? SheetLogAcao.Adicionou : SheetLogAcao.Removeu,
                    itemNome: await NomeDaLinhaAsync(entry, map, salvamento.Nomes, salvamento.Ct)));
        }
        return (linhas, efeitos);
    }

    /// <summary>Uma linha "Efeitos" por Magia/Habilidade cujo conjunto de Efeitos mudou neste salvamento.</summary>
    private static async Task<List<SheetLogEntry>> LinhasDosEfeitosAsync(Salvamento salvamento, Dictionary<(Type Pai, Guid PaiId), List<EntityEntry>> efeitos)
    {
        var linhas = new List<SheetLogEntry>();
        foreach (var ((tipoDoPai, paiId), grupo) in efeitos)
        {
            // Os controllers sempre editam os Efeitos com a Magia carregada (Include): sem ela rastreada não há
            // como nomear a linha, e nada é registrado. Magia criada ou removida neste salvamento: a linha dela já diz tudo.
            if (!salvamento.PorChave.TryGetValue((tipoDoPai, paiId), out var pai) || pai.State is EntityState.Added or EntityState.Deleted || !EfeitosMudaram(grupo))
                continue;
            var mapDoPai = SheetLogMap.Entidades[tipoDoPai];
            var sheetId = (Guid)Valor(pai, mapDoPai.SheetIdProperty!)!;
            if (salvamento.CriadaOuApagada(sheetId) || await salvamento.FichaAsync(mapDoPai.Kind, sheetId) is not { } ficha)
                continue;
            var nome = SheetLogValues.Render(Valor(pai, mapDoPai.ItemNomeProperty!));
            linhas.Add(Linha(ficha, mapDoPai.Secao, SheetLogAcao.Alterou, itemNome: nome, campo: CampoEfeitos));
        }
        return linhas;
    }

    /// <summary>As linhas que o servidor registrou por conta própria; uma ficha fora do log (NPC/Criatura do GM, ou apagada) não ganha a sua.</summary>
    private static async Task<List<SheetLogEntry>> LinhasExplicitasAsync(Salvamento salvamento)
    {
        var linhas = new List<SheetLogEntry>();
        foreach (var explicita in salvamento.DoServidor.Explicitas)
        {
            if (salvamento.CriadaOuApagada(explicita.SheetId) || await salvamento.FichaAsync(explicita.Kind, explicita.SheetId) is not { } ficha)
                continue;
            var linha = Linha(ficha, explicita.Secao, explicita.Acao, itemNome: explicita.ItemNome);
            linha.Automatico = explicita.Automatico;
            linhas.Add(linha);
        }
        return linhas;
    }

    /// <summary>Um instante e um autor por salvamento: as linhas de uma mesma ação aparecem juntas no log.</summary>
    private static async Task AssinarAsync(RuinaRpgDbContext db, List<SheetLogEntry> linhas, Guid actorId, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var nickname = await db.Users.AsNoTracking().Where(u => u.Id == actorId).Select(u => u.Nickname).FirstOrDefaultAsync(ct) ?? "";
        foreach (var linha in linhas)
        {
            linha.Id = Guid.NewGuid();
            linha.OccurredAt = agora;
            linha.ActorUserId = actorId;
            linha.ActorNickname = nickname;
        }
    }

    /// <summary>Uma linha por coluna que realmente mudou (o EF marca tudo como alterado num Update() de entidade solta).</summary>
    private static async Task<List<SheetLogEntry>> AlteracoesAsync(EntityEntry entry, SheetLogEntityMap map, Ficha ficha, SheetLogNames nomes, SheetLogDoServidor doServidor, CancellationToken ct)
    {
        var linhas = new List<SheetLogEntry>();
        string? itemNome = null;
        foreach (var p in entry.Properties)
        {
            var nome = p.Metadata.Name;
            if (!p.IsModified || map.Ignorados.Contains(nome) || p.Metadata.GetValueComparer().Equals(p.OriginalValue, p.CurrentValue))
                continue;
            if (!map.IsSheet)
                itemNome ??= await NomeDaLinhaAsync(entry, map, nomes, ct);
            if (map.TextosLongos.Contains(nome))
            {
                // Texto longo (História, Descrição…): só o fato de ter mudado, sem o conteúdo.
                linhas.Add(Linha(ficha, map.Secao, SheetLogAcao.Alterou, itemNome, SheetLogMap.RotulosDeTextoLongo.GetValueOrDefault(nome, nome)));
                continue;
            }
            var linha = Linha(ficha, map.Secao, SheetLogAcao.Alterou, itemNome, map.Campos.GetValueOrDefault(nome, nome));
            linha.Categoria = map.Categorias.GetValueOrDefault(nome, SheetLogCategoria.Geral);
            linha.ValorAntes = await nomes.RenderAsync(nome, p.OriginalValue, ct);
            linha.ValorDepois = await nomes.RenderAsync(nome, p.CurrentValue, ct);
            linha.Automatico = doServidor.EhAutomatica(entry.Entity, nome);
            linhas.Add(linha);
        }
        return linhas;
    }

    /// <summary>
    /// A ficha de uma linha: de quem é o GM (Personagem: o GM da campanha; NPC/Criatura: o GmId da ficha) e, para
    /// NPC/Criatura, se está concedida a um jogador — só essas entram no log. Nulo = fora do log (não concedida, ou
    /// a ficha não existe). Lê da entidade já rastreada quando há; senão, uma consulta por ficha por salvamento.
    /// </summary>
    private static async Task<Ficha?> ResolverFichaAsync(RuinaRpgDbContext db, Dictionary<(Type, Guid), EntityEntry> porChave,
        SheetLogKind kind, Guid sheetId, Guid actorId, CancellationToken ct)
    {
        Guid? gmId;
        switch (kind)
        {
            case SheetLogKind.Character:
                gmId = porChave.TryGetValue((typeof(CharacterSheet), sheetId), out var personagem)
                    ? await GmDaCampanhaAsync(((CharacterSheet)personagem.Entity).CampaignId)
                    : await db.CharacterSheets.AsNoTracking().Where(s => s.Id == sheetId)
                        .Join(db.Campaigns, s => s.CampaignId, c => c.Id, (_, c) => (Guid?)c.GmId).FirstOrDefaultAsync(ct);
                break;
            case SheetLogKind.Npc:
                var npc = porChave.TryGetValue((typeof(NpcSheet), sheetId), out var n)
                    ? new { ((NpcSheet)n.Entity).GmId, ((NpcSheet)n.Entity).OwnerId }
                    : await db.NpcSheets.AsNoTracking().Where(s => s.Id == sheetId).Select(s => new { s.GmId, s.OwnerId }).FirstOrDefaultAsync(ct);
                gmId = npc?.OwnerId is null ? null : npc.GmId;
                break;
            default:
                var criatura = porChave.TryGetValue((typeof(CreatureSheet), sheetId), out var k)
                    ? new { ((CreatureSheet)k.Entity).GmId, ((CreatureSheet)k.Entity).OwnerId }
                    : await db.CreatureSheets.AsNoTracking().Where(s => s.Id == sheetId).Select(s => new { s.GmId, s.OwnerId }).FirstOrDefaultAsync(ct);
                gmId = criatura?.OwnerId is null ? null : criatura.GmId;
                break;
        }
        return gmId is null ? null : new Ficha(kind, sheetId, gmId == actorId);

        Task<Guid?> GmDaCampanhaAsync(Guid campaignId) =>
            db.Campaigns.AsNoTracking().Where(c => c.Id == campaignId).Select(c => (Guid?)c.GmId).FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// O nome que identifica uma linha filha no log. Quando o mapa dá uma referência (item do Catálogo, Perícia,
    /// Característica) é o nome dela; sem referência preenchida (arma manual da Criatura), a coluna de nome. Uma
    /// coluna de nome que é enum (o slot de armadura) identifica uma linha fixa e vale sempre: ali o item é só um campo.
    /// </summary>
    private static async Task<string> NomeDaLinhaAsync(EntityEntry entry, SheetLogEntityMap map, SheetLogNames nomes, CancellationToken ct)
    {
        var nome = map.ItemNomeProperty is { } propriedadeDoNome ? Valor(entry, propriedadeDoNome) : null;
        if (map.ItemRefProperty is { } referencia && nome is not Enum)
        {
            var id = Valor(entry, referencia);
            // Característica de Criatura: a do livro (TraitId) ou a exclusiva de Criaturas.
            if (id is null && referencia == "TraitId" && entry.Metadata.FindProperty("CreatureExclusiveTraitId") is not null)
                (referencia, id) = ("CreatureExclusiveTraitId", Valor(entry, "CreatureExclusiveTraitId"));
            if (id is not null)
                return await nomes.RenderAsync(referencia, id, ct);
        }
        return SheetLogValues.Render(nome);
    }

    /// <summary>
    /// Os controllers trocam os Efeitos de uma Magia por inteiro a cada edição (apagam todos e gravam todos de
    /// novo); só é alteração quando o conjunto gravado difere do apagado, ou quando um Efeito foi editado no lugar.
    /// </summary>
    private static bool EfeitosMudaram(List<EntityEntry> grupo)
    {
        var map = SheetLogMap.Entidades[grupo[0].Metadata.ClrType];
        if (grupo.Any(e => e.State == EntityState.Modified
                && e.Properties.Any(p => p.IsModified && map.Campos.ContainsKey(p.Metadata.Name) && !p.Metadata.GetValueComparer().Equals(p.OriginalValue, p.CurrentValue))))
            return true;
        List<string> Conteudo(EntityState estado) => grupo.Where(e => e.State == estado)
            .Select(e => string.Join('\u001f', map.Campos.Keys.Select(p => SheetLogValues.Render(Valor(e, p))))).Order(StringComparer.Ordinal).ToList();
        return !Conteudo(EntityState.Added).SequenceEqual(Conteudo(EntityState.Deleted));
    }

    private static SheetLogEntry Linha(Ficha ficha, string secao, SheetLogAcao acao, string? itemNome = null, string? campo = null) => new()
    {
        CharacterSheetId = ficha.Kind == SheetLogKind.Character ? ficha.Id : null,
        NpcSheetId = ficha.Kind == SheetLogKind.Npc ? ficha.Id : null,
        CreatureSheetId = ficha.Kind == SheetLogKind.Creature ? ficha.Id : null,
        ActorNickname = "", // preenchido, com o instante e o autor, em AssinarAsync
        ActorIsGm = ficha.ActorIsGm,
        Acao = acao,
        Secao = secao,
        ItemNome = itemNome,
        Campo = campo,
    };

    private static HashSet<Guid> FichasEm(List<EntityEntry> alteradas, EntityState estado) =>
        alteradas.Where(e => e.State == estado && SheetLogMap.Entidades[e.Metadata.ClrType].IsSheet).Select(Id).ToHashSet();

    private static Guid Id(EntityEntry entry) => (Guid)entry.Property("Id").CurrentValue!;

    /// <summary>O valor que identifica a linha para quem lê o log: o novo numa linha criada, o de antes numa alterada ou removida.</summary>
    private static object? Valor(EntityEntry entry, string propriedade) =>
        entry.State == EntityState.Added ? entry.Property(propriedade).CurrentValue : entry.Property(propriedade).OriginalValue;
}
