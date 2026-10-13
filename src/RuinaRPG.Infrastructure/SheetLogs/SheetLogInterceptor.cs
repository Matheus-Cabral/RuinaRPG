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
/// </summary>
public sealed class SheetLogInterceptor(ICurrentActor currentActor, ILogger<SheetLogInterceptor> logger) : SaveChangesInterceptor
{
    private const string CampoEfeitos = "Efeitos";

    // Interceptor com escopo de requisição = um contexto: as linhas que ESTE salvamento acrescentou, para soltá-las se ele falhar.
    private List<SheetLogEntry> _acrescentadas = [];

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
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        _acrescentadas = [];
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
        List<SheetLogEntry> linhas;
        try
        {
            linhas = await MontarLinhasAsync(context, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to build the sheet log entries; saving without them");
            return;
        }
        if (linhas.Count == 0)
            return;
        // A inserção em si fica no MESMO SaveChanges do usuário: log e alteração são gravados ou falham juntos.
        ((RuinaRpgDbContext)context!).SheetLogEntries.AddRange(linhas);
        _acrescentadas = linhas;
    }

    private async Task<List<SheetLogEntry>> MontarLinhasAsync(DbContext? context, CancellationToken ct)
    {
        // Este método roda em todo salvamento da aplicação: sai o quanto antes quando não há o que registrar.
        if (currentActor.UserId is not { } actorId || context is not RuinaRpgDbContext db)
            return [];
        // Fotografia tirada ANTES de acrescentar as linhas do log. As não alteradas ficam: servem para achar a
        // ficha ou a Magia já carregada sem consultar o banco.
        var rastreadas = db.ChangeTracker.Entries().Where(e => SheetLogMap.Entidades.ContainsKey(e.Metadata.ClrType)).ToList();
        var alteradas = rastreadas.Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        if (alteradas.Count == 0)
            return [];

        var porChave = rastreadas.ToDictionary(e => (e.Metadata.ClrType, Id(e)));
        var fichasNovas = FichasEm(alteradas, EntityState.Added);
        var fichasApagadas = FichasEm(alteradas, EntityState.Deleted);
        var fichas = new Dictionary<(SheetLogKind, Guid), Ficha?>();
        var nomes = new SheetLogNames(db);
        var linhas = new List<SheetLogEntry>();
        var efeitos = new Dictionary<(Type Pai, Guid PaiId), List<EntityEntry>>();

        async Task<Ficha?> FichaAsync(SheetLogKind kind, Guid sheetId)
        {
            if (!fichas.TryGetValue((kind, sheetId), out var ficha))
                fichas[(kind, sheetId)] = ficha = await ResolverFichaAsync(db, porChave, kind, sheetId, actorId, ct);
            return ficha;
        }

        foreach (var entry in alteradas)
        {
            var map = SheetLogMap.Entidades[entry.Metadata.ClrType];
            if (map.ParentType is not null)
            {
                // Efeitos de Magia/Habilidade nunca viram linha própria: entram na linha da Magia (abaixo).
                var chave = (map.ParentType, (Guid)Valor(entry, map.ParentIdProperty!)!);
                if (!efeitos.TryGetValue(chave, out var grupo))
                    efeitos[chave] = grupo = [];
                grupo.Add(entry);
                continue;
            }

            var sheetId = map.IsSheet ? Id(entry) : (Guid)Valor(entry, map.SheetIdProperty!)!;
            // Criar a ficha é uma linha só (as filhas semeadas ou copiadas com ela não contam); apagar não é
            // nenhuma — a FK em cascata leva o log junto, e uma linha nova apontaria para uma ficha que some.
            if (map.IsSheet ? entry.State == EntityState.Deleted : fichasNovas.Contains(sheetId) || fichasApagadas.Contains(sheetId))
                continue;
            if (await FichaAsync(map.Kind, sheetId) is not { } ficha)
                continue;

            if (entry.State == EntityState.Modified)
                linhas.AddRange(await AlteracoesAsync(entry, map, ficha, nomes, ct));
            else if (map.IsSheet)
                linhas.Add(Linha(ficha, map.Secao, SheetLogAcao.Criou));
            else
                linhas.Add(Linha(ficha, map.Secao, entry.State == EntityState.Added ? SheetLogAcao.Adicionou : SheetLogAcao.Removeu,
                    itemNome: await NomeDaLinhaAsync(entry, map, nomes, ct)));
        }

        foreach (var ((tipoDoPai, paiId), grupo) in efeitos)
        {
            // Os controllers sempre editam os Efeitos com a Magia carregada (Include): sem ela rastreada não há
            // como nomear a linha, e nada é registrado. Magia criada ou removida neste salvamento: a linha dela já diz tudo.
            if (!porChave.TryGetValue((tipoDoPai, paiId), out var pai) || pai.State is EntityState.Added or EntityState.Deleted || !EfeitosMudaram(grupo))
                continue;
            var mapDoPai = SheetLogMap.Entidades[tipoDoPai];
            var sheetId = (Guid)Valor(pai, mapDoPai.SheetIdProperty!)!;
            if (fichasNovas.Contains(sheetId) || fichasApagadas.Contains(sheetId) || await FichaAsync(mapDoPai.Kind, sheetId) is not { } ficha)
                continue;
            var nome = SheetLogValues.Render(Valor(pai, mapDoPai.ItemNomeProperty!));
            linhas.Add(Linha(ficha, mapDoPai.Secao, SheetLogAcao.Alterou, itemNome: nome, campo: CampoEfeitos));
        }

        if (linhas.Count == 0)
            return linhas;
        // Um instante e um autor por salvamento: as linhas de uma mesma ação aparecem juntas no log.
        var agora = DateTime.UtcNow;
        var nickname = await db.Users.AsNoTracking().Where(u => u.Id == actorId).Select(u => u.Nickname).FirstOrDefaultAsync(ct) ?? "";
        foreach (var linha in linhas)
        {
            linha.Id = Guid.NewGuid();
            linha.OccurredAt = agora;
            linha.ActorUserId = actorId;
            linha.ActorNickname = nickname;
        }
        return linhas;
    }

    /// <summary>Uma linha por coluna que realmente mudou (o EF marca tudo como alterado num Update() de entidade solta).</summary>
    private static async Task<List<SheetLogEntry>> AlteracoesAsync(EntityEntry entry, SheetLogEntityMap map, Ficha ficha, SheetLogNames nomes, CancellationToken ct)
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
        ActorNickname = "", // preenchido, com o instante e o autor, ao fim de MontarLinhasAsync
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
