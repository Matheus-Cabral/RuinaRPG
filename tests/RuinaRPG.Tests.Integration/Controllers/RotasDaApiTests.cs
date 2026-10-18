using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace RuinaRPG.Tests.Integration.Controllers;

// Rede de segurança do sub-projeto D (spec D, "Route inventory"): a superfície HTTP da API —
// verbo, rota, parâmetros, corpo, resposta e autorização de cada ação — tem de continuar
// idêntica à capturada antes da consolidação. O nome do controller fica de fora de propósito:
// mudar de classe é permitido, mudar o contrato não.
public class RotasDaApiTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string Arquivo = "Controllers/rotas-da-api.txt";

    [Fact]
    public void A_superficie_da_api_continua_a_mesma()
    {
        var esperado = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, Arquivo));
        var atual = Inventario(Factory.Services);
        Assert.Equal(esperado, atual);
    }

    public static string[] Inventario(IServiceProvider services) =>
        services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>()
            .SelectMany(Linhas)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> Linhas(ControllerActionDescriptor a)
    {
        var verbos = a.ActionConstraints?.OfType<Microsoft.AspNetCore.Mvc.ActionConstraints.HttpMethodActionConstraint>()
            .SelectMany(c => c.HttpMethods).ToList() ?? [];
        if (verbos.Count == 0) verbos.Add("ANY");
        var parametros = string.Join(", ", a.Parameters.Select(p =>
            $"{p.Name}:{Nome(p.ParameterType)}:{p.BindingInfo?.BindingSource?.Id ?? "-"}"));
        var autorizacao = string.Join(";", a.EndpointMetadata.OfType<IAuthorizeData>()
                .Select(d => $"authorize(policy={d.Policy},roles={d.Roles})").Distinct().Order(StringComparer.Ordinal)
            .Concat(a.EndpointMetadata.OfType<IAllowAnonymous>().Any() ? ["anonymous"] : []));
        foreach (var verbo in verbos)
            yield return $"{verbo} /{a.AttributeRouteInfo?.Template} ({parametros}) -> {Nome(a.MethodInfo.ReturnType)} [{autorizacao}]";
    }

    private static string Nome(Type t) => t.IsGenericType
        ? $"{t.Name[..t.Name.IndexOf('`')]}<{string.Join(",", t.GetGenericArguments().Select(Nome))}>"
        : t.Name;
}
