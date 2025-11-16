using NFE.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml;

namespace NFE.Services
{
    /// <summary>
    /// Serviço de NFe seguindo padrão MVC
    /// </summary>
    public class NFeService : INFeService
    {
        private readonly IWebServiceClient _webServiceClient;
        private readonly ILogger<NFeService> _logger;
        
        // Cultura invariante para garantir ponto decimal (padrão XML/SEFAZ)
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public NFeService(IWebServiceClient webServiceClient, ILogger<NFeService> logger)
        {
            _webServiceClient = webServiceClient;
            _logger = logger;
        }
        
        /// <summary>
        /// Formata valor decimal para string com ponto decimal (padrão SEFAZ)
        /// </summary>
        private static string FormatarValor(decimal valor, int casasDecimais = 2)
        {
            return valor.ToString($"F{casasDecimais}", InvariantCulture);
        }
        
        /// <summary>
        /// Formata valor decimal para string com 4 casas decimais (padrão SEFAZ)
        /// </summary>
        private static string FormatarValor4Casas(decimal valor)
        {
            return valor.ToString("F4", InvariantCulture);
        }

        /// <summary>
        /// Formata data/hora para formato UTC conforme schema (AAAA-MM-DDThh:mm:ssTZD)
        /// </summary>
        private static string FormatarDataHoraUTC(DateTime data)
        {
            var offset = TimeZoneInfo.Local.GetUtcOffset(data);
            var offsetString = $"{(offset.Hours >= 0 ? "+" : "-")}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
            return data.ToString("yyyy-MM-ddTHH:mm:ss", InvariantCulture) + offsetString;
        }

        public async Task<NFeResponseViewModel> ProcessarNFeAsync(NFeViewModel model, string ambiente)
        {
            try
            {
                _logger.LogInformation("Processando NFe - Modelo: {Modelo}, Número: {Numero}", 
                    model.Identificacao.Modelo, model.Identificacao.NumeroNota);

                // Gerar XML
                string xml;
                try
                {
                    xml = await GerarXmlAsync(model);
                    _logger.LogInformation("XML gerado com sucesso - Tamanho: {Tamanho} bytes", xml.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao gerar XML da NFe");
                    return new NFeResponseViewModel
                    {
                        Sucesso = false,
                        Mensagem = $"Erro ao gerar XML da NFe. Local: Geração do XML. Detalhes: {ex.Message}",
                        Erros = new Dictionary<string, string[]>
                        {
                            { "TipoErro", new[] { "ErroGeracaoXML" } },
                            { "Local", new[] { "Geração do XML" } },
                            { "Mensagem", new[] { ex.Message } },
                            { "TipoExcecao", new[] { ex.GetType().Name } }
                        }
                    };
                }

                // Validar XML
                bool isValid;
                try
                {
                    isValid = await ValidarXmlAsync(xml);
                    if (!isValid)
                    {
                        _logger.LogWarning("XML gerado é inválido");
                        return new NFeResponseViewModel
                        {
                            Sucesso = false,
                            Mensagem = "XML gerado é inválido. Local: Validação do XML. O XML não está bem formado.",
                            XmlEnviado = xml,
                            Erros = new Dictionary<string, string[]>
                            {
                                { "TipoErro", new[] { "XMLInvalido" } },
                                { "Local", new[] { "Validação do XML" } },
                                { "Mensagem", new[] { "O XML gerado não está bem formado" } }
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao validar XML da NFe");
                    return new NFeResponseViewModel
                    {
                        Sucesso = false,
                        Mensagem = $"Erro ao validar XML da NFe. Local: Validação do XML. Detalhes: {ex.Message}",
                        XmlEnviado = xml,
                        Erros = new Dictionary<string, string[]>
                        {
                            { "TipoErro", new[] { "ErroValidacaoXML" } },
                            { "Local", new[] { "Validação do XML" } },
                            { "Mensagem", new[] { ex.Message } }
                        }
                    };
                }

                // Enviar para webservice
                var resposta = await _webServiceClient.EnviarNFeAsync(xml, ambiente);

                return new NFeResponseViewModel
                {
                    Sucesso = resposta.Sucesso,
                    Mensagem = resposta.Mensagem ?? "Processamento concluído",
                    XmlEnviado = xml,
                    XmlRetorno = resposta.XmlRetorno,
                    Protocolo = resposta.Protocolo,
                    ChaveAcesso = resposta.ChaveAcesso,
                    CodigoStatus = resposta.CodigoStatus,
                    Motivo = resposta.Motivo,
                    Erros = resposta.Erros?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new[] { kvp.Value }
                    )
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar NFe");
                return new NFeResponseViewModel
                {
                    Sucesso = false,
                    Mensagem = $"Erro inesperado ao processar NFe. Local: Processamento geral. Detalhes: {ex.Message}",
                    Erros = new Dictionary<string, string[]>
                    {
                        { "TipoErro", new[] { "ErroInesperado" } },
                        { "Local", new[] { "Processamento geral" } },
                        { "Mensagem", new[] { ex.Message } },
                        { "TipoExcecao", new[] { ex.GetType().Name } },
                        { "StackTrace", new[] { ex.StackTrace ?? "N/A" } }
                    }
                };
            }
        }

        public async Task<string> GerarXmlAsync(NFeViewModel model)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfe");
                    var nfe = new XElement(ns + "NFe");

                    var infNFe = new XElement(ns + "infNFe",
                        new XAttribute("Id", GerarIdNFe(model)),
                        new XAttribute("versao", "4.00")
                    );

                    // ide
                    var ide = CriarIde(model, ns);
                    infNFe.Add(ide);

                    // emit
                    var emit = CriarEmitente(model, ns);
                    infNFe.Add(emit);

                    // dest
                    var dest = CriarDestinatario(model, ns);
                    infNFe.Add(dest);

                    // det - produtos
                    int nItem = 1;
                    foreach (var produto in model.Produtos)
                    {
                        var det = CriarDetalhe(produto, ns, nItem);
                        infNFe.Add(det);
                        nItem++;
                    }

                    // total
                    var total = CriarTotal(model, ns);
                    infNFe.Add(total);

                    // transp
                    if (model.Transporte != null)
                    {
                        var transp = CriarTransporte(model.Transporte, ns);
                        infNFe.Add(transp);
                    }

                    // cobr
                    if (model.Cobranca != null)
                    {
                        var cobr = CriarCobranca(model.Cobranca, ns);
                        infNFe.Add(cobr);
                    }

                    // infAdic
                    if (!string.IsNullOrEmpty(model.InformacoesAdicionais))
                    {
                        var infAdic = new XElement(ns + "infAdic");
                        infAdic.Add(new XElement(ns + "infCpl", model.InformacoesAdicionais));
                        infNFe.Add(infAdic);
                    }

                    nfe.Add(infNFe);

                    var xmlDocument = new XDocument(
                        new XDeclaration("1.0", "UTF-8", null),
                        nfe
                    );

                    return xmlDocument.ToString();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao gerar XML de NFe");
                    throw;
                }
            });
        }

