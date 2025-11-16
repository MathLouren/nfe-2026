using NFE.Models;

namespace NFE.Services
{
    /// <summary>
    /// Interface do serviço de NFe
    /// </summary>
    public interface INFeService
    {
        /// <summary>
        /// Processa NFe: gera XML e envia para webservice
        /// </summary>
        Task<NFeResponseViewModel> ProcessarNFeAsync(NFeViewModel model, string ambiente);

        /// <summary>
        /// Gera XML da NFe
        /// </summary>
        Task<string> GerarXmlAsync(NFeViewModel model);

        /// <summary>
        /// Valida XML da NFe
        /// </summary>
        Task<bool> ValidarXmlAsync(string xml);
    }
}

