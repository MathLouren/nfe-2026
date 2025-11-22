using Microsoft.AspNetCore.Mvc;
using NFE.Models;
using NFE.Services;
using System.Security.Cryptography.X509Certificates;

namespace NFE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NFSeController : ControllerBase
    {
        private readonly INFSeService _nfseService;
        private readonly INFSeWebServiceClient _webServiceClient;
        private readonly AssinaturaDigital _assinaturaDigital;
        private readonly ILogger<NFSeController> _logger;

        public NFSeController(
            INFSeService nfseService,
            INFSeWebServiceClient webServiceClient,
            AssinaturaDigital assinaturaDigital,
            ILogger<NFSeController> logger)
        {
            _nfseService = nfseService;
            _webServiceClient = webServiceClient;
            _assinaturaDigital = assinaturaDigital;
            _logger = logger;
        }

        /// <summary>
        /// Endpoint LEGADO - SEM certificado (modo simulação)
        /// </summary>
        [HttpPost]
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
        /// Endpoint NOVO - COM certificado digital (recomendado)
        /// </summary>
        [HttpPost("emitir")]
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

                // 3. Gerar XML da NFS-e
                string xml = await _nfseService.GerarXmlAsync(request.DadosNFSe);
                _logger.LogInformation("XML gerado - Tamanho: {Tamanho} bytes", xml.Length);

                // 4. Assinar XML
                string xmlAssinado;
                try
                {
                    xmlAssinado = _assinaturaDigital.AssinarXml(xml, certificado);
                    _logger.LogInformation("XML assinado com sucesso");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao assinar XML");
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Erro ao assinar XML",
                        erro = ex.Message,
                        xmlNaoAssinado = xml
                    });
                }

                // 5. Criar envelope SOAP
                string soapEnvelope = Utils.SoapEnvelopeBuilderNFSe.CriarEnvelopeAutorizacao(
                    xmlAssinado,
                    request.DadosNFSe.Identificacao.CodigoMunicipio,
                    "1.00"
                );

                // 6. Enviar para webservice
                var resultado = await _webServiceClient.EnviarNFSeComCertificado(
                    soapEnvelope,
                    request.Ambiente,
                    certificado
                );

                _logger.LogInformation("NFS-e processada - Sucesso: {Sucesso}", resultado.Sucesso);

                var response = new NFSeResponseViewModel
                {
                    Sucesso = resultado.Sucesso,
                    Mensagem = resultado.Mensagem ?? "Processamento concluído",
                    XmlEnviado = xmlAssinado,
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
        /// Gera apenas o XML da NFS-e sem enviar
        /// </summary>
        [HttpPost("gerar-xml")]
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
        /// Valida um XML de NFS-e
        /// </summary>
        [HttpPost("validar-xml")]
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
                dataHora = DateTime.Now
            });
        }
    }
}

