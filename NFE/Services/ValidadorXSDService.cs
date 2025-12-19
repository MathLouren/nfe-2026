using System.Xml;
using System.Xml.Schema;
using System.Xml.Linq;

namespace NFE.Services
{
    /// <summary>
    /// Serviço para validação XSD de documentos NFS-e
    /// </summary>
    public class ValidadorXSDService
    {
        private readonly ILogger<ValidadorXSDService> _logger;
        private readonly IWebHostEnvironment _environment;

        public ValidadorXSDService(ILogger<ValidadorXSDService> logger, IWebHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Valida XML DPS contra o schema XSD
        /// </summary>
        public async Task<ValidacaoXSDResultado> ValidarDPSAsync(string xmlDPS)
        {
            try
            {
                _logger.LogInformation("Iniciando validação XSD do DPS");

                var resultado = new ValidacaoXSDResultado
                {
                    Valido = true,
                    Erros = new List<string>()
                };

                // Carregar schemas
                var schemas = new XmlSchemaSet();
                string schemasPath = Path.Combine(_environment.ContentRootPath, "..", "leiautes-NSF-e");

                // Adicionar schemas principais
                await CarregarSchema(schemas, schemasPath, "DPS_v1.00.xsd");
                await CarregarSchema(schemas, schemasPath, "tiposComplexos_v1.00.xsd");
                await CarregarSchema(schemas, schemasPath, "tiposSimples_v1.00.xsd");
                await CarregarSchema(schemas, schemasPath, "xmldsig-core-schema.xsd");

                // Validar XML
                var doc = XDocument.Parse(xmlDPS);
                doc.Validate(schemas, (sender, args) =>
                {
                    resultado.Valido = false;
                    resultado.Erros.Add($"Linha {args.Exception.LineNumber}, Coluna {args.Exception.LinePosition}: {args.Message}");
                    _logger.LogWarning("Erro de validação XSD: {Erro}", args.Message);
                });

                if (resultado.Valido)
                {
                    _logger.LogInformation("DPS validado com sucesso contra XSD");
                }
                else
                {
                    _logger.LogWarning("DPS possui {Quantidade} erro(s) de validação XSD", resultado.Erros.Count);
                }

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar DPS contra XSD");
                return new ValidacaoXSDResultado
                {
                    Valido = false,
                    Erros = new List<string> { $"Erro ao validar: {ex.Message}" }
                };
            }
        }

        /// <summary>
        /// Valida XML de evento contra o schema XSD
        /// </summary>
        public async Task<ValidacaoXSDResultado> ValidarEventoAsync(string xmlEvento)
        {
            try
            {
                _logger.LogInformation("Iniciando validação XSD do evento");

                var resultado = new ValidacaoXSDResultado
                {
                    Valido = true,
                    Erros = new List<string>()
                };

                // Carregar schemas
                var schemas = new XmlSchemaSet();
                string schemasPath = Path.Combine(_environment.ContentRootPath, "..", "leiautes-NSF-e");

                // Adicionar schemas principais
                await CarregarSchema(schemas, schemasPath, "evento_v1.00.xsd");
                await CarregarSchema(schemas, schemasPath, "pedRegEvento_v1.00.xsd");
                await CarregarSchema(schemas, schemasPath, "tiposEventos_v1.00.xsd");
                await CarregarSchema(schemas, schemasPath, "tiposComplexos_v1.00.xsd");
                await CarregarSchema(schemas, schemasPath, "tiposSimples_v1.00.xsd");
                await CarregarSchema(schemas, schemasPath, "xmldsig-core-schema.xsd");

                // Validar XML
                var doc = XDocument.Parse(xmlEvento);
                doc.Validate(schemas, (sender, args) =>
                {
                    resultado.Valido = false;
                    resultado.Erros.Add($"Linha {args.Exception.LineNumber}, Coluna {args.Exception.LinePosition}: {args.Message}");
                    _logger.LogWarning("Erro de validação XSD: {Erro}", args.Message);
                });

                if (resultado.Valido)
                {
                    _logger.LogInformation("Evento validado com sucesso contra XSD");
                }

                return resultado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao validar evento contra XSD");
                return new ValidacaoXSDResultado
                {
                    Valido = false,
                    Erros = new List<string> { $"Erro ao validar: {ex.Message}" }
                };
            }
        }

        private async Task CarregarSchema(XmlSchemaSet schemas, string schemasPath, string nomeArquivo)
        {
            try
            {
                string caminhoCompleto = Path.Combine(schemasPath, nomeArquivo);
                
                if (!File.Exists(caminhoCompleto))
                {
                    _logger.LogWarning("Schema não encontrado: {Caminho}", caminhoCompleto);
                    return;
                }

                using var reader = XmlReader.Create(caminhoCompleto);
                var schema = await Task.Run(() => XmlSchema.Read(reader, (sender, args) =>
                {
                    _logger.LogWarning("Aviso ao ler schema {Arquivo}: {Mensagem}", nomeArquivo, args.Message);
                }));

                if (schema != null)
                {
                    schemas.Add(schema);
                    _logger.LogDebug("Schema carregado: {Arquivo}", nomeArquivo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao carregar schema {Arquivo}", nomeArquivo);
            }
        }
    }

    /// <summary>
    /// Resultado da validação XSD
    /// </summary>
    public class ValidacaoXSDResultado
    {
        public bool Valido { get; set; }
        public List<string> Erros { get; set; } = new();
    }
}
