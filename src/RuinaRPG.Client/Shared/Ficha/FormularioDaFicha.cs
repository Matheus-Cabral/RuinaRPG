using System.ComponentModel.DataAnnotations;
using RuinaRPG.Contracts.Images;

namespace RuinaRPG.Client.Shared.Ficha;

/// <summary>
/// O formulário auto-salvo de uma ficha (Personagem, NPC ou Criatura): os campos da ficha que a página carrega
/// (<c>GET</c> da ficha), mostra nas abas e grava (o <c>PUT</c> geral e os endpoints próprios de Nível, Experiência e
/// PI). É da página: ela o cria, o preenche, monta o pedido do tipo dela e coordena o autosave; as abas o recebem como
/// parâmetro (<c>Formulario</c>), junto com os retornos da página que salvam. Um tipo só para os três: cada tipo usa os
/// campos que tem (a Criatura tem Raça e Rank, não Linhagem nem Âmbares) e ignora os outros.
/// <para>
/// A validação vale campo a campo (o <c>For</c> de cada campo MudBlazor com o <c>DataAnnotationsValidator</c>), nunca
/// do modelo inteiro: há <c>[Required]</c> em campos que um tipo não tem e nunca preenche (o VIS e os Núcleos de Rank
/// na Criatura, o Estresse na Criatura), então um <c>Validator.TryValidateObject</c> ou um <c>EditContext.Validate()</c>
/// sobre ele todo falharia sem motivo. Não há suporte a isso.
/// </para>
/// </summary>
public sealed class FormularioDaFicha(TipoDeFichaCliente tipo)
{
    /// <summary>O tipo da ficha: a mensagem de validação do PI usa o rótulo do campo nele.</summary>
    public TipoDeFichaCliente Tipo { get; } = tipo;

    public string? ImageId { get; set; }
    public string? ImageUrl { get; set; }
    public string? Nome { get; set; }

    // Personagem e NPC (TemLinhagemEVocacao).
    public string? Linhagem { get; set; }
    public string? Variante { get; set; }
    public string? Vocacao { get; set; }
    public string? SubVocacao { get; set; }
    public string? Propriedade { get; set; }

    // Criatura.
    public string? Raca { get; set; }
    public string? Arquetipo { get; set; }
    public string? SubArquetipo { get; set; }
    public string? Rank { get; set; }

    /// <summary>Id do elemento do catálogo de Afinidades.</summary>
    public int? Afinidade { get; set; }
    public string? Estrela { get; set; }
    public int SinaAtual { get; set; }
    public string? HistoricoId { get; set; }
    public string? Historia { get; set; }
    public string? EquipmentKitId { get; set; }

    /// <summary>
    /// No Personagem o Nível vem do servidor (calculado pela Experiência) e só é mostrado; NPC e Criatura o editam à mão
    /// (<see cref="TipoDeFichaCliente.EditaNivelAManual"/>).
    /// </summary>
    [Required(ErrorMessage = "Informe o Nível.")]
    public int? Nivel { get; set; } = 1;

    public int Graduacao { get; set; }
    public string GraduacaoLabel { get; set; } = "Grau";
    public bool PossuiCoracaoDeMana { get; set; }
    public int AfinidadeAdicional { get; set; }
    public int VocacaoArcanaMaxima { get; set; }

    [Required(ErrorMessage = "Informe a Experiência Atual.")]
    public int? ExperienciaAtual { get; set; }

    /// <summary>Só no Personagem: quanto falta para o próximo nível (null no nível máximo).</summary>
    public int? XpParaProximoNivel { get; set; }

    /// <summary>NPC e Criatura: calculados pela API. No NPC só vêm para o GM da ficha (null para o jogador dono).</summary>
    public int? Abate { get; set; }
    public int? Assistencia { get; set; }

    [Required(ErrorMessage = "Informe o VIS Atual.")]
    public int? EAPAtual { get; set; }

    [Required(ErrorMessage = "Informe os Núcleos de Rank F.")]
    public int? NucleosRankF { get; set; }
    [Required(ErrorMessage = "Informe os Núcleos de Rank E.")]
    public int? NucleosRankE { get; set; }
    [Required(ErrorMessage = "Informe os Núcleos de Rank D.")]
    public int? NucleosRankD { get; set; }
    [Required(ErrorMessage = "Informe os Núcleos de Rank C.")]
    public int? NucleosRankC { get; set; }
    [Required(ErrorMessage = "Informe os Núcleos de Rank B.")]
    public int? NucleosRankB { get; set; }
    [Required(ErrorMessage = "Informe os Núcleos de Rank A.")]
    public int? NucleosRankA { get; set; }
    [Required(ErrorMessage = "Informe os Núcleos de Rank S.")]
    public int? NucleosRankS { get; set; }

    /// <summary>O saldo de PI (na Criatura, <c>PontosDeIgnicao</c> no fio). Tem endpoint próprio; o PUT geral o ignora.</summary>
    [PontosDeIgnicaoObrigatorios]
    public int? PontosDeIgnicaoAtual { get; set; }

    /// <summary>Só no Personagem: o bônus manual de PI que o GM concede.</summary>
    public int PontosDeIgnicaoBonusManual { get; set; }

