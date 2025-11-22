using System.ComponentModel.DataAnnotations;

namespace NFE.Models
{
    /// <summary>
    /// ViewModel para receber dados de NFS-e (Nota Fiscal de Serviço Eletrônica) - Modelo 2026
    /// </summary>
    public class NFSeViewModel
    {
        [Required(ErrorMessage = "Dados de identificação são obrigatórios")]
        public NFSeIdentificacaoViewModel Identificacao { get; set; } = new();

        [Required(ErrorMessage = "Dados do prestador são obrigatórios")]
        public PrestadorViewModel Prestador { get; set; } = new();

        [Required(ErrorMessage = "Dados do tomador são obrigatórios")]
        public TomadorViewModel Tomador { get; set; } = new();

        [Required(ErrorMessage = "Pelo menos um serviço é obrigatório")]
        [MinLength(1, ErrorMessage = "Deve haver pelo menos um serviço")]
        public List<ServicoViewModel> Servicos { get; set; } = new();

        public PagamentoNFSeViewModel? Pagamento { get; set; }
        public string? InformacoesAdicionais { get; set; }

        // Totais de IBS/CBS (Reforma Tributária 2026)
        public IBSCBSTotNFSeViewModel? IBSCBSTot { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor total da NFS-e com impostos por fora deve ser maior ou igual a zero")]
        public decimal? ValorNFSeTot { get; set; }
    }

