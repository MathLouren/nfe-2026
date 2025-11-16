using System.ComponentModel.DataAnnotations;

namespace NFE.Models
{
    /// <summary>
    /// ViewModel para receber dados de NFe
    /// </summary>
    public class NFeViewModel
    {
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
        public string? InformacoesAdicionais { get; set; }
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
}