        public async Task<bool> ValidarXmlAsync(string xml)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Primeiro verifica se está bem formado
                    var doc = XDocument.Parse(xml);
                    
                    // Carrega schemas XSD
                    var schemas = new XmlSchemaSet();
                    
                    // Tenta diferentes caminhos para encontrar os schemas
                    var currentDir = Directory.GetCurrentDirectory();
                    var possiblePaths = new[]
                    {
                        Path.Combine(currentDir, "leiautes"),
                        Path.Combine(currentDir, "..", "leiautes"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "leiautes"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "leiautes"),
                        Path.GetFullPath(Path.Combine(currentDir, "..", "leiautes"))
                    };

                    string? schemasPath = null;
                    foreach (var path in possiblePaths)
                    {
                        var testPath = Path.Combine(path, "nfe_v4.00.xsd");
                        if (File.Exists(testPath))
                        {
                            schemasPath = path;
                            break;
                        }
                    }

                    if (schemasPath == null)
                    {
                        _logger.LogWarning("Schemas XSD não encontrados. Validação XSD será ignorada.");
                        return true; // Retorna true se não encontrar schemas (compatibilidade)
                    }
                    
                    // Carrega schemas na ordem correta (dependências primeiro)
                    schemas.Add("http://www.w3.org/2000/09/xmldsig#", 
                        Path.Combine(schemasPath, "xmldsig-core-schema_v1.01.xsd"));
                    schemas.Add("http://www.portalfiscal.inf.br/nfe", 
                        Path.Combine(schemasPath, "tiposBasico_v4.00.xsd"));
                    schemas.Add("http://www.portalfiscal.inf.br/nfe", 
                        Path.Combine(schemasPath, "DFeTiposBasicos_v1.00.xsd"));
                    schemas.Add("http://www.portalfiscal.inf.br/nfe", 
                        Path.Combine(schemasPath, "leiauteNFe_v4.00.xsd"));
                    schemas.Add("http://www.portalfiscal.inf.br/nfe", 
                        Path.Combine(schemasPath, "nfe_v4.00.xsd"));

                    // Valida XML contra schemas
                    var validationErrors = new List<string>();
                    doc.Validate(schemas, (sender, args) =>
                    {
                        if (args.Severity == XmlSeverityType.Error)
                        {
                            var errorMsg = $"Erro de validação XSD: {args.Message} - Linha: {args.Exception?.LineNumber}, Posição: {args.Exception?.LinePosition}";
                            _logger.LogError(errorMsg);
                            validationErrors.Add(errorMsg);
                        }
                    });

                    if (validationErrors.Any())
                    {
                        throw new XmlException(string.Join("; ", validationErrors));
                    }

