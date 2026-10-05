using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

public class EquipmentKitGrantServiceTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;

    public EquipmentKitGrantServiceTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    // Item.GmId and Campaign.GmId both carry a real DB-level FK to AspNetUsers, and
    // CampaignAttachment.CampaignId/ItemId carry real FKs to Campaigns/Items — a bare
    // Guid.NewGuid() used directly as GmId/CampaignId/ItemId (as a unit-test double might get
    // away with) violates those constraints here. These helpers create the actual parent rows.
    private static async Task<Guid> NewGmAsync(RuinaRpgDbContext db, string nickname)
    {
        var gm = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = $"{nickname}@equipagemgranttest.com",
            Email = $"{nickname}@equipagemgranttest.com", Nickname = nickname, Role = UserRole.GM,
        };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        return gm.Id;
    }

    private static async Task<Guid> NewCampaignAsync(RuinaRpgDbContext db, Guid gmId)
    {
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gmId, Nome = "Campanha", Descricao = "D" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        return campaign.Id;
    }

    private static string Unico(string prefixo) => $"{prefixo} {Guid.NewGuid():N}";

    private static CreateItemRequest FixoItemGeral(string nome, decimal peso = 2.5m, int preco = 15, string subcategoria = "Equipamentos de Aventura") =>
        new("ItemGeral", nome, peso, preco, null, subcategoria, "Descrição do fixo",
            null, null, null, null, null, null, null,
            null, null, null, null,
            null, null, null, null, null);

    private static CreateItemRequest FixoArma(string nome, int dano = 6, string rank = "F") =>
        new("Arma", nome, 1.5m, 50, null, "Espadas", "Uma lâmina curta e leve.",
            rank, "UmaMao", "2D6", dano, "19", 2, "Cortante",
            null, null, null, null,
            null, null, null, null, null);

    private static CreateItemRequest FixoArmadura(string nome) =>
        new("Armadura", nome, 8m, 100, null, null, "Couro curtido.",
            "E", null, null, null, null, null, null,
            "Leve", 5, 2, 1,
            null, null, null, null, null);

    /// <summary>Um item da base global de itens fixos, já salvo (Nome+Tipo são únicos na base, por isso os nomes únicos).</summary>
    private static async Task<EquipmentKitFixedItem> NewFixoAsync(RuinaRpgDbContext db, CreateItemRequest request,
        RequisitosDePassiva? requisitos = null, PenalidadeDeEquipamento? penalidade = null, bool incompleto = false)
    {
        var fixo = new EquipmentKitFixedItem
        {
            Id = Guid.NewGuid(), Nome = request.Nome, Tipo = Enum.Parse<ItemTipo>(request.Tipo), Dados = ItemFactory.Serializar(request),
            Requisitos = requisitos, PenalidadeDeRequisitos = penalidade, DetalhesIncompletos = incompleto,
        };
        db.EquipmentKitFixedItems.Add(fixo);
        await db.SaveChangesAsync();
        return fixo;
    }

    private static EquipmentKitItem LinhaDoKit(EquipmentKit kit, EquipmentKitFixedItem fixo, int qtd = 1, ArmorSlotType? armorSlot = null) =>
        new() { Id = Guid.NewGuid(), KitId = kit.Id, FixedItemId = fixo.Id, Nome = fixo.Nome, Tipo = fixo.Tipo, Qtd = qtd, ArmorSlot = armorSlot };

    private static EquipmentKitGrantService NewService(RuinaRpgDbContext db) => new(db, new DurabilidadePorRankProvider(db));

    [Fact]
    public async Task A_fixed_weapon_missing_from_the_gms_catalog_is_created_complete_instead_of_failing()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmArmaFixa");
        var nome = Unico("Espada Curta");
        var requisitos = new RequisitosDePassiva { Atributos = [new RequisitoDeAtributo(Atributo.Vigor, 8)] };
        var penalidade = new PenalidadeDeEquipamento { Texto = "Desvantagem em furtividade" };
        var fixo = await NewFixoAsync(db, FixoArma(nome), requisitos, penalidade);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = LinhaDoKit(kit, fixo);
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var (plan, error) = await NewService(db).BuildPlanAsync(kit, [kitItem], [], gmId, []);
        // BuildPlanAsync só põe o item criado no change tracker: persistir é de quem chama.
        await db.SaveChangesAsync();

        error.Should().BeNull();
        var arma = await db.Set<Arma>().AsNoTracking().SingleAsync(a => a.GmId == gmId && a.Nome == nome);
        arma.Dano.Should().Be(6);
        arma.Rank.Should().Be(RankDeItem.F);
        arma.Empunhadura.Should().Be(Empunhadura.UmaMao);
        arma.Dados.Should().Be("2D6");
        arma.Subcategoria.Should().Be("Espadas");
        arma.Peso.Should().Be(1.5m);
        arma.Preco.Should().Be(50);
        arma.Requisitos!.Atributos.Should().Equal(new RequisitoDeAtributo(Atributo.Vigor, 8));
        arma.PenalidadeDeRequisitos!.Texto.Should().Be("Desvantagem em furtividade");
        plan!.Grants.Should().ContainSingle(g => g.Tipo == ItemTipo.Arma && g.ItemId == arma.Id && g.DurabilidadeMaxima == 20);
    }

    [Fact]
    public async Task An_item_geral_is_created_with_the_fixed_items_weight_price_and_subcategoria_not_zeros()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmItemGeralFixo");
        var nome = Unico("Corda");
        var fixo = await NewFixoAsync(db, FixoItemGeral(nome, peso: 2.5m, preco: 15, subcategoria: "Ferramentas"));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = LinhaDoKit(kit, fixo, qtd: 2);
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var (plan, error) = await NewService(db).BuildPlanAsync(kit, [kitItem], [], gmId, []);
        await db.SaveChangesAsync();

        error.Should().BeNull();
        plan!.Grants.Should().ContainSingle(g => g.Tipo == ItemTipo.ItemGeral && g.Qtd == 2 && g.DurabilidadeMaxima == null);
        var criado = await db.Set<ItemGeral>().AsNoTracking().SingleAsync(i => i.GmId == gmId && i.Nome == nome);
        criado.Peso.Should().Be(2.5m);
        criado.Preco.Should().Be(15);
        criado.Subcategoria.Should().Be("Ferramentas");
        criado.Descricao.Should().Be("Descrição do fixo");
    }

    [Fact]
    public async Task An_existing_gm_item_with_the_same_name_and_type_is_reused_and_not_modified()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmReusaItem");
        var nome = Unico("Mochila");
        var existing = new ItemGeral { Id = Guid.NewGuid(), GmId = gmId, Nome = nome, Subcategoria = "Minha Subcategoria", Peso = 5, Preco = 20 };
        db.Add(existing);
        var fixo = await NewFixoAsync(db, FixoItemGeral(nome, peso: 1m, preco: 99));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = LinhaDoKit(kit, fixo);
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var (plan, error) = await NewService(db).BuildPlanAsync(kit, [kitItem], [], gmId, []);
        await db.SaveChangesAsync();

        error.Should().BeNull();
        plan!.Grants.Single().ItemId.Should().Be(existing.Id);
        (await db.Set<ItemGeral>().CountAsync(i => i.GmId == gmId && i.Nome == nome)).Should().Be(1);
        var depois = await db.Set<ItemGeral>().AsNoTracking().SingleAsync(i => i.Id == existing.Id);
        depois.Peso.Should().Be(5);
        depois.Preco.Should().Be(20);
        depois.Subcategoria.Should().Be("Minha Subcategoria");
    }

    [Fact]
    public async Task The_same_name_with_another_type_in_the_gms_catalog_is_not_reused()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmOutroTipo");
        var nome = Unico("Lamina");
        var armaDoGm = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = nome, Subcategoria = "Espadas", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        db.Add(armaDoGm);
        var fixo = await NewFixoAsync(db, FixoItemGeral(nome));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = LinhaDoKit(kit, fixo);
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var (plan, error) = await NewService(db).BuildPlanAsync(kit, [kitItem], [], gmId, []);
        await db.SaveChangesAsync();

        error.Should().BeNull();
        var grant = plan!.Grants.Single();
        grant.ItemId.Should().NotBe(armaDoGm.Id);
        grant.Tipo.Should().Be(ItemTipo.ItemGeral);
        (await db.Set<ItemGeral>().CountAsync(i => i.GmId == gmId && i.Nome == nome)).Should().Be(1);
        (await db.Set<Arma>().CountAsync(a => a.GmId == gmId && a.Nome == nome)).Should().Be(1);
    }

    // Review Focus 5: aplicar um kit nunca cria item para outro GM nem aponta a ficha para item de outro GM.
    [Fact]
    public async Task A_gm_who_renamed_or_deleted_their_copy_gets_a_fresh_complete_copy_and_never_another_gms_item()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmA = await NewGmAsync(db, "GmCopiaA");
        var gmB = await NewGmAsync(db, "GmCopiaB");
        var nome = Unico("Espada Curta");
        var fixo = await NewFixoAsync(db, FixoArma(nome));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = LinhaDoKit(kit, fixo);
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();
        var service = NewService(db);

        // GM A aplica o kit: ganha uma cópia, e a renomeia.
        var (planA1, errorA1) = await service.BuildPlanAsync(kit, [kitItem], [], gmA, []);
        await db.SaveChangesAsync();
        errorA1.Should().BeNull();
        var copiaA1 = await db.Set<Arma>().SingleAsync(a => a.GmId == gmA && a.Nome == nome);
        planA1!.Grants.Single().ItemId.Should().Be(copiaA1.Id);
        copiaA1.Nome = nome + " (renomeada)";
        await db.SaveChangesAsync();

        // GM B aplica o mesmo kit: recebe uma cópia sua, nunca a de A.
        var (planB, errorB) = await service.BuildPlanAsync(kit, [kitItem], [], gmB, []);
        await db.SaveChangesAsync();
        errorB.Should().BeNull();
        var copiaB = await db.Set<Arma>().SingleAsync(a => a.GmId == gmB && a.Nome == nome);
        planB!.Grants.Single().ItemId.Should().Be(copiaB.Id).And.NotBe(copiaA1.Id);

        // GM A aplica de novo: como renomeou a cópia, ganha uma cópia nova e completa, sua.
        var (planA2, errorA2) = await service.BuildPlanAsync(kit, [kitItem], [], gmA, []);
        await db.SaveChangesAsync();
        errorA2.Should().BeNull();
        var novaA = await db.Set<Arma>().AsNoTracking().SingleAsync(a => a.GmId == gmA && a.Nome == nome);
        novaA.Id.Should().NotBe(copiaA1.Id).And.NotBe(copiaB.Id);
        novaA.Dano.Should().Be(6);
        novaA.Rank.Should().Be(RankDeItem.F);
        planA2!.Grants.Single().ItemId.Should().Be(novaA.Id);

        // Todo ItemId concedido pertence ao GM para quem o plano foi montado.
        foreach (var (plan, gm) in new[] { (planA1, gmA), (planB, gmB), (planA2, gmA) })
            foreach (var grant in plan!.Grants)
                (await db.Items.AsNoTracking().SingleAsync(i => i.Id == grant.ItemId)).GmId.Should().Be(gm);

        // Cada GM tem só os seus itens: A (renomeada + nova), B (uma).
        (await db.Items.CountAsync(i => i.GmId == gmA && (i.Nome == nome || i.Nome == nome + " (renomeada)"))).Should().Be(2);
        (await db.Items.CountAsync(i => i.GmId == gmB && i.Nome == nome)).Should().Be(1);
    }

    [Fact]
    public async Task A_fixed_armadura_is_granted_to_its_armor_slot()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmArmaduraFixa");
        var nome = Unico("Gibao");
        var fixo = await NewFixoAsync(db, FixoArmadura(nome));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = LinhaDoKit(kit, fixo, armorSlot: ArmorSlotType.Capacete);
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var (plan, error) = await NewService(db).BuildPlanAsync(kit, [kitItem], [], gmId, []);
        await db.SaveChangesAsync();

        error.Should().BeNull();
        var armadura = await db.Set<Armadura>().AsNoTracking().SingleAsync(a => a.GmId == gmId && a.Nome == nome);
        armadura.Defesa.Should().Be(5);
        var grant = plan!.Grants.Single();
        grant.Tipo.Should().Be(ItemTipo.Armadura);
        grant.ItemId.Should().Be(armadura.Id);
        grant.ArmorSlot.Should().Be(ArmorSlotType.Capacete);
        grant.DurabilidadeMaxima.Should().Be(45); // Rank E, Tabela de Durabilidade por Rank
    }

    [Fact]
    public async Task A_fixed_item_with_incomplete_details_is_still_granted_name_only()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmIncompleto");
        var nome = Unico("Item Incompleto");
        var fixo = await NewFixoAsync(db, FixoItemGeral(nome, peso: 0m, preco: 0), incompleto: true);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = LinhaDoKit(kit, fixo, qtd: 3);
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var (plan, error) = await NewService(db).BuildPlanAsync(kit, [kitItem], [], gmId, []);
        await db.SaveChangesAsync();

        error.Should().BeNull();
        plan!.Grants.Should().ContainSingle(g => g.Qtd == 3);
        var criado = await db.Set<ItemGeral>().AsNoTracking().SingleAsync(i => i.GmId == gmId && i.Nome == nome);
        criado.Peso.Should().Be(0);
        criado.Preco.Should().Be(0);
    }

    [Fact]
    public async Task A_slot_bonus_comes_from_its_fixed_item()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmBonusFixo");
        var bow = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = Unico("Arco"), Subcategoria = "Arcos", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        db.Add(bow);
        var flecha = await NewFixoAsync(db, FixoItemGeral(Unico("Flecha"), peso: 0.1m, preco: 1, subcategoria: "Munição"));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma à distância", Tipo = ItemTipo.Arma,
            SubcategoriasCsv = "Arcos", Rank = RankDeItem.F, Qtd = 1,
            BonusSubcategoria = "Arcos", BonusFixedItemId = flecha.Id, BonusQtd = 10,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var (plan, error) = await NewService(db).BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), bow.Id.ToString())]);
        await db.SaveChangesAsync();

        error.Should().BeNull();
        plan!.Grants.Should().HaveCount(2);
        var criado = await db.Set<ItemGeral>().AsNoTracking().SingleAsync(i => i.GmId == gmId && i.Nome == flecha.Nome);
        criado.Peso.Should().Be(0.1m);
        criado.Preco.Should().Be(1);
        criado.Subcategoria.Should().Be("Munição");
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.ItemGeral && g.ItemId == criado.Id && g.Qtd == 10);
    }

    [Fact]
    public async Task A_legacy_kit_item_without_a_fixed_item_fails_with_a_message_asking_for_make_migrate()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmKitLegado");
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var kitItem = new EquipmentKitItem { Id = Guid.NewGuid(), KitId = kit.Id, FixedItemId = null, Nome = "Item Legado", Tipo = ItemTipo.ItemGeral, Qtd = 1 };
        db.EquipmentKitItems.Add(kitItem);
        await db.SaveChangesAsync();

        var (plan, error) = await NewService(db).BuildPlanAsync(kit, [kitItem], [], gmId, []);

        plan.Should().BeNull();
        error.Should().Be("O kit ainda não foi convertido para a base de itens fixos. Rode `make migrate`.");
        (await db.Set<ItemGeral>().CountAsync(i => i.GmId == gmId)).Should().Be(0);
    }

    [Fact]
    public async Task BuildPlanAsync_rejects_a_missing_choice_slot_selection()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = Guid.NewGuid();
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Rank = RankDeItem.F, Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, []);

        plan.Should().BeNull();
        error.Should().Contain("Arma");
    }

    [Fact]
    public async Task BuildPlanAsync_rejects_a_choice_selection_outside_the_slot_s_filter()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmForaDoFiltro");
        var outOfTierWeapon = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Espada Lendária", Subcategoria = "Espadas", Rank = RankDeItem.S, Peso = 1, Preco = 0 };
        db.Add(outOfTierWeapon);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Rank = RankDeItem.F, Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), outOfTierWeapon.Id.ToString())]);

        plan.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildPlanAsync_grants_the_conditional_bonus_only_when_the_matching_alternative_is_chosen()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmBonusCondicional");
        var bow = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arco de Teste", Subcategoria = "Arcos", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        db.Add(bow);
        var flecha = await NewFixoAsync(db, FixoItemGeral(Unico("Bônus")));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma à distância", Tipo = ItemTipo.Arma,
            SubcategoriasCsv = "Arcos,Fundas e Baladeiras", Rank = RankDeItem.F, Qtd = 1,
            BonusSubcategoria = "Arcos", BonusFixedItemId = flecha.Id, BonusQtd = 10,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), bow.Id.ToString())]);

        error.Should().BeNull();
        plan!.Grants.Should().HaveCount(2);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.Arma && g.ItemId == bow.Id);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.ItemGeral && g.Qtd == 10);
    }

    // The test above only proves the bonus fires FOR the matching alternative — on its own it
    // can't rule out a bug where BonusNome always grants regardless of which eligible item was
    // picked. This is the other half: an equally eligible, non-matching alternative (a sling,
    // valid for the slot's own filter, but whose Subcategoria isn't the slot's BonusSubcategoria)
    // must NOT trigger the bonus.
    [Fact]
    public async Task BuildPlanAsync_does_not_grant_the_conditional_bonus_when_a_non_matching_alternative_is_chosen()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmBonusNaoCondicional");
        var sling = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Funda de Teste", Subcategoria = "Fundas e Baladeiras", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        db.Add(sling);
        var flecha = await NewFixoAsync(db, FixoItemGeral(Unico("Bônus")));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma à distância", Tipo = ItemTipo.Arma,
            SubcategoriasCsv = "Arcos,Fundas e Baladeiras", Rank = RankDeItem.F, Qtd = 1,
            BonusSubcategoria = "Arcos", BonusFixedItemId = flecha.Id, BonusQtd = 10,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), sling.Id.ToString())]);

        error.Should().BeNull();
        plan!.Grants.Should().ContainSingle();
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.Arma && g.ItemId == sling.Id);
        plan.Grants.Should().NotContain(g => g.Tipo == ItemTipo.ItemGeral);
    }

    [Fact]
    public async Task ResolveEligibleOptionsAsync_matches_by_parsed_Familia_for_items_built_by_the_constructor()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmFamiliaParse");
        var varinha = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Varinha Composta", Subcategoria = "Equipamento inicial - Arma - Mágica - Varinha", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        var machado = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Machado Composto", Subcategoria = "Equipamento inicial - Arma - Corpo a Corpo - Machado", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        db.AddRange(varinha, machado);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Condutor", Tipo = ItemTipo.Arma, SubcategoriasCsv = "Varinha", Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var options = await service.ResolveEligibleOptionsAsync(slot, gmId);

        options.Should().ContainSingle(o => o.ItemId == varinha.Id.ToString());
    }

    [Fact]
    public async Task ResolveEligibleOptionsAsync_still_matches_the_legacy_raw_Subcategoria_string()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmLegacyRaw");
        var arco = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arco Legado", Subcategoria = "Arcos", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        db.Add(arco);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma à distância", Tipo = ItemTipo.Arma, SubcategoriasCsv = "Arcos", Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var options = await service.ResolveEligibleOptionsAsync(slot, gmId);

        options.Should().ContainSingle(o => o.ItemId == arco.Id.ToString());
    }

    [Fact]
    public async Task ResolveEligibleOptionsAsync_resolves_Armadura_Escudo_and_Artefato_choice_slots()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmArmaduraEscudoArtefato");
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gmId, Nome = "Armadura Teste", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Peso = 1, Preco = 0 };
        var escudo = new Escudo { Id = Guid.NewGuid(), GmId = gmId, Nome = "Escudo Teste", Subcategoria = "Equipamento inicial - Escudo - Leve - Rodela", Peso = 1, Preco = 0 };
        var artefato = new Artefato { Id = Guid.NewGuid(), GmId = gmId, Nome = "Artefato Teste", Subcategoria = "Equipamento inicial - Artefato - Passivo - Anel", Peso = 0, Preco = 0 };
        db.AddRange(armadura, escudo, artefato);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var armaduraSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Armadura", Tipo = ItemTipo.Armadura, SubcategoriasCsv = "Couro", Qtd = 1, ArmorSlot = RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Superior };
        var escudoSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Escudo", Tipo = ItemTipo.Escudo, SubcategoriasCsv = "Rodela", Qtd = 1 };
        var artefatoSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Artefato", Tipo = ItemTipo.Artefato, SubcategoriasCsv = "Anel", Qtd = 1 };
        db.EquipmentKitChoiceSlots.AddRange(armaduraSlot, escudoSlot, artefatoSlot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));

        (await service.ResolveEligibleOptionsAsync(armaduraSlot, gmId)).Should().ContainSingle(o => o.ItemId == armadura.Id.ToString());
        (await service.ResolveEligibleOptionsAsync(escudoSlot, gmId)).Should().ContainSingle(o => o.ItemId == escudo.Id.ToString());
        (await service.ResolveEligibleOptionsAsync(artefatoSlot, gmId)).Should().ContainSingle(o => o.ItemId == artefato.Id.ToString());
    }

    [Fact]
    public async Task BuildPlanAsync_carries_the_slot_s_ArmorSlot_through_to_the_grant_for_a_chosen_Armadura()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmArmorSlotCarry");
        var armadura = new Armadura { Id = Guid.NewGuid(), GmId = gmId, Nome = "Armadura Teste", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Rank = RankDeItem.E, Peso = 1, Preco = 0 };
        db.Add(armadura);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Armadura", Tipo = ItemTipo.Armadura, SubcategoriasCsv = "Couro", Qtd = 1, ArmorSlot = RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Capacete };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), armadura.Id.ToString())]);

        error.Should().BeNull();
        var grant = plan!.Grants.Single();
        grant.Tipo.Should().Be(ItemTipo.Armadura);
        grant.ArmorSlot.Should().Be(RuinaRPG.Domain.CharacterSheets.ArmorSlotType.Capacete);
        grant.DurabilidadeMaxima.Should().Be(45); // Rank E, Tabela de Durabilidade por Rank
    }

    // A parsed-Família match must not cross Tipos: an Arma slot whose allowed Família list
    // contains "Couro" must not offer an Arma row whose Subcategoria is mis-composed to parse
    // as Tipo "Armadura" (Família segment "Couro") even though the raw Família string matches.
    // The row is an Arma (so it IS in the Arma-slot candidate set via the per-Tipo query — this
    // is not excluded for free by the switch dispatch), so only Matches()'s own
    // tipo == slot.Tipo gate can exclude it. Verified load-bearing: removing that gate makes
    // this test FAIL (see task-8-report.md, "Fix round 1").
    [Fact]
    public async Task ResolveEligibleOptionsAsync_parsed_Familia_match_does_not_cross_Tipos()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmFamiliaCrossTipo");
        var armaMisComposta = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arma Mal-Composta", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        db.Add(armaMisComposta);
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var armaSlot = new EquipmentKitChoiceSlot { Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, SubcategoriasCsv = "Couro", Qtd = 1 };
        db.EquipmentKitChoiceSlots.Add(armaSlot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var options = await service.ResolveEligibleOptionsAsync(armaSlot, gmId);

        options.Should().BeEmpty();
    }

    // BuildPlanAsync's conditional-bonus check must fire for a selection whose Subcategoria
    // only matches the slot's BonusSubcategoria via the parsed-Família path, not just via the
    // legacy raw-string equality already proven by the Caçador-style tests above.
    [Fact]
    public async Task BuildPlanAsync_grants_the_conditional_bonus_when_the_match_is_via_parsed_Familia()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmBonusFamilia");
        var varinha = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Varinha Composta", Subcategoria = "Equipamento inicial - Arma - Mágica - Varinha", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        db.Add(varinha);
        var flecha = await NewFixoAsync(db, FixoItemGeral(Unico("Bônus")));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Condutor", Tipo = ItemTipo.Arma,
            SubcategoriasCsv = "Varinha", Rank = RankDeItem.F, Qtd = 1,
            BonusSubcategoria = "Varinha", BonusFixedItemId = flecha.Id, BonusQtd = 1,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), varinha.Id.ToString())]);

        error.Should().BeNull();
        plan!.Grants.Should().HaveCount(2);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.Arma && g.ItemId == varinha.Id);
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.ItemGeral && g.Qtd == 1);
    }

    // Mirrors ResolveEligibleOptionsAsync_parsed_Familia_match_does_not_cross_Tipos above, but for
    // BuildPlanAsync's conditional-bonus check: the selected item's Subcategoria parses to a
    // DIFFERENT Tipo than the slot's own (Arma slot, but the item is mis-composed as an "Armadura"
    // string) whose parsed Família happens to equal BonusSubcategoria. Matches() itself already
    // guards ResolveEligibleOptionsAsync against this cross-Tipo case, but with SubcategoriasCsv
    // left null (matches any Subcategoria unconditionally) the item is still an eligible choice —
    // so only the bonus-match expression's own Tipo gate can stop the bonus from firing here.
    [Fact]
    public async Task BuildPlanAsync_does_not_grant_the_conditional_bonus_when_the_parsed_Familia_match_crosses_Tipos()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmBonusFamiliaCrossTipo");
        var armaMisComposta = new Arma { Id = Guid.NewGuid(), GmId = gmId, Nome = "Arma Mal-Composta", Subcategoria = "Equipamento inicial - Armadura - Leve - Couro", Rank = RankDeItem.F, Peso = 1, Preco = 0 };
        db.Add(armaMisComposta);
        var flecha = await NewFixoAsync(db, FixoItemGeral(Unico("Bônus")));
        var kit = new EquipmentKit { Id = Guid.NewGuid(), Nome = "Kit", Descricao = "D", Ciclos = 0 };
        db.EquipmentKits.Add(kit);
        var slot = new EquipmentKitChoiceSlot
        {
            Id = Guid.NewGuid(), KitId = kit.Id, Label = "Arma", Tipo = ItemTipo.Arma, Qtd = 1,
            BonusSubcategoria = "Couro", BonusFixedItemId = flecha.Id, BonusQtd = 1,
        };
        db.EquipmentKitChoiceSlots.Add(slot);
        await db.SaveChangesAsync();

        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));
        var (plan, error) = await service.BuildPlanAsync(kit, [], [slot], gmId, [new ChoiceSlotSelectionRequest(slot.Id.ToString(), armaMisComposta.Id.ToString())]);

        error.Should().BeNull();
        plan!.Grants.Should().ContainSingle();
        plan.Grants.Should().Contain(g => g.Tipo == ItemTipo.Arma && g.ItemId == armaMisComposta.Id);
        plan.Grants.Should().NotContain(g => g.Tipo == ItemTipo.ItemGeral);
    }

    [Fact]
    public async Task UpsertCampaignAttachmentAsync_creates_a_public_attachment_when_none_exists()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmUpsertNovo");
        var campaignId = await NewCampaignAsync(db, gmId);
        var item = new ItemGeral { Id = Guid.NewGuid(), GmId = gmId, Nome = "Mochila", Subcategoria = "Equipamentos de Aventura", Peso = 5, Preco = 20 };
        db.Add(item);
        await db.SaveChangesAsync();
        var itemId = item.Id;
        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));

        await service.UpsertCampaignAttachmentAsync(campaignId, itemId);
        await db.SaveChangesAsync();

        var attachment = await db.CampaignAttachments.SingleAsync(a => a.CampaignId == campaignId && a.ItemId == itemId);
        attachment.IsPublic.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertCampaignAttachmentAsync_flips_an_existing_private_attachment_to_public()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var gmId = await NewGmAsync(db, "GmUpsertFlip");
        var campaignId = await NewCampaignAsync(db, gmId);
        var item = new ItemGeral { Id = Guid.NewGuid(), GmId = gmId, Nome = "Mochila", Subcategoria = "Equipamentos de Aventura", Peso = 5, Preco = 20 };
        db.Add(item);
        await db.SaveChangesAsync();
        var itemId = item.Id;
        db.CampaignAttachments.Add(new CampaignAttachment { Id = Guid.NewGuid(), CampaignId = campaignId, ItemId = itemId, IsPublic = false });
        await db.SaveChangesAsync();
        var service = new EquipmentKitGrantService(db, new DurabilidadePorRankProvider(db));

        await service.UpsertCampaignAttachmentAsync(campaignId, itemId);
        await db.SaveChangesAsync();

        (await db.CampaignAttachments.CountAsync(a => a.CampaignId == campaignId && a.ItemId == itemId)).Should().Be(1);
        (await db.CampaignAttachments.SingleAsync(a => a.CampaignId == campaignId && a.ItemId == itemId)).IsPublic.Should().BeTrue();
    }
}
