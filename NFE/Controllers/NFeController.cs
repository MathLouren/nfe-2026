using Microsoft.AspNetCore.Mvc;
using NFE.Models;
using NFE.Services;
using System.Security.Cryptography.X509Certificates;

namespace NFE.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NFeController : ControllerBase
    {
        private readonly INFeService _nfeService;
        private readonly IWebServiceClient _webServiceClient;
        private readonly AssinaturaDigital _assinaturaDigital;
        private readonly ILogger<NFeController> _logger;

        public NFeController(
            INFeService nfeService,
            IWebServiceClient webServiceClient,
            AssinaturaDigital assinaturaDigital,
            ILogger<NFeController> logger)
        {
            _nfeService = nfeService;
            _webServiceClient = webServiceClient;
            _assinaturaDigital = assinaturaDigital;
            _logger = logger;
        }

        /// <summary>
        /// Endpoint LEGADO - SEM certificado (modo simulação)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CriarNFe(
            [FromBody] NFeViewModel model,
            [FromQuery] string ambiente = "homologacao")
        {
            try
            {
                _logger.LogInformation("Recebendo solicitação de criação de NFe (LEGADO) - Ambiente: {Ambiente}", ambiente);

                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        sucesso = false,
                        mensagem = "Dados inválidos",
                        erros = ModelState
                    });
                }

                var resultado = await _nfeService.ProcessarNFeAsync(model, ambiente);

                _logger.LogInformation("NFe processada - Sucesso: {Sucesso}, Status: {Status}", 
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
                _logger.LogError(ex, "Erro ao processar NFe (endpoint legado)");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao processar NFe",
                    erro = ex.Message
                });
            }
        }

        /// <summary>
        /// Endpoint NOVO - COM certificado digital (recomendado)
        /// </summary>
        [HttpPost("emitir")]
        public async Task<IActionResult> EmitirNFe([FromBody] NFeRequestViewModel request)
        {
            try
            {
                _logger.LogInformation("Recebendo solicitação de emissão de NFe COM certificado - Ambiente: {Ambiente}", 
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

                // 3. Gerar XML da NFe
                string xml = await _nfeService.GerarXmlAsync(request.DadosNFe);
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
                string soapEnvelope = Utils.SoapEnvelopeBuilder.CriarEnvelopeAutorizacao(
                    xmlAssinado,
                    request.DadosNFe.Identificacao.CodigoUF,
                    "4.00"
                );

                // 6. Enviar para SEFAZ
                var resultado = await _webServiceClient.EnviarNFeComCertificado(
                    soapEnvelope,
                    request.Ambiente,
                    certificado
                );

                _logger.LogInformation("NFe processada - Sucesso: {Sucesso}", resultado.Sucesso);

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
                _logger.LogError(ex, "Erro ao processar NFe");
                return StatusCode(500, new
                {
                    sucesso = false,
                    mensagem = "Erro ao processar NFe",
                    erro = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        /// <summary>
        /// Consulta status do serviço da SEFAZ
        /// </summary>
        [HttpGet("status")]
        public IActionResult ConsultarStatus([FromQuery] string uf = "SP", [FromQuery] string ambiente = "homologacao")
        {
            return Ok(new
            {
                sucesso = true,
                mensagem = "API NFe está funcionando",
                uf = uf,
                ambiente = ambiente,
                versao = "1.0.0",
                dataHora = DateTime.Now
            });
        }
    }
}
