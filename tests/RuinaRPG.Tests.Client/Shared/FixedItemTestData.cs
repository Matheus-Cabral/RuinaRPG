using System.Net;
using System.Net.Http.Json;
using Bunit;
using Bunit.Rendering;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.Rules;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>Dados e atalhos comuns dos testes dos itens fixos dos kits (picker, diálogos e página).</summary>
public static class FixedItemTestData
{
    public static EquipmentKitFixedItemResponse Item(string id, string nome, string tipo = "ItemGeral",
        bool incompleto = false, params string[] kits) =>
        new(id, nome, tipo, incompleto,
            new CreateItemRequest(tipo, nome, 1m, 10, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null),
            kits.ToList());

    public static HttpResponseMessage Json(HttpStatusCode status, object body) => new(status) { Content = JsonContent.Create(body) };

    public static string Rota(HttpRequestMessage request) => $"{request.Method} {request.RequestUri!.AbsolutePath}{request.RequestUri!.Query}";
}
