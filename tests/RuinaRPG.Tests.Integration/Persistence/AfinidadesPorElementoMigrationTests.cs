using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RuinaRPG.Domain.Afinidades;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Condicoes;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

/// <summary>
/// Um banco na migration anterior (2.1.0) com linhas de Afinidade no formato antigo e Condições com o elemento
/// por nome; depois a migration AfinidadesPorElemento. Montado uma vez para a classe toda.
/// </summary>
public sealed class BancoComAfinidadesAntigas : IAsyncLifetime
{
    public const string MigrationAnterior = "20261029154815_EstadoDaConta";

    private readonly BlankDatabaseFixture _banco = new();

    public DbContextOptions<RuinaRpgDbContext> Options { get; private set; } = null!;

    /// <summary>Linhas antigas por ficha de Personagem (a ordem não importa para a conversão).</summary>
    public Dictionary<Guid, LinhaDeAfinidade[]> Personagens { get; } = [];
    public Dictionary<Guid, LinhaDeAfinidade[]> Npcs { get; } = [];
    public Guid PersonagemDoReviewFocus1 { get; private set; }
    public Guid NpcDoReviewFocus1 { get; private set; }
    public Guid CondicaoId { get; private set; }
    public Guid AplicadaId { get; private set; }
    public Guid[] LinhasAplicadas { get; } = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

    // Os casos do Step 1 da Task 2 (ConversaoDeLinhasTests) e alguns limites.
    private static readonly LinhaDeAfinidade[] ReviewFocus1 =
    [
        new(Elemento.Ar, 2, SubElemento.Gelo, 3, EssenciaBasica.Agua, 1, 5),
        new(Elemento.Ar, 1, SubElemento.Raio, 1, EssenciaBasica.Fogo, 2, null),
    ];

    public static readonly LinhaDeAfinidade[][] Casos =
    [
        ReviewFocus1,
        [new(Elemento.Fogo, 1, null, null, null, null, 4)],
        [
            new(Elemento.Ar, 1, SubElemento.Gelo, 1, null, null, 3),
            new(Elemento.Agua, 1, SubElemento.Gelo, 1, null, null, 7),
            new(Elemento.Terra, 1, SubElemento.Flora, 1, null, null, 6),
            new(Elemento.Terra, 1, SubElemento.Flora, 1, null, null, 2),
        ],
        [new(null, null, null, null, null, null, null)],
        [],
        [
            new(Elemento.Ar, 1, SubElemento.Prever, 1, EssenciaBasica.Alma, 2, null),
            new(Elemento.Fogo, 1, SubElemento.Curar, 1, EssenciaBasica.Vida, 3, null),
            new(Elemento.Terra, 1, SubElemento.Invocacao, 1, EssenciaBasica.Mundano, 4, null),
        ],
        [new(null, null, SubElemento.Alma, 2, null, null, null), new(null, null, SubElemento.Vida, 3, null, null, null)],
        [new(Elemento.Ar, 1, null, null, EssenciaBasica.Agua, 3, null), new(Elemento.Agua, 2, null, null, EssenciaBasica.Ar, 2, null)],
        // Todos os 14 Sub-Elementos antigos e as 7 Essências, para cobrir cada mapeamento de enum → id.
        Enum.GetValues<SubElemento>().Select((s, i) => new LinhaDeAfinidade(
            (Elemento)(i % 4), i, s, i + 1, (EssenciaBasica)(i % 7), 14 - i, i % 3 == 0 ? i : null)).ToArray(),
        // Valores nulos ao lado de elementos escolhidos; Experiência sem destino; Experiência zero.
        [
            new(Elemento.Terra, null, SubElemento.Ferro, null, EssenciaBasica.Fogo, null, null),
            new(null, null, null, null, EssenciaBasica.Mundano, 2, 9),
            new(Elemento.Agua, 0, null, null, null, null, 0),
        ],
        // Caminho como Essência e o Sub-Elemento legado de mesmo id: o valor do Sub-Elemento prevalece.
        [new(Elemento.Ar, 1, null, null, EssenciaBasica.Alma, 5, null), new(null, null, SubElemento.Alma, 2, null, null, 1)],
        // Valores negativos (a API antiga não os recusava).
        [new(Elemento.Fogo, -2, SubElemento.Raio, 3, EssenciaBasica.Ar, -1, -4), new(Elemento.Terra, -3, null, null, null, null, 2)],
    ];

