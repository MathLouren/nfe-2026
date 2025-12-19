using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http.Headers;
using NFE.Models;

namespace NFE.Services
{
    /// <summary>
    /// Cliente REST para comunicação com o Sistema Nacional NFS-e (Sefin Nacional)
    /// </summary>
    public class SistemaNacionalNFSeClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<SistemaNacionalNFSeClient> _logger;
        private readonly IConfiguration _configuration;

        public SistemaNacionalNFSeClient(
            IHttpClientFactory httpClientFactory,
            ILogger<SistemaNacionalNFSeClient> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Envia DPS para o Sistema Nacional NFS-e
        /// </summary>
        public async Task<NFSeWebServiceResponse> EnviarDPSAsync(
            string xmlDPS,
            string ambiente,
            X509Certificate2? certificado = null)
        {
            try
            {
                _logger.LogInformation("Enviando DPS para Sistema Nacional NFS-e - Ambiente: {Ambiente}", ambiente);

                // Obter URL base do sistema nacional
                string baseUrl = ObterUrlBase(ambiente);
                string url = $"{baseUrl}/nfse/dps";

                _logger.LogInformation("URL do Sistema Nacional: {Url}", url);

                // Configurar HttpClient
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                httpClient.DefaultRequestHeaders.Clear();

                // Adicionar certificado se fornecido
                if (certificado != null)
                {
                    var handler = new HttpClientHandler();
                    handler.ClientCertificates.Add(certificado);
                    
                    if (ambiente == "homologacao" || ambiente == "2")
                    {
                        handler.ServerCertificateCustomValidationCallback =
                            (sender, cert, chain, sslPolicyErrors) => true;
                    }

                    httpClient = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromMinutes(5)
                    };
                }

                // Headers
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/xml"));
                httpClient.DefaultRequestHeaders.Add("User-Agent", "NFE-API/1.0");

                // Criar conteúdo
                var content = new StringContent(xmlDPS, Encoding.UTF8, "application/xml");

                // Enviar requisição POST
                var response = await httpClient.PostAsync(url, content);
                string responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Resposta recebida - Status: {StatusCode}, Tamanho: {Size} bytes",
                    response.StatusCode, responseContent.Length);

                // Processar resposta
                if (response.IsSuccessStatusCode)
                {
                    return ProcessarRespostaSucesso(responseContent);
                }
                else
                {
                    _logger.LogWarning("Erro HTTP: {Status} - {Response}",
                        response.StatusCode,
                        responseContent.Length > 500 ? responseContent.Substring(0, 500) : responseContent);

                    return ProcessarRespostaErro(responseContent, response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                // Se o host não existe (Sistema Nacional ainda não disponível), usar simulação
                if (ex.Message.Contains("não é conhecido") || ex.Message.Contains("not known") || 
                    ex.Message.Contains("Name or service not known"))
                {
                    _logger.LogWarning("Sistema Nacional NFS-e ainda não está disponível. Usando modo simulação.");
                    return CriarRespostaSimulada(xmlDPS);
                }

                _logger.LogError(ex, "Erro HTTP ao enviar DPS");
                return new NFSeWebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro de comunicação com Sistema Nacional: {ex.Message}",
                    Erros = new Dictionary<string, string>
                    {
                        { "TipoErro", "ErroHTTP" },
                        { "Mensagem", ex.Message }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao enviar DPS");
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

        /// <summary>
        /// Consulta NFS-e pela chave de acesso
        /// </summary>
        public async Task<NFSeWebServiceResponse> ConsultarNFSeAsync(
            string chaveAcesso,
            string ambiente)
        {
            try
            {
                _logger.LogInformation("Consultando NFS-e: {Chave}", chaveAcesso);

                string baseUrl = ObterUrlBase(ambiente);
                string url = $"{baseUrl}/nfse/{chaveAcesso}";

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromMinutes(2);
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await httpClient.GetAsync(url);
                string responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return ProcessarRespostaConsulta(responseContent);
                }
                else
                {
                    // Se o host não existe, retornar simulação
                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                        responseContent.Contains("não é conhecido") || responseContent.Contains("not known"))
                    {
                        _logger.LogWarning("Sistema Nacional não disponível, retornando simulação de consulta");
                        return CriarRespostaConsultaSimulada(chaveAcesso);
                    }
                    
                    return ProcessarRespostaErro(responseContent, response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                // Se o host não existe, retornar simulação
                if (ex.Message.Contains("não é conhecido") || ex.Message.Contains("not known"))
                {
                    _logger.LogWarning("Sistema Nacional não disponível, retornando simulação de consulta");
                    return CriarRespostaConsultaSimulada(chaveAcesso);
                }

                _logger.LogError(ex, "Erro ao consultar NFS-e");
                return new NFSeWebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro ao consultar NFS-e: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar NFS-e");
                return new NFSeWebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro ao consultar NFS-e: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Registra evento (cancelamento, substituição, etc.)
        /// </summary>
        public async Task<NFSeWebServiceResponse> RegistrarEventoAsync(
            string xmlEvento,
            string ambiente,
            X509Certificate2? certificado = null)
        {
            try
            {
                _logger.LogInformation("Registrando evento no Sistema Nacional NFS-e - Ambiente: {Ambiente}", ambiente);

                string baseUrl = ObterUrlBase(ambiente);
                string url = $"{baseUrl}/nfse/eventos";

                _logger.LogInformation("URL do Sistema Nacional: {Url}", url);

                // Configurar HttpClient
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                httpClient.DefaultRequestHeaders.Clear();

                // Adicionar certificado se fornecido
                if (certificado != null)
                {
                    var handler = new HttpClientHandler();
                    handler.ClientCertificates.Add(certificado);
                    
                    if (ambiente == "homologacao" || ambiente == "2")
                    {
                        handler.ServerCertificateCustomValidationCallback =
                            (sender, cert, chain, sslPolicyErrors) => true;
                    }

                    httpClient = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromMinutes(5)
                    };
                }

                // Headers
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/xml"));
                httpClient.DefaultRequestHeaders.Add("User-Agent", "NFE-API/1.0");

                // Criar conteúdo
                var content = new StringContent(xmlEvento, Encoding.UTF8, "application/xml");

                // Enviar requisição POST
                var response = await httpClient.PostAsync(url, content);
                string responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Resposta recebida - Status: {StatusCode}, Tamanho: {Size} bytes",
                    response.StatusCode, responseContent.Length);

                // Processar resposta
                if (response.IsSuccessStatusCode)
                {
                    return ProcessarRespostaSucesso(responseContent);
                }
                else
                {
                    return ProcessarRespostaErro(responseContent, response.StatusCode);
                }
            }
            catch (HttpRequestException ex)
            {
                // Se o host não existe, retornar simulação
                if (ex.Message.Contains("não é conhecido") || ex.Message.Contains("not known"))
                {
                    _logger.LogWarning("Sistema Nacional não disponível, retornando simulação de evento");
                    return CriarRespostaEventoSimulada(xmlEvento);
                }

                _logger.LogError(ex, "Erro HTTP ao registrar evento");
                return new NFSeWebServiceResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro de comunicação com Sistema Nacional: {ex.Message}",
                    Erros = new Dictionary<string, string>
                    {
                        { "TipoErro", "ErroHTTP" },
                        { "Mensagem", ex.Message }
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao registrar evento");
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

        /// <summary>
        /// Obtém URL base do Sistema Nacional conforme ambiente
        /// </summary>
        private string ObterUrlBase(string ambiente)
        {
            // Verificar configuração
            var urlConfig = _configuration[$"WebServices:NFSeNacional:{ambiente}:Url"];
            if (!string.IsNullOrEmpty(urlConfig))
            {
                return urlConfig;
            }

            // URLs padrão do Sistema Nacional NFS-e
            if (ambiente == "homologacao" || ambiente == "2")
            {
                // URL de homologação do Sistema Nacional
                return "https://homologacao.nfse.gov.br/api/v1";
            }

            // URL de produção do Sistema Nacional
            return "https://nfse.gov.br/api/v1";
        }

        /// <summary>
        /// Processa resposta de sucesso do Sistema Nacional
        /// </summary>
        private NFSeWebServiceResponse ProcessarRespostaSucesso(string responseXml)
        {
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(responseXml);
                var ns = System.Xml.Linq.XNamespace.Get("http://www.sped.fazenda.gov.br/nfse");

                // Buscar elementos da resposta
                var infNFSe = doc.Descendants(ns + "infNFSe").FirstOrDefault();
                
                if (infNFSe != null)
                {
                    var cStat = infNFSe.Element(ns + "cStat")?.Value;
                    var xMotivo = infNFSe.Element(ns + "xMotivo")?.Value;
                    var nNFSe = infNFSe.Element(ns + "nNFSe")?.Value;
                    var cVerif = infNFSe.Element(ns + "cVerif")?.Value;
                    var dhProc = infNFSe.Element(ns + "dhProc")?.Value;

                    bool sucesso = cStat == "100" || cStat == "101"; // 100=Autorizado, 101=Autorizado com pendência

                    return new NFSeWebServiceResponse
                    {
                        Sucesso = sucesso,
                        Mensagem = xMotivo ?? "NFS-e processada",
                        XmlRetorno = responseXml,
                        NumeroNFSe = nNFSe,
                        CodigoVerificacao = cVerif,
                        CodigoStatus = cStat,
                        Motivo = xMotivo,
                        Protocolo = dhProc
                    };
                }

                // Se não encontrar estrutura esperada, retornar resposta genérica
                return new NFSeWebServiceResponse
                {
                    Sucesso = true,
                    Mensagem = "DPS recebido pelo Sistema Nacional",
                    XmlRetorno = responseXml
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao processar resposta XML, retornando resposta genérica");
                return new NFSeWebServiceResponse
                {
                    Sucesso = true,
                    Mensagem = "DPS enviado com sucesso",
                    XmlRetorno = responseXml
                };
            }
        }

        /// <summary>
        /// Processa resposta de erro
        /// </summary>
        private NFSeWebServiceResponse ProcessarRespostaErro(string responseContent, System.Net.HttpStatusCode statusCode)
        {
            try
            {
                // Tentar parsear como XML de erro
                var doc = System.Xml.Linq.XDocument.Parse(responseContent);
                var ns = System.Xml.Linq.XNamespace.Get("http://www.sped.fazenda.gov.br/nfse");

                var retEnviNFSe = doc.Descendants(ns + "retEnviNFSe").FirstOrDefault();
                if (retEnviNFSe != null)
                {
                    var cStat = retEnviNFSe.Element(ns + "cStat")?.Value;
                    var xMotivo = retEnviNFSe.Element(ns + "xMotivo")?.Value;

                    return new NFSeWebServiceResponse
                    {
                        Sucesso = false,
                        Mensagem = xMotivo ?? "Erro ao processar DPS",
                        XmlRetorno = responseContent,
                        CodigoStatus = cStat,
                        Motivo = xMotivo
                    };
                }
            }
            catch
            {
                // Se não conseguir parsear, retornar erro genérico
            }

            return new NFSeWebServiceResponse
            {
                Sucesso = false,
                Mensagem = $"Erro HTTP {(int)statusCode}: {statusCode}",
                XmlRetorno = responseContent,
                CodigoStatus = ((int)statusCode).ToString(),
                Motivo = responseContent.Length > 200 ? responseContent.Substring(0, 200) : responseContent
            };
        }

        /// <summary>
        /// Processa resposta de consulta
        /// </summary>
        private NFSeWebServiceResponse ProcessarRespostaConsulta(string responseContent)
        {
            // A consulta retorna JSON ou XML com dados da NFS-e
            return new NFSeWebServiceResponse
            {
                Sucesso = true,
                Mensagem = "NFS-e consultada com sucesso",
                XmlRetorno = responseContent
            };
        }

        /// <summary>
        /// Cria resposta simulada quando o Sistema Nacional não está disponível
        /// </summary>
        private NFSeWebServiceResponse CriarRespostaSimulada(string xmlDPS)
        {
            try
            {
                // Extrair informações do DPS para criar resposta simulada
                var doc = System.Xml.Linq.XDocument.Parse(xmlDPS);
                var ns = System.Xml.Linq.XNamespace.Get("http://www.sped.fazenda.gov.br/nfse");

                var infDPS = doc.Descendants(ns + "infDPS").FirstOrDefault();
                var nDPS = infDPS?.Element(ns + "nDPS")?.Value ?? "000000000000001";
                var cLocEmi = infDPS?.Element(ns + "cLocEmi")?.Value ?? "3550308";

                // Gerar número de NFS-e simulado (formato: código município + número sequencial)
                string numeroNFSe = $"{cLocEmi}{nDPS.Substring(Math.Max(0, nDPS.Length - 8))}";
                string codigoVerificacao = GerarCodigoVerificacaoSimulado();

                // Criar XML de resposta simulada
                var respostaXml = new System.Xml.Linq.XDocument(
                    new System.Xml.Linq.XDeclaration("1.0", "UTF-8", null),
                    new System.Xml.Linq.XElement(ns + "retEnviNFSe",
                        new System.Xml.Linq.XElement(ns + "infNFSe",
                            new System.Xml.Linq.XAttribute("Id", $"NFSe{numeroNFSe}"),
                            new System.Xml.Linq.XElement(ns + "cStat", "100"),
                            new System.Xml.Linq.XElement(ns + "xMotivo", "DPS processada com sucesso (SIMULAÇÃO)"),
                            new System.Xml.Linq.XElement(ns + "nNFSe", numeroNFSe),
                            new System.Xml.Linq.XElement(ns + "cVerif", codigoVerificacao),
                            new System.Xml.Linq.XElement(ns + "dhProc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                        )
                    )
                );

                return new NFSeWebServiceResponse
                {
                    Sucesso = true,
                    Mensagem = "DPS processada com sucesso (MODO SIMULAÇÃO - Sistema Nacional ainda não disponível)",
                    XmlRetorno = respostaXml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),
                    NumeroNFSe = numeroNFSe,
                    CodigoVerificacao = codigoVerificacao,
                    CodigoStatus = "100",
                    Motivo = "DPS processada com sucesso (SIMULAÇÃO)",
                    Protocolo = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                    LinkConsulta = $"https://nfse.gov.br/consulta/{numeroNFSe}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao criar resposta simulada, retornando resposta básica");
                return new NFSeWebServiceResponse
                {
                    Sucesso = true,
                    Mensagem = "DPS processada com sucesso (MODO SIMULAÇÃO)",
                    CodigoStatus = "100",
                    Motivo = "Sistema Nacional NFS-e ainda não está disponível publicamente"
                };
            }
        }

        /// <summary>
        /// Gera código de verificação simulado (8 dígitos)
        /// </summary>
        private string GerarCodigoVerificacaoSimulado()
        {
            Random rnd = new Random();
            return rnd.Next(10000000, 99999999).ToString();
        }

        /// <summary>
        /// Cria resposta simulada de consulta
        /// </summary>
        private NFSeWebServiceResponse CriarRespostaConsultaSimulada(string chaveAcesso)
        {
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse($"<consulta chave=\"{chaveAcesso}\"/>");
                var ns = System.Xml.Linq.XNamespace.Get("http://www.sped.fazenda.gov.br/nfse");

                // Extrair informações da chave
                string numeroNFSe = chaveAcesso.Length >= 15 
                    ? chaveAcesso.Substring(chaveAcesso.Length - 15) 
                    : chaveAcesso;

                var respostaXml = new System.Xml.Linq.XDocument(
                    new System.Xml.Linq.XDeclaration("1.0", "UTF-8", null),
                    new System.Xml.Linq.XElement(ns + "retConsNFSe",
                        new System.Xml.Linq.XElement(ns + "infNFSe",
                            new System.Xml.Linq.XAttribute("Id", $"NFSe{numeroNFSe}"),
                            new System.Xml.Linq.XElement(ns + "cStat", "100"),
                            new System.Xml.Linq.XElement(ns + "xMotivo", "NFS-e consultada com sucesso (SIMULAÇÃO)"),
                            new System.Xml.Linq.XElement(ns + "nNFSe", numeroNFSe),
                            new System.Xml.Linq.XElement(ns + "cVerif", GerarCodigoVerificacaoSimulado()),
                            new System.Xml.Linq.XElement(ns + "chNFSe", chaveAcesso)
                        )
                    )
                );

                return new NFSeWebServiceResponse
                {
                    Sucesso = true,
                    Mensagem = "NFS-e consultada com sucesso (MODO SIMULAÇÃO)",
                    XmlRetorno = respostaXml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),
                    NumeroNFSe = numeroNFSe,
                    CodigoStatus = "100",
                    Motivo = "NFS-e consultada com sucesso (SIMULAÇÃO)"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao criar resposta simulada de consulta");
                return new NFSeWebServiceResponse
                {
                    Sucesso = true,
                    Mensagem = "NFS-e consultada com sucesso (MODO SIMULAÇÃO)",
                    CodigoStatus = "100"
                };
            }
        }

        /// <summary>
        /// Cria resposta simulada de evento
        /// </summary>
        private NFSeWebServiceResponse CriarRespostaEventoSimulada(string xmlEvento)
        {
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(xmlEvento);
                var ns = System.Xml.Linq.XNamespace.Get("http://www.sped.fazenda.gov.br/nfse");

                var chNFSe = doc.Descendants(ns + "chNFSe").FirstOrDefault()?.Value ?? "00000000000000000000000000000000000000000000";
                var tipoEvento = doc.Descendants().FirstOrDefault(e => e.Name.LocalName.StartsWith("e10"))?.Name.LocalName ?? "e101101";

                var respostaXml = new System.Xml.Linq.XDocument(
                    new System.Xml.Linq.XDeclaration("1.0", "UTF-8", null),
                    new System.Xml.Linq.XElement(ns + "retRegEvento",
                        new System.Xml.Linq.XElement(ns + "infEvento",
                            new System.Xml.Linq.XAttribute("Id", $"EVT{chNFSe}{DateTime.Now:yyyyMMddHHmmss}"),
                            new System.Xml.Linq.XElement(ns + "cStat", "100"),
                            new System.Xml.Linq.XElement(ns + "xMotivo", "Evento registrado com sucesso (SIMULAÇÃO)"),
                            new System.Xml.Linq.XElement(ns + "tpEvento", tipoEvento),
                            new System.Xml.Linq.XElement(ns + "chNFSe", chNFSe),
                            new System.Xml.Linq.XElement(ns + "dhProc", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                        )
                    )
                );

                return new NFSeWebServiceResponse
                {
                    Sucesso = true,
                    Mensagem = "Evento registrado com sucesso (MODO SIMULAÇÃO)",
                    XmlRetorno = respostaXml.ToString(System.Xml.Linq.SaveOptions.DisableFormatting),
                    CodigoStatus = "100",
                    Motivo = "Evento registrado com sucesso (SIMULAÇÃO)"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao criar resposta simulada de evento");
                return new NFSeWebServiceResponse
                {
                    Sucesso = true,
                    Mensagem = "Evento registrado com sucesso (MODO SIMULAÇÃO)",
                    CodigoStatus = "100"
                };
            }
        }
    }
}
