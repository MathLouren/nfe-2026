using NFE.Models;
using System.Text;
using System.Xml.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http.Headers;

namespace NFE.Services
{
    /// <summary>
    /// Cliente para webservice de NFS-e
    /// </summary>
    public class NFSeWebServiceClient : INFSeWebServiceClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<NFSeWebServiceClient> _logger;
        private readonly IConfiguration _configuration;

        public NFSeWebServiceClient(
            IHttpClientFactory httpClientFactory, 
            ILogger<NFSeWebServiceClient> logger, 
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<NFSeWebServiceResponse> EnviarNFSeAsync(string xml, string ambiente)
        {
            // Implementação básica - retorna simulação
            // Em produção, implementar comunicação real com webservice
            return await Task.FromResult(new NFSeWebServiceResponse
            {
                Sucesso = true,
                Mensagem = "NFS-e processada (simulação)",
                Protocolo = "999999999999999",
                NumeroNFSe = ExtrairNumeroNFSe(xml),
                CodigoVerificacao = ExtrairCodigoVerificacao(xml),
                CodigoStatus = "100",
                Motivo = "NFS-e autorizada (simulação)"
            });
        }

        public async Task<NFSeWebServiceResponse> EnviarNFSeComCertificado(
            string soapEnvelope, 
            string ambiente, 
            X509Certificate2 certificado)
        {
            try
            {
                _logger.LogInformation("Enviando NFS-e para webservice com certificado - Ambiente: {Ambiente}", ambiente);

                // Extrair município do envelope SOAP
                string codigoMunicipio = ExtrairCodigoMunicipioDoSOAP(soapEnvelope);
                _logger.LogInformation("Código do município detectado: {Codigo}", codigoMunicipio);

                // Determinar URL do webservice
                string url = ObterUrlWebService(codigoMunicipio, ambiente);
                _logger.LogInformation("URL do webservice: {Url}", url);

                // Configurar handler HTTP com certificado
                var handler = new HttpClientHandler();
                handler.ClientCertificates.Add(certificado);
                
                if (ambiente == "homologacao" || ambiente == "2")
                {
                    handler.ServerCertificateCustomValidationCallback = 
                        (sender, cert, chain, sslPolicyErrors) => true;
                }

                using var httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                // Criar conteúdo
                var content = new StringContent(
                    soapEnvelope, 
                    Encoding.UTF8, 
                    "text/xml"
                );

                // Headers obrigatórios
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("SOAPAction", 
                    "http://www.portalfiscal.inf.br/nfse/wsdl/NFSeAutorizacao/nfseAutorizacaoLote");

                // Enviar requisição
                var response = await httpClient.PostAsync(url, content);
                string responseXml = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Resposta recebida - Status: {StatusCode}, Tamanho: {Size} bytes", 
                    response.StatusCode, responseXml.Length);

                // Processar resposta
                if (response.IsSuccessStatusCode)
                {
                    return ProcessarRespostaWebService(responseXml, soapEnvelope);
                }
                else
                {
                    _logger.LogWarning("Erro HTTP: {Status} - {Response}", 
                        response.StatusCode, 
                        responseXml.Length > 500 ? responseXml.Substring(0, 500) : responseXml);

                    return new NFSeWebServiceResponse
                    {
                        Sucesso = false,
                        Mensagem = $"Erro HTTP {(int)response.StatusCode} - {response.StatusCode}",
                        XmlRetorno = responseXml,
                        CodigoStatus = ((int)response.StatusCode).ToString(),
                        Motivo = response.ReasonPhrase ?? "Erro desconhecido"
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro HTTP ao enviar NFS-e");
                return new NFSeWebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro de comunicação com webservice: {ex.Message}",
                    Erros = new Dictionary<string, string>
                    {
                        { "TipoErro", "ErroHTTP" },
                        { "Mensagem", ex.Message }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar NFS-e");
                return new NFSeWebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro inesperado: {ex.Message}",
                    Erros = new Dictionary<string, string>
                    {
                        { "TipoErro", ex.GetType().Name },
                        { "Mensagem", ex.Message }
                    }
                };
            }
        }

        private string ExtrairCodigoMunicipioDoSOAP(string soapEnvelope)
        {
            try
            {
                var doc = XDocument.Parse(soapEnvelope);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfse");
                
                var cMun = doc.Descendants(ns + "cMun").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(cMun))
                {
                    return cMun;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair código do município do SOAP");
            }

            return "3550308"; // Padrão: São Paulo
        }

        private string ObterUrlWebService(string codigoMunicipio, string ambiente)
        {
            // URLs serão configuradas por município
            // Por enquanto, retorna URL padrão de homologação nacional
            var urlConfig = _configuration[$"WebServices:NFSe:{codigoMunicipio}:{ambiente}:Url"];
            if (!string.IsNullOrEmpty(urlConfig))
            {
                return urlConfig;
            }

            // URLs padrão de homologação (NFS-e Nacional 2026)
            if (ambiente == "homologacao" || ambiente == "2")
            {
                return "https://homologacao.nfse.gov.br/ws/nfseautorizacao/nfseautorizacao.asmx";
            }

            // URLs de produção
            return "https://nfse.gov.br/ws/nfseautorizacao/nfseautorizacao.asmx";
        }

        private NFSeWebServiceResponse ProcessarRespostaWebService(string responseXml, string xmlEnviado)
        {
            try
            {
                _logger.LogInformation("Processando resposta do webservice");

                string xmlLimpo = RemoverEnvelopeSOAP(responseXml);
                var doc = XDocument.Parse(xmlLimpo);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfse");

                var retEnviNFSe = doc.Root;
                
                if (retEnviNFSe == null)
                {
                    return new NFSeWebServiceResponse
                    {
                        Sucesso = false,
                        Mensagem = "Resposta inválida do webservice",
                        XmlRetorno = responseXml
                    };
                }

                var cStat = retEnviNFSe.Element(ns + "cStat")?.Value;
                var xMotivo = retEnviNFSe.Element(ns + "xMotivo")?.Value;
                var nProt = retEnviNFSe.Element(ns + "nProt")?.Value;
                var nNFSe = retEnviNFSe.Element(ns + "nNFSe")?.Value;
                var cVerif = retEnviNFSe.Element(ns + "cVerif")?.Value;
                var linkConsulta = retEnviNFSe.Element(ns + "linkConsulta")?.Value;

                bool sucesso = false;
                if (cStat != null && int.TryParse(cStat, out int status))
                {
                    sucesso = status == 100; // 100 = Autorizado
                }

                return new NFSeWebServiceResponse
                {
                    Sucesso = sucesso,
                    Mensagem = xMotivo ?? "Resposta do webservice recebida",
                    XmlRetorno = xmlLimpo,
                    Protocolo = nProt,
                    NumeroNFSe = nNFSe,
                    CodigoVerificacao = cVerif,
                    CodigoStatus = cStat,
                    Motivo = xMotivo,
                    LinkConsulta = linkConsulta
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar resposta");
                return new NFSeWebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro ao processar resposta: {ex.Message}",
                    XmlRetorno = responseXml
                };
            }
        }

        private string RemoverEnvelopeSOAP(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var soapNs = XNamespace.Get("http://www.w3.org/2003/05/soap-envelope");
                var body = doc.Descendants(soapNs + "Body").FirstOrDefault();
                
                if (body != null)
                {
                    var firstElement = body.Elements().FirstOrDefault();
                    if (firstElement != null)
                    {
                        return firstElement.ToString();
                    }
                }
                
                return xml;
            }
            catch
            {
                return xml;
            }
        }

        private string? ExtrairNumeroNFSe(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfse");
                var nNFSe = doc.Descendants(ns + "nNFSe").FirstOrDefault()?.Value;
                return nNFSe;
            }
            catch
            {
                return null;
            }
        }

        private string? ExtrairCodigoVerificacao(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfse");
                var cVerif = doc.Descendants(ns + "cVerif").FirstOrDefault()?.Value;
                return cVerif;
            }
            catch
            {
                return null;
            }
        }
    }
}

