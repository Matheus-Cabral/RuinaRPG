namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>Quais records o tipo lê e escreve no fio: os compartilhados, ou os da Criatura (sem Histórico nem marca racial, com Durabilidade anulável).</summary>
public enum FormatoDosDados
{
    Comum,
    Criatura,
}

/// <summary>
/// O tipo de ficha (Personagem, NPC, Criatura) do lado do cliente: o prefixo da API e os fatos que as abas
/// compartilhadas perguntam. É um valor passado adiante, sem HTTP próprio. Os fatos têm o nome da capacidade
/// ("a ficha tem Diário?"), nunca o do tipo ("é Criatura?"); os valores vêm do inventário de diferenças do E
/// (docs/superpowers/specs/2026-10-19-e-inventario-de-diferencas.md), aprovado pelo usuário em 2026-10-22.
/// </summary>
/// <param name="Nome">"Personagem", "NPC" ou "Criatura".</param>
/// <param name="Rota">O prefixo da API: <c>character-sheets</c>, <c>npc-sheets</c> ou <c>creature-sheets</c>.</param>
public sealed record TipoDeFichaCliente(string Nome, string Rota)
{
    /// <summary>O <c>Kind</c> do aviso <c>CondicoesAlteradas</c> que vale para este tipo.</summary>
    public required string TipoDaNotificacao { get; init; }

    /// <summary>O título da página (h1) e o último item da trilha enquanto a ficha não tem nome.</summary>
    public required string Titulo { get; init; }

    /// <summary>As Condições ativas só existem numa ficha concedida a um jogador (NPC, Criatura); o Personagem sempre as tem.</summary>
    public required bool CondicoesSoSeConcedida { get; init; }

    /// <summary>Linhagem, Variante, Vocação, Classe, Graduação, Coração de Mana, VIS, Âmbares e a Habilidade Racial.</summary>
    public required bool TemLinhagemEVocacao { get; init; }

    /// <summary>A escolha das Características Raciais (<c>racial-traits</c>).</summary>
    public required bool TemTracosRaciais { get; init; }

    /// <summary>O título do diálogo da escolha de Característica Racial (onde <see cref="TemTracosRaciais"/>; A1: fica por tipo).</summary>
    public required string? TituloDaEscolhaRacial { get; init; }

    /// <summary>O começo da frase da Característica Obrigatória única no mesmo diálogo ("… recebe automaticamente:").</summary>
    public required string? RecebeAutomaticamente { get; init; }

    /// <summary>Nível e Experiência editados à mão, cada um no seu endpoint (<c>PUT nivel</c>, <c>PUT experiencia-atual</c>).</summary>
    public required bool EditaNivelAManual { get; init; }

    /// <summary>"Pontos de Perícia: gasto / disponível" e o Acerto Crítico; sem orçamento, só "Pontos de Perícia Gastos: N".</summary>
    public required bool TemOrcamentoDePericias { get; init; }

    /// <summary>A seção Afinidades e a Afinidade dentro dos Sub-Atributos.</summary>
    public required bool TemAfinidades { get; init; }

    /// <summary>
    /// O GM não é recusado pelas regras das Afinidades (máximo, teto, bloqueio): a ficha mostra, não recusa (Ficha de
    /// NPCs R0010). O jogador de uma ficha concedida segue as regras.
    /// </summary>
    public required bool AfinidadesLivresParaOGm { get; init; }

    /// <summary>Os Pontos de Sina e o Estresse no Combate.</summary>
    public required bool TemSinaEEstresse { get; init; }

    /// <summary>O rótulo do Foco no Combate ("Foco (PF)" ou "Arcana (PF)").</summary>
    public required string RotuloDoFoco { get; init; }

    /// <summary>O rótulo dos Pontos de Ignição em Informações Básicas.</summary>
    public required string RotuloDosPontosDeIgnicao { get; init; }

    /// <summary>O painel "Progressão do nível" no alto de Atributos &amp; Perícias.</summary>
    public required bool MostraProgressaoDoNivel { get; init; }