    public async Task InitializeAsync()
    {
        await _banco.InitializeAsync();
        Options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_banco.ConnectionString).Options;

        await using var db = new RuinaRpgDbContext(Options);
        await db.GetService<IMigrator>().MigrateAsync(MigrationAnterior);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@afinidades22.com", Email = "gm@afinidades22.com", Nickname = "Afin22Gm", Role = UserRole.GM };
        var jogador = new ApplicationUser { Id = Guid.NewGuid(), UserName = "pl@afinidades22.com", Email = "pl@afinidades22.com", Nickname = "Afin22Pl", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, jogador);
        var campanha = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Afinidades", Descricao = "" };
        db.Campaigns.Add(campanha);
        await db.SaveChangesAsync();

        foreach (var caso in Casos)
        {
            var ficha = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campanha.Id, OwnerId = jogador.Id };
            db.CharacterSheets.Add(ficha);
            db.CharacterAffinities.AddRange(caso.Select(l => new CharacterAffinity
            {
                Id = Guid.NewGuid(), SheetId = ficha.Id, Elemento = l.Elemento, ElementoValor = l.ElementoValor, SubElemento = l.SubElemento,
                SubElementoValor = l.SubElementoValor, SegundaEssencia = l.SegundaEssencia, SegundaEssenciaValor = l.SegundaEssenciaValor, Experiencia = l.Experiencia,
            }));
            Personagens[ficha.Id] = caso;

            var npc = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id };
            db.NpcSheets.Add(npc);
            db.NpcAffinities.AddRange(caso.Select(l => new NpcAffinity
            {
                Id = Guid.NewGuid(), SheetId = npc.Id, Elemento = l.Elemento, ElementoValor = l.ElementoValor, SubElemento = l.SubElemento,
                SubElementoValor = l.SubElementoValor, SegundaEssencia = l.SegundaEssencia, SegundaEssenciaValor = l.SegundaEssenciaValor, Experiencia = l.Experiencia,
            }));
            Npcs[npc.Id] = caso;

            if (ReferenceEquals(caso, ReviewFocus1))
            {
                PersonagemDoReviewFocus1 = ficha.Id;
                NpcDoReviewFocus1 = npc.Id;
            }
        }

        var condicao = new Condicao { Id = Guid.NewGuid(), Nome = "Encharcado", Icone = "Agua" };
        CondicaoId = condicao.Id;
        db.Condicoes.Add(condicao);
        db.CondicaoModificadores.AddRange(
            new CondicaoModificador { Id = Guid.NewGuid(), CondicaoId = condicao.Id, Ordem = 0, Alvo = AlvoDeCondicao.AfinidadeElemental, AlvoEspecifico = "Agua", TipoDeValor = TipoDeValorDeCondicao.Fixo, Valor = -1 },
            new CondicaoModificador { Id = Guid.NewGuid(), CondicaoId = condicao.Id, Ordem = 1, Alvo = AlvoDeCondicao.DanoElemental, AlvoEspecifico = "Terra", TipoDeValor = TipoDeValorDeCondicao.Fixo, Valor = 2 },
            new CondicaoModificador { Id = Guid.NewGuid(), CondicaoId = condicao.Id, Ordem = 2, Alvo = AlvoDeCondicao.AfinidadeElemental, AlvoEspecifico = null, TipoDeValor = TipoDeValorDeCondicao.Fixo, Valor = 1 },
            new CondicaoModificador { Id = Guid.NewGuid(), CondicaoId = condicao.Id, Ordem = 3, Alvo = AlvoDeCondicao.Atributo, AlvoEspecifico = "Vigor", TipoDeValor = TipoDeValorDeCondicao.Fixo, Valor = -2 },
            new CondicaoModificador { Id = Guid.NewGuid(), CondicaoId = condicao.Id, Ordem = 4, Alvo = AlvoDeCondicao.DanoElemental, AlvoEspecifico = "Ar", TipoDeValor = TipoDeValorDeCondicao.Percentual, Valor = 50 },
            new CondicaoModificador { Id = Guid.NewGuid(), CondicaoId = condicao.Id, Ordem = 5, Alvo = AlvoDeCondicao.AfinidadeElemental, AlvoEspecifico = "Fogo", TipoDeValor = TipoDeValorDeCondicao.Fixo, Valor = 3 });

        var encontro = new Encounter { Id = Guid.NewGuid(), CampaignId = campanha.Id };
        var participante = new EncounterParticipant { Id = Guid.NewGuid(), EncounterId = encontro.Id, Nome = "Goblin" };
        db.Encounters.Add(encontro);
        db.EncounterParticipants.Add(participante);
        var aplicada = new EncounterParticipantCondicao
        {
            Id = Guid.NewGuid(), EncounterParticipantId = participante.Id, CondicaoId = condicao.Id, Nome = "Encharcado", Icone = "Agua",
            Modificadores =
            [
                new(LinhasAplicadas[0], AlvoDeCondicao.AfinidadeElemental, "Agua", TipoDeValorDeCondicao.Fixo, -1, null),
                new(LinhasAplicadas[1], AlvoDeCondicao.Atributo, "Vigor", TipoDeValorDeCondicao.Fixo, -2, null),
                new(LinhasAplicadas[2], AlvoDeCondicao.DanoElemental, "Fogo", TipoDeValorDeCondicao.Percentual, 25, null),
                new(LinhasAplicadas[3], AlvoDeCondicao.DanoElemental, null, TipoDeValorDeCondicao.Fixo, 1, null),
            ],
        };
        AplicadaId = aplicada.Id;
        db.EncounterParticipantCondicoes.Add(aplicada);
        // Uma aplicada sem linhas de elemento (e outra sem linha nenhuma) não muda.
        db.EncounterParticipantCondicoes.Add(new EncounterParticipantCondicao { Id = Guid.NewGuid(), EncounterParticipantId = participante.Id, Nome = "Vazia", Icone = "Agua" });
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _banco.DisposeAsync();
}

