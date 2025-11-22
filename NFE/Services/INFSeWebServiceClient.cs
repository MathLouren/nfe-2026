using NFE.Models;

namespace NFE.Services
{
    /// <summary>
    /// Interface para cliente de webservice de NFS-e
    /// </summary>
    public interface INFSeWebServiceClient
    {
        /// <summary>
        /// Envia NFS-e para webservice
        /// </summary>
        Task<NFSeWebServiceResponse> EnviarNFSeAsync(string xml, string ambiente);

        /// <summary>
        /// Envia NFS-e com certificado digital
        /// </summary>
        Task<NFSeWebServiceResponse> EnviarNFSeComCertificado(
            string soapEnvelope, 
            string ambiente, 
            System.Security.Cryptography.X509Certificates.X509Certificate2 certificado);
    }

    /// <summary>
    /// Resposta do webservice de NFS-e
    /// </summary>
    public class NFSeWebServiceResponse
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
        public string? XmlRetorno { get; set; }
        public string? Protocolo { get; set; }
        public string? NumeroNFSe { get; set; }
        public string? CodigoVerificacao { get; set; }
        public string? CodigoStatus { get; set; }
        public string? Motivo { get; set; }
        public string? LinkConsulta { get; set; }
        public Dictionary<string, string>? Erros { get; set; }
    }
}

