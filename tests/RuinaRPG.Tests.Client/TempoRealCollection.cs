using Xunit;

namespace RuinaRPG.Tests.Client;

/// <summary>
/// Testes que afirmam sobre tempo real (esperas fixas contra o debounce de 400 ms do auto-save, o tique da
/// <c>RecargaAdiada</c>, ou limites curtos para um salvamento com debounce). Com a suíte inteira em paralelo eles
/// disputam CPU e estouram a janela sem nada errado; nesta coleção rodam em série, sem nenhum outro teste junto.
/// </summary>
[CollectionDefinition(Nome, DisableParallelization = true)]
public sealed class TempoRealCollection
{
    public const string Nome = "Tempo real";
}
