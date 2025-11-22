using System.ComponentModel.DataAnnotations;

namespace NFE.Models
{
    /// <summary>
    /// ViewModel para receber dados de NFe
    /// </summary>
    public class NFeViewModel
    {
        [Required(ErrorMessage = "O campo model é obrigatório")]
        public string Model { get; set; } = "55";    
        [Required(ErrorMessage = "Dados de identificação são obrigatórios")]
        public IdentificacaoViewModel Identificacao { get; set; } = new();

        [Required(ErrorMessage = "Dados do emitente são obrigatórios")]
        public EmitenteViewModel Emitente { get; set; } = new();

        [Required(ErrorMessage = "Dados do destinatário são obrigatórios")]
        public DestinatarioViewModel Destinatario { get; set; } = new();

        [Required(ErrorMessage = "Pelo menos um produto é obrigatório")]
        [MinLength(1, ErrorMessage = "Deve haver pelo menos um produto")]
        public List<ProdutoViewModel> Produtos { get; set; } = new();

        public TransporteViewModel? Transporte { get; set; }
        public CobrancaViewModel? Cobranca { get; set; }
        public PagamentoViewModel? Pagamento { get; set; }
        public string? InformacoesAdicionais { get; set; }

        public IBSCBSTotViewModel? IBSCBSTot { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor total da NF com impostos por fora deve ser maior ou igual a zero")]
        public decimal? ValorNFTot { get; set; }
    }

    /// <summary>
    /// ViewModel para formas de pagamento (obrigatório desde 2016)
    /// </summary>
    public class PagamentoViewModel
    {
        [Required(ErrorMessage = "Pelo menos uma forma de pagamento é obrigatória")]
        [MinLength(1, ErrorMessage = "Deve haver pelo menos uma forma de pagamento")]
        public List<FormaPagamentoViewModel> FormasPagamento { get; set; } = new();
    }

    /// <summary>
    /// Forma de pagamento individual
    /// </summary>
    public class FormaPagamentoViewModel
    {
        /// <summary>
        /// Indicador da Forma de Pagamento
        /// 0=Pagamento à Vista
        /// 1=Pagamento à Prazo
        /// </summary>
        [RegularExpression("^(0|1)$", ErrorMessage = "Indicador deve ser 0 (À vista) ou 1 (À prazo)")]
        public string? IndicadorPagamento { get; set; } = "0";

        /// <summary>
        /// Meio de pagamento
        /// 01=Dinheiro
        /// 02=Cheque
        /// 03=Cartão de Crédito
        /// 04=Cartão de Débito
        /// 05=Crédito Loja
        /// 10=Vale Alimentação
        /// 11=Vale Refeição
        /// 12=Vale Presente
        /// 13=Vale Combustível
        /// 15=Boleto Bancário
        /// 16=Depósito Bancário
        /// 17=Pagamento Instantâneo (PIX)
        /// 18=Transferência bancária, Carteira Digital
        /// 19=Programa de fidelidade, Cashback, Crédito Virtual
        /// 90=Sem pagamento
        /// 99=Outros
        /// </summary>
        [Required(ErrorMessage = "Meio de pagamento é obrigatório")]
        [RegularExpression("^(01|02|03|04|05|10|11|12|13|15|16|17|18|19|90|99)$", 
            ErrorMessage = "Meio de pagamento inválido")]
        public string MeioPagamento { get; set; } = "01";

        /// <summary>
        /// Valor do pagamento
        /// </summary>
        [Required(ErrorMessage = "Valor do pagamento é obrigatório")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Valor deve ser maior que zero")]
        public decimal Valor { get; set; }

        /// <summary>
        /// Dados do cartão (se meio = 03 ou 04)
        /// </summary>
        public CartaoViewModel? Cartao { get; set; }
    }

    /// <summary>
    /// Informações do cartão de crédito/débito
    /// </summary>
    public class CartaoViewModel
    {
        /// <summary>
        /// Tipo de Integração
        /// 1=Pagamento integrado com o sistema de automação da empresa
        /// 2=Pagamento não integrado com o sistema de automação da empresa
        /// </summary>
        [Required(ErrorMessage = "Tipo de integração é obrigatório")]
        [RegularExpression("^(1|2)$", ErrorMessage = "Tipo deve ser 1 (Integrado) ou 2 (Não integrado)")]
        public string TipoIntegracao { get; set; } = "2";

        /// <summary>
        /// CNPJ da Credenciadora de cartão
        /// </summary>
        [Required(ErrorMessage = "CNPJ da credenciadora é obrigatório")]
        [StringLength(14, MinimumLength = 14, ErrorMessage = "CNPJ deve ter 14 dígitos")]
        public string CNPJ { get; set; } = string.Empty;

