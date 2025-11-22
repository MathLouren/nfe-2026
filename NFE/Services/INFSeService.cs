using NFE.Models;

namespace NFE.Services
{
    /// <summary>
    /// Interface do serviço de NFS-e
    /// </summary>
    public interface INFSeService
    {
        /// <summary>
        /// Processa NFS-e: gera XML e envia para webservice
        /// </summary>
        Task<NFSeResponseViewModel> ProcessarNFSeAsync(NFSeViewModel model, string ambiente);

        /// <summary>
        /// Gera XML da NFS-e
        /// </summary>
        Task<string> GerarXmlAsync(NFSeViewModel model);

        /// <summary>
        /// Valida XML da NFS-e
        /// </summary>
        Task<bool> ValidarXmlAsync(string xml);
    }
}