public class AfinidadesPorElementoMigrationTests : IClassFixture<BancoComAfinidadesAntigas>
{
    private readonly BancoComAfinidadesAntigas _banco;

    public AfinidadesPorElementoMigrationTests(BancoComAfinidadesAntigas banco) => _banco = banco;

    private RuinaRpgDbContext Db() => new(_banco.Options);

    private static async Task<Dictionary<int, (int Pontos, int? Experiencia)>> PontosAsync(RuinaRpgDbContext db, string tabela, string coluna, Guid ficha)
    {
        var linhas = await db.Database.SqlQueryRaw<string>(
            $"SELECT \"ElementoId\" || ':' || \"Pontos\" || ':' || COALESCE(\"Experiencia\"::text, '') AS \"Value\" FROM \"{tabela}\" WHERE \"{coluna}\" = {{0}}", ficha).ToListAsync();
        return linhas.Select(l => l.Split(':')).ToDictionary(
            p => int.Parse(p[0]), p => (int.Parse(p[1]), p[2] == "" ? (int?)null : int.Parse(p[2])));
    }

    [Fact]
    public async Task A_conversao_em_SQL_e_igual_a_ConversaoDeLinhas_em_Personagem_e_NPC()
    {
        await using var db = Db();
        foreach (var (ficha, linhas) in _banco.Personagens)
            (await PontosAsync(db, "CharacterPontosDeAfinidade", "CharacterSheetId", ficha))
                .Should().BeEquivalentTo(ConversaoDeLinhas.Converter(linhas), $"Personagem {string.Join(" | ", linhas)}");
        foreach (var (ficha, linhas) in _banco.Npcs)
            (await PontosAsync(db, "NpcPontosDeAfinidade", "NpcSheetId", ficha))
                .Should().BeEquivalentTo(ConversaoDeLinhas.Converter(linhas), $"NPC {string.Join(" | ", linhas)}");
    }

