using System.ComponentModel.DataAnnotations;

namespace NFE.Models
{
    /// <summary>
    /// ViewModel para eventos de NFS-e (cancelamento, substituição, etc.)
    /// </summary>
    public class NFSeEventoViewModel
    {
        [Required(ErrorMessage = "Chave de acesso da NFS-e é obrigatória")]
        [StringLength(50, MinimumLength = 50, ErrorMessage = "Chave de acesso deve ter 50 caracteres")]
        public string ChaveAcesso { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tipo de evento é obrigatório")]
        [RegularExpression("^(101101|105102)$", ErrorMessage = "Tipo de evento inválido. Use 101101 para cancelamento ou 105102 para cancelamento por substituição")]
        public string TipoEvento { get; set; } = "101101"; // 101101=Cancelamento, 105102=Cancelamento por Substituição

        [Required(ErrorMessage = "Código de justificativa é obrigatório")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Código de justificativa deve ter 2 caracteres")]
        public string CodigoJustificativa { get; set; } = string.Empty;

        [Required(ErrorMessage = "Motivo é obrigatório")]
        [StringLength(255, ErrorMessage = "Motivo deve ter no máximo 255 caracteres")]
        public string Motivo { get; set; } = string.Empty;

        // Para cancelamento por substituição
        [StringLength(50, MinimumLength = 50, ErrorMessage = "Chave da NFS-e substituta deve ter 50 caracteres")]
        public string? ChaveSubstituta { get; set; }

        [Required(ErrorMessage = "CNPJ ou CPF do autor do evento é obrigatório")]
        public string DocumentoAutor { get; set; } = string.Empty;

        public string Ambiente { get; set; } = "homologacao";
    }

    /// <summary>
    /// ViewModel para consulta de NFS-e
    /// </summary>
    public class NFSeConsultaViewModel
    {
        [Required(ErrorMessage = "Chave de acesso é obrigatória")]
        [StringLength(50, MinimumLength = 50, ErrorMessage = "Chave de acesso deve ter 50 caracteres")]
        public string ChaveAcesso { get; set; } = string.Empty;

        public string Ambiente { get; set; } = "homologacao";
    }

    /// <summary>
    /// ViewModel para substituição de NFS-e
    /// </summary>
    public class NFSeSubstituicaoViewModel
    {
        [Required(ErrorMessage = "Chave da NFS-e a ser substituída é obrigatória")]
        [StringLength(50, MinimumLength = 50, ErrorMessage = "Chave de acesso deve ter 50 caracteres")]
        public string ChaveSubstituida { get; set; } = string.Empty;

        [Required(ErrorMessage = "Código de justificativa é obrigatório")]
        [StringLength(2, MinimumLength = 2, ErrorMessage = "Código de justificativa deve ter 2 caracteres")]
        public string CodigoJustificativa { get; set; } = string.Empty;

        [StringLength(255, ErrorMessage = "Motivo deve ter no máximo 255 caracteres")]
        public string? Motivo { get; set; }

        [Required(ErrorMessage = "Dados da nova NFS-e são obrigatórios")]
        public NFSeViewModel DadosNFSeNova { get; set; } = new();

        [Required(ErrorMessage = "Certificado digital é obrigatório")]
        public string CertificadoBase64 { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha do certificado é obrigatória")]
        public string SenhaCertificado { get; set; } = string.Empty;

        public string Ambiente { get; set; } = "homologacao";
    }
}