                    return true;
                }
                catch (XmlException ex)
                {
                    _logger.LogError(ex, "Erro ao validar XML contra schemas XSD");
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro inesperado ao validar XML");
                    return false;
                }
            });
        }

        private string GerarIdNFe(NFeViewModel model)
        {
            var ide = model.Identificacao;
            var cUF = ide.CodigoUF.PadLeft(2, '0');
            var anoMes = ide.DataEmissao.ToString("yyMM");
            var cnpj = RemoverFormatacao(model.Emitente.CNPJ);
            var mod = ide.Modelo;
            var serie = ide.Serie.PadLeft(3, '0');
            var nNF = ide.NumeroNota.ToString().PadLeft(9, '0');
            var tpEmis = ide.TipoEmissao;

            var chave = $"{cUF}{anoMes}{cnpj}{mod}{serie}{nNF}{tpEmis}";
            var dv = CalcularDigitoVerificador(chave);

            return $"NFe{chave}{dv}";
        }

        private int CalcularDigitoVerificador(string chave)
        {
            int soma = 0;
            int peso = 2;

            for (int i = chave.Length - 1; i >= 0; i--)
            {
                soma += int.Parse(chave[i].ToString()) * peso;
                peso++;
                if (peso > 9) peso = 2;
            }

            int resto = soma % 11;
            return resto < 2 ? 0 : 11 - resto;
        }

        private XElement CriarIde(NFeViewModel model, XNamespace ns)
        {
            var ide = model.Identificacao;
            var ideElement = new XElement(ns + "ide");

            // Calcula cDV corretamente
            var chaveAcesso = GerarIdNFe(model).Replace("NFe", "");
            var cDV = chaveAcesso.Substring(chaveAcesso.Length - 1);

            ideElement.Add(new XElement(ns + "cUF", ide.CodigoUF));
            ideElement.Add(new XElement(ns + "cNF", new Random().Next(10000000, 99999999).ToString()));
            ideElement.Add(new XElement(ns + "natOp", ide.NaturezaOperacao));
            ideElement.Add(new XElement(ns + "mod", ide.Modelo));
            ideElement.Add(new XElement(ns + "serie", ide.Serie));
            ideElement.Add(new XElement(ns + "nNF", ide.NumeroNota));
            ideElement.Add(new XElement(ns + "dhEmi", FormatarDataHoraUTC(ide.DataEmissao)));
            
            if (ide.DataSaidaEntrada != default)
            {
                ideElement.Add(new XElement(ns + "dhSaiEnt", FormatarDataHoraUTC(ide.DataSaidaEntrada)));
            }

            if (ide.DataPrevisaoEntrega.HasValue)
            {
                ideElement.Add(new XElement(ns + "dPrevEntrega", ide.DataPrevisaoEntrega.Value.ToString("yyyy-MM-dd")));
            }

            ideElement.Add(new XElement(ns + "tpNF", ide.TipoOperacao));
            ideElement.Add(new XElement(ns + "idDest", ide.IdentificadorLocalDestino));
            ideElement.Add(new XElement(ns + "cMunFG", ide.CodigoMunicipioFatoGerador));
            
            if (!string.IsNullOrEmpty(ide.CodigoMunicipioFGIBS))
            {
                ideElement.Add(new XElement(ns + "cMunFGIBS", ide.CodigoMunicipioFGIBS));
            }

            ideElement.Add(new XElement(ns + "tpImp", ide.TipoImpressao));
            ideElement.Add(new XElement(ns + "tpEmis", ide.TipoEmissao));
            ideElement.Add(new XElement(ns + "cDV", cDV));
            ideElement.Add(new XElement(ns + "tpAmb", ide.Ambiente));
            ideElement.Add(new XElement(ns + "finNFe", ide.Finalidade));
            
            if (!string.IsNullOrEmpty(ide.TipoNFDebito))
            {
                ideElement.Add(new XElement(ns + "tpNFDebito", ide.TipoNFDebito));
            }

            if (!string.IsNullOrEmpty(ide.TipoNFCredito))
            {
                ideElement.Add(new XElement(ns + "tpNFCredito", ide.TipoNFCredito));
            }

            ideElement.Add(new XElement(ns + "indFinal", ide.IndicadorConsumidorFinal));
            ideElement.Add(new XElement(ns + "indPres", ide.IndicadorPresenca));
            
            if (!string.IsNullOrEmpty(ide.IndicadorIntermediador))
            {
                ideElement.Add(new XElement(ns + "indIntermed", ide.IndicadorIntermediador));
            }

            ideElement.Add(new XElement(ns + "procEmi", ide.ProcessoEmissao));
            ideElement.Add(new XElement(ns + "verProc", "API NFe MVC v1.0"));

            return ideElement;
        }

        private XElement CriarEmitente(NFeViewModel model, XNamespace ns)
        {
            var emit = model.Emitente;
            var emitElement = new XElement(ns + "emit");

            emitElement.Add(new XElement(ns + "CNPJ", RemoverFormatacao(emit.CNPJ)));
            emitElement.Add(new XElement(ns + "xNome", emit.RazaoSocial));

            if (!string.IsNullOrEmpty(emit.NomeFantasia))
            {
                emitElement.Add(new XElement(ns + "xFant", emit.NomeFantasia));
            }

            var enderEmit = new XElement(ns + "enderEmit");
            enderEmit.Add(new XElement(ns + "xLgr", emit.Endereco.Logradouro));
            enderEmit.Add(new XElement(ns + "nro", emit.Endereco.Numero));
            if (!string.IsNullOrEmpty(emit.Endereco.Complemento))
            {
                enderEmit.Add(new XElement(ns + "xCpl", emit.Endereco.Complemento));
            }
            enderEmit.Add(new XElement(ns + "xBairro", emit.Endereco.Bairro));
            enderEmit.Add(new XElement(ns + "cMun", emit.Endereco.CodigoMunicipio));
            enderEmit.Add(new XElement(ns + "xMun", emit.Endereco.NomeMunicipio));
            enderEmit.Add(new XElement(ns + "UF", emit.Endereco.UF));
            enderEmit.Add(new XElement(ns + "CEP", RemoverFormatacao(emit.Endereco.CEP)));
            enderEmit.Add(new XElement(ns + "cPais", "1058"));
            enderEmit.Add(new XElement(ns + "xPais", "BRASIL"));
            if (!string.IsNullOrEmpty(emit.Endereco.Telefone))
            {
                enderEmit.Add(new XElement(ns + "fone", RemoverFormatacao(emit.Endereco.Telefone)));
            }

            emitElement.Add(enderEmit);
            emitElement.Add(new XElement(ns + "IE", emit.InscricaoEstadual));
            emitElement.Add(new XElement(ns + "CRT", emit.CodigoRegimeTributario));

            return emitElement;
        }

        private XElement CriarDestinatario(NFeViewModel model, XNamespace ns)
        {
            var dest = model.Destinatario;
            var destElement = new XElement(ns + "dest");

            if (dest.Tipo == "PJ")
            {
                destElement.Add(new XElement(ns + "CNPJ", RemoverFormatacao(dest.Documento)));
            }
            else
            {
                destElement.Add(new XElement(ns + "CPF", RemoverFormatacao(dest.Documento)));
            }

            destElement.Add(new XElement(ns + "xNome", dest.NomeRazaoSocial));

            var enderDest = new XElement(ns + "enderDest");
            enderDest.Add(new XElement(ns + "xLgr", dest.Endereco.Logradouro));
            enderDest.Add(new XElement(ns + "nro", dest.Endereco.Numero));
            if (!string.IsNullOrEmpty(dest.Endereco.Complemento))
            {
                enderDest.Add(new XElement(ns + "xCpl", dest.Endereco.Complemento));
            }
            enderDest.Add(new XElement(ns + "xBairro", dest.Endereco.Bairro));
            enderDest.Add(new XElement(ns + "cMun", dest.Endereco.CodigoMunicipio));
            enderDest.Add(new XElement(ns + "xMun", dest.Endereco.NomeMunicipio));
            enderDest.Add(new XElement(ns + "UF", dest.Endereco.UF));
            enderDest.Add(new XElement(ns + "CEP", RemoverFormatacao(dest.Endereco.CEP)));
            enderDest.Add(new XElement(ns + "cPais", "1058"));
            enderDest.Add(new XElement(ns + "xPais", "BRASIL"));
            if (!string.IsNullOrEmpty(dest.Endereco.Telefone))
            {
                enderDest.Add(new XElement(ns + "fone", RemoverFormatacao(dest.Endereco.Telefone)));
            }

            destElement.Add(enderDest);
            destElement.Add(new XElement(ns + "indIEDest", dest.IndicadorIE));

            if (!string.IsNullOrEmpty(dest.InscricaoEstadual))
            {
                destElement.Add(new XElement(ns + "IE", dest.InscricaoEstadual));
            }

            return destElement;
        }

        private XElement CriarDetalhe(ProdutoViewModel produto, XNamespace ns, int nItem)
        {
            var det = new XElement(ns + "det", new XAttribute("nItem", nItem));

            var prod = new XElement(ns + "prod");
            prod.Add(new XElement(ns + "cProd", produto.Codigo));
            prod.Add(new XElement(ns + "cEAN", produto.EAN ?? "SEM GTIN"));
            prod.Add(new XElement(ns + "xProd", produto.Descricao));
            prod.Add(new XElement(ns + "NCM", produto.NCM));
            prod.Add(new XElement(ns + "CFOP", produto.CFOP));
            prod.Add(new XElement(ns + "uCom", produto.UnidadeComercial));
            prod.Add(new XElement(ns + "qCom", FormatarValor4Casas(produto.QuantidadeComercial)));
            prod.Add(new XElement(ns + "vUnCom", FormatarValor4Casas(produto.ValorUnitarioComercial)));
            prod.Add(new XElement(ns + "vProd", FormatarValor(produto.ValorTotal)));
            prod.Add(new XElement(ns + "cEANTrib", produto.EAN ?? "SEM GTIN"));
            prod.Add(new XElement(ns + "uTrib", produto.UnidadeTributavel ?? produto.UnidadeComercial));
            prod.Add(new XElement(ns + "qTrib", FormatarValor4Casas(produto.QuantidadeTributavel ?? produto.QuantidadeComercial)));
            prod.Add(new XElement(ns + "vUnTrib", FormatarValor4Casas(produto.ValorUnitarioTributavel ?? produto.ValorUnitarioComercial)));
            prod.Add(new XElement(ns + "vFrete", FormatarValor(produto.ValorFrete)));
            prod.Add(new XElement(ns + "vSeg", FormatarValor(produto.ValorSeguro)));
            prod.Add(new XElement(ns + "vDesc", FormatarValor(produto.ValorDesconto)));
            prod.Add(new XElement(ns + "vOutro", FormatarValor(produto.ValorOutros)));
            prod.Add(new XElement(ns + "indTot", produto.IndicadorTotal));

            det.Add(prod);

            // imposto
            var imposto = CriarImposto(produto, ns);
            det.Add(imposto);

            return det;
        }

        private XElement CriarImposto(ProdutoViewModel produto, XNamespace ns)
        {
            var imposto = new XElement(ns + "imposto");
            var valorProduto = produto.ValorTotal - produto.ValorDesconto;

            // ICMS
            var icms = new XElement(ns + "ICMS");
            var icms00 = new XElement(ns + "ICMS00");
            icms00.Add(new XElement(ns + "orig", "0"));
            icms00.Add(new XElement(ns + "CST", "000"));
            icms00.Add(new XElement(ns + "modBC", "0"));
            icms00.Add(new XElement(ns + "vBC", FormatarValor(valorProduto)));
            icms00.Add(new XElement(ns + "pICMS", "18.00"));
            icms00.Add(new XElement(ns + "vICMS", FormatarValor(valorProduto * 0.18m)));
            icms.Add(icms00);
            imposto.Add(icms);

            // IPI
            var ipi = new XElement(ns + "IPI");
            ipi.Add(new XElement(ns + "cEnq", "999"));
            var ipint = new XElement(ns + "IPINT");
            ipint.Add(new XElement(ns + "CST", "53"));
            ipi.Add(ipint);
            imposto.Add(ipi);

            // PIS
            var pis = new XElement(ns + "PIS");
            var pisAliq = new XElement(ns + "PISAliq");
            pisAliq.Add(new XElement(ns + "CST", "01"));
            pisAliq.Add(new XElement(ns + "vBC", FormatarValor(valorProduto)));
            pisAliq.Add(new XElement(ns + "pPIS", "1.65"));
            pisAliq.Add(new XElement(ns + "vPIS", FormatarValor(valorProduto * 0.0165m)));
            pis.Add(pisAliq);
            imposto.Add(pis);

            // COFINS
            var cofins = new XElement(ns + "COFINS");
            var cofinsAliq = new XElement(ns + "COFINSAliq");
            cofinsAliq.Add(new XElement(ns + "CST", "01"));
            cofinsAliq.Add(new XElement(ns + "vBC", FormatarValor(valorProduto)));
            cofinsAliq.Add(new XElement(ns + "pCOFINS", "7.60"));
            cofinsAliq.Add(new XElement(ns + "vCOFINS", FormatarValor(valorProduto * 0.076m)));
            cofins.Add(cofinsAliq);
            imposto.Add(cofins);

            imposto.Add(new XElement(ns + "vTotTrib", FormatarValor(valorProduto * 0.18m)));

            // IS (Imposto Seletivo) - opcional
            if (produto.ImpostoSeletivo != null)
            {
                var isElement = CriarImpostoSeletivo(produto.ImpostoSeletivo, ns);
                imposto.Add(isElement);
            }

            // IBSCBS - Reforma Tributária 2026
            if (produto.IBSCBS != null)
            {
                var ibscbsElement = CriarIBSCBS(produto.IBSCBS, ns);
                imposto.Add(ibscbsElement);
            }

            return imposto;
        }

        private XElement CriarTotal(NFeViewModel model, XNamespace ns)
        {
            var total = new XElement(ns + "total");
            var icmsTot = new XElement(ns + "ICMSTot");

            var valorTotal = model.Produtos.Sum(p => p.ValorTotal - p.ValorDesconto);
            var valorDesconto = model.Produtos.Sum(p => p.ValorDesconto);

            icmsTot.Add(new XElement(ns + "vBC", FormatarValor(valorTotal)));
            icmsTot.Add(new XElement(ns + "vICMS", FormatarValor(valorTotal * 0.18m)));
            icmsTot.Add(new XElement(ns + "vICMSDeson", "0.00"));
            icmsTot.Add(new XElement(ns + "vFCP", "0.00"));
            icmsTot.Add(new XElement(ns + "vBCST", "0.00"));
            icmsTot.Add(new XElement(ns + "vST", "0.00"));
            icmsTot.Add(new XElement(ns + "vFCPST", "0.00"));
            icmsTot.Add(new XElement(ns + "vFCPSTRet", "0.00"));
            icmsTot.Add(new XElement(ns + "vProd", FormatarValor(valorTotal)));
            icmsTot.Add(new XElement(ns + "vFrete", FormatarValor(model.Produtos.Sum(p => p.ValorFrete))));
            icmsTot.Add(new XElement(ns + "vSeg", FormatarValor(model.Produtos.Sum(p => p.ValorSeguro))));
            icmsTot.Add(new XElement(ns + "vDesc", FormatarValor(valorDesconto)));
            icmsTot.Add(new XElement(ns + "vII", "0.00"));
            icmsTot.Add(new XElement(ns + "vIPI", "0.00"));
            icmsTot.Add(new XElement(ns + "vIPIDevol", "0.00"));
            icmsTot.Add(new XElement(ns + "vPIS", FormatarValor(valorTotal * 0.0165m)));
            icmsTot.Add(new XElement(ns + "vCOFINS", FormatarValor(valorTotal * 0.076m)));
            icmsTot.Add(new XElement(ns + "vOutro", FormatarValor(model.Produtos.Sum(p => p.ValorOutros))));
            icmsTot.Add(new XElement(ns + "vNF", FormatarValor(valorTotal)));
            icmsTot.Add(new XElement(ns + "vTotTrib", FormatarValor(valorTotal * 0.18m)));

            total.Add(icmsTot);

            // IBSCBSTot - Reforma Tributária 2026
            if (model.IBSCBSTot != null)
            {
                var ibscbsTot = CriarIBSCBSTot(model.IBSCBSTot, ns);
                total.Add(ibscbsTot);
            }

            // vNFTot - Valor Total com impostos por fora
            if (model.ValorNFTot.HasValue)
            {
                total.Add(new XElement(ns + "vNFTot", FormatarValor(model.ValorNFTot.Value)));
            }

            return total;
        }

        private XElement CriarTransporte(TransporteViewModel transporte, XNamespace ns)
        {
            var transp = new XElement(ns + "transp");
            transp.Add(new XElement(ns + "modFrete", transporte.ModalidadeFrete));

            if (transporte.Transportadora != null)
            {
                var transporta = new XElement(ns + "transporta");
                if (!string.IsNullOrEmpty(transporte.Transportadora.CNPJ))
                {
                    transporta.Add(new XElement(ns + "CNPJ", RemoverFormatacao(transporte.Transportadora.CNPJ)));
                }
                if (!string.IsNullOrEmpty(transporte.Transportadora.Nome))
                {
                    transporta.Add(new XElement(ns + "xNome", transporte.Transportadora.Nome));
                }
                if (!string.IsNullOrEmpty(transporte.Transportadora.InscricaoEstadual))
                {
                    transporta.Add(new XElement(ns + "IE", transporte.Transportadora.InscricaoEstadual));
                }
                if (!string.IsNullOrEmpty(transporte.Transportadora.Endereco))
                {
                    transporta.Add(new XElement(ns + "xEnder", transporte.Transportadora.Endereco));
                }
                if (!string.IsNullOrEmpty(transporte.Transportadora.Municipio))
                {
                    transporta.Add(new XElement(ns + "xMun", transporte.Transportadora.Municipio));
                }
                if (!string.IsNullOrEmpty(transporte.Transportadora.UF))
                {
                    transporta.Add(new XElement(ns + "UF", transporte.Transportadora.UF));
                }
                transp.Add(transporta);
            }

            return transp;
        }

        private XElement CriarCobranca(CobrancaViewModel cobranca, XNamespace ns)
        {
            var cobr = new XElement(ns + "cobr");

            if (cobranca.Fatura != null)
            {
                var fat = new XElement(ns + "fat");
                if (!string.IsNullOrEmpty(cobranca.Fatura.Numero))
                {
                    fat.Add(new XElement(ns + "nFat", cobranca.Fatura.Numero));
                }
                fat.Add(new XElement(ns + "vOrig", FormatarValor(cobranca.Fatura.ValorOriginal)));
                fat.Add(new XElement(ns + "vDesc", FormatarValor(cobranca.Fatura.ValorDesconto)));
                fat.Add(new XElement(ns + "vLiq", FormatarValor(cobranca.Fatura.ValorLiquido)));
                cobr.Add(fat);
            }

            foreach (var dup in cobranca.Duplicatas)
            {
                var dupElement = new XElement(ns + "dup");
                if (!string.IsNullOrEmpty(dup.Numero))
                {
                    dupElement.Add(new XElement(ns + "nDup", dup.Numero));
                }
                dupElement.Add(new XElement(ns + "dVenc", dup.DataVencimento.ToString("yyyy-MM-dd")));
                dupElement.Add(new XElement(ns + "vDup", FormatarValor(dup.Valor)));
                cobr.Add(dupElement);
            }

            return cobr;
        }

        private XElement CriarImpostoSeletivo(ISViewModel isModel, XNamespace ns)
        {
            var isElement = new XElement(ns + "IS");
            isElement.Add(new XElement(ns + "CSTIS", isModel.CSTIS));
            isElement.Add(new XElement(ns + "cClassTribIS", isModel.CodigoClassificacaoTributariaIS));

            if (isModel.ValorBaseCalculoIS.HasValue && isModel.AliquotaIS.HasValue && isModel.ValorIS.HasValue)
            {
                isElement.Add(new XElement(ns + "vBCIS", FormatarValor(isModel.ValorBaseCalculoIS.Value)));
                isElement.Add(new XElement(ns + "pIS", FormatarValor(isModel.AliquotaIS.Value, 4)));

                if (isModel.AliquotaISEspecifica.HasValue)
                {
                    isElement.Add(new XElement(ns + "pISEspec", FormatarValor(isModel.AliquotaISEspecifica.Value, 4)));
                }

                if (!string.IsNullOrEmpty(isModel.UnidadeTributaria) && isModel.QuantidadeTributaria.HasValue)
                {
                    isElement.Add(new XElement(ns + "uTrib", isModel.UnidadeTributaria));
                    isElement.Add(new XElement(ns + "qTrib", FormatarValor4Casas(isModel.QuantidadeTributaria.Value)));
                }

                isElement.Add(new XElement(ns + "vIS", FormatarValor(isModel.ValorIS.Value)));
            }

            return isElement;
        }

        private XElement CriarIBSCBS(IBSCBSViewModel ibscbs, XNamespace ns)
        {
            var ibscbsElement = new XElement(ns + "IBSCBS");
            ibscbsElement.Add(new XElement(ns + "CST", ibscbs.CST));
            ibscbsElement.Add(new XElement(ns + "cClassTrib", ibscbs.CodigoClassificacaoTributaria));

            if (!string.IsNullOrEmpty(ibscbs.IndicadorDoacao))
            {
                ibscbsElement.Add(new XElement(ns + "indDoacao", ibscbs.IndicadorDoacao));
            }

            // Choice: gIBSCBS, gIBSCBSMono, gTransfCred, gAjusteCompet
            if (ibscbs.GrupoIBSCBS != null)
            {
                var grupo = CriarGrupoIBSCBS(ibscbs.GrupoIBSCBS, ns);
                ibscbsElement.Add(grupo);
            }
            else if (ibscbs.GrupoIBSCBSMonofasia != null)
            {
                var grupo = CriarGrupoIBSCBSMonofasia(ibscbs.GrupoIBSCBSMonofasia, ns);
                ibscbsElement.Add(grupo);
            }
            else if (ibscbs.GrupoTransferenciaCredito != null)
            {
                var grupo = new XElement(ns + "gTransfCred");
                grupo.Add(new XElement(ns + "vIBS", FormatarValor(ibscbs.GrupoTransferenciaCredito.ValorIBSTransferir)));
                grupo.Add(new XElement(ns + "vCBS", FormatarValor(ibscbs.GrupoTransferenciaCredito.ValorCBSTransferir)));
                ibscbsElement.Add(grupo);
            }
            else if (ibscbs.GrupoAjusteCompetencia != null)
            {
                var grupo = new XElement(ns + "gAjusteCompet");
                grupo.Add(new XElement(ns + "competApur", ibscbs.GrupoAjusteCompetencia.CompetenciaApuracao));
                grupo.Add(new XElement(ns + "vIBS", FormatarValor(ibscbs.GrupoAjusteCompetencia.ValorIBS)));
                grupo.Add(new XElement(ns + "vCBS", FormatarValor(ibscbs.GrupoAjusteCompetencia.ValorCBS)));
                ibscbsElement.Add(grupo);
            }

            if (ibscbs.GrupoEstornoCredito != null)
            {
                var grupo = new XElement(ns + "gEstornoCred");
                grupo.Add(new XElement(ns + "vIBSEstCred", FormatarValor(ibscbs.GrupoEstornoCredito.ValorIBSEstornar)));
                grupo.Add(new XElement(ns + "vCBSEstCred", FormatarValor(ibscbs.GrupoEstornoCredito.ValorCBSEstornar)));
                ibscbsElement.Add(grupo);
            }

            // Choice: gCredPresOper, gCredPresIBSZFM
            if (ibscbs.GrupoCreditoPresumidoOperacao != null)
            {
                var grupo = CriarGrupoCreditoPresumidoOperacao(ibscbs.GrupoCreditoPresumidoOperacao, ns);
                ibscbsElement.Add(grupo);
            }
            else if (ibscbs.GrupoCreditoPresumidoIBSZFM != null)
            {
                var grupo = new XElement(ns + "gCredPresIBSZFM");
                grupo.Add(new XElement(ns + "competApur", ibscbs.GrupoCreditoPresumidoIBSZFM.CompetenciaApuracao));
                grupo.Add(new XElement(ns + "tpCredPresIBSZFM", ibscbs.GrupoCreditoPresumidoIBSZFM.TipoCreditoPresumidoIBSZFM));
                grupo.Add(new XElement(ns + "vCredPresIBSZFM", FormatarValor(ibscbs.GrupoCreditoPresumidoIBSZFM.ValorCreditoPresumidoIBSZFM)));
                ibscbsElement.Add(grupo);
            }

            return ibscbsElement;
        }

        private XElement CriarGrupoIBSCBS(IBSCBSGrupoViewModel grupo, XNamespace ns)
        {
            var gElement = new XElement(ns + "gIBSCBS");
            gElement.Add(new XElement(ns + "vBC", FormatarValor(grupo.ValorBaseCalculo)));

            // gIBSUF
            var gIBSUF = new XElement(ns + "gIBSUF");
            gIBSUF.Add(new XElement(ns + "pIBSUF", FormatarValor(grupo.GrupoIBSUF.AliquotaIBSUF, 4)));

            if (grupo.GrupoIBSUF.GrupoDiferimento != null)
            {
                var gDif = new XElement(ns + "gDif");
                gDif.Add(new XElement(ns + "pDif", FormatarValor(grupo.GrupoIBSUF.GrupoDiferimento.PercentualDiferimento, 4)));
                gDif.Add(new XElement(ns + "vDif", FormatarValor(grupo.GrupoIBSUF.GrupoDiferimento.ValorDiferimento)));
                gIBSUF.Add(gDif);
            }

            if (grupo.GrupoIBSUF.GrupoDevolucaoTributo != null)
            {
                var gDevTrib = new XElement(ns + "gDevTrib");
                gDevTrib.Add(new XElement(ns + "vDevTrib", FormatarValor(grupo.GrupoIBSUF.GrupoDevolucaoTributo.ValorDevolucaoTributo)));
                gIBSUF.Add(gDevTrib);
            }

            if (grupo.GrupoIBSUF.GrupoReducaoAliquota != null)
            {
                var gRed = new XElement(ns + "gRed");
                gRed.Add(new XElement(ns + "pRedAliq", FormatarValor(grupo.GrupoIBSUF.GrupoReducaoAliquota.PercentualReducaoAliquota, 4)));
                gRed.Add(new XElement(ns + "pAliqEfet", FormatarValor(grupo.GrupoIBSUF.GrupoReducaoAliquota.AliquotaEfetiva, 4)));
                gIBSUF.Add(gRed);
            }

            gIBSUF.Add(new XElement(ns + "vIBSUF", FormatarValor(grupo.GrupoIBSUF.ValorIBSUF)));
            gElement.Add(gIBSUF);

            // gIBSMun
            var gIBSMun = new XElement(ns + "gIBSMun");
            gIBSMun.Add(new XElement(ns + "pIBSMun", FormatarValor(grupo.GrupoIBSMunicipio.AliquotaIBSMunicipio, 4)));

            if (grupo.GrupoIBSMunicipio.GrupoDiferimento != null)
            {
                var gDif = new XElement(ns + "gDif");
                gDif.Add(new XElement(ns + "pDif", FormatarValor(grupo.GrupoIBSMunicipio.GrupoDiferimento.PercentualDiferimento, 4)));
                gDif.Add(new XElement(ns + "vDif", FormatarValor(grupo.GrupoIBSMunicipio.GrupoDiferimento.ValorDiferimento)));
                gIBSMun.Add(gDif);
            }

            if (grupo.GrupoIBSMunicipio.GrupoDevolucaoTributo != null)
            {
                var gDevTrib = new XElement(ns + "gDevTrib");
                gDevTrib.Add(new XElement(ns + "vDevTrib", FormatarValor(grupo.GrupoIBSMunicipio.GrupoDevolucaoTributo.ValorDevolucaoTributo)));
                gIBSMun.Add(gDevTrib);
            }

            if (grupo.GrupoIBSMunicipio.GrupoReducaoAliquota != null)
            {
                var gRed = new XElement(ns + "gRed");
                gRed.Add(new XElement(ns + "pRedAliq", FormatarValor(grupo.GrupoIBSMunicipio.GrupoReducaoAliquota.PercentualReducaoAliquota, 4)));
                gRed.Add(new XElement(ns + "pAliqEfet", FormatarValor(grupo.GrupoIBSMunicipio.GrupoReducaoAliquota.AliquotaEfetiva, 4)));
                gIBSMun.Add(gRed);
            }

            gIBSMun.Add(new XElement(ns + "vIBSMun", FormatarValor(grupo.GrupoIBSMunicipio.ValorIBSMunicipio)));
            gElement.Add(gIBSMun);

            gElement.Add(new XElement(ns + "vIBS", FormatarValor(grupo.ValorIBS)));

            // gCBS
            var gCBS = new XElement(ns + "gCBS");
            gCBS.Add(new XElement(ns + "pCBS", FormatarValor(grupo.GrupoCBS.AliquotaCBS, 4)));

            if (grupo.GrupoCBS.GrupoDiferimento != null)
            {
                var gDif = new XElement(ns + "gDif");
                gDif.Add(new XElement(ns + "pDif", FormatarValor(grupo.GrupoCBS.GrupoDiferimento.PercentualDiferimento, 4)));
                gDif.Add(new XElement(ns + "vDif", FormatarValor(grupo.GrupoCBS.GrupoDiferimento.ValorDiferimento)));
                gCBS.Add(gDif);
            }

            if (grupo.GrupoCBS.GrupoDevolucaoTributo != null)
            {
                var gDevTrib = new XElement(ns + "gDevTrib");
                gDevTrib.Add(new XElement(ns + "vDevTrib", FormatarValor(grupo.GrupoCBS.GrupoDevolucaoTributo.ValorDevolucaoTributo)));
                gCBS.Add(gDevTrib);
            }

            if (grupo.GrupoCBS.GrupoReducaoAliquota != null)
            {
                var gRed = new XElement(ns + "gRed");
                gRed.Add(new XElement(ns + "pRedAliq", FormatarValor(grupo.GrupoCBS.GrupoReducaoAliquota.PercentualReducaoAliquota, 4)));
                gRed.Add(new XElement(ns + "pAliqEfet", FormatarValor(grupo.GrupoCBS.GrupoReducaoAliquota.AliquotaEfetiva, 4)));
                gCBS.Add(gRed);
            }

            gCBS.Add(new XElement(ns + "vCBS", FormatarValor(grupo.GrupoCBS.ValorCBS)));
            gElement.Add(gCBS);

            return gElement;
        }

        private XElement CriarGrupoIBSCBSMonofasia(IBSCBSMonofasiaViewModel grupo, XNamespace ns)
        {
            var gElement = new XElement(ns + "gIBSCBSMono");

            if (grupo.GrupoMonofasiaPadrao != null)
            {
                var gMonoPadrao = new XElement(ns + "gMonoPadrao");
                gMonoPadrao.Add(new XElement(ns + "qBCMono", FormatarValor4Casas(grupo.GrupoMonofasiaPadrao.QuantidadeTributada)));
                gMonoPadrao.Add(new XElement(ns + "adRemIBS", FormatarValor(grupo.GrupoMonofasiaPadrao.AliquotaAdRemIBS, 4)));
                gMonoPadrao.Add(new XElement(ns + "adRemCBS", FormatarValor(grupo.GrupoMonofasiaPadrao.AliquotaAdRemCBS, 4)));
                gMonoPadrao.Add(new XElement(ns + "vIBSMono", FormatarValor(grupo.GrupoMonofasiaPadrao.ValorIBSMonofasico)));
                gMonoPadrao.Add(new XElement(ns + "vCBSMono", FormatarValor(grupo.GrupoMonofasiaPadrao.ValorCBSMonofasica)));
                gElement.Add(gMonoPadrao);
            }

            if (grupo.GrupoMonofasiaRetencao != null)
            {
                var gMonoReten = new XElement(ns + "gMonoReten");
                gMonoReten.Add(new XElement(ns + "qBCMonoReten", FormatarValor4Casas(grupo.GrupoMonofasiaRetencao.QuantidadeTributadaRetencao)));
                gMonoReten.Add(new XElement(ns + "adRemIBSReten", FormatarValor(grupo.GrupoMonofasiaRetencao.AliquotaAdRemIBSRetencao, 4)));
                gMonoReten.Add(new XElement(ns + "vIBSMonoReten", FormatarValor(grupo.GrupoMonofasiaRetencao.ValorIBSMonofasicoRetencao)));
                gMonoReten.Add(new XElement(ns + "adRemCBSReten", FormatarValor(grupo.GrupoMonofasiaRetencao.AliquotaAdRemCBSRetencao, 4)));
                gMonoReten.Add(new XElement(ns + "vCBSMonoReten", FormatarValor(grupo.GrupoMonofasiaRetencao.ValorCBSMonofasicaRetencao)));
                gElement.Add(gMonoReten);
            }

            if (grupo.GrupoMonofasiaRetidoAnteriormente != null)
            {
                var gMonoRet = new XElement(ns + "gMonoRet");
                gMonoRet.Add(new XElement(ns + "qBCMonoRet", FormatarValor4Casas(grupo.GrupoMonofasiaRetidoAnteriormente.QuantidadeTributadaRetida)));
                gMonoRet.Add(new XElement(ns + "adRemIBSRet", FormatarValor(grupo.GrupoMonofasiaRetidoAnteriormente.AliquotaAdRemIBSRetido, 4)));
                gMonoRet.Add(new XElement(ns + "vIBSMonoRet", FormatarValor(grupo.GrupoMonofasiaRetidoAnteriormente.ValorIBSRetidoAnteriormente)));
                gMonoRet.Add(new XElement(ns + "adRemCBSRet", FormatarValor(grupo.GrupoMonofasiaRetidoAnteriormente.AliquotaAdRemCBSRetida, 4)));
                gMonoRet.Add(new XElement(ns + "vCBSMonoRet", FormatarValor(grupo.GrupoMonofasiaRetidoAnteriormente.ValorCBSRetidaAnteriormente)));
                gElement.Add(gMonoRet);
            }

            if (grupo.GrupoMonofasiaDiferimento != null)
            {
                var gMonoDif = new XElement(ns + "gMonoDif");
                gMonoDif.Add(new XElement(ns + "pDifIBS", FormatarValor(grupo.GrupoMonofasiaDiferimento.PercentualDiferimentoIBS, 4)));
                gMonoDif.Add(new XElement(ns + "vIBSMonoDif", FormatarValor(grupo.GrupoMonofasiaDiferimento.ValorIBSMonofasicoDiferido)));
                gMonoDif.Add(new XElement(ns + "pDifCBS", FormatarValor(grupo.GrupoMonofasiaDiferimento.PercentualDiferimentoCBS, 4)));
                gMonoDif.Add(new XElement(ns + "vCBSMonoDif", FormatarValor(grupo.GrupoMonofasiaDiferimento.ValorCBSMonofasicaDiferida)));
                gElement.Add(gMonoDif);
            }

            gElement.Add(new XElement(ns + "vTotIBSMonoItem", FormatarValor(grupo.TotalIBSMonofasicoItem)));
            gElement.Add(new XElement(ns + "vTotCBSMonoItem", FormatarValor(grupo.TotalCBSMonofasicaItem)));

            return gElement;
        }

        private XElement CriarGrupoCreditoPresumidoOperacao(CreditoPresumidoOperacaoViewModel grupo, XNamespace ns)
        {
            var gElement = new XElement(ns + "gCredPresOper");
            gElement.Add(new XElement(ns + "vBCCredPres", FormatarValor(grupo.ValorBaseCalculoCreditoPresumido)));
            gElement.Add(new XElement(ns + "cCredPres", grupo.CodigoCreditoPresumido));

            if (grupo.GrupoIBSCreditoPresumido != null)
            {
                var gIBSCredPres = CriarCreditoPresumido(grupo.GrupoIBSCreditoPresumido, ns);
                var gIBSCredPresElement = new XElement(ns + "gIBSCredPres");
                foreach (var child in gIBSCredPres.Elements())
                {
                    gIBSCredPresElement.Add(child);
                }
                gElement.Add(gIBSCredPresElement);
            }

            if (grupo.GrupoCBSCreditoPresumido != null)
            {
                var gCBSCredPres = CriarCreditoPresumido(grupo.GrupoCBSCreditoPresumido, ns);
                var gCBSCredPresElement = new XElement(ns + "gCBSCredPres");
                foreach (var child in gCBSCredPres.Elements())
                {
                    gCBSCredPresElement.Add(child);
                }
                gElement.Add(gCBSCredPresElement);
            }

            return gElement;
        }

        private XElement CriarCreditoPresumido(CreditoPresumidoViewModel credito, XNamespace ns)
        {
            var gElement = new XElement(ns + "gCredPres");
            gElement.Add(new XElement(ns + "pCredPres", FormatarValor(credito.PercentualCreditoPresumido, 4)));

            if (credito.ValorCreditoPresumido.HasValue)
            {
                gElement.Add(new XElement(ns + "vCredPres", FormatarValor(credito.ValorCreditoPresumido.Value)));
            }
            else if (credito.ValorCreditoPresumidoCondicaoSuspensiva.HasValue)
            {
                gElement.Add(new XElement(ns + "vCredPresCondSus", FormatarValor(credito.ValorCreditoPresumidoCondicaoSuspensiva.Value)));
            }

            return gElement;
        }

        private XElement CriarIBSCBSTot(IBSCBSTotViewModel tot, XNamespace ns)
        {
            var totElement = new XElement(ns + "IBSCBSTot");
            totElement.Add(new XElement(ns + "vBCIBSCBS", FormatarValor(tot.ValorBaseCalculoIBSCBS)));

            if (tot.GrupoIBSTot != null)
            {
                var gIBS = new XElement(ns + "gIBS");
                
                var gIBSUF = new XElement(ns + "gIBSUF");
                gIBSUF.Add(new XElement(ns + "vDif", FormatarValor(tot.GrupoIBSTot.GrupoIBSUF.ValorDiferimento)));
                gIBSUF.Add(new XElement(ns + "vDevTrib", FormatarValor(tot.GrupoIBSTot.GrupoIBSUF.ValorDevolucaoTributos)));
                gIBSUF.Add(new XElement(ns + "vIBSUF", FormatarValor(tot.GrupoIBSTot.GrupoIBSUF.ValorIBSUF)));
                gIBS.Add(gIBSUF);

                var gIBSMun = new XElement(ns + "gIBSMun");
                gIBSMun.Add(new XElement(ns + "vDif", FormatarValor(tot.GrupoIBSTot.GrupoIBSMunicipio.ValorDiferimento)));
                gIBSMun.Add(new XElement(ns + "vDevTrib", FormatarValor(tot.GrupoIBSTot.GrupoIBSMunicipio.ValorDevolucaoTributos)));
                gIBSMun.Add(new XElement(ns + "vIBSMun", FormatarValor(tot.GrupoIBSTot.GrupoIBSMunicipio.ValorIBSMunicipio)));
                gIBS.Add(gIBSMun);

                gIBS.Add(new XElement(ns + "vIBS", FormatarValor(tot.GrupoIBSTot.ValorIBS)));
                totElement.Add(gIBS);
            }

            if (tot.GrupoCBSTot != null)
            {
                var gCBS = new XElement(ns + "gCBS");
                gCBS.Add(new XElement(ns + "vDif", FormatarValor(tot.GrupoCBSTot.ValorDiferimento)));
                gCBS.Add(new XElement(ns + "vDevTrib", FormatarValor(tot.GrupoCBSTot.ValorDevolucaoTributos)));
                gCBS.Add(new XElement(ns + "vCBS", FormatarValor(tot.GrupoCBSTot.ValorCBS)));

                if (tot.GrupoCBSTot.TotalCreditoPresumido.HasValue)
                {
                    gCBS.Add(new XElement(ns + "vCredPres", FormatarValor(tot.GrupoCBSTot.TotalCreditoPresumido.Value)));
                }

                if (tot.GrupoCBSTot.TotalCreditoPresumidoCondicaoSuspensiva.HasValue)
                {
                    gCBS.Add(new XElement(ns + "vCredPresCondSus", FormatarValor(tot.GrupoCBSTot.TotalCreditoPresumidoCondicaoSuspensiva.Value)));
                }

                totElement.Add(gCBS);
            }

            if (tot.GrupoMonofasiaTot != null)
            {
                var gMono = new XElement(ns + "gMono");
                gMono.Add(new XElement(ns + "vIBSMono", FormatarValor(tot.GrupoMonofasiaTot.ValorIBSMonofasico)));
                gMono.Add(new XElement(ns + "vCBSMono", FormatarValor(tot.GrupoMonofasiaTot.ValorCBSMonofasica)));
                gMono.Add(new XElement(ns + "vIBSMonoReten", FormatarValor(tot.GrupoMonofasiaTot.ValorIBSMonofasicoRetencao)));
                gMono.Add(new XElement(ns + "vCBSMonoReten", FormatarValor(tot.GrupoMonofasiaTot.ValorCBSMonofasicaRetencao)));
                gMono.Add(new XElement(ns + "vIBSMonoRet", FormatarValor(tot.GrupoMonofasiaTot.ValorIBSMonofasicoRetido)));
                gMono.Add(new XElement(ns + "vCBSMonoRet", FormatarValor(tot.GrupoMonofasiaTot.ValorCBSMonofasicaRetida)));
                totElement.Add(gMono);
            }

            if (tot.GrupoEstornoCreditoTot != null)
            {
                var gEstornoCred = new XElement(ns + "gEstornoCred");
                gEstornoCred.Add(new XElement(ns + "vIBSEstCred", FormatarValor(tot.GrupoEstornoCreditoTot.ValorIBSEstornado)));
                gEstornoCred.Add(new XElement(ns + "vCBSEstCred", FormatarValor(tot.GrupoEstornoCreditoTot.ValorCBSEstornada)));
                totElement.Add(gEstornoCred);
            }

            return totElement;
        }

        private string RemoverFormatacao(string? valor)
        {
            if (string.IsNullOrEmpty(valor))
                return string.Empty;

            return valor.Replace(".", "").Replace("-", "").Replace("/", "").Replace(" ", "").Replace("(", "").Replace(")", "");
        }
    }
}

