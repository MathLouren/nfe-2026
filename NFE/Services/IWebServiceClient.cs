using NFE.Models;
using System.Security.Cryptography.X509Certificates;

namespace NFE.Services
{
    public interface IWebServiceClient
    {
        /// <summary>
        /// Envia NFe COM certificado digital (NOVO - Recomendado)
        /// </summary>
        Task<NFeResponseViewModel> EnviarNFeComCertificado(
            string soapEnvelope, 
            string ambiente, 
            X509Certificate2 certificado);

        /// <summary>
        /// Envia NFe SEM certificado (LEGADO - Para compatibilidade)
        /// </summary>
        [Obsolete("Use EnviarNFeComCertificado para produção")]
        Task<WebServiceResponse> EnviarNFeAsync(string xml, string ambiente);
    }

    /// <summary>
    /// Resposta do WebService (modelo legado)
    /// </summary>
    public class WebServiceResponse
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
        public string? XmlRetorno { get; set; }
        public string? Protocolo { get; set; }
        public string? ChaveAcesso { get; set; }
        public int? CodigoStatus { get; set; }
        public string? Motivo { get; set; }
        public Dictionary<string, string>? Erros { get; set; }
    }
}
