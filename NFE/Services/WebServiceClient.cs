using System.Text;
using System.Xml.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http.Headers;
using NFE.Models;

namespace NFE.Services
{
    public class WebServiceClient : IWebServiceClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebServiceClient> _logger;
        private readonly IConfiguration _configuration;

        public WebServiceClient(
            IHttpClientFactory httpClientFactory, 
            ILogger<WebServiceClient> logger, 
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<NFeResponseViewModel> EnviarNFeComCertificado(
            string soapEnvelope, 
            string ambiente, 
            X509Certificate2 certificado)
        {
            try
            {
                _logger.LogInformation("Enviando NFe para SEFAZ com certificado - Ambiente: {Ambiente}", ambiente);

                // Extrair UF do envelope SOAP
                string uf = ExtrairUFDoSOAP(soapEnvelope);
                _logger.LogInformation("UF detectada: {UF}", uf);

                // Determinar URL do webservice
                string url = ObterUrlWebService(uf, ambiente);
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
                    "http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4/nfeAutorizacaoLote");

                // Enviar requisição
                var response = await httpClient.PostAsync(url, content);
                string responseXml = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Resposta recebida - Status: {StatusCode}, Tamanho: {Size} bytes", 
                    response.StatusCode, responseXml.Length);

                // Processar resposta
                if (response.IsSuccessStatusCode)
                {
                    return ProcessarRespostaSEFAZ(responseXml, soapEnvelope);
                }
                else
                {
                    _logger.LogWarning("Erro HTTP: {Status} - {Response}", 
                        response.StatusCode, 
                        responseXml.Length > 500 ? responseXml.Substring(0, 500) : responseXml);

                    return new NFeResponseViewModel
                    {
                        Sucesso = false,
                        Mensagem = $"Erro HTTP {(int)response.StatusCode} - {response.StatusCode}",
                        XmlRetorno = responseXml,
                        CodigoStatus = ((int)response.StatusCode).ToString(),
                        Motivo = response.ReasonPhrase ?? "Erro desconhecido",
                        DataProcessamento = DateTime.Now
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro HTTP ao enviar NFe");
                return new NFeResponseViewModel
                {
                    Sucesso = false,
                    Mensagem = $"Erro de comunicação com SEFAZ: {ex.Message}",
                    DataProcessamento = DateTime.Now,
                    Erros = new Dictionary<string, string[]>
                    {
                        { "TipoErro", new[] { "ErroHTTP" } },
                        { "Mensagem", new[] { ex.Message } }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao enviar NFe");
                return new NFeResponseViewModel
                {
                    Sucesso = false,
                    Mensagem = $"Erro inesperado: {ex.Message}",
                    DataProcessamento = DateTime.Now,
                    Erros = new Dictionary<string, string[]>
                    {
                        { "TipoErro", new[] { ex.GetType().Name } },
                        { "Mensagem", new[] { ex.Message } }
                    }
                };
            }
        }

        /// <summary>
        /// Extrai UF do envelope SOAP
        /// </summary>
        private string ExtrairUFDoSOAP(string soapEnvelope)
        {
            try
            {
                var doc = XDocument.Parse(soapEnvelope);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");
                
                // Buscar cUF no XML da NFe
                var cUF = doc.Descendants(ns + "cUF").FirstOrDefault()?.Value;

                if (!string.IsNullOrEmpty(cUF))
                {
                    var ufMap = new Dictionary<string, string>
                    {
                        { "11", "RO" }, { "12", "AC" }, { "13", "AM" }, { "14", "RR" },
                        { "15", "PA" }, { "16", "AP" }, { "17", "TO" }, { "21", "MA" },
                        { "22", "PI" }, { "23", "CE" }, { "24", "RN" }, { "25", "PB" },
                        { "26", "PE" }, { "27", "AL" }, { "28", "SE" }, { "29", "BA" },
                        { "31", "MG" }, { "32", "ES" }, { "33", "RJ" }, { "35", "SP" },
                        { "41", "PR" }, { "42", "SC" }, { "43", "RS" }, { "50", "MS" },
                        { "51", "MT" }, { "52", "GO" }, { "53", "DF" }
                    };

                    if (ufMap.TryGetValue(cUF, out string? uf))
                    {
                        _logger.LogInformation("UF extraída do XML - Código: {CUF}, UF: {UF}", cUF, uf);
                        return uf;
                    }
                }

                _logger.LogWarning("Não foi possível extrair UF, usando RJ como padrão");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair UF do SOAP, usando RJ como padrão");
            }

            return "RJ"; // Padrão
        }

        /// <summary>
        /// Obtém URL do webservice baseado na UF e ambiente
        /// </summary>
                private string ObterUrlWebService(string uf, string ambiente)
        {
            var urlConfig = _configuration[$"WebServices:NFe:{uf}:{ambiente}:Url"];
            if (!string.IsNullOrEmpty(urlConfig))
            {
                return urlConfig;
            }

            // ✅ URLs DE HOMOLOGAÇÃO - ATUALIZADAS
            var urlsHomologacao = new Dictionary<string, string>
            {
                // Estados com Sefaz Próprio
                { "SP", "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx" },
                { "MG", "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeAutorizacao4" },
                { "RS", "https://nfe-homologacao.sefazrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "BA", "https://hnfe.sefaz.ba.gov.br/webservices/NFeAutorizacao4/NFeAutorizacao4.asmx" },
                { "PR", "https://homologacao.nfe.fazenda.pr.gov.br/nfe/NFeAutorizacao4?wsdl" },
                { "GO", "https://homolog.sefaz.go.gov.br/nfe/services/NFeAutorizacao4" },
                { "CE", "https://nfeh.sefaz.ce.gov.br/nfe2/services/NFeAutorizacao4" },
                { "PE", "https://nfehomolog.sefaz.pe.gov.br/nfe-service/services/NFeAutorizacao4" },
                { "AM", "https://homnfe.sefaz.am.gov.br/services2/services/NfeAutorizacao4" },
                { "MT", "https://homologacao.sefaz.mt.gov.br/nfews/v2/services/NfeAutorizacao4?wsdl" },
                
                // ✅ Estados que usam SVRS (Sefaz Virtual RS) - INCLUINDO RJ
                { "RJ", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "AC", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "AL", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "AP", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "DF", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "ES", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "MA", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "MS", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "PA", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "PB", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "PI", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "RN", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "RO", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "RR", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "SC", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "SE", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "TO", "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" }
            };

            // URLs DE PRODUÇÃO
            var urlsProducao = new Dictionary<string, string>
            {
                { "SP", "https://nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx" },
                { "MG", "https://nfe.fazenda.mg.gov.br/nfe2/services/NFeAutorizacao4" },
                { "RS", "https://nfe.sefazrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "BA", "https://nfe.sefaz.ba.gov.br/webservices/NFeAutorizacao4/NFeAutorizacao4.asmx" },
                { "PR", "https://nfe.fazenda.pr.gov.br/nfe/NFeAutorizacao4?wsdl" },
                { "GO", "https://nfe.sefaz.go.gov.br/nfe/services/NFeAutorizacao4" },
                { "CE", "https://nfe.sefaz.ce.gov.br/nfe2/services/NFeAutorizacao4" },
                { "PE", "https://nfe.sefaz.pe.gov.br/nfe-service/services/NFeAutorizacao4" },
                { "AM", "https://nfe.sefaz.am.gov.br/services2/services/NfeAutorizacao4" },
                { "MT", "https://nfe.sefaz.mt.gov.br/nfews/v2/services/NfeAutorizacao4?wsdl" },
                
                // Estados que usam SVRS em produção
                { "RJ", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "AC", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "AL", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "AP", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "DF", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "ES", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "MA", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "MS", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "PA", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "PB", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "PI", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "RN", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "RO", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "RR", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "SC", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "SE", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" },
                { "TO", "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx" }
            };

            var urls = ambiente == "homologacao" || ambiente == "2" ? urlsHomologacao : urlsProducao;
            
            if (urls.TryGetValue(uf, out string? urlEncontrada))
            {
                return urlEncontrada;
            }

            _logger.LogWarning("UF {UF} não encontrada, usando SVRS", uf);
            return ambiente == "homologacao" || ambiente == "2"
                ? "https://nfe-homologacao.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx"
                : "https://nfe.svrs.rs.gov.br/ws/NfeAutorizacao/NFeAutorizacao4.asmx";
        }

        private NFeResponseViewModel ProcessarRespostaSEFAZ(string responseXml, string xmlEnviado)
        {
            try
            {
                _logger.LogInformation("Processando resposta da SEFAZ");

                string xmlLimpo = RemoverEnvelopeSOAP(responseXml);
                var doc = XDocument.Parse(xmlLimpo);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");

                var retEnviNFe = doc.Root;
                
                if (retEnviNFe == null)
                {
                    return new NFeResponseViewModel
                    {
                        Sucesso = false,
                        Mensagem = "Resposta inválida da SEFAZ",
                        XmlRetorno = responseXml,
                        XmlEnviado = ExtrairXMLNFeDoSOAP(xmlEnviado),
                        DataProcessamento = DateTime.Now
                    };
                }

                var cStat = retEnviNFe.Element(ns + "cStat")?.Value;
                var xMotivo = retEnviNFe.Element(ns + "xMotivo")?.Value;
                var nRec = retEnviNFe.Element(ns + "infRec")?.Element(ns + "nRec")?.Value;

                var protNFe = retEnviNFe.Element(ns + "protNFe");
                string? protocolo = null;
                string? chaveAcesso = null;

                if (protNFe != null)
                {
                    var infProt = protNFe.Element(ns + "infProt");
                    if (infProt != null)
                    {
                        protocolo = infProt.Element(ns + "nProt")?.Value;
                        chaveAcesso = infProt.Element(ns + "chNFe")?.Value;
                        cStat = infProt.Element(ns + "cStat")?.Value ?? cStat;
                        xMotivo = infProt.Element(ns + "xMotivo")?.Value ?? xMotivo;
                    }
                }

                if (string.IsNullOrEmpty(chaveAcesso))
                {
                    chaveAcesso = ExtrairChaveAcessoDoXML(xmlEnviado);
                }

                bool sucesso = false;
                bool requerConsulta = false;

                if (cStat != null && int.TryParse(cStat, out int status))
                {
                    sucesso = status == 100;
                    requerConsulta = status == 103 || status == 105;
                }

                return new NFeResponseViewModel
                {
                    Sucesso = sucesso,
                    Mensagem = xMotivo ?? "Resposta da SEFAZ recebida",
                    XmlEnviado = ExtrairXMLNFeDoSOAP(xmlEnviado),
                    XmlRetorno = xmlLimpo,
                    Protocolo = protocolo ?? nRec,
                    ChaveAcesso = chaveAcesso,
                    CodigoStatus = cStat,
                    Motivo = xMotivo,
                    NumeroRecibo = nRec,
                    RequerConsultaRecibo = requerConsulta,
                    DataProcessamento = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar resposta");
                return new NFeResponseViewModel
                {
                    Sucesso = false,
                    Mensagem = $"Erro ao processar resposta: {ex.Message}",
                    XmlRetorno = responseXml,
                    DataProcessamento = DateTime.Now
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

        private string ExtrairXMLNFeDoSOAP(string soapEnvelope)
        {
            try
            {
                var doc = XDocument.Parse(soapEnvelope);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");
                var nfe = doc.Descendants(ns + "NFe").FirstOrDefault();
                
                return nfe?.ToString() ?? soapEnvelope;
            }
            catch
            {
                return soapEnvelope;
            }
        }

        private string? ExtrairChaveAcessoDoXML(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");
                var infNFe = doc.Descendants(ns + "infNFe").FirstOrDefault();
                
                if (infNFe != null)
                {
                    var id = infNFe.Attribute("Id")?.Value;
                    if (!string.IsNullOrEmpty(id) && id.StartsWith("NFe"))
                    {
                        return id.Substring(3);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao extrair chave de acesso");
            }
            
            return null;
        }

        [Obsolete("Use EnviarNFeComCertificado")]
        public Task<WebServiceResponse> EnviarNFeAsync(string xml, string ambiente = "homologacao")
        {
            var resultado = new WebServiceResponse
            {
                Sucesso = true,
                Mensagem = "Simulação",
                Protocolo = "999999999999999",
                ChaveAcesso = ExtrairChaveAcessoDoXML(xml),
                CodigoStatus = 100
            };
            
            return Task.FromResult(resultado);
        }
    }
}