    /// <summary>Os seletores de item do Catálogo com "+" para o GM (<c>CatalogoItemPicker</c>) em vez do <c>EntityPicker</c>.</summary>
    public required bool CadastraItemNoPicker { get; init; }

    /// <summary>
    /// O Auditor de Regras cria uma Característica nova ali mesmo, ao lado do seletor ("Nova característica de
    /// criatura"), no catálogo que o seletor busca.
    /// </summary>
    public required bool CriaCaracteristicaNoPicker { get; init; }

    /// <summary>
    /// A ajuda (InfoPopup) ao lado do Abate e da Assistência em Nível e Progressão; <c>null</c> mostra os números sem
    /// ajuda (A2 do inventário: mantido por tipo).
    /// </summary>
    public string? AjudaDoXpPorAbate { get; init; }

    /// <summary>A coluna Peso da tabela de armas.</summary>
    public required bool MostraPesoDasArmas { get; init; }

    /// <summary>
    /// A arma preenchida à mão (ataque natural, sem Item do Catálogo): a Origem Catálogo/Manual do formulário. Os
    /// records de Combate que o tipo lê são o <see cref="Formato"/>.
    /// </summary>
    public required bool TemArmaManual { get; init; }

    /// <summary>A edição na linha das Afeições (<c>PUT affections/{id}</c>).</summary>
    public required bool EditaAfeicaoNaLinha { get; init; }

    /// <summary>A seção Runas.</summary>
    public required bool TemRunas { get; init; }

    /// <summary>O Equipamento Inicial e o Inventário.</summary>
    public required bool TemInventario { get; init; }

    /// <summary>Os Espólios (em Posses, no lugar do Inventário).</summary>
    public required bool TemEspolios { get; init; }

    /// <summary>
    /// A aba Antecedentes (Estrela, Histórico e História; igual em todo tipo que a tem).
    /// </summary>
    public required bool TemAntecedentes { get; init; }

    /// <summary>A aba Diário.</summary>
    public required bool TemDiario { get; init; }

    /// <summary>
    /// O cemitério (Ficha de Personagem R0010): a ficha vem com <c>NoCemiterio</c> e os motivos, fica só leitura no
    /// cemitério, e o GM tem a caixa "Morto" no cabeçalho.
    /// </summary>
    public required bool TemCemiterio { get; init; }

    /// <summary>
    /// A lista do GM onde a ficha mora (NPC: "NPCs do GM", <c>npcs</c>; Criatura: "Bestiário do GM", <c>bestiario</c>): o
    /// segundo item da trilha para o GM. <c>null</c> onde a trilha sempre passa pela campanha (Personagem).
    /// </summary>
    public (string Rotulo, string Rota)? ListaDoGm { get; init; }

    /// <summary>
    /// Quais records o tipo usa no fio (Combate, Perícias, Características e a arma nova). É o fato que escolhe o record;
    /// as flags de interface (<see cref="TemArmaManual"/>, <see cref="TemAntecedentes"/>, <see cref="TemTracosRaciais"/>)
    /// só decidem o que a tela mostra. O record da Criatura passa pelo <see cref="AdaptadorDeCriatura"/>.
    /// </summary>
    public required FormatoDosDados Formato { get; init; }

    /// <summary>O endereço da busca de Características (<c>traits</c> ou <c>creature-exclusive-traits</c>).</summary>
    public required string UrlDeBuscaDeCaracteristicas { get; init; }

    /// <summary>O endereço da ficha ou, com <paramref name="sufixo"/>, de algo dentro dela (<c>weapons/w1</c>).</summary>
    public string Url(string sheetId, string sufixo = "") => $"{Rota}/{sheetId}{(sufixo == "" ? "" : "/" + sufixo)}";

