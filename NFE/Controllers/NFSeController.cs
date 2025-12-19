using Microsoft.AspNetCore.Mvc;
using NFE.Models;
using NFE.Services;
using System.Security.Cryptography.X509Certificates;
using System.ComponentModel.DataAnnotations;

namespace NFE.Controllers
{
    /// <summary>
    /// Controller para operações de NFS-e (Nota Fiscal de Serviço Eletrônica)
    /// Sistema Nacional 2026 - Conforme leiautes-NSF-e
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class NFSeController : ControllerBase
    {
        private readonly INFSeService _nfseService;
        private readonly INFSeWebServiceClient _webServiceClient;
        private readonly SistemaNacionalNFSeClient _sistemaNacionalClient;
        private readonly DPSService _dpsService;
        private readonly EventoNFSeService _eventoService;
        private readonly ValidadorXSDService _validadorXSD;
        private readonly AssinaturaDigital _assinaturaDigital;
        private readonly ILogger<NFSeController> _logger;

        public NFSeController(
            INFSeService nfseService,
            INFSeWebServiceClient webServiceClient,
            SistemaNacionalNFSeClient sistemaNacionalClient,
            DPSService dpsService,
            EventoNFSeService eventoService,
            ValidadorXSDService validadorXSD,
            AssinaturaDigital assinaturaDigital,
            ILogger<NFSeController> logger)
        {
            _nfseService = nfseService;
            _webServiceClient = webServiceClient;
            _sistemaNacionalClient = sistemaNacionalClient;
            _dpsService = dpsService;
            _eventoService = eventoService;
            _validadorXSD = validadorXSD;
            _assinaturaDigital = assinaturaDigital;
            _logger = logger;
        }

        /// <summary>
        /// Emite NFS-e sem certificado digital (modo simulação)
        /// </summary>
        /// <param name="model">Dados da NFS-e em JSON</param>
        /// <param name="ambiente">Ambiente: homologacao ou producao</param>
        /// <returns>Resposta com resultado da emissão</returns>
        /// <response code="200">NFS-e emitida com sucesso</response>
        /// <response code="400">Dados inválidos ou erro na emissão</response>
        [HttpPost]
        [ProducesResponseType(typeof(NFSeResponseViewModel), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> CriarNFSe(
            [FromBody] NFSeViewModel model,
            [FromQuery] string ambiente = "homologacao")
        {
            try
            {
                _logger.LogInformation("Recebendo solicitação de criação de NFS-e (LEGADO) - Ambiente: {Ambiente}", ambiente);

                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Dados inválidos",
                        erros = ModelState
                    });
                }

                var resultado = await _nfseService.ProcessarNFSeAsync(model, ambiente);

                _logger.LogInformation("NFS-e processada - Sucesso: {Sucesso}, Status: {Status}", 
                    resultado.Sucesso, resultado.CodigoStatus);

                if (resultado.Sucesso)
                {
                    return Ok(resultado);
                }
                else
                {
                    return BadRequest(resultado);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar NFS-e (endpoint legado)");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao processar NFS-e",
                    erro = ex.Message
                });
            }
        }

        /// <summary>
        /// Emite NFS-e com certificado digital (recomendado)
        /// </summary>
        /// <param name="request">Dados da NFS-e e certificado digital</param>
        /// <returns>Resposta com resultado da emissão</returns>
        /// <response code="200">NFS-e emitida com sucesso</response>
        /// <response code="400">Dados inválidos, certificado inválido ou erro na emissão</response>
        [HttpPost("emitir")]
        [ProducesResponseType(typeof(NFSeResponseViewModel), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> EmitirNFSe([FromBody] NFSeRequestViewModel request)
        {
            try
            {
                _logger.LogInformation("Recebendo solicitação de emissão de NFS-e COM certificado - Ambiente: {Ambiente}", 
                    request.Ambiente);

                // 1. Validar modelo
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Dados inválidos",
                        erros = ModelState
                    });
                }

                // 2. Carregar certificado
                X509Certificate2 certificado;
                try
                {
                    certificado = _assinaturaDigital.CarregarCertificadoBase64(
                        request.CertificadoBase64,
                        request.SenhaCertificado
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao carregar certificado");
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Erro ao carregar certificado digital",
                        erro = ex.Message
                    });
                }

                // 3. Gerar XML DPS conforme leiautes-NSF-e
                string xmlDPS = _dpsService.GerarDPS(request.DadosNFSe);
                _logger.LogInformation("DPS gerado - Tamanho: {Tamanho} bytes", xmlDPS.Length);

                // 4. Assinar DPS usando método específico para DPS
                string dpsAssinado;
                try
                {
                    dpsAssinado = _assinaturaDigital.AssinarDPS(xmlDPS, certificado);
                    _logger.LogInformation("DPS assinado com sucesso");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao assinar DPS");
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Erro ao assinar DPS",
                        erro = ex.Message,
                        xmlNaoAssinado = xmlDPS
                    });
                }

                // 5. Enviar DPS para Sistema Nacional NFS-e
                var resultado = await _sistemaNacionalClient.EnviarDPSAsync(
                    dpsAssinado,
                    request.Ambiente,
                    certificado
                );

                _logger.LogInformation("NFS-e processada - Sucesso: {Sucesso}", resultado.Sucesso);

                var response = new NFSeResponseViewModel
                {
                    Sucesso = resultado.Sucesso,
                    Mensagem = resultado.Mensagem ?? "Processamento concluído",
                    XmlEnviado = dpsAssinado,
                    XmlRetorno = resultado.XmlRetorno,
                    Protocolo = resultado.Protocolo,
                    NumeroNFSe = resultado.NumeroNFSe,
                    CodigoVerificacao = resultado.CodigoVerificacao,
                    CodigoStatus = resultado.CodigoStatus,
                    Motivo = resultado.Motivo,
                    LinkConsulta = resultado.LinkConsulta,
                    Erros = resultado.Erros?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new[] { kvp.Value }
                    ),
                    DataProcessamento = DateTime.Now
                };

                if (resultado.Sucesso)
                {
                    return Ok(response);
                }
                else
                {
                    return BadRequest(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar NFS-e");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao processar NFS-e",
                    erro = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Gera apenas o XML DPS sem enviar para o Sistema Nacional
        /// </summary>
        /// <param name="model">Dados da NFS-e em JSON</param>
        /// <returns>XML DPS gerado</returns>
        /// <response code="200">XML gerado com sucesso</response>
        /// <response code="400">Dados inválidos</response>
        [HttpPost("gerar-xml")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GerarXml([FromBody] NFSeViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Dados inválidos",
                        erros = ModelState
                    });
                }

                string xml = await _nfseService.GerarXmlAsync(model);

                return Ok(new
                {
                    sucesso = true,
                    xml = xml
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar XML da NFS-e");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao gerar XML",
                    erro = ex.Message
                });
            }
        }

        /// <summary>
        /// Valida estrutura básica de um XML (verifica se está bem formado)
        /// </summary>
        /// <param name="xml">XML a ser validado</param>
        /// <returns>Resultado da validação</returns>
        /// <response code="200">Validação realizada</response>
        [HttpPost("validar-xml")]
        [ProducesResponseType(200)]
        public async Task<IActionResult> ValidarXml([FromBody] string xml)
        {
            try
            {
                bool isValid = await _nfseService.ValidarXmlAsync(xml);

                return Ok(new
                {
                    sucesso = true,
                    valido = isValid
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar XML");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao validar XML",
                    erro = ex.Message
                });
            }
        }

        /// <summary>
        /// Consulta NFS-e pela chave de acesso
        /// </summary>
        /// <param name="chaveAcesso">Chave de acesso da NFS-e (50 caracteres numéricos)</param>
        /// <param name="ambiente">Ambiente: homologacao ou producao</param>
        /// <returns>Dados da NFS-e consultada</returns>
        /// <response code="200">NFS-e encontrada</response>
        /// <response code="400">Chave de acesso inválida</response>
        /// <response code="404">NFS-e não encontrada</response>
        [HttpGet("consulta/{chaveAcesso}")]
        [ProducesResponseType(typeof(NFSeResponseViewModel), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> ConsultarNFSe(
            [FromRoute] string chaveAcesso,
            [FromQuery] string ambiente = "homologacao")
        {
            try
            {
                _logger.LogInformation("Consultando NFS-e: {Chave}", chaveAcesso);

                // Decodificar URL se necessário
                chaveAcesso = Uri.UnescapeDataString(chaveAcesso);

                // Validar formato da chave de acesso (deve ser 50 dígitos numéricos)
                if (string.IsNullOrEmpty(chaveAcesso))
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Chave de acesso não informada.",
                        ajuda = "A chave de acesso deve ter exatamente 50 dígitos numéricos."
                    });
                }

                // Detectar se parece ser uma assinatura digital ou certificado
                bool pareceAssinatura = chaveAcesso.Contains("SignatureValue") || 
                                       chaveAcesso.Contains("X509Certificate") || 
                                       chaveAcesso.Contains("MII") ||
                                       chaveAcesso.Length > 200;

                // Remover caracteres não numéricos e verificar tamanho
                string chaveLimpa = System.Text.RegularExpressions.Regex.Replace(chaveAcesso, @"[^\d]", "");
                
                if (chaveLimpa.Length != 50)
                {
                    _logger.LogWarning("Chave de acesso inválida - Tamanho original: {TamanhoOriginal}, Tamanho após limpeza: {TamanhoLimpo}", 
                        chaveAcesso.Length, chaveLimpa.Length);
                    
                    string mensagemErro = pareceAssinatura
                        ? "O valor informado parece ser uma assinatura digital ou certificado, não uma chave de acesso."
                        : $"Chave de acesso inválida. Deve ter exatamente 50 dígitos numéricos. Após remover caracteres não numéricos, restaram {chaveLimpa.Length} dígitos.";
                    
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = mensagemErro,
                        detalhes = new
                        {
                            tamanhoOriginal = chaveAcesso.Length,
                            tamanhoAposLimpeza = chaveLimpa.Length,
                            digitosEncontrados = chaveLimpa.Length > 0 ? chaveLimpa.Substring(0, Math.Min(20, chaveLimpa.Length)) + "..." : "nenhum",
                            pareceAssinaturaDigital = pareceAssinatura
                        },
                        formatoEsperado = "A chave de acesso da NFS-e deve ter exatamente 50 dígitos numéricos.",
                        exemplo = "35503082025000000000000000000000000000000000",
                        comoObter = "A chave de acesso é retornada na resposta do endpoint de emissão (/api/nfse/emitir) no campo 'numeroNFSe' ou 'codigoVerificacao'."
                    });
                }

                // Usar chave limpa para consulta
                var resultado = await _sistemaNacionalClient.ConsultarNFSeAsync(chaveLimpa, ambiente);

                var response = new NFSeResponseViewModel
                {
                    Sucesso = resultado.Sucesso,
                    Mensagem = resultado.Mensagem ?? "Consulta realizada",
                    XmlRetorno = resultado.XmlRetorno,
                    NumeroNFSe = resultado.NumeroNFSe,
                    CodigoVerificacao = resultado.CodigoVerificacao,
                    CodigoStatus = resultado.CodigoStatus,
                    Motivo = resultado.Motivo,
                    DataProcessamento = DateTime.Now
                };

                if (resultado.Sucesso)
                {
                    return Ok(response);
                }
                else
                {
                    return BadRequest(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao consultar NFS-e");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao consultar NFS-e",
                    erro = ex.Message
                });
            }
        }

        /// <summary>
        /// Cancela uma NFS-e
        /// </summary>
        [HttpPost("cancelar")]
        public async Task<IActionResult> CancelarNFSe([FromBody] NFSeEventoRequestViewModel request)
        {
            try
            {
                _logger.LogInformation("Cancelando NFS-e: {Chave}", request.Evento.ChaveAcesso);

                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Dados inválidos",
                        erros = ModelState
                    });
                }

                // Validar tipo de evento
                if (request.Evento.TipoEvento != "101101" && request.Evento.TipoEvento != "105102")
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Tipo de evento inválido para cancelamento. Use 101101 para cancelamento ou 105102 para cancelamento por substituição."
                    });
                }

                // Carregar certificado
                X509Certificate2 certificado;
                try
                {
                    certificado = _assinaturaDigital.CarregarCertificadoBase64(
                        request.CertificadoBase64,
                        request.SenhaCertificado
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao carregar certificado");
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Erro ao carregar certificado digital",
                        erro = ex.Message
                    });
                }

                // Gerar XML do evento
                string xmlEvento = _eventoService.GerarEventoCancelamento(request.Evento);

                // Assinar evento
                string eventoAssinado;
                try
                {
                    eventoAssinado = _assinaturaDigital.AssinarDPS(xmlEvento, certificado);
                    _logger.LogInformation("Evento assinado com sucesso");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao assinar evento");
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Erro ao assinar evento",
                        erro = ex.Message,
                        xmlNaoAssinado = xmlEvento
                    });
                }

                // Validar XSD (opcional)
                var validacao = await _validadorXSD.ValidarEventoAsync(eventoAssinado);
                if (!validacao.Valido)
                {
                    _logger.LogWarning("Evento possui erros de validação XSD: {Erros}", string.Join(", ", validacao.Erros));
                }

                // Registrar evento no Sistema Nacional
                var resultado = await _sistemaNacionalClient.RegistrarEventoAsync(
                    eventoAssinado,
                    request.Ambiente,
                    certificado
                );

                var response = new NFSeResponseViewModel
                {
                    Sucesso = resultado.Sucesso,
                    Mensagem = resultado.Mensagem ?? "Evento processado",
                    XmlEnviado = eventoAssinado,
                    XmlRetorno = resultado.XmlRetorno,
                    CodigoStatus = resultado.CodigoStatus,
                    Motivo = resultado.Motivo,
                    DataProcessamento = DateTime.Now,
                    Erros = validacao.Valido ? null : new Dictionary<string, string[]>
                    {
                        { "ValidacaoXSD", validacao.Erros.ToArray() }
                    }
                };

                if (resultado.Sucesso)
                {
                    return Ok(response);
                }
                else
                {
                    return BadRequest(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao cancelar NFS-e");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao cancelar NFS-e",
                    erro = ex.Message
                });
            }
        }

        /// <summary>
        /// Substitui uma NFS-e por outra
        /// </summary>
        [HttpPost("substituir")]
        public async Task<IActionResult> SubstituirNFSe([FromBody] NFSeSubstituicaoViewModel request)
        {
            try
            {
                _logger.LogInformation("Substituindo NFS-e: {ChaveSubstituida}", request.ChaveSubstituida);

                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Dados inválidos",
                        erros = ModelState
                    });
                }

                // Carregar certificado
                X509Certificate2 certificado;
                try
                {
                    certificado = _assinaturaDigital.CarregarCertificadoBase64(
                        request.CertificadoBase64,
                        request.SenhaCertificado
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao carregar certificado");
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Erro ao carregar certificado digital",
                        erro = ex.Message
                    });
                }

                // 1. Gerar DPS da nova NFS-e
                string xmlDPSNova = _dpsService.GerarDPS(request.DadosNFSeNova);
                string dpsNovaAssinado = _assinaturaDigital.AssinarDPS(xmlDPSNova, certificado);

                // 2. Enviar nova NFS-e
                var resultadoNova = await _sistemaNacionalClient.EnviarDPSAsync(
                    dpsNovaAssinado,
                    request.Ambiente,
                    certificado
                );

                if (!resultadoNova.Sucesso)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Erro ao emitir NFS-e substituta",
                        erro = resultadoNova.Mensagem
                    });
                }

                // 3. Criar evento de cancelamento por substituição
                var eventoSubstituicao = new NFSeEventoViewModel
                {
                    ChaveAcesso = request.ChaveSubstituida,
                    TipoEvento = "105102", // Cancelamento por substituição
                    CodigoJustificativa = request.CodigoJustificativa,
                    Motivo = request.Motivo ?? "Substituição de NFS-e",
                    ChaveSubstituta = resultadoNova.NumeroNFSe ?? request.ChaveSubstituida,
                    DocumentoAutor = request.DadosNFSeNova.Prestador.CNPJ ?? request.DadosNFSeNova.Prestador.CPF ?? "",
                    Ambiente = request.Ambiente
                };

                string xmlEvento = _eventoService.GerarEventoCancelamento(eventoSubstituicao);
                string eventoAssinado = _assinaturaDigital.AssinarDPS(xmlEvento, certificado);

                // 4. Registrar evento de cancelamento por substituição
                var resultadoEvento = await _sistemaNacionalClient.RegistrarEventoAsync(
                    eventoAssinado,
                    request.Ambiente,
                    certificado
                );

                var response = new NFSeResponseViewModel
                {
                    Sucesso = resultadoEvento.Sucesso && resultadoNova.Sucesso,
                    Mensagem = resultadoEvento.Sucesso && resultadoNova.Sucesso
                        ? "NFS-e substituída com sucesso"
                        : "NFS-e substituta emitida, mas erro ao cancelar original",
                    XmlEnviado = eventoAssinado,
                    XmlRetorno = resultadoEvento.XmlRetorno,
                    NumeroNFSe = resultadoNova.NumeroNFSe,
                    CodigoVerificacao = resultadoNova.CodigoVerificacao,
                    CodigoStatus = resultadoEvento.CodigoStatus,
                    Motivo = resultadoEvento.Motivo,
                    DataProcessamento = DateTime.Now
                };

                if (response.Sucesso)
                {
                    return Ok(response);
                }
                else
                {
                    return BadRequest(response);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao substituir NFS-e");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao substituir NFS-e",
                    erro = ex.Message
                });
            }
        }

        /// <summary>
        /// Valida XML contra schemas XSD
        /// </summary>
        [HttpPost("validar-xsd")]
        public async Task<IActionResult> ValidarXSD([FromBody] ValidacaoXSDRequestViewModel request)
        {
            try
            {
                _logger.LogInformation("Validando XML contra XSD - Tipo: {Tipo}", request.Tipo);

                ValidacaoXSDResultado resultado;

                if (request.Tipo == "DPS")
                {
                    resultado = await _validadorXSD.ValidarDPSAsync(request.Xml);
                }
                else if (request.Tipo == "Evento")
                {
                    resultado = await _validadorXSD.ValidarEventoAsync(request.Xml);
                }
                else
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Tipo inválido. Use 'DPS' ou 'Evento'"
                    });
                }

                return Ok(new
                {
                    sucesso = true,
                    valido = resultado.Valido,
                    erros = resultado.Erros,
                    quantidadeErros = resultado.Erros.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar XML contra XSD");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao validar XML",
                    erro = ex.Message
                });
            }
        }

        /// <summary>
        /// Consulta status do serviço
        /// </summary>
        [HttpGet("status")]
        public IActionResult ConsultarStatus([FromQuery] string municipio = "3550308", [FromQuery] string ambiente = "homologacao")
        {
            return Ok(new
            {
                sucesso = true,
                mensagem = "API NFS-e está funcionando",
                municipio = municipio,
                ambiente = ambiente,
                versao = "1.0.0",
                funcionalidades = new[]
                {
                    "Emissão de NFS-e",
                    "Consulta de NFS-e",
                    "Cancelamento de NFS-e",
                    "Substituição de NFS-e",
                    "Validação XSD",
                    "Modo simulação automático"
                },
                dataHora = DateTime.Now
            });
        }
    }

    /// <summary>
    /// Request para evento de NFS-e
    /// </summary>
    public class NFSeEventoRequestViewModel
    {
        [Required(ErrorMessage = "Dados do evento são obrigatórios")]
        public NFSeEventoViewModel Evento { get; set; } = new();

        [Required(ErrorMessage = "Certificado digital é obrigatório")]
        public string CertificadoBase64 { get; set; } = string.Empty;

        [Required(ErrorMessage = "Senha do certificado é obrigatória")]
        public string SenhaCertificado { get; set; } = string.Empty;

        public string Ambiente { get; set; } = "homologacao";
    }

    /// <summary>
    /// Request para validação XSD
    /// </summary>
    public class ValidacaoXSDRequestViewModel
    {
        [Required(ErrorMessage = "XML é obrigatório")]
        public string Xml { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tipo é obrigatório (DPS ou Evento)")]
        [RegularExpression("^(DPS|Evento)$", ErrorMessage = "Tipo deve ser 'DPS' ou 'Evento'")]
        public string Tipo { get; set; } = "DPS";
    }
}

