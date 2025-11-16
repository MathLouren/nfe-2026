using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using NFE.Models;
using NFE.Services;

namespace NFE.Controllers
{
    /// <summary>
    /// Controller para gerenciamento de NFe seguindo padrão MVC
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class NFeController : ControllerBase
    {
        private readonly INFeService _nfeService;
        private readonly ILogger<NFeController> _logger;

        public NFeController(INFeService nfeService, ILogger<NFeController> logger)
        {
            _nfeService = nfeService;
            _logger = logger;
        }

        /// <summary>
        /// Recebe dados de NFe e processa (gera XML e envia para webservice)
        /// </summary>
        /// <param name="model">Dados da NFe</param>
        /// <param name="ambiente">Ambiente: homologacao ou producao</param>
        /// <returns>Resposta com resultado do processamento</returns>
        [HttpPost]
        [ProducesResponseType(typeof(NFeResponseViewModel), 200)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<IActionResult> Criar([FromBody] NFeViewModel model, [FromQuery] string ambiente = "homologacao")
        {
            try
            {
                _logger.LogInformation("Recebendo solicitação de criação de NFe - Ambiente: {Ambiente}", ambiente);

                // Validação do modelo
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Modelo inválido: {Errors}", 
                        string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

                    var errors = ModelState
                        .Where(x => x.Value?.Errors.Count > 0)
                        .ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                        );

                    return BadRequest(new NFeResponseViewModel
                    {
                        Sucesso = false,
                        Mensagem = "Dados inválidos",
                        Erros = errors
                    });
                }

                // Processar NFe
                var resultado = await _nfeService.ProcessarNFeAsync(model, ambiente);

                _logger.LogInformation("NFe processada - Sucesso: {Sucesso}, Status: {Status}", 
                    resultado.Sucesso, resultado.CodigoStatus);

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar NFe");
                return Problem(
                    title: "Erro interno do servidor",
                    detail: $"Ocorreu um erro ao processar a solicitação: {ex.Message}",
                    statusCode: 500
                );
            }
        }

        /// <summary>
        /// Apenas gera XML da NFe sem enviar para webservice
        /// </summary>
        /// <param name="model">Dados da NFe</param>
        /// <returns>XML gerado</returns>
        [HttpPost("gerar-xml")]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        public async Task<IActionResult> GerarXml([FromBody] NFeViewModel model)
        {
            try
            {
                _logger.LogInformation("Gerando XML de NFe");

                if (!ModelState.IsValid)
                {
                    return ValidationProblem(ModelState);
                }

                var xml = await _nfeService.GerarXmlAsync(model);
                return Content(xml, "application/xml", System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar XML de NFe");
                return Problem("Erro ao gerar XML", statusCode: 500);
            }
        }

        /// <summary>
        /// Valida XML de NFe
        /// </summary>
        /// <param name="xml">XML para validação</param>
        /// <returns>Resultado da validação</returns>
        [HttpPost("validar-xml")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> ValidarXml([FromBody] string xml)
        {
            try
            {
                _logger.LogInformation("Validando XML de NFe");

                if (string.IsNullOrWhiteSpace(xml))
                {
                    return BadRequest(new { valid = false, message = "XML não pode estar vazio" });
                }

                var isValid = await _nfeService.ValidarXmlAsync(xml);
                return Ok(new { valid = isValid, message = isValid ? "XML válido" : "XML inválido" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar XML de NFe");
                return Problem("Erro ao validar XML", statusCode: 500);
            }
        }

        /// <summary>
        /// Retorna informações sobre a API
        /// </summary>
        [HttpGet("info")]
        [ProducesResponseType(typeof(object), 200)]
        public IActionResult Info()
        {
            return Ok(new
            {
                api = "API NFe - Padrão MVC",
                versao = "1.0.0",
                padrao = "SEFAZ NFe 4.00",
                descricao = "API para recebimento e processamento de NFe seguindo padrão MVC"
            });
        }
    }
}

