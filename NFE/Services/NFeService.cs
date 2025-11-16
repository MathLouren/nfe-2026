using NFE.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

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
                    XDocument.Parse(xml);
                    return true;
                }
                catch
                {
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

            ideElement.Add(new XElement(ns + "cUF", ide.CodigoUF));
            ideElement.Add(new XElement(ns + "cNF", new Random().Next(10000000, 99999999).ToString()));
            ideElement.Add(new XElement(ns + "natOp", ide.NaturezaOperacao));
            ideElement.Add(new XElement(ns + "mod", ide.Modelo));
            ideElement.Add(new XElement(ns + "serie", ide.Serie));
            ideElement.Add(new XElement(ns + "nNF", ide.NumeroNota));
            ideElement.Add(new XElement(ns + "dhEmi", ide.DataEmissao.ToString("yyyy-MM-ddTHH:mm:sszzz")));
            ideElement.Add(new XElement(ns + "dhSaiEnt", ide.DataSaidaEntrada.ToString("yyyy-MM-ddTHH:mm:sszzz")));
            ideElement.Add(new XElement(ns + "tpNF", ide.TipoOperacao));
            ideElement.Add(new XElement(ns + "idDest", "1"));
            ideElement.Add(new XElement(ns + "cMunFG", ide.CodigoMunicipioFatoGerador));
            ideElement.Add(new XElement(ns + "tpImp", ide.TipoImpressao));
            ideElement.Add(new XElement(ns + "tpEmis", ide.TipoEmissao));
            ideElement.Add(new XElement(ns + "cDV", "0"));
            ideElement.Add(new XElement(ns + "tpAmb", ide.Ambiente));
            ideElement.Add(new XElement(ns + "finNFe", ide.Finalidade));
            ideElement.Add(new XElement(ns + "indFinal", ide.IndicadorConsumidorFinal));
            ideElement.Add(new XElement(ns + "indPres", ide.IndicadorPresenca));
            ideElement.Add(new XElement(ns + "procEmi", "0"));
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

            // imposto (simplificado)
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

        private string RemoverFormatacao(string? valor)
        {
            if (string.IsNullOrEmpty(valor))
                return string.Empty;

            return valor.Replace(".", "").Replace("-", "").Replace("/", "").Replace(" ", "").Replace("(", "").Replace(")", "");
        }
    }
}

