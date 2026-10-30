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
using RuinaRPG.Infrastructure.Fichas;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

/// <summary>
/// Um banco na migration anterior (2.1.0) com linhas de Afinidade no formato antigo e Condições com o elemento
/// por nome; depois a migration AfinidadesPorElemento. Montado uma vez para a classe toda. As linhas antigas entram
/// por SQL: o modelo atual não tem mais as tabelas (a RemoverLinhasDeAfinidade as tira).
/// </summary>
public sealed class BancoComAfinidadesAntigas : IAsyncLifetime
{
    public const string MigrationAnterior = "20261029154815_EstadoDaConta";
    public const string MigrationDaConversao = "20261030162938_AfinidadesPorElemento";

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

    public static readonly LinhaDeAfinidade[] SubElementoNegativo =
    [
        new(Elemento.Agua, 1, SubElemento.Gelo, -3, null, null, 4),
        new(Elemento.Ar, 1, SubElemento.Gelo, 1, null, null, null),
    ];

    public Guid PersonagemComSubElementoNegativo { get; private set; }

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
        // Sub-Elemento cujas linhas somam abaixo de 0, com Experiência: fica com 0 pontos e a Experiência.
        SubElementoNegativo,
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
            Personagens[ficha.Id] = caso;

            var npc = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id };
            db.NpcSheets.Add(npc);
            Npcs[npc.Id] = caso;

            if (ReferenceEquals(caso, SubElementoNegativo))
                PersonagemComSubElementoNegativo = ficha.Id;
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
            new CondicaoModificador { Id = Guid.NewGuid(), CondicaoId = condicao.Id, Ordem = 5, Alvo = AlvoDeCondicao.AfinidadeElemental, AlvoEspecifico = "Fogo", TipoDeValor = TipoDeValorDeCondicao.Fixo, Valor = 3 },
            // Nome fora do enum (minúsculo): a migration o deixa como está, sem falhar.
            new CondicaoModificador { Id = Guid.NewGuid(), CondicaoId = condicao.Id, Ordem = 6, Alvo = AlvoDeCondicao.AfinidadeElemental, AlvoEspecifico = "agua", TipoDeValor = TipoDeValorDeCondicao.Fixo, Valor = 1 });

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

        await db.Database.OpenConnectionAsync();
        foreach (var (ficha, linhas) in Personagens)
            foreach (var l in linhas)
                await InserirLinhaAntigaAsync(db, "CharacterAffinities", "CharacterSheetId", ficha, l);
        foreach (var (ficha, linhas) in Npcs)
            foreach (var l in linhas)
                await InserirLinhaAntigaAsync(db, "NpcAffinities", "NpcSheetId", ficha, l);
        await db.Database.CloseConnectionAsync();

        await db.GetService<IMigrator>().MigrateAsync(MigrationDaConversao);
    }

    /// <summary>Uma linha no formato antigo (enums gravados como inteiro), direto na tabela.</summary>
    private static async Task InserirLinhaAntigaAsync(RuinaRpgDbContext db, string tabela, string colunaDaFicha, Guid ficha, LinhaDeAfinidade l)
    {
        await using var comando = db.Database.GetDbConnection().CreateCommand();
        comando.CommandText = $"INSERT INTO \"{tabela}\" (\"Id\", \"{colunaDaFicha}\", \"Elemento\", \"ElementoValor\", \"SubElemento\", \"SubElementoValor\", " +
            "\"SegundaEssencia\", \"SegundaEssenciaValor\", \"Experiencia\") VALUES (@id, @ficha, @e1, @v1, @sub, @vsub, @e2, @v2, @xp)";
        void Parametro(string nome, object? valor)
        {
            var p = comando.CreateParameter();
            p.ParameterName = nome;
            p.Value = valor ?? DBNull.Value;
            p.DbType = valor is Guid ? System.Data.DbType.Guid : System.Data.DbType.Int32;
            comando.Parameters.Add(p);
        }
        Parametro("id", Guid.NewGuid());
        Parametro("ficha", ficha);
        Parametro("e1", (int?)l.Elemento);
        Parametro("v1", l.ElementoValor);
        Parametro("sub", (int?)l.SubElemento);
        Parametro("vsub", l.SubElementoValor);
        Parametro("e2", (int?)l.SegundaEssencia);
        Parametro("v2", l.SegundaEssenciaValor);
        Parametro("xp", l.Experiencia);
        await comando.ExecuteNonQueryAsync();
    }

    /// <summary>Quantas linhas uma tabela tem agora (as antigas não estão mais no modelo).</summary>
    public async Task<int> ContarAsync(string tabela)
    {
        await using var db = new RuinaRpgDbContext(Options);
        var sql = tabela switch
        {
            "CharacterAffinities" => "SELECT COUNT(*)::int AS \"Value\" FROM \"CharacterAffinities\"",
            "NpcAffinities" => "SELECT COUNT(*)::int AS \"Value\" FROM \"NpcAffinities\"",
            _ => throw new ArgumentOutOfRangeException(nameof(tabela)),
        };
        return (await db.Database.SqlQueryRaw<int>(sql).ToListAsync()).Single();
    }

    public Task DisposeAsync() => _banco.DisposeAsync();
}

public class AfinidadesPorElementoMigrationTests : IClassFixture<BancoComAfinidadesAntigas>
{
    private readonly BancoComAfinidadesAntigas _banco;

    public AfinidadesPorElementoMigrationTests(BancoComAfinidadesAntigas banco) => _banco = banco;

    private RuinaRpgDbContext Db() => new(_banco.Options);

