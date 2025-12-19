using NFE.Models;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Security.Cryptography.X509Certificates;

namespace NFE.Services
{
    /// <summary>
    /// Serviço para gerar XML de eventos de NFS-e (cancelamento, substituição, etc.)
    /// </summary>
    public class EventoNFSeService
    {
        private readonly ILogger<EventoNFSeService> _logger;
        private static readonly XNamespace NsNFSe = "http://www.sped.fazenda.gov.br/nfse";
        private static readonly XNamespace NsDsig = "http://www.w3.org/2000/09/xmldsig#";
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public EventoNFSeService(ILogger<EventoNFSeService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gera XML de evento de cancelamento
        /// </summary>
        public string GerarEventoCancelamento(NFSeEventoViewModel evento)
        {
            try
            {
                _logger.LogInformation("Gerando evento de cancelamento - Chave: {Chave}", evento.ChaveAcesso);

                // Criar pedRegEvento
                var pedRegEvento = new XElement(NsNFSe + "pedRegEvento",
                    new XAttribute("versao", "1.00")
                );

                var infPedReg = new XElement(NsNFSe + "infPedReg",
                    new XAttribute("Id", $"ID{evento.ChaveAcesso}EVT{DateTime.Now:yyyyMMddHHmmss}")
                );

                // Ambiente
                infPedReg.Add(new XElement(NsNFSe + "tpAmb", evento.Ambiente == "producao" ? "1" : "2"));

                // Versão do aplicativo
                infPedReg.Add(new XElement(NsNFSe + "verAplic", "1.0.0"));

                // Data/Hora do evento
                infPedReg.Add(new XElement(NsNFSe + "dhEvento", FormatarDataHoraUTC(DateTime.Now)));

                // CNPJ ou CPF do autor
                if (evento.DocumentoAutor.Length == 14)
                {
                    infPedReg.Add(new XElement(NsNFSe + "CNPJAutor", RemoverFormatacao(evento.DocumentoAutor)));
                }
                else if (evento.DocumentoAutor.Length == 11)
                {
                    infPedReg.Add(new XElement(NsNFSe + "CPFAutor", RemoverFormatacao(evento.DocumentoAutor)));
                }

                // Chave da NFS-e
                infPedReg.Add(new XElement(NsNFSe + "chNFSe", evento.ChaveAcesso));

                // Número do pedido de registro de evento
                infPedReg.Add(new XElement(NsNFSe + "nPedRegEvento", "1"));

                // Evento de cancelamento
                if (evento.TipoEvento == "101101")
                {
                    var eventoCancelamento = new XElement(NsNFSe + "e101101");
                    eventoCancelamento.Add(new XElement(NsNFSe + "xDesc", "Cancelamento de NFS-e"));
                    eventoCancelamento.Add(new XElement(NsNFSe + "cMotivo", evento.CodigoJustificativa));
                    eventoCancelamento.Add(new XElement(NsNFSe + "xMotivo", evento.Motivo));
                    infPedReg.Add(eventoCancelamento);
                }
                else if (evento.TipoEvento == "105102")
                {
                    // Cancelamento por substituição
                    var eventoSubstituicao = new XElement(NsNFSe + "e105102");
                    eventoSubstituicao.Add(new XElement(NsNFSe + "xDesc", "Cancelamento de NFS-e por Substituicao"));
                    eventoSubstituicao.Add(new XElement(NsNFSe + "cMotivo", evento.CodigoJustificativa));
                    
                    if (!string.IsNullOrEmpty(evento.Motivo))
                    {
                        eventoSubstituicao.Add(new XElement(NsNFSe + "xMotivo", evento.Motivo));
                    }
                    
                    if (!string.IsNullOrEmpty(evento.ChaveSubstituta))
                    {
                        eventoSubstituicao.Add(new XElement(NsNFSe + "chSubstituta", evento.ChaveSubstituta));
                    }
                    
                    infPedReg.Add(eventoSubstituicao);
                }

                pedRegEvento.Add(infPedReg);

                // Criar evento completo
                var eventoCompleto = new XElement(NsNFSe + "evento",
                    new XAttribute("versao", "1.00")
                );

                var infEvento = new XElement(NsNFSe + "infEvento",
                    new XAttribute("Id", $"EVT{evento.ChaveAcesso}{DateTime.Now:yyyyMMddHHmmss}")
                );

                infEvento.Add(new XElement(NsNFSe + "verAplic", "1.0.0"));
                infEvento.Add(new XElement(NsNFSe + "ambGer", evento.Ambiente == "producao" ? "1" : "2"));
                infEvento.Add(new XElement(NsNFSe + "nSeqEvento", "1"));
                infEvento.Add(new XElement(NsNFSe + "dhProc", FormatarDataHoraUTC(DateTime.Now)));
                
                // Extrair número DFe da chave (últimos 15 dígitos)
                string nDFe = evento.ChaveAcesso.Length >= 15 
                    ? evento.ChaveAcesso.Substring(evento.ChaveAcesso.Length - 15) 
                    : evento.ChaveAcesso;
                infEvento.Add(new XElement(NsNFSe + "nDFe", nDFe));

                infEvento.Add(pedRegEvento);
                eventoCompleto.Add(infEvento);

                var xmlDocument = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    eventoCompleto
                );

                string xmlString = xmlDocument.ToString(SaveOptions.DisableFormatting);
                
                _logger.LogInformation("Evento gerado com sucesso - Tamanho: {Size} bytes", xmlString.Length);
                
                return xmlString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar evento");
                throw;
            }
        }

        private string FormatarDataHoraUTC(DateTime data)
        {
            var offset = TimeZoneInfo.Local.GetUtcOffset(data);
            var offsetString = $"{(offset.Hours >= 0 ? "+" : "-")}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
            return data.ToString("yyyy-MM-ddTHH:mm:ss", InvariantCulture) + offsetString;
        }

        private string RemoverFormatacao(string? valor)
        {
            if (string.IsNullOrEmpty(valor))
                return string.Empty;
            
            return Regex.Replace(valor, @"[^\d]", "");
        }
    }
}
