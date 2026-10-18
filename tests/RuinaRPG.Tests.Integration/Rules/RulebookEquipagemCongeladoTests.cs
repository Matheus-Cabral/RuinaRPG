using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Rules;

// Rede de segurança do D.6: o documento "equipagem" do Livro de Regras tem de sair byte a byte
// igual ao de antes da troca das consultas por kit por uma consulta só.
public class RulebookEquipagemCongeladoTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string Arquivo = "Rules/rulebook-equipagem.json";

    [Fact]
    public async Task O_documento_equipagem_continua_identico()
    {
        await SemearKitDeTodosOsCasosAsync();
        var atual = await ObterAsync();
        var esperado = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, Arquivo));
        Assert.Equal(esperado, atual);
    }

    private async Task<string> ObterAsync()
    {
        var documento = await Client.GetFromJsonAsync<RulebookDocumentResponse>("/api/rulebook/equipagem");
        return JsonSerializer.Serialize(documento, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task SemearKitDeTodosOsCasosAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var kitId = new Guid("00000000-0000-0000-0000-0000000000a1");
        if (await db.EquipmentKits.FindAsync(kitId) is not null) return; // a fixture é por classe; semeia uma vez

        // Os kits semeados saem com a ordem dos itens variando de execução para execução (sem ORDER BY,
        // ordem física do insert), o que tornaria o JSON instável. Por isso eles são marcados como
        // excluídos e o documento é montado só com kits de ids fixos criados aqui, em ordem de insert fixa.
        foreach (var semeado in db.EquipmentKits) semeado.IsDeleted = true;

        var fixoId = new Guid("00000000-0000-0000-0000-0000000000f1");
        db.EquipmentKitFixedItems.Add(new EquipmentKitFixedItem
        {
            Id = fixoId, Nome = "Corda de teste", Tipo = ItemTipo.ItemGeral, Dados = "{}",
        });
        db.EquipmentKits.Add(new EquipmentKit
        {
            Id = kitId, Nome = "Zz Kit de Teste", Descricao = "Linha 1\nLinha 2 & <b>", Ciclos = 2,
        });
        // Segundo kit (fica antes na ordenação por Nome): sem slots, sem Ciclos.
        var outroKitId = new Guid("00000000-0000-0000-0000-0000000000a6");
        db.EquipmentKits.Add(new EquipmentKit { Id = outroKitId, Nome = "Aa Kit de Teste", Descricao = "Simples", Ciclos = 0 });
        db.EquipmentKitItems.Add(new EquipmentKitItem
        {
            Id = new Guid("00000000-0000-0000-0000-0000000000a7"), KitId = outroKitId,
            Nome = "Pedra legada", Tipo = ItemTipo.ItemGeral, Qtd = 1,
        });
        db.EquipmentKitItems.AddRange(
            new EquipmentKitItem
            {
                Id = new Guid("00000000-0000-0000-0000-0000000000a2"), KitId = kitId,
                FixedItemId = fixoId, Tipo = ItemTipo.ItemGeral, Qtd = 2,
            },
            new EquipmentKitItem
            {
                Id = new Guid("00000000-0000-0000-0000-0000000000a3"), KitId = kitId,
                Nome = "Tocha legada", Tipo = ItemTipo.ItemGeral, Qtd = 3,
            });
        db.EquipmentKitChoiceSlots.AddRange(
            new EquipmentKitChoiceSlot
            {
                Id = new Guid("00000000-0000-0000-0000-0000000000a4"), KitId = kitId,
                Label = "Arma simples", Tipo = ItemTipo.Arma, Qtd = 1,
            },
            new EquipmentKitChoiceSlot
            {
                Id = new Guid("00000000-0000-0000-0000-0000000000a5"), KitId = kitId,
                Label = "Armadura", Tipo = ItemTipo.Armadura, Qtd = 1,
                BonusFixedItemId = fixoId, BonusQtd = 1, BonusSubcategoria = "Leve",
            });
        await db.SaveChangesAsync();
    }
}