    public static TipoDeFichaCliente Personagem { get; } = new("Personagem", "character-sheets")
    {
        TipoDaNotificacao = "character",
        Titulo = "Ficha de Personagem",
        CondicoesSoSeConcedida = false,
        TemLinhagemEVocacao = true,
        TemTracosRaciais = true,
        TituloDaEscolhaRacial = "Escolha suas Características Raciais",
        RecebeAutomaticamente = "Você também recebe automaticamente:",
        EditaNivelAManual = false,
        TemOrcamentoDePericias = true,
        TemAfinidades = true,
        AfinidadesLivresParaOGm = false,
        TemSinaEEstresse = true,
        RotuloDoFoco = "Foco (PF)",
        RotuloDosPontosDeIgnicao = "Pontos de Ignição Atual",
        MostraProgressaoDoNivel = true,
        CadastraItemNoPicker = false,
        CriaCaracteristicaNoPicker = false,
        MostraPesoDasArmas = true,
        TemArmaManual = false,
        EditaAfeicaoNaLinha = true,
        TemRunas = true,
        TemInventario = true,
        TemEspolios = false,
        TemAntecedentes = true,
        TemDiario = true,
        Formato = FormatoDosDados.Comum,
        TemCemiterio = true,
        UrlDeBuscaDeCaracteristicas = "traits",
    };

    public static TipoDeFichaCliente Npc { get; } = new("NPC", "npc-sheets")
    {
        TipoDaNotificacao = "npc",
        Titulo = "Ficha de NPC",
        ListaDoGm = ("NPCs do GM", "npcs"),
        CondicoesSoSeConcedida = true,
        TemLinhagemEVocacao = true,
        TemTracosRaciais = true,
        TituloDaEscolhaRacial = "Escolha a Característica Racial do NPC",
        RecebeAutomaticamente = "O NPC também recebe automaticamente:",
        EditaNivelAManual = true,
        TemOrcamentoDePericias = false,
        TemAfinidades = true,
        AfinidadesLivresParaOGm = true,
        TemSinaEEstresse = true,
        RotuloDoFoco = "Foco (PF)",
        RotuloDosPontosDeIgnicao = "Pontos de Ignição Atual",
        MostraProgressaoDoNivel = true,
        CadastraItemNoPicker = true,
        CriaCaracteristicaNoPicker = false,
        AjudaDoXpPorAbate = "XP que o NPC concede aos jogadores ao ser derrotado: Abate para quem deu o golpe final, Assistência para quem ajudou. Calculado a partir da Experiência atual do NPC; só o GM vê.",
        MostraPesoDasArmas = true,
        TemArmaManual = false,
        EditaAfeicaoNaLinha = false,
        TemRunas = true,
        TemInventario = true,
        TemEspolios = false,
        TemAntecedentes = true,
        TemDiario = false,
        Formato = FormatoDosDados.Comum,
        TemCemiterio = false,
        UrlDeBuscaDeCaracteristicas = "traits",
    };

    public static TipoDeFichaCliente Criatura { get; } = new("Criatura", "creature-sheets")
    {
        TipoDaNotificacao = "creature",
        Titulo = "Ficha de Criatura",
        ListaDoGm = ("Bestiário do GM", "bestiario"),
        CondicoesSoSeConcedida = true,
        TemLinhagemEVocacao = false,
        TemTracosRaciais = false,
        TituloDaEscolhaRacial = null,
        RecebeAutomaticamente = null,
        EditaNivelAManual = true,
        TemOrcamentoDePericias = false,
        TemAfinidades = false,
        AfinidadesLivresParaOGm = false,
        TemSinaEEstresse = false,
        RotuloDoFoco = "Arcana (PF)",
        RotuloDosPontosDeIgnicao = "Pontos de Ignição",
        MostraProgressaoDoNivel = false,
        CadastraItemNoPicker = true,
        CriaCaracteristicaNoPicker = true,
        MostraPesoDasArmas = false,
        TemArmaManual = true,
        EditaAfeicaoNaLinha = false,
        TemRunas = false,
        TemInventario = false,
        TemEspolios = true,
        TemAntecedentes = false,
        TemDiario = false,
        Formato = FormatoDosDados.Criatura,
        TemCemiterio = false,
        UrlDeBuscaDeCaracteristicas = "creature-exclusive-traits",
    };
}