    /// <summary>
    /// Identificação da NFS-e
    /// </summary>
    public class NFSeIdentificacaoViewModel
    {
        [Required(ErrorMessage = "Código da UF é obrigatório")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Código da UF deve ter 2 caracteres")]
        public string CodigoUF { get; set; } = "35";

        [Required(ErrorMessage = "Código do município é obrigatório")]
        [StringLength(7, MinimumLength = 7, ErrorMessage = "Código do município deve ter 7 caracteres")]
        public string CodigoMunicipio { get; set; } = "3550308";

        [Required(ErrorMessage = "Natureza da operação é obrigatória")]
        [StringLength(60, ErrorMessage = "Natureza da operação deve ter no máximo 60 caracteres")]
        public string NaturezaOperacao { get; set; } = "PRESTAÇÃO DE SERVIÇOS";

        [Required(ErrorMessage = "Regime especial de tributação é obrigatório")]
        [RegularExpression("^(1|2|3|4|5|6)$", ErrorMessage = "Regime especial inválido")]
        public string RegimeEspecialTributacao { get; set; } = "1"; // 1=Microempresa Municipal, 2=Estimativa, 3=Sociedade de Profissionais, 4=Cooperativa, 5=MEI, 6=ME EPP

        [Required(ErrorMessage = "Optante pelo simples nacional é obrigatório")]
        [RegularExpression("^(1|2)$", ErrorMessage = "Optante pelo simples nacional deve ser 1 (Sim) ou 2 (Não)")]
        public string OptanteSimplesNacional { get; set; } = "2";

        [Required(ErrorMessage = "Incentivador cultural é obrigatório")]
        [RegularExpression("^(1|2)$", ErrorMessage = "Incentivador cultural deve ser 1 (Sim) ou 2 (Não)")]
        public string IncentivadorCultural { get; set; } = "2";

        [Required(ErrorMessage = "Número da NFS-e é obrigatório")]
        [Range(1, int.MaxValue, ErrorMessage = "Número da NFS-e deve ser maior que zero")]
        public int NumeroNFSe { get; set; } = 1;

        // Código de verificação é gerado pela prefeitura APÓS a emissão, não é obrigatório na criação
        [StringLength(8, MinimumLength = 8, ErrorMessage = "Código de verificação deve ter 8 caracteres")]
        public string? CodigoVerificacao { get; set; }

        [Required(ErrorMessage = "Data de emissão é obrigatória")]
        public DateTime DataEmissao { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Ambiente é obrigatório")]
        [RegularExpression("^(1|2)$", ErrorMessage = "Ambiente deve ser 1 (Produção) ou 2 (Homologação)")]
        public string Ambiente { get; set; } = "2";

        [StringLength(7, MinimumLength = 7, ErrorMessage = "Código do município de fato gerador IBS deve ter 7 caracteres")]
        public string? CodigoMunicipioFGIBS { get; set; }

        [RegularExpression("^(0|1|2|3|4|5|9)$", ErrorMessage = "Indicador de presença inválido")]
        public string? IndicadorPresenca { get; set; }
    }

    /// <summary>
    /// Prestador de serviços (quem emite a NFS-e)
    /// </summary>
    public class PrestadorViewModel
    {
        [Required(ErrorMessage = "CNPJ é obrigatório")]
        [StringLength(14, MinimumLength = 14, ErrorMessage = "CNPJ deve ter 14 caracteres")]
        public string CNPJ { get; set; } = string.Empty;

        [StringLength(11, MinimumLength = 11, ErrorMessage = "CPF deve ter 11 caracteres")]
        public string? CPF { get; set; }

        [Required(ErrorMessage = "Razão social é obrigatória")]
        [StringLength(150, ErrorMessage = "Razão social deve ter no máximo 150 caracteres")]
        public string RazaoSocial { get; set; } = string.Empty;

        [StringLength(60, ErrorMessage = "Nome fantasia deve ter no máximo 60 caracteres")]
        public string? NomeFantasia { get; set; }

        [Required(ErrorMessage = "Inscrição municipal é obrigatória")]
        [StringLength(15, ErrorMessage = "Inscrição municipal deve ter no máximo 15 caracteres")]
        public string InscricaoMunicipal { get; set; } = string.Empty;

        [StringLength(14, ErrorMessage = "Inscrição estadual deve ter no máximo 14 caracteres")]
        public string? InscricaoEstadual { get; set; }

        [Required(ErrorMessage = "Endereço é obrigatório")]
        public EnderecoNFSeViewModel Endereco { get; set; } = new();

        [StringLength(20, ErrorMessage = "Telefone deve ter no máximo 20 caracteres")]
        public string? Telefone { get; set; }

        [StringLength(60, ErrorMessage = "Email deve ter no máximo 60 caracteres")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public string? Email { get; set; }
    }

    /// <summary>
    /// Tomador de serviços (quem recebe a NFS-e)
    /// </summary>
    public class TomadorViewModel
    {
        [Required(ErrorMessage = "Tipo é obrigatório (PJ, PF ou Estrangeiro)")]
        [RegularExpression("^(PJ|PF|Estrangeiro)$", ErrorMessage = "Tipo deve ser PJ, PF ou Estrangeiro")]
        public string Tipo { get; set; } = "PJ";

        [Required(ErrorMessage = "CNPJ, CPF ou NIF é obrigatório")]
        public string Documento { get; set; } = string.Empty;

        [Required(ErrorMessage = "Nome/Razão social é obrigatório")]
        [StringLength(150, ErrorMessage = "Nome/Razão social deve ter no máximo 150 caracteres")]
        public string NomeRazaoSocial { get; set; } = string.Empty;

        [Required(ErrorMessage = "Endereço é obrigatório")]
        public EnderecoNFSeViewModel Endereco { get; set; } = new();

        [StringLength(20, ErrorMessage = "Telefone deve ter no máximo 20 caracteres")]
        public string? Telefone { get; set; }

        [StringLength(60, ErrorMessage = "Email deve ter no máximo 60 caracteres")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        public string? Email { get; set; }

        [StringLength(14, ErrorMessage = "Inscrição estadual deve ter no máximo 14 caracteres")]
        public string? InscricaoEstadual { get; set; }

        [StringLength(15, ErrorMessage = "Inscrição municipal deve ter no máximo 15 caracteres")]
        public string? InscricaoMunicipal { get; set; }
    }

    /// <summary>
    /// Endereço para NFS-e
    /// </summary>
    public class EnderecoNFSeViewModel
    {
        [Required(ErrorMessage = "Logradouro é obrigatório")]
        [StringLength(125, ErrorMessage = "Logradouro deve ter no máximo 125 caracteres")]
        public string Logradouro { get; set; } = string.Empty;

        [Required(ErrorMessage = "Número é obrigatório")]
        [StringLength(10, ErrorMessage = "Número deve ter no máximo 10 caracteres")]
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
    }

    /// <summary>
    /// Serviço prestado
    /// </summary>
    public class ServicoViewModel
    {
        [Required(ErrorMessage = "Código do serviço é obrigatório")]
        [StringLength(20, ErrorMessage = "Código do serviço deve ter no máximo 20 caracteres")]
        public string Codigo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Descrição do serviço é obrigatória")]
        [StringLength(2000, ErrorMessage = "Descrição deve ter no máximo 2000 caracteres")]
        public string Descricao { get; set; } = string.Empty;

        [Required(ErrorMessage = "Código de classificação do serviço (LC 116) é obrigatório")]
        [RegularExpression(@"^\d{4}-\d{2}$", ErrorMessage = "Código de classificação deve estar no formato XXXX-XX (ex: 0101-01)")]
        [StringLength(7, MinimumLength = 7, ErrorMessage = "Código de classificação deve ter 7 caracteres (formato XXXX-XX)")]
        public string CodigoClassificacao { get; set; } = string.Empty; // Ex: 0101-01

        [Required(ErrorMessage = "Código de tributação municipal é obrigatório")]
        [StringLength(20, ErrorMessage = "Código de tributação municipal deve ter no máximo 20 caracteres")]
        public string CodigoTributacaoMunicipal { get; set; } = string.Empty;

        [Required(ErrorMessage = "Discriminação do serviço é obrigatória")]
        [StringLength(2000, ErrorMessage = "Discriminação deve ter no máximo 2000 caracteres")]
        public string Discriminacao { get; set; } = string.Empty;

        [Required(ErrorMessage = "Código do município de prestação é obrigatório")]
        [StringLength(7, MinimumLength = 7, ErrorMessage = "Código do município deve ter 7 caracteres")]
        public string CodigoMunicipioPrestacao { get; set; } = string.Empty;

        [Required(ErrorMessage = "Quantidade é obrigatória")]
        [Range(0.0001, double.MaxValue, ErrorMessage = "Quantidade deve ser maior que zero")]
        public decimal Quantidade { get; set; } = 1.0m;

        [Required(ErrorMessage = "Valor unitário é obrigatório")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Valor unitário deve ser maior que zero")]
        public decimal ValorUnitario { get; set; }

        [Required(ErrorMessage = "Valor total do serviço é obrigatório")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Valor total deve ser maior que zero")]
        public decimal ValorTotal { get; set; }

        [StringLength(6, ErrorMessage = "Unidade deve ter no máximo 6 caracteres")]
        public string Unidade { get; set; } = "UN";

        public decimal ValorDeducoes { get; set; } = 0;
        public decimal ValorDescontoIncondicionado { get; set; } = 0;
        public decimal ValorDescontoCondicionado { get; set; } = 0;
        public decimal ValorOutrasRetencoes { get; set; } = 0;
        public decimal ValorLiquido { get; set; }

        // Tributação (ISS atual, IBS/CBS em 2026)
        public TributacaoServicoViewModel? Tributacao { get; set; }

        // IBS/CBS (Reforma Tributária 2026)
        public IBSCBSServicoViewModel? IBSCBS { get; set; }

        // Imposto Seletivo (IS)
        public ISServicoViewModel? ImpostoSeletivo { get; set; }
    }

    /// <summary>
    /// Tributação do serviço (ISS atual)
    /// </summary>
    public class TributacaoServicoViewModel
    {
        [Required(ErrorMessage = "Situação tributária é obrigatória")]
        [RegularExpression("^(00|01|02|03|04|05|06|07|08)$", ErrorMessage = "Situação tributária inválida")]
        public string SituacaoTributaria { get; set; } = "00"; // 00=Tributado, 01=Isento, etc.

        [Range(0, 100, ErrorMessage = "Alíquota deve estar entre 0 e 100")]
        public decimal? Aliquota { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor da base de cálculo deve ser maior ou igual a zero")]
        public decimal? ValorBaseCalculo { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor do ISS deve ser maior ou igual a zero")]
        public decimal? ValorISS { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor do PIS deve ser maior ou igual a zero")]
        public decimal? ValorPIS { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor da COFINS deve ser maior ou igual a zero")]
        public decimal? ValorCOFINS { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor do INSS deve ser maior ou igual a zero")]
        public decimal? ValorINSS { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor do IR deve ser maior ou igual a zero")]
        public decimal? ValorIR { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Valor do CSLL deve ser maior ou igual a zero")]
        public decimal? ValorCSLL { get; set; }
    }

    /// <summary>
    /// IBS/CBS para serviços (Reforma Tributária 2026)
    /// </summary>
    public class IBSCBSServicoViewModel
    {
        [Required(ErrorMessage = "CST do IBS/CBS é obrigatório")]
        [RegularExpression("^[0-9]{3}$", ErrorMessage = "CST deve ter 3 dígitos")]
        public string CST { get; set; } = string.Empty;

        [Required(ErrorMessage = "Código de classificação tributária é obrigatório")]
        [RegularExpression("^[0-9]{6}$", ErrorMessage = "Código de classificação tributária deve ter 6 dígitos")]
        public string CodigoClassificacaoTributaria { get; set; } = string.Empty;

        // Reutilizar os mesmos ViewModels de NFe para grupos IBS/CBS
        public IBSCBSGrupoViewModel? GrupoIBSCBS { get; set; }
        public IBSCBSMonofasiaViewModel? GrupoIBSCBSMonofasia { get; set; }
        public TransferenciaCreditoViewModel? GrupoTransferenciaCredito { get; set; }
        public AjusteCompetenciaViewModel? GrupoAjusteCompetencia { get; set; }
    }

    /// <summary>
    /// Imposto Seletivo para serviços
    /// </summary>
    public class ISServicoViewModel
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

        [Range(0, double.MaxValue, ErrorMessage = "Valor do Imposto Seletivo deve ser maior ou igual a zero")]
        public decimal? ValorIS { get; set; }
    }

    /// <summary>
    /// Totais de IBS/CBS para NFS-e
    /// </summary>
    public class IBSCBSTotNFSeViewModel
    {
        [Required(ErrorMessage = "Valor total da base de cálculo IBS/CBS é obrigatório")]
        [Range(0, double.MaxValue, ErrorMessage = "Valor deve ser maior ou igual a zero")]
        public decimal ValorBaseCalculoIBSCBS { get; set; }

        // Reutilizar os mesmos ViewModels de NFe para totais
        public IBSTotViewModel? GrupoIBSTot { get; set; }
        public CBSTotViewModel? GrupoCBSTot { get; set; }
        public EstornoCreditoTotViewModel? GrupoEstornoCreditoTot { get; set; }
        public MonofasiaTotViewModel? GrupoMonofasiaTot { get; set; }
    }

    /// <summary>
    /// Pagamento para NFS-e
    /// </summary>
    public class PagamentoNFSeViewModel
    {
        [Required(ErrorMessage = "Pelo menos uma forma de pagamento é obrigatória")]
        [MinLength(1, ErrorMessage = "Deve haver pelo menos uma forma de pagamento")]
        public List<FormaPagamentoNFSeViewModel> FormasPagamento { get; set; } = new();
    }

    /// <summary>
    /// Forma de pagamento para NFS-e
    /// </summary>
    public class FormaPagamentoNFSeViewModel
    {
        [Required(ErrorMessage = "Meio de pagamento é obrigatório")]
        [RegularExpression("^(01|02|03|04|05|10|11|12|13|15|16|17|18|19|90|99)$", 
            ErrorMessage = "Meio de pagamento inválido")]
        public string MeioPagamento { get; set; } = "01";

        [Required(ErrorMessage = "Valor do pagamento é obrigatório")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Valor deve ser maior que zero")]
        public decimal Valor { get; set; }

        public DateTime? DataVencimento { get; set; }
    }

    /// <summary>
    /// Request para emissão de NFS-e com certificado
    /// </summary>
    public class NFSeRequestViewModel
    {
        [Required(ErrorMessage = "Dados da NFS-e são obrigatórios")]
        public NFSeViewModel DadosNFSe { get; set; } = new();

        [Required(ErrorMessage = "Certificado digital é obrigatório")]
        public string CertificadoBase64 { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha do certificado é obrigatória")]
        public string SenhaCertificado { get; set; } = string.Empty;

        public string Ambiente { get; set; } = "homologacao";
    }
}