    private static async Task<Dictionary<int, (int Pontos, int? Experiencia)>> PontosAsync<T>(IQueryable<T> pontos, Guid ficha)
        where T : class, IPontosDeAfinidadeDaFicha =>
        (await pontos.AsNoTracking().Where(p => p.SheetId == ficha).ToListAsync())
            .ToDictionary(p => p.ElementoId, p => (p.Pontos, p.Experiencia));

    [Fact]
    public async Task A_conversao_em_SQL_e_igual_a_ConversaoDeLinhas_em_Personagem_e_NPC()
    {
        await using var db = Db();
        foreach (var (ficha, linhas) in _banco.Personagens)
            (await PontosAsync(db.CharacterPontosDeAfinidade, ficha))
                .Should().BeEquivalentTo(ConversaoDeLinhas.Converter(linhas), $"Personagem {string.Join(" | ", linhas)}");
        foreach (var (ficha, linhas) in _banco.Npcs)
            (await PontosAsync(db.NpcPontosDeAfinidade, ficha))
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
        var personagem = await PontosAsync(db.CharacterPontosDeAfinidade, _banco.PersonagemDoReviewFocus1);
        var npc = await PontosAsync(db.NpcPontosDeAfinidade, _banco.NpcDoReviewFocus1);

        personagem.Should().BeEquivalentTo(esperado);
        npc.Should().BeEquivalentTo(esperado);
        // O gasto de antes: cada Essência pelo maior valor (Ar 2, Água 1, Fogo 2) + os Sub-Elementos (Gelo 3, Raio 1).
        personagem.Values.Sum(v => v.Pontos).Should().Be(9);
    }

    [Fact]
    public async Task Sub_Elemento_que_somava_abaixo_de_0_fica_com_0_pontos_e_a_Experiencia()
    {
        await using var db = Db();
        (await PontosAsync(db.CharacterPontosDeAfinidade, _banco.PersonagemComSubElementoNegativo))[4].Should().Be((0, 4));
    }

    [Theory]
    [InlineData("CharacterPontosDeAfinidade")]
    [InlineData("NpcPontosDeAfinidade")]
    public async Task As_tabelas_de_pontos_recusam_pontos_negativos(string tabela)
    {
        await using var db = Db();
        var checks = await db.Database.SqlQuery<string>(
            $"SELECT pg_get_constraintdef(oid) AS \"Value\" FROM pg_constraint WHERE conrelid = to_regclass({'"' + tabela + '"'}) AND contype = 'c'").ToListAsync();
        checks.Should().ContainSingle().Which.Should().Be("CHECK ((\"Pontos\" >= 0))");
    }

    [Fact]
    public async Task A_conversao_nao_apaga_as_linhas_antigas()
    {
        (await _banco.ContarAsync("CharacterAffinities")).Should().Be(_banco.Personagens.Values.Sum(l => l.Length));
        (await _banco.ContarAsync("NpcAffinities")).Should().Be(_banco.Npcs.Values.Sum(l => l.Length));
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

        linhas.Should().Equal("1", "0", null, "Vigor", "3", "2", "agua");
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
            .Should().Equal("Agua", "Terra", null, "Vigor", "Ar", "Fogo", "agua");
        (await depois.EncounterParticipantCondicoes.SingleAsync(c => c.Id == _banco.AplicadaId)).Modificadores.Select(m => m.AlvoEspecifico)
            .Should().Equal("Agua", "Vigor", "Fogo", null);
        (await depois.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_name IN ('ElementosDeAfinidade', 'CruzamentosDeAfinidade', 'TotaisDeAfinidadePorCirculo', 'VisibilidadesDeElemento', 'CharacterPontosDeAfinidade', 'NpcPontosDeAfinidade')").ToListAsync())
            .Should().BeEmpty();
        (await _banco.ContarAsync("CharacterAffinities")).Should().Be(_banco.Personagens.Values.Sum(l => l.Length));
    }
}

/// <summary>A RemoverLinhasDeAfinidade, depois da conversão: as tabelas antigas saem e os pontos ficam.</summary>
public class RemoverLinhasDeAfinidadeMigrationTests : IClassFixture<BancoComAfinidadesAntigas>
{
    private const string Tabelas = "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_name IN ('CharacterAffinities', 'NpcAffinities')";

    private readonly BancoComAfinidadesAntigas _banco;

    public RemoverLinhasDeAfinidadeMigrationTests(BancoComAfinidadesAntigas banco) => _banco = banco;

    [Fact]
    public async Task Tira_as_tabelas_antigas_mantem_os_pontos_e_o_Down_as_recria_vazias()
    {
        await using var db = new RuinaRpgDbContext(_banco.Options);
        var pontosAntes = await db.CharacterPontosDeAfinidade.CountAsync() + await db.NpcPontosDeAfinidade.CountAsync();

        await db.Database.MigrateAsync();

        (await db.Database.SqlQueryRaw<string>(Tabelas).ToListAsync()).Should().BeEmpty();
        (await db.CharacterPontosDeAfinidade.CountAsync() + await db.NpcPontosDeAfinidade.CountAsync()).Should().Be(pontosAntes).And.BePositive();

        await db.GetService<IMigrator>().MigrateAsync(BancoComAfinidadesAntigas.MigrationDaConversao);

        (await db.Database.SqlQueryRaw<string>(Tabelas).ToListAsync()).Should().BeEquivalentTo("CharacterAffinities", "NpcAffinities");
        (await _banco.ContarAsync("CharacterAffinities")).Should().Be(0);
        (await _banco.ContarAsync("NpcAffinities")).Should().Be(0);
    }
}
