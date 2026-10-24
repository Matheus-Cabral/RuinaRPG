using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Unit.SheetLogs;

/// <summary>As caixas do log do encontro dependem dos rótulos do SheetLogMap; este teste falha quando um recurso novo ou renomeado escapa delas.</summary>
public class LogDoEncontroRotulosTests
{
    [Fact]
    public void The_encounter_log_filters_cover_every_resource_label_of_the_sheet_log_map()
    {
        var fuso = TimeZoneInfo.Utc;
        var recursos = SheetLogMap.Entidades.Values.Where(m => m.IsSheet).SelectMany(m => m.Categorias.Select(c => (Rotulo: m.Campos[c.Key], c.Value))).ToList();
        recursos.Should().NotBeEmpty();

        foreach (var (rotulo, categoria) in recursos)
            foreach (var tipo in new[] { "RecursoDaFicha", "AjusteDeCondicao", "CampoAlterado" })
            {
                var linha = new EncounterLogEntryResponse(1, DateTime.UtcNow, 1, null, tipo, "X", rotulo, "1", "2", null);
                var eEstresse = categoria == SheetLogCategoria.Estresse;

                LogDoEncontroAgrupador.Filtrar([linha], null, null, recursos: false, estresse: true, fuso)
                    .Should().HaveCount(eEstresse ? 1 : 0, $"{tipo} {rotulo} ({categoria}) some só com a caixa de Vitalidade/Foco/Adrenalina");
                LogDoEncontroAgrupador.Filtrar([linha], null, null, recursos: true, estresse: false, fuso)
                    .Should().HaveCount(eEstresse ? 0 : 1, $"{tipo} {rotulo} ({categoria}) some só com a caixa de Estresse");
            }
    }
}