    [Fact]
    public async Task Review_Focus_1_a_ficha_real_vira_pontos_por_elemento_com_o_mesmo_gasto()
    {
        var esperado = new Dictionary<int, (int, int?)>
        {
            [3] = (2, null), // Ar
            [1] = (1, null), // Água
            [2] = (2, null), // Fogo
            [4] = (3, 5),    // Gelo, com a Experiência da linha
            [7] = (1, null), // Raio
        };
        await using var db = Db();
        var personagem = await PontosAsync(db, "CharacterPontosDeAfinidade", "CharacterSheetId", _banco.PersonagemDoReviewFocus1);
        var npc = await PontosAsync(db, "NpcPontosDeAfinidade", "NpcSheetId", _banco.NpcDoReviewFocus1);

        personagem.Should().BeEquivalentTo(esperado);
        npc.Should().BeEquivalentTo(esperado);
        var gastoAntes = VocacaoArcanaCalculator.Gasto(_banco.Personagens[_banco.PersonagemDoReviewFocus1]);
        personagem.Values.Sum(v => v.Pontos).Should().Be(gastoAntes).And.Be(9);
    }

    [Fact]
    public async Task As_linhas_antigas_ficam_ate_a_Task_5()
    {
        await using var db = Db();
        (await db.CharacterAffinities.CountAsync()).Should().Be(_banco.Personagens.Values.Sum(l => l.Length));
        (await db.NpcAffinities.CountAsync()).Should().Be(_banco.Npcs.Values.Sum(l => l.Length));
    }

    [Fact]
    public async Task O_catalogo_nasce_com_os_19_elementos_de_ids_fixos_sem_teto()
    {
        await using var db = Db();
        var elementos = await db.Database.SqlQueryRaw<string>(
            "SELECT \"Id\" || ':' || \"Nome\" || ':' || \"Tipo\" || ':' || COALESCE(\"Teto\"::text, '-') AS \"Value\" FROM \"ElementosDeAfinidade\" ORDER BY \"Id\"").ToListAsync();

        elementos.Should().Equal(
            "0:Terra:Base:-", "1:Água:Base:-", "2:Fogo:Base:-", "3:Ar:Base:-",
            "4:Gelo:SubElemento:-", "5:Flora:SubElemento:-", "6:Ferro:SubElemento:-", "7:Raio:SubElemento:-",
            "8:Prever:SubElemento:-", "9:Alma:Caminho:-", "10:Purificar:SubElemento:-", "11:Ecomancia:SubElemento:-",
            "12:Hemomancia:SubElemento:-", "13:Curar:SubElemento:-", "14:Vida:Caminho:-", "15:Aprimorar:SubElemento:-",
            "16:Necromancia:SubElemento:-", "17:Invocação:SubElemento:-", "18:Mundano:Caminho:-");
    }

    [Fact]
    public async Task Um_elemento_criado_depois_recebe_o_id_19()
    {
        await using var db = Db();
        await using var tx = await db.Database.BeginTransactionAsync();
        var id = await db.Database.SqlQueryRaw<int>(
            "INSERT INTO \"ElementosDeAfinidade\" (\"Nome\", \"Tipo\") VALUES ('Luz', 'SubElemento') RETURNING \"Id\" AS \"Value\"").ToListAsync();
        id.Should().Equal(19);
        await tx.RollbackAsync();
    }

    [Fact]
    public async Task Os_12_cruzamentos_entram_com_requisito_1_e_1()
    {
        await using var db = Db();
        var cruzamentos = await db.Database.SqlQueryRaw<string>(
            "SELECT LEAST(\"ElementoAId\", \"ElementoBId\") || '+' || GREATEST(\"ElementoAId\", \"ElementoBId\") || '=' || \"SubElementoId\" || ':' || \"MinimoA\" || ',' || \"MinimoB\" AS \"Value\" FROM \"CruzamentosDeAfinidade\"").ToListAsync();

        // Ar+Água=Gelo, Ar+Fogo=Raio, Água+Terra=Flora, Fogo+Terra=Ferro, Ar+Alma=Prever, Água+Alma=Purificar,
        // Fogo+Vida=Curar, Terra+Vida=Aprimorar, Ar+Mundano=Ecomancia, Água+Mundano=Hemomancia,
        // Fogo+Mundano=Necromancia, Terra+Mundano=Invocação.
        cruzamentos.Should().BeEquivalentTo(
            "1+3=4:1,1", "2+3=7:1,1", "0+1=5:1,1", "0+2=6:1,1", "3+9=8:1,1", "1+9=10:1,1",
            "2+14=13:1,1", "0+14=15:1,1", "3+18=11:1,1", "1+18=12:1,1", "2+18=16:1,1", "0+18=17:1,1");
    }