    /// <summary>Só no NPC. Não editável: o servidor ignora o Total do pedido; a propriedade só devolve o valor carregado.</summary>
    public int PontosDeIgnicaoTotal { get; set; }

    /// <summary>Só no Personagem: os pontos de Perícia ganhos por Acerto Crítico.</summary>
    public int PontosDePericiaBonusCritico { get; set; }

    [Required(ErrorMessage = "Informe a Vitalidade.")]
    public int? VitalidadeAtual { get; set; }
    [Required(ErrorMessage = "Informe o Foco.")]
    public int? FocoAtual { get; set; }
    [Required(ErrorMessage = "Informe a Adrenalina.")]
    public int? AdrenalinaAtual { get; set; }
    [Required(ErrorMessage = "Informe o Estresse.")]
    public int? EstresseAtual { get; set; }

    // Os Atuais como a ficha foi carregada: o servidor só grava o Atual que o jogador editou (ele também os escreve).
    public int? VitalidadeAtualCarregada { get; private set; }
    public int? FocoAtualCarregado { get; private set; }
    public int? AdrenalinaAtualCarregada { get; private set; }
    public int? EstresseAtualCarregado { get; private set; }

    /// <summary>Os Atuais como estão no formulário agora (os que um PUT montado agora envia).</summary>
    public (int? Vitalidade, int? Foco, int? Adrenalina, int? Estresse) Atuais => (VitalidadeAtual, FocoAtual, AdrenalinaAtual, EstresseAtual);

    public void GuardarAtuaisCarregados() => GuardarAtuaisCarregados(Atuais);

    /// <summary>
    /// Guarda como carregados os Atuais que o servidor acabou de gravar: um segundo PUT antes da releitura (adiada pelo
    /// auto-save) não reenvia os carregados antigos.
    /// </summary>
    public void GuardarAtuaisCarregados((int? Vitalidade, int? Foco, int? Adrenalina, int? Estresse) atuais) =>
        (VitalidadeAtualCarregada, FocoAtualCarregado, AdrenalinaAtualCarregada, EstresseAtualCarregado) = atuais;

    public string Cobertura { get; set; } = "Nenhuma";
    public int Ciclos { get; set; }
    public int? ArcaRolada { get; set; }
    public int VitalidadeMaximo { get; set; }
    public int FocoMaximo { get; set; }
    public int AdrenalinaMaximo { get; set; }
    public int EstresseMaximo { get; set; }

    /// <summary>
    /// O "Adicionar/subtrair XP" do Personagem: soma o valor com sinal à Experiência Atual, nunca abaixo de 0. Vazio ou 0
    /// não muda nada; o retorno diz se há o que salvar.
    /// </summary>
    public bool SomarExperiencia(int? delta)
    {
        if (delta is null || delta == 0)
            return false;

        ExperienciaAtual = Math.Max(0, (ExperienciaAtual ?? 0) + delta.Value);
        return true;
    }

    /// <summary>
    /// O "Adicionar/subtrair PI (bônus manual)" do Personagem (o GM também pode conceder PI). O servidor soma o mesmo
    /// valor ao saldo; a recarga depois de salvar o traz de volta. Vazio ou 0 não muda nada.
    /// </summary>
    public bool SomarBonusManualDePi(int? delta)
    {
        if (delta is null || delta == 0)
            return false;

        PontosDeIgnicaoBonusManual += delta.Value;
        return true;
    }

    /// <summary>
    /// O "Adicionar/subtrair pontos de Acerto Crítico" do Personagem: pontos de Perícia que não vêm do orçamento do
    /// nível; o servidor os desconta do Gasto Total. Vazio ou 0 não muda nada; o retorno diz se há o que salvar.
    /// </summary>
    public bool SomarPontosDeAcertoCritico(int? delta)
    {
        if (delta is null || delta == 0)
            return false;

        PontosDePericiaBonusCritico += delta.Value;
        return true;
    }

    /// <summary>Uma imagem recém-enviada vira a da ficha.</summary>
    public void UsarImagem(string id, string url)
    {
        ImageId = id;
        ImageUrl = url;
    }

    /// <summary>Uma imagem da galeria (ou nenhuma, com <c>null</c>) vira a da ficha.</summary>
    public void EscolherImagem(string? id, IEnumerable<ImageSummaryResponse> galeria)
    {
        ImageId = id;
        ImageUrl = id is null ? null : galeria.FirstOrDefault(i => i.Id == id)?.Url;
    }

    public void RemoverImagem()
    {
        ImageId = null;
        ImageUrl = null;
    }

    /// <summary>
    /// O PI é obrigatório, e a mensagem diz o nome do campo no tipo da ficha ("Informe os Pontos de Ignição Atual." no
    /// Personagem e no NPC, "Informe os Pontos de Ignição." na Criatura).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    private sealed class PontosDeIgnicaoObrigatoriosAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is not null)
                return ValidationResult.Success;

            var rotulo = (validationContext.ObjectInstance as FormularioDaFicha)?.Tipo.RotuloDosPontosDeIgnicao ?? "Pontos de Ignição Atual";
            return new ValidationResult($"Informe os {rotulo}.", validationContext.MemberName is { } membro ? new[] { membro } : null);
        }
    }
}
