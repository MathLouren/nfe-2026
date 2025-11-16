using System.Text;
using System.Xml.Linq;

namespace NFE.Services
{
    public class WebServiceClient : IWebServiceClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WebServiceClient> _logger;
        private readonly IConfiguration _configuration;

        public WebServiceClient(IHttpClientFactory httpClientFactory, ILogger<WebServiceClient> logger, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<WebServiceResponse> EnviarNFeAsync(string xml, string ambiente = "homologacao")
        {
            string url = string.Empty;
            string uf = string.Empty;
            
            try
            {
                _logger.LogInformation("Enviando NFe para webservice - Ambiente: {Ambiente}", ambiente);

                uf = ExtrairUFDoXml(xml);
                url = ObterUrlNFe(uf, ambiente);

                _logger.LogInformation("URL do webservice: {Url}, UF: {UF}", url, uf);

                var soapEnvelope = CriarSoapEnvelopeNFe(xml);

                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPAction", "\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4/nfeAutorizacaoLote\"");

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30);
                
                var response = await httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Resposta do webservice NFe - Status: {Status}, URL: {Url}", response.StatusCode, url);

                return ProcessarRespostaNFe(responseContent, response.IsSuccessStatusCode, response.StatusCode, url, xml);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Erro HTTP ao enviar NFe para webservice - URL: {Url}, UF: {UF}", url, uf);
                return new WebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro HTTP ao comunicar com webservice da SEFAZ. URL: {url}, UF: {uf}. Detalhes: {ex.Message}",
                    Erros = new Dictionary<string, string> 
                    { 
                        { "TipoErro", "ErroHTTP" },
                        { "Local", "Comunicação com webservice" },
                        { "URL", url },
                        { "UF", uf },
                        { "Mensagem", ex.Message },
                        { "InnerException", ex.InnerException?.Message ?? "N/A" }
                    }
                };
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout ao enviar NFe para webservice - URL: {Url}", url);
                return new WebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Timeout ao comunicar com webservice da SEFAZ. URL: {url}. O servidor não respondeu no tempo esperado.",
                    Erros = new Dictionary<string, string> 
                    { 
                        { "TipoErro", "Timeout" },
                        { "Local", "Comunicação com webservice" },
                        { "URL", url },
                        { "UF", uf },
                        { "Mensagem", "Timeout na requisição HTTP" }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao enviar NFe para webservice - URL: {Url}", url);
                return new WebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro inesperado ao processar NFe. Local: Envio para webservice. URL: {url}. Detalhes: {ex.Message}",
                    Erros = new Dictionary<string, string> 
                    { 
                        { "TipoErro", "ErroInesperado" },
                        { "Local", "Envio para webservice" },
                        { "URL", url },
                        { "UF", uf },
                        { "Mensagem", ex.Message },
                        { "TipoExcecao", ex.GetType().Name },
                        { "StackTrace", ex.StackTrace ?? "N/A" }
                    }
                };
            }
        }

        private string CriarSoapEnvelopeNFe(string xml)
        {
            var xmlLimpo = xml;
            if (xmlLimpo.StartsWith("<?xml"))
            {
                var index = xmlLimpo.IndexOf("?>");
                if (index > 0)
                {
                    xmlLimpo = xmlLimpo.Substring(index + 2).Trim();
                }
            }

            var xmlBytes = Encoding.UTF8.GetBytes(xmlLimpo);
            var xmlBase64 = Convert.ToBase64String(xmlBytes);

            return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap12:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"" xmlns:soap12=""http://www.w3.org/2003/05/soap-envelope"">
    <soap12:Body>
        <nfeAutorizacaoLote xmlns=""http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4"">
            <nfeDadosMsg>{xmlBase64}</nfeDadosMsg>
        </nfeAutorizacaoLote>
    </soap12:Body>
</soap12:Envelope>";
        }

        private WebServiceResponse ProcessarRespostaNFe(string responseContent, bool sucessoHttp, System.Net.HttpStatusCode statusCode, string url, string xmlOriginal)
        {
            try
            {
                if (!sucessoHttp)
                {
                    // Verificar se a resposta é HTML (erro do servidor)
                    bool isHtmlError = responseContent.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                                      responseContent.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase);

                    string mensagemErro = $"Erro HTTP {((int)statusCode)} ({statusCode}) ao enviar para webservice da SEFAZ";
                    string detalhesErro = string.Empty;

                    if (isHtmlError)
                    {
                        // Tentar extrair título do erro HTML
                        var titleMatch = System.Text.RegularExpressions.Regex.Match(responseContent, @"<title>(.*?)</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (titleMatch.Success)
                        {
                            detalhesErro = titleMatch.Groups[1].Value;
                        }
                        else
                        {
                            // Tentar extrair mensagem de erro comum
                            var h2Match = System.Text.RegularExpressions.Regex.Match(responseContent, @"<h2>(.*?)</h2>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (h2Match.Success)
                            {
                                detalhesErro = h2Match.Groups[1].Value;
                            }
                        }

                        mensagemErro += $". O servidor retornou uma página HTML de erro: {detalhesErro}";
                    }
                    else
                    {
                        // Tentar extrair informações de erro XML/SOAP
                        try
                        {
                            var docErro = XDocument.Parse(responseContent);
                            var faultString = docErro.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring" || e.Name.LocalName == "FaultString");
                            if (faultString != null)
                            {
                                detalhesErro = faultString.Value;
                                mensagemErro += $". Detalhes: {detalhesErro}";
                            }
                        }
                        catch
                        {
                            // Se não conseguir parsear, usar conteúdo truncado
                            detalhesErro = responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent;
                        }
                    }

                    return new WebServiceResponse
                    {
                        Sucesso = false,
                        Mensagem = mensagemErro,
                        XmlRetorno = responseContent,
                        Erros = new Dictionary<string, string>
                        {
                            { "TipoErro", "ErroHTTP" },
                            { "Local", "Resposta do webservice" },
                            { "StatusCode", ((int)statusCode).ToString() },
                            { "StatusDescription", statusCode.ToString() },
                            { "URL", url },
                            { "Detalhes", detalhesErro },
                            { "RespostaCompleta", responseContent.Length > 1000 ? responseContent.Substring(0, 1000) + "..." : responseContent }
                        }
                    };
                }

                var chaveAcesso = ExtrairChaveAcesso(xmlOriginal);
                var doc = XDocument.Parse(responseContent);
                var ns = XNamespace.Get("http://www.w3.org/2003/05/soap-envelope");
                var body = doc.Descendants(ns + "Body").FirstOrDefault();

                if (body != null)
                {
                    var xmlRetorno = body.Value;
                    var protocolo = ExtrairProtocolo(xmlRetorno);
                    var status = ExtrairStatus(xmlRetorno);

                    return new WebServiceResponse
                    {
                        Sucesso = status == 100 || status == 150,
                        Mensagem = status == 100 || status == 150 ? "NFe autorizada com sucesso" : "NFe rejeitada",
                        XmlRetorno = xmlRetorno,
                        Protocolo = protocolo,
                        ChaveAcesso = chaveAcesso,
                        CodigoStatus = status
                    };
                }

                return new WebServiceResponse
                {
                    Sucesso = true,
                    Mensagem = "NFe enviada com sucesso",
                    XmlRetorno = responseContent,
                    ChaveAcesso = chaveAcesso
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar resposta do webservice NFe");
                return new WebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro ao processar resposta: {ex.Message}",
                    XmlRetorno = responseContent
                };
            }
        }

        private string ExtrairUFDoXml(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");
                var cUF = doc.Descendants(ns + "cUF").FirstOrDefault()?.Value;

                if (!string.IsNullOrEmpty(cUF))
                {
                    var ufMap = new Dictionary<string, string>
                    {
                        { "35", "SP" }, { "33", "RJ" }, { "31", "MG" },
                        { "29", "BA" }, { "53", "DF" }, { "23", "CE" }
                    };

                    return ufMap.GetValueOrDefault(cUF, "SP");
                }
            }
            catch
            {
            }

            return "SP";
        }

        private string ObterUrlNFe(string uf, string ambiente)
        {
            var baseUrl = _configuration[$"WebServices:NFe:{uf}:{ambiente}:Url"];

            if (!string.IsNullOrEmpty(baseUrl))
            {
                return baseUrl;
            }

            return uf switch
            {
                "SP" => "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx",
                "RJ" => "https://nfehomolog.sefaz.rj.gov.br/ws/nfeautorizacao4.asmx",
                "MG" => "https://hnfe.fazenda.mg.gov.br/nfe2/services/NFeAutorizacao4",
                _ => "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx"
            };
        }

        private string? ExtrairChaveAcesso(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");
                var infNFe = doc.Descendants(ns + "infNFe").FirstOrDefault();
                var id = infNFe?.Attribute("Id")?.Value;

                if (!string.IsNullOrEmpty(id) && id.StartsWith("NFe"))
                {
                    return id.Substring(3);
                }
            }
            catch
            {
            }

            return null;
        }

        private string? ExtrairProtocolo(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");
                var protNFe = doc.Descendants(ns + "protNFe").FirstOrDefault();
                var infProt = protNFe?.Element(ns + "infProt");
                return infProt?.Element(ns + "nProt")?.Value;
            }
            catch
            {
                return null;
            }
        }

        private int? ExtrairStatus(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");
                var protNFe = doc.Descendants(ns + "protNFe").FirstOrDefault();
                var infProt = protNFe?.Element(ns + "infProt");
                var cStat = infProt?.Element(ns + "cStat")?.Value;

                if (int.TryParse(cStat, out var status))
                {
                    return status;
                }
            }
            catch
            {
            }

            return null;
        }
    }
}