    [Fact]
    public async Task Feiticeiro_Adepto_e_Bruxo_veem_todos_os_Caminhos_e_Sub_Elementos()
    {
        await using var db = Db();
        var ligacoes = await db.Database.SqlQueryRaw<string>(
            "SELECT \"Origem\" || ':' || \"Chave\" || ':' || \"ElementoId\" AS \"Value\" FROM \"VisibilidadesDeElemento\"").ToListAsync();

        var naoBase = Enumerable.Range(4, 15);
        ligacoes.Should().BeEquivalentTo(
            new[] { "Feiticeiro", "Adepto", "Bruxo" }.SelectMany(v => naoBase.Select(id => $"Vocacao:{v}:{id}")));
        new[] { "Feiticeiro", "Adepto", "Bruxo" }.Should().Equal(
            Vocacao.Feiticeiro.ToString(), Vocacao.Adepto.ToString(), Vocacao.Bruxo.ToString());
    }

    [Fact]
    public async Task O_elemento_das_Condicoes_passa_do_nome_ao_id_do_catalogo()
    {
        await using var db = Db();
        var linhas = await db.CondicaoModificadores.Where(m => m.CondicaoId == _banco.CondicaoId).OrderBy(m => m.Ordem)
            .Select(m => m.AlvoEspecifico).ToListAsync();

        linhas.Should().Equal("1", "0", null, "Vigor", "3", "2");
    }

    [Fact]
    public async Task O_elemento_das_Condicoes_ja_aplicadas_tambem_passa_ao_id()
    {
        await using var db = Db();
        var aplicada = await db.EncounterParticipantCondicoes.SingleAsync(c => c.Id == _banco.AplicadaId);
        var l = _banco.LinhasAplicadas;

        aplicada.Modificadores.Should().Equal(
            new LinhaDeCondicaoAplicada(l[0], AlvoDeCondicao.AfinidadeElemental, "1", TipoDeValorDeCondicao.Fixo, -1, null),
            new LinhaDeCondicaoAplicada(l[1], AlvoDeCondicao.Atributo, "Vigor", TipoDeValorDeCondicao.Fixo, -2, null),
            new LinhaDeCondicaoAplicada(l[2], AlvoDeCondicao.DanoElemental, "2", TipoDeValorDeCondicao.Percentual, 25, null),
            new LinhaDeCondicaoAplicada(l[3], AlvoDeCondicao.DanoElemental, null, TipoDeValorDeCondicao.Fixo, 1, null));
        (await db.EncounterParticipantCondicoes.SingleAsync(c => c.Nome == "Vazia")).Modificadores.Should().BeEmpty();
    }
}

public class AfinidadesPorElementoMigrationDownTests : IClassFixture<BancoComAfinidadesAntigas>
{
    private readonly BancoComAfinidadesAntigas _banco;

    public AfinidadesPorElementoMigrationDownTests(BancoComAfinidadesAntigas banco) => _banco = banco;

    [Fact]
    public async Task Voltar_a_migration_anterior_devolve_os_nomes_das_Condicoes_e_tira_as_tabelas_novas()
    {
        await using (var db = new RuinaRpgDbContext(_banco.Options))
            await db.GetService<IMigrator>().MigrateAsync(BancoComAfinidadesAntigas.MigrationAnterior);

        await using var depois = new RuinaRpgDbContext(_banco.Options);
        (await depois.CondicaoModificadores.Where(m => m.CondicaoId == _banco.CondicaoId).OrderBy(m => m.Ordem).Select(m => m.AlvoEspecifico).ToListAsync())
            .Should().Equal("Agua", "Terra", null, "Vigor", "Ar", "Fogo");
        (await depois.EncounterParticipantCondicoes.SingleAsync(c => c.Id == _banco.AplicadaId)).Modificadores.Select(m => m.AlvoEspecifico)
            .Should().Equal("Agua", "Vigor", "Fogo", null);
        (await depois.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_name IN ('ElementosDeAfinidade', 'CruzamentosDeAfinidade', 'TotaisDeAfinidadePorCirculo', 'VisibilidadesDeElemento', 'CharacterPontosDeAfinidade', 'NpcPontosDeAfinidade')").ToListAsync())
            .Should().BeEmpty();
        (await depois.CharacterAffinities.CountAsync()).Should().Be(_banco.Personagens.Values.Sum(l => l.Length));
    }
}