        /// <summary>
        /// Bandeira do cartão
        /// 01=Visa
        /// 02=Mastercard
        /// 03=American Express
        /// 04=Sorocred
        /// 05=Diners Club
        /// 06=Elo
        /// 07=Hipercard
        /// 08=Aura
        /// 09=Cabal
        /// 99=Outros
        /// </summary>
        [Required(ErrorMessage = "Bandeira do cartão é obrigatória")]
        [RegularExpression("^(01|02|03|04|05|06|07|08|09|99)$", 
            ErrorMessage = "Bandeira inválida")]
        public string Bandeira { get; set; } = "01";

        /// <summary>
        /// Número de autorização da operação
        /// </summary>
        [StringLength(20, ErrorMessage = "Número de autorização deve ter no máximo 20 caracteres")]
        public string? NumeroAutorizacao { get; set; }
    }

    public class IdentificacaoViewModel
    {
        [Required(ErrorMessage = "Código da UF é obrigatório")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Código da UF deve ter 2 caracteres")]
        public string CodigoUF { get; set; } = "35";

        [Required(ErrorMessage = "Natureza da operação é obrigatória")]
        [StringLength(60, ErrorMessage = "Natureza da operação deve ter no máximo 60 caracteres")]
        public string NaturezaOperacao { get; set; } = "VENDA";

        [Required(ErrorMessage = "Modelo é obrigatório")]
        [RegularExpression("^(55|65)$", ErrorMessage = "Modelo deve ser 55 (NFe) ou 65 (NFCe)")]
        public string Modelo { get; set; } = "55";

        [Required(ErrorMessage = "Série é obrigatória")]
        [RegularExpression("^(0|[1-9][0-9]{0,2})$", ErrorMessage = "Série deve ser 0 ou um número entre 1 e 999 (não pode começar com 0, exceto se for exatamente '0')")]
        [StringLength(3, ErrorMessage = "Série deve ter no máximo 3 caracteres")]
        public string Serie { get; set; } = "1";

        [Required(ErrorMessage = "Número da nota é obrigatório")]
        [Range(1, int.MaxValue, ErrorMessage = "Número da nota deve ser maior que zero")]
        public int NumeroNota { get; set; } = 1;

        [Required(ErrorMessage = "Data de emissão é obrigatória")]
        public DateTime DataEmissao { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Data de saída/entrada é obrigatória")]
        public DateTime DataSaidaEntrada { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Tipo de operação é obrigatório")]
        [RegularExpression("^(0|1)$", ErrorMessage = "Tipo de operação deve ser 0 (Entrada) ou 1 (Saída)")]
        public string TipoOperacao { get; set; } = "1";

        [Required(ErrorMessage = "Código do município de fato gerador é obrigatório")]
        [StringLength(7, MinimumLength = 7, ErrorMessage = "Código do município deve ter 7 caracteres")]
        public string CodigoMunicipioFatoGerador { get; set; } = "3550308";

        [Required(ErrorMessage = "Tipo de impressão é obrigatório")]
        [RegularExpression("^(1|2|3|4)$", ErrorMessage = "Tipo de impressão inválido")]
        public string TipoImpressao { get; set; } = "1";

        [Required(ErrorMessage = "Tipo de emissão é obrigatório")]
        [RegularExpression("^(1|2|3|4|5|6|7|9)$", ErrorMessage = "Tipo de emissão inválido")]
        public string TipoEmissao { get; set; } = "1";

        [Required(ErrorMessage = "Ambiente é obrigatório")]
        [RegularExpression("^(1|2)$", ErrorMessage = "Ambiente deve ser 1 (Produção) ou 2 (Homologação)")]
        public string Ambiente { get; set; } = "2";

        [Required(ErrorMessage = "Finalidade é obrigatória")]
        [RegularExpression("^(1|2|3|4)$", ErrorMessage = "Finalidade inválida")]
        public string Finalidade { get; set; } = "1";

        [Required(ErrorMessage = "Indicador de consumidor final é obrigatório")]
        [RegularExpression("^(0|1)$", ErrorMessage = "Indicador deve ser 0 (Não) ou 1 (Sim)")]
        public string IndicadorConsumidorFinal { get; set; } = "0";

        [Required(ErrorMessage = "Indicador de presença é obrigatório")]
        [RegularExpression("^(0|1|2|3|4|5|9)$", ErrorMessage = "Indicador de presença inválido")]
        public string IndicadorPresenca { get; set; } = "1";

        [StringLength(7, MinimumLength = 7, ErrorMessage = "Código do município IBS deve ter 7 caracteres")]
        public string? CodigoMunicipioFGIBS { get; set; }

        public DateTime? DataPrevisaoEntrega { get; set; }

        [RegularExpression("^(0|1)$", ErrorMessage = "Indicador de intermediador deve ser 0 ou 1")]
        public string? IndicadorIntermediador { get; set; }

        [RegularExpression("^(1|2|3|4|5|6)$", ErrorMessage = "Tipo de nota de débito inválido")]
        public string? TipoNFDebito { get; set; }

        [RegularExpression("^(1|2|3|4|5|6)$", ErrorMessage = "Tipo de nota de crédito inválido")]
        public string? TipoNFCredito { get; set; }

        [RegularExpression("^(0|1|2|3)$", ErrorMessage = "Processo de emissão deve ser 0, 1, 2 ou 3")]
        public string ProcessoEmissao { get; set; } = "0";

        [Required(ErrorMessage = "Identificador de local de destino é obrigatório")]
        [RegularExpression("^(1|2|3)$", ErrorMessage = "Identificador de local de destino deve ser 1, 2 ou 3")]
        public string IdentificadorLocalDestino { get; set; } = "1";
    }

    public class EmitenteViewModel
    {
        [Required(ErrorMessage = "CNPJ é obrigatório")]
        [StringLength(14, MinimumLength = 14, ErrorMessage = "CNPJ deve ter 14 caracteres")]
        public string CNPJ { get; set; } = string.Empty;

        [Required(ErrorMessage = "Razão social é obrigatória")]
        [StringLength(60, ErrorMessage = "Razão social deve ter no máximo 60 caracteres")]
        public string RazaoSocial { get; set; } = string.Empty;

        [StringLength(60, ErrorMessage = "Nome fantasia deve ter no máximo 60 caracteres")]
        public string? NomeFantasia { get; set; }

        [Required(ErrorMessage = "Inscrição estadual é obrigatória")]
        [StringLength(14, ErrorMessage = "Inscrição estadual deve ter no máximo 14 caracteres")]
        public string InscricaoEstadual { get; set; } = string.Empty;

        [Required(ErrorMessage = "Código de regime tributário é obrigatório")]
        [RegularExpression("^(1|2|3)$", ErrorMessage = "CRT deve ser 1 (Simples Nacional), 2 (Simples Nacional - excesso) ou 3 (Regime Normal)")]
        public string CodigoRegimeTributario { get; set; } = "1";

        [Required(ErrorMessage = "Endereço é obrigatório")]
        public EnderecoViewModel Endereco { get; set; } = new();
    }

    public class DestinatarioViewModel
    {
        [Required(ErrorMessage = "Tipo é obrigatório (PJ ou PF)")]
        [RegularExpression("^(PJ|PF)$", ErrorMessage = "Tipo deve ser PJ ou PF")]
        public string Tipo { get; set; } = "PJ";

        [Required(ErrorMessage = "CNPJ ou CPF é obrigatório")]
        public string Documento { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nome/Razão social é obrigatório")]
        [StringLength(60, ErrorMessage = "Nome/Razão social deve ter no máximo 60 caracteres")]
        public string NomeRazaoSocial { get; set; } = string.Empty;

        [Required(ErrorMessage = "Indicador de IE é obrigatório")]
        [RegularExpression("^(1|2|9)$", ErrorMessage = "Indicador de IE inválido")]
        public string IndicadorIE { get; set; } = "1";

        public string? InscricaoEstadual { get; set; }

        [Required(ErrorMessage = "Endereço é obrigatório")]
        public EnderecoViewModel Endereco { get; set; } = new();
    }

    public class EnderecoViewModel
    {
        [Required(ErrorMessage = "Logradouro é obrigatório")]
        [StringLength(60, ErrorMessage = "Logradouro deve ter no máximo 60 caracteres")]
        public string Logradouro { get; set; } = string.Empty;

        [Required(ErrorMessage = "Número é obrigatório")]
        [StringLength(60, ErrorMessage = "Número deve ter no máximo 60 caracteres")]
        public string Numero { get; set; } = string.Empty;

        [StringLength(60, ErrorMessage = "Complemento deve ter no máximo 60 caracteres")]
        public string? Complemento { get; set; }

        [Required(ErrorMessage = "Bairro é obrigatório")]
        [StringLength(60, ErrorMessage = "Bairro deve ter no máximo 60 caracteres")]
        public string Bairro { get; set; } = string.Empty;

        [Required(ErrorMessage = "Código do município é obrigatório")]
        [StringLength(7, MinimumLength = 7, ErrorMessage = "Código do município deve ter 7 caracteres")]
        public string CodigoMunicipio { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nome do município é obrigatório")]
        [StringLength(60, ErrorMessage = "Nome do município deve ter no máximo 60 caracteres")]
        public string NomeMunicipio { get; set; } = string.Empty;

        [Required(ErrorMessage = "UF é obrigatória")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "UF deve ter 2 caracteres")]
        public string UF { get; set; } = string.Empty;

        [Required(ErrorMessage = "CEP é obrigatório")]
        [StringLength(8, MinimumLength = 8, ErrorMessage = "CEP deve ter 8 caracteres")]
        public string CEP { get; set; } = string.Empty;

        [StringLength(14, ErrorMessage = "Telefone deve ter no máximo 14 caracteres")]
        public string? Telefone { get; set; }
    }

    public class ProdutoViewModel
    {
        [Required(ErrorMessage = "Código do produto é obrigatório")]
        [StringLength(60, ErrorMessage = "Código do produto deve ter no máximo 60 caracteres")]
        public string Codigo { get; set; } = string.Empty;

        [StringLength(14, ErrorMessage = "EAN deve ter no máximo 14 caracteres")]
        public string? EAN { get; set; }

        [Required(ErrorMessage = "Descrição do produto é obrigatória")]
        [StringLength(120, ErrorMessage = "Descrição deve ter no máximo 120 caracteres")]
        public string Descricao { get; set; } = string.Empty;

        [Required(ErrorMessage = "NCM é obrigatório")]
        [StringLength(8, MinimumLength = 8, ErrorMessage = "NCM deve ter 8 caracteres")]
        public string NCM { get; set; } = string.Empty;

        [Required(ErrorMessage = "CFOP é obrigatório")]
        [StringLength(4, MinimumLength = 4, ErrorMessage = "CFOP deve ter 4 caracteres")]
        public string CFOP { get; set; } = string.Empty;

        [Required(ErrorMessage = "Unidade comercial é obrigatória")]
        [StringLength(6, ErrorMessage = "Unidade comercial deve ter no máximo 6 caracteres")]
        public string UnidadeComercial { get; set; } = "UN";

        [Required(ErrorMessage = "Quantidade comercial é obrigatória")]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero")]
        public decimal QuantidadeComercial { get; set; }

        [Required(ErrorMessage = "Valor unitário comercial é obrigatório")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Valor unitário deve ser maior que zero")]
        public decimal ValorUnitarioComercial { get; set; }

        [Required(ErrorMessage = "Valor total do produto é obrigatório")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Valor total deve ser maior que zero")]
        public decimal ValorTotal { get; set; }

        [StringLength(6, ErrorMessage = "Unidade tributável deve ter no máximo 6 caracteres")]
        public string? UnidadeTributavel { get; set; }

        public decimal? QuantidadeTributavel { get; set; }
        public decimal? ValorUnitarioTributavel { get; set; }
        public decimal ValorFrete { get; set; } = 0;
        public decimal ValorSeguro { get; set; } = 0;
        public decimal ValorDesconto { get; set; } = 0;
        public decimal ValorOutros { get; set; } = 0;

        [Required(ErrorMessage = "Indicador de total é obrigatório")]
        [RegularExpression("^(0|1)$", ErrorMessage = "Indicador deve ser 0 ou 1")]
        public string IndicadorTotal { get; set; } = "1";

        public IBSCBSViewModel? IBSCBS { get; set; }

        public ISViewModel? ImpostoSeletivo { get; set; }
    }

    public class IBSCBSViewModel
    {
        [Required(ErrorMessage = "CST do IBS/CBS é obrigatório")]
        [RegularExpression("^[0-9]{3}$", ErrorMessage = "CST deve ter 3 dígitos")]
        public string CST { get; set; } = string.Empty;

        [Required(ErrorMessage = "Código de classificação tributária é obrigatório")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "Código de classificação tributária deve ter 6 dígitos")]
        public string CodigoClassificacaoTributaria { get; set; } = string.Empty;

        [RegularExpression("^1$", ErrorMessage = "Indicador de doação deve ser 1")]
        public string? IndicadorDoacao { get; set; }

        public IBSCBSGrupoViewModel? GrupoIBSCBS { get; set; }

        public IBSCBSMonofasiaViewModel? GrupoIBSCBSMonofasia { get; set; }

        public TransferenciaCreditoViewModel? GrupoTransferenciaCredito { get; set; }

        public AjusteCompetenciaViewModel? GrupoAjusteCompetencia { get; set; }

        public EstornoCreditoViewModel? GrupoEstornoCredito { get; set; }

        public CreditoPresumidoOperacaoViewModel? GrupoCreditoPresumidoOperacao { get; set; }

        public CreditoPresumidoIBSZFMViewModel? GrupoCreditoPresumidoIBSZFM { get; set; }
    }

    public class IBSCBSGrupoViewModel
    {
        [Required(ErrorMessage = "Valor da base de cálculo é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor da base de cálculo deve ser maior ou igual a zero")]
        public decimal ValorBaseCalculo { get; set; }

        [Required(ErrorMessage = "Grupo IBS UF é obrigatório")]
        public IBSGrupoUFViewModel GrupoIBSUF { get; set; } = new();

        [Required(ErrorMessage = "Grupo IBS Município é obrigatório")]
        public IBSGrupoMunicipioViewModel GrupoIBSMunicipio { get; set; } = new();

        [Required(ErrorMessage = "Valor do IBS é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor do IBS deve ser maior ou igual a zero")]
        public decimal ValorIBS { get; set; }

        [Required(ErrorMessage = "Grupo CBS é obrigatório")]
        public CBSGrupoViewModel GrupoCBS { get; set; } = new();
    }

    public class IBSGrupoUFViewModel
    {
        [Required(ErrorMessage = "Alíquota do IBS UF é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal AliquotaIBSUF { get; set; }

        public DiferimentoViewModel? GrupoDiferimento { get; set; }

        public DevolucaoTributoViewModel? GrupoDevolucaoTributo { get; set; }

        public ReducaoAliquotaViewModel? GrupoReducaoAliquota { get; set; }

        [Required(ErrorMessage = "Valor do IBS UF é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor do IBS UF deve ser maior ou igual a zero")]
        public decimal ValorIBSUF { get; set; }
    }

    public class IBSGrupoMunicipioViewModel
    {
        [Required(ErrorMessage = "Alíquota do IBS Município é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal AliquotaIBSMunicipio { get; set; }

        public DiferimentoViewModel? GrupoDiferimento { get; set; }

        public DevolucaoTributoViewModel? GrupoDevolucaoTributo { get; set; }

        public ReducaoAliquotaViewModel? GrupoReducaoAliquota { get; set; }

        [Required(ErrorMessage = "Valor do IBS Município é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor do IBS Município deve ser maior ou igual a zero")]
        public decimal ValorIBSMunicipio { get; set; }
    }

    public class CBSGrupoViewModel
    {
        [Required(ErrorMessage = "Alíquota da CBS é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal AliquotaCBS { get; set; }

        public DiferimentoViewModel? GrupoDiferimento { get; set; }

        public DevolucaoTributoViewModel? GrupoDevolucaoTributo { get; set; }

        public ReducaoAliquotaViewModel? GrupoReducaoAliquota { get; set; }

        [Required(ErrorMessage = "Valor da CBS é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor da CBS deve ser maior ou igual a zero")]
        public decimal ValorCBS { get; set; }
    }

    public class DiferimentoViewModel
    {
        [Required(ErrorMessage = "Percentual de diferimento é obrigatório")]
        [Range(0, 100, ErrorMessage = "Percentual deve estar entre 0 e 100")]
        public decimal PercentualDiferimento { get; set; }

        [Required(ErrorMessage = "Valor do diferimento é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor do diferimento deve ser maior ou igual a zero")]
        public decimal ValorDiferimento { get; set; }
    }

    public class DevolucaoTributoViewModel
    {
        [Required(ErrorMessage = "Valor da devolução de tributo é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor da devolução deve ser maior ou igual a zero")]
        public decimal ValorDevolucaoTributo { get; set; }
    }

    public class ReducaoAliquotaViewModel
    {
        [Required(ErrorMessage = "Percentual de redução de alíquota é obrigatório")]
        [Range(0, 100, ErrorMessage = "Percentual deve estar entre 0 e 100")]
        public decimal PercentualReducaoAliquota { get; set; }

        [Required(ErrorMessage = "Alíquota efetiva é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota efetiva deve estar entre 0 e 100")]
        public decimal AliquotaEfetiva { get; set; }
    }

    public class IBSCBSMonofasiaViewModel
    {
        public MonofasiaPadraoViewModel? GrupoMonofasiaPadrao { get; set; }

        public MonofasiaRetencaoViewModel? GrupoMonofasiaRetencao { get; set; }

        public MonofasiaRetidoAnteriormenteViewModel? GrupoMonofasiaRetidoAnteriormente { get; set; }

        public MonofasiaDiferimentoViewModel? GrupoMonofasiaDiferimento { get; set; }

        [Required(ErrorMessage = "Total de IBS monofásico do item é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Total deve ser maior ou igual a zero")]
        public decimal TotalIBSMonofasicoItem { get; set; }

        [Required(ErrorMessage = "Total de CBS monofásica do item é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Total deve ser maior ou igual a zero")]
        public decimal TotalCBSMonofasicaItem { get; set; }
    }

    public class MonofasiaPadraoViewModel
    {
        [Required(ErrorMessage = "Quantidade tributada na monofasia é obrigatória")]
        [Range(0, double.MaxValue, ErrorMessage = "Quantidade deve ser maior ou igual a zero")]
        public decimal QuantidadeTributada { get; set; }

        [Required(ErrorMessage = "Alíquota ad rem do IBS é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal AliquotaAdRemIBS { get; set; }

        [Required(ErrorMessage = "Alíquota ad rem da CBS é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal AliquotaAdRemCBS { get; set; }

        [Required(ErrorMessage = "Valor do IBS monofásico é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSMonofasico { get; set; }

        [Required(ErrorMessage = "Valor da CBS monofásica é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSMonofasica { get; set; }
    }

    public class MonofasiaRetencaoViewModel
    {
        [Required(ErrorMessage = "Quantidade tributada sujeita a retenção é obrigatória")]
        [Range(0, double.MaxValue, ErrorMessage = "Quantidade deve ser maior ou igual a zero")]
        public decimal QuantidadeTributadaRetencao { get; set; }

        [Required(ErrorMessage = "Alíquota ad rem do IBS sujeito a retenção é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal AliquotaAdRemIBSRetencao { get; set; }

        [Required(ErrorMessage = "Valor do IBS monofásico sujeito a retenção é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSMonofasicoRetencao { get; set; }

        [Required(ErrorMessage = "Alíquota ad rem da CBS sujeita a retenção é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal AliquotaAdRemCBSRetencao { get; set; }

        [Required(ErrorMessage = "Valor da CBS monofásica sujeita a retenção é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSMonofasicaRetencao { get; set; }
    }

    public class MonofasiaRetidoAnteriormenteViewModel
    {
        [Required(ErrorMessage = "Quantidade tributada retida anteriormente é obrigatória")]
        [Range(0, double.MaxValue, ErrorMessage = "Quantidade deve ser maior ou igual a zero")]
        public decimal QuantidadeTributadaRetida { get; set; }

        [Required(ErrorMessage = "Alíquota ad rem do IBS retido anteriormente é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal AliquotaAdRemIBSRetido { get; set; }

        [Required(ErrorMessage = "Valor do IBS retido anteriormente é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSRetidoAnteriormente { get; set; }

        [Required(ErrorMessage = "Alíquota ad rem da CBS retida anteriormente é obrigatória")]
        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal AliquotaAdRemCBSRetida { get; set; }

        [Required(ErrorMessage = "Valor da CBS retida anteriormente é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSRetidaAnteriormente { get; set; }
    }

    public class MonofasiaDiferimentoViewModel
    {
        [Required(ErrorMessage = "Percentual do diferimento do imposto monofásico é obrigatório")]
        [Range(0, 100, ErrorMessage = "Percentual deve estar entre 0 e 100")]
        public decimal PercentualDiferimentoIBS { get; set; }

        [Required(ErrorMessage = "Valor do IBS monofásico diferido é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSMonofasicoDiferido { get; set; }

        [Required(ErrorMessage = "Percentual do diferimento da CBS monofásica é obrigatório")]
        [Range(0, 100, ErrorMessage = "Percentual deve estar entre 0 e 100")]
        public decimal PercentualDiferimentoCBS { get; set; }

        [Required(ErrorMessage = "Valor da CBS monofásica diferida é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSMonofasicaDiferida { get; set; }
    }

    public class TransferenciaCreditoViewModel
    {
        [Required(ErrorMessage = "Valor do IBS a ser transferido é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSTransferir { get; set; }

        [Required(ErrorMessage = "Valor da CBS a ser transferida é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSTransferir { get; set; }
    }

    public class AjusteCompetenciaViewModel
    {
        [Required(ErrorMessage = "Competência de apuração é obrigatória (AAAA-MM)")]
        [RegularExpression("^20[0-9]{2}-(0[1-9]|1[0-2])$", ErrorMessage = "Competência deve estar no formato AAAA-MM")]
        public string CompetenciaApuracao { get; set; } = string.Empty;

        [Required(ErrorMessage = "Valor do IBS é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBS { get; set; }

        [Required(ErrorMessage = "Valor da CBS é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBS { get; set; }
    }

    public class EstornoCreditoViewModel
    {
        [Required(ErrorMessage = "Valor do IBS a ser estornado é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSEstornar { get; set; }

        [Required(ErrorMessage = "Valor da CBS a ser estornada é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSEstornar { get; set; }
    }

    public class CreditoPresumidoOperacaoViewModel
    {
        [Required(ErrorMessage = "Valor da base de cálculo do crédito presumido é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorBaseCalculoCreditoPresumido { get; set; }

        [Required(ErrorMessage = "Código de classificação do crédito presumido é obrigatório")]
        [RegularExpression("^[0-9]{2}$", ErrorMessage = "Código deve ter 2 dígitos")]
        public string CodigoCreditoPresumido { get; set; } = string.Empty;

        public CreditoPresumidoViewModel? GrupoIBSCreditoPresumido { get; set; }

        public CreditoPresumidoViewModel? GrupoCBSCreditoPresumido { get; set; }
    }

    public class CreditoPresumidoViewModel
    {
        [Required(ErrorMessage = "Percentual do crédito presumido é obrigatório")]
        [Range(0, 100, ErrorMessage = "Percentual deve estar entre 0 e 100")]
        public decimal PercentualCreditoPresumido { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal? ValorCreditoPresumido { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal? ValorCreditoPresumidoCondicaoSuspensiva { get; set; }
    }

    public class CreditoPresumidoIBSZFMViewModel
    {
        [Required(ErrorMessage = "Competência de apuração é obrigatória (AAAA-MM)")]
        [RegularExpression("^20[0-9]{2}-(0[1-9]|1[0-2])$", ErrorMessage = "Competência deve estar no formato AAAA-MM")]
        public string CompetenciaApuracao { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tipo de crédito presumido IBS ZFM é obrigatório")]
        [RegularExpression("^(0|1|2|3|4)$", ErrorMessage = "Tipo deve ser 0, 1, 2, 3 ou 4")]
        public string TipoCreditoPresumidoIBSZFM { get; set; } = string.Empty;

        [Required(ErrorMessage = "Valor do crédito presumido IBS ZFM é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCreditoPresumidoIBSZFM { get; set; }
    }

    public class ISViewModel
    {
        [Required(ErrorMessage = "CST do Imposto Seletivo é obrigatório")]
        [RegularExpression("^[0-9]{3}$", ErrorMessage = "CST deve ter 3 dígitos")]
        public string CSTIS { get; set; } = string.Empty;

        [Required(ErrorMessage = "Código de classificação tributária do IS é obrigatório")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "Código deve ter 6 dígitos")]
        public string CodigoClassificacaoTributariaIS { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "Valor da base de cálculo deve ser maior ou igual a zero")]
        public decimal? ValorBaseCalculoIS { get; set; }

        [Range(0, 100, ErrorMessage = "Alíquota do Imposto Seletivo (percentual) deve estar entre 0 e 100")]
        public decimal? AliquotaIS { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Alíquota do Imposto Seletivo (por valor) deve ser maior ou igual a zero")]
        public decimal? AliquotaISEspecifica { get; set; }

        [StringLength(6, ErrorMessage = "Unidade de medida deve ter no máximo 6 caracteres")]
        public string? UnidadeTributaria { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Quantidade deve ser maior ou igual a zero")]
        public decimal? QuantidadeTributaria { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor do Imposto Seletivo deve ser maior ou igual a zero")]
        public decimal? ValorIS { get; set; }
    }

    public class IBSCBSTotViewModel
    {
        [Required(ErrorMessage = "Valor total da base de cálculo IBS/CBS é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorBaseCalculoIBSCBS { get; set; }

        public IBSTotViewModel? GrupoIBSTot { get; set; }

        public CBSTotViewModel? GrupoCBSTot { get; set; }

        public EstornoCreditoTotViewModel? GrupoEstornoCreditoTot { get; set; }

        public MonofasiaTotViewModel? GrupoMonofasiaTot { get; set; }
    }

    public class IBSTotViewModel
    {
        [Required(ErrorMessage = "Grupo IBS UF é obrigatório")]
        public IBSTotUFViewModel GrupoIBSUF { get; set; } = new();

        [Required(ErrorMessage = "Grupo IBS Município é obrigatório")]
        public IBSTotMunicipioViewModel GrupoIBSMunicipio { get; set; } = new();

        [Required(ErrorMessage = "Valor total do IBS é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBS { get; set; }
    }

    public class IBSTotUFViewModel
    {
        [Required(ErrorMessage = "Total do diferimento é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorDiferimento { get; set; }

        [Required(ErrorMessage = "Total de devoluções de tributos é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorDevolucaoTributos { get; set; }

        [Required(ErrorMessage = "Valor total do IBS Estadual é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSUF { get; set; }
    }

    public class IBSTotMunicipioViewModel
    {
        [Required(ErrorMessage = "Total do diferimento é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorDiferimento { get; set; }

        [Required(ErrorMessage = "Total de devoluções de tributos é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorDevolucaoTributos { get; set; }

        [Required(ErrorMessage = "Valor total do IBS Municipal é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSMunicipio { get; set; }
    }

    public class CBSTotViewModel
    {
        [Required(ErrorMessage = "Total do diferimento é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorDiferimento { get; set; }

        [Required(ErrorMessage = "Total de devoluções de tributos é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorDevolucaoTributos { get; set; }

        [Required(ErrorMessage = "Valor total da CBS é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBS { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Total do crédito presumido deve ser maior ou igual a zero")]
        public decimal? TotalCreditoPresumido { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Total do crédito presumido condição suspensiva deve ser maior ou igual a zero")]
        public decimal? TotalCreditoPresumidoCondicaoSuspensiva { get; set; }
    }

    public class EstornoCreditoTotViewModel
    {
        [Required(ErrorMessage = "Valor total do IBS estornado é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSEstornado { get; set; }

        [Required(ErrorMessage = "Valor total da CBS estornada é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSEstornada { get; set; }
    }

    public class MonofasiaTotViewModel
    {
        [Required(ErrorMessage = "Valor total do IBS monofásico é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSMonofasico { get; set; }

        [Required(ErrorMessage = "Valor total da CBS monofásica é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSMonofasica { get; set; }

        [Required(ErrorMessage = "Valor total do IBS monofásico sujeito a retenção é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSMonofasicoRetencao { get; set; }

        [Required(ErrorMessage = "Valor total da CBS monofásica sujeita a retenção é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSMonofasicaRetencao { get; set; }

        [Required(ErrorMessage = "Valor do IBS monofásico retido anteriormente é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorIBSMonofasicoRetido { get; set; }

        [Required(ErrorMessage = "Valor da CBS monofásica retida anteriormente é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorCBSMonofasicaRetida { get; set; }
    }

    public class TransporteViewModel
    {
        [Required(ErrorMessage = "Modalidade de frete é obrigatória")]
        [RegularExpression("^(0|1|2|3|4|9)$", ErrorMessage = "Modalidade de frete inválida")]
        public string ModalidadeFrete { get; set; } = "0";

        public TransportadoraViewModel? Transportadora { get; set; }
    }

    public class TransportadoraViewModel
    {
        [StringLength(14, MinimumLength = 14, ErrorMessage = "CNPJ deve ter 14 caracteres")]
        public string? CNPJ { get; set; }

        [StringLength(60, ErrorMessage = "Nome deve ter no máximo 60 caracteres")]
        public string? Nome { get; set; }

        [StringLength(14, ErrorMessage = "IE deve ter no máximo 14 caracteres")]
        public string? InscricaoEstadual { get; set; }

        [StringLength(60, ErrorMessage = "Endereço deve ter no máximo 60 caracteres")]
        public string? Endereco { get; set; }

        [StringLength(60, ErrorMessage = "Município deve ter no máximo 60 caracteres")]
        public string? Municipio { get; set; }

        [StringLength(2, MinimumLength = 2, ErrorMessage = "UF deve ter 2 caracteres")]
        public string? UF { get; set; }
    }

    public class CobrancaViewModel
    {
        public FaturaViewModel? Fatura { get; set; }
        public List<DuplicataViewModel> Duplicatas { get; set; } = new();
    }

    public class FaturaViewModel
    {
        [StringLength(60, ErrorMessage = "Número da fatura deve ter no máximo 60 caracteres")]
        public string? Numero { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor original deve ser maior ou igual a zero")]
        public decimal ValorOriginal { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor do desconto deve ser maior ou igual a zero")]
        public decimal ValorDesconto { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor líquido deve ser maior ou igual a zero")]
        public decimal ValorLiquido { get; set; }
    }

    public class DuplicataViewModel
    {
        [StringLength(60, ErrorMessage = "Número da duplicata deve ter no máximo 60 caracteres")]
        public string? Numero { get; set; }

        [Required(ErrorMessage = "Data de vencimento é obrigatória")]
        public DateTime DataVencimento { get; set; }

        [Required(ErrorMessage = "Valor da duplicata é obrigatório")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Valor deve ser maior que zero")]
        public decimal Valor { get; set; }
    }
    
    public class NFeRequestViewModel
    {
        [Required(ErrorMessage = "Dados da NFe são obrigatórios")]
        public NFeViewModel DadosNFe { get; set; } = new(); // ✅ Inicializar

        [Required(ErrorMessage = "Certificado digital é obrigatório")]
        public string CertificadoBase64 { get; set; } = string.Empty; // ✅ Inicializar

        [Required(ErrorMessage = "Senha do certificado é obrigatória")]
        public string SenhaCertificado { get; set; } = string.Empty; // ✅ Inicializar

        public string Ambiente { get; set; } = "homologacao";
    }


}

