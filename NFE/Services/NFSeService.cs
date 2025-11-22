using NFE.Models;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml;

namespace NFE.Services
{
    /// <summary>
    /// Serviço de NFS-e seguindo padrão MVC
    /// </summary>
    public class NFSeService : INFSeService
    {
        private readonly INFSeWebServiceClient _webServiceClient;
        private readonly ILogger<NFSeService> _logger;
        
        // Cultura invariante para garantir ponto decimal (padrão XML)
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public NFSeService(INFSeWebServiceClient webServiceClient, ILogger<NFSeService> logger)
        {
            _webServiceClient = webServiceClient;
            _logger = logger;
        }
        
        /// <summary>
        /// Formata valor decimal para string com ponto decimal
        /// </summary>
        private static string FormatarValor(decimal valor, int casasDecimais = 2)
        {
            return valor.ToString($"F{casasDecimais}", InvariantCulture);
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

        public async Task<NFSeResponseViewModel> ProcessarNFSeAsync(NFSeViewModel model, string ambiente)
        {
            try
            {
                _logger.LogInformation("Processando NFS-e - Número: {Numero}", 
                    model.Identificacao.NumeroNFSe);

                // Gerar XML
                string xml;
                try
                {
                    xml = await GerarXmlAsync(model);
                    _logger.LogInformation("XML gerado com sucesso - Tamanho: {Tamanho} bytes", xml.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao gerar XML da NFS-e");
                    return new NFSeResponseViewModel
                    {
                        Sucesso = false,
                        Mensagem = $"Erro ao gerar XML da NFS-e. Detalhes: {ex.Message}",
                        Erros = new Dictionary<string, string[]>
                        {
                            { "TipoErro", new[] { "ErroGeracaoXML" } },
                            { "Mensagem", new[] { ex.Message } }
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
                        return new NFSeResponseViewModel
                        {
                            Sucesso = false,
                            Mensagem = "XML gerado é inválido",
                            XmlEnviado = xml,
                            Erros = new Dictionary<string, string[]>
                            {
                                { "TipoErro", new[] { "XMLInvalido" } }
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao validar XML da NFS-e");
                    return new NFSeResponseViewModel
                    {
                        Sucesso = false,
                        Mensagem = $"Erro ao validar XML da NFS-e. Detalhes: {ex.Message}",
                        XmlEnviado = xml,
                        Erros = new Dictionary<string, string[]>
                        {
                            { "TipoErro", new[] { "ErroValidacaoXML" } },
                            { "Mensagem", new[] { ex.Message } }
                        }
                    };
                }

                // Enviar para webservice
                var resposta = await _webServiceClient.EnviarNFSeAsync(xml, ambiente);

                return new NFSeResponseViewModel
                {
                    Sucesso = resposta.Sucesso,
                    Mensagem = resposta.Mensagem ?? "Processamento concluído",
                    XmlEnviado = xml,
                    XmlRetorno = resposta.XmlRetorno,
                    Protocolo = resposta.Protocolo,
                    NumeroNFSe = resposta.NumeroNFSe,
                    CodigoVerificacao = resposta.CodigoVerificacao,
                    CodigoStatus = resposta.CodigoStatus,
                    Motivo = resposta.Motivo,
                    LinkConsulta = resposta.LinkConsulta,
                    Erros = resposta.Erros?.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new[] { kvp.Value }
                    )
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar NFS-e");
                return new NFSeResponseViewModel
                {
                    Sucesso = false,
                    Mensagem = $"Erro inesperado ao processar NFS-e. Detalhes: {ex.Message}",
                    Erros = new Dictionary<string, string[]>
                    {
                        { "TipoErro", new[] { "ErroInesperado" } },
                        { "Mensagem", new[] { ex.Message } }
                    }
                };
            }
        }

        public async Task<string> GerarXmlAsync(NFSeViewModel model)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Iniciando geração de XML da NFS-e");
                    
                    // Namespace padrão NFS-e Nacional 2026
                    var ns = XNamespace.Get("http://www.portalfiscal.inf.br/nfse");
                    
                    // Gerar código de verificação (8 dígitos aleatórios)
                    string codigoVerificacao = GerarCodigoVerificacao();
                    
                    _logger.LogInformation("Código de verificação gerado: {Codigo}", codigoVerificacao);
                    
                    var nfse = new XElement(ns + "NFSe");

                    var infNFSe = new XElement(ns + "infNFSe",
                        new XAttribute("Id", $"NFSe{model.Identificacao.NumeroNFSe}{codigoVerificacao}"),
                        new XAttribute("versao", "1.00")
                    );

                    // 1. Identificação
                    infNFSe.Add(CriarIdentificacao(model, ns, codigoVerificacao));
                    
                    // 2. Prestador
                    infNFSe.Add(CriarPrestador(model, ns));
                    
                    // 3. Tomador
                    infNFSe.Add(CriarTomador(model, ns));
                    
                    // 4. Serviços
                    int nItem = 1;
                    foreach (var servico in model.Servicos)
                    {
                        infNFSe.Add(CriarServico(servico, ns, nItem++));
                    }

                    // 5. Totais
                    infNFSe.Add(CriarTotal(model, ns));

                    // 6. Pagamento (se houver)
                    if (model.Pagamento?.FormasPagamento != null && 
                        model.Pagamento.FormasPagamento.Any())
                    {
                        infNFSe.Add(CriarPagamento(model.Pagamento, ns));
                    }

                    // 7. Informações Adicionais
                    if (!string.IsNullOrEmpty(model.InformacoesAdicionais))
                    {
                        var infAdic = new XElement(ns + "infAdic");
                        infAdic.Add(new XElement(ns + "infCpl", model.InformacoesAdicionais));
                        infNFSe.Add(infAdic);
                    }

                    nfse.Add(infNFSe);

                    var xmlDocument = new XDocument(
                        new XDeclaration("1.0", "UTF-8", null),
                        nfse
                    );

                    string xmlString = xmlDocument.ToString(SaveOptions.DisableFormatting);
                    
                    _logger.LogInformation("XML gerado - Tamanho: {Size} bytes", xmlString.Length);
                    
                    return xmlString;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao gerar XML de NFS-e");
                    throw;
                }
            });
        }

        private XElement CriarIdentificacao(NFSeViewModel model, XNamespace ns, string codigoVerificacao)
        {
            var ide = model.Identificacao;
            var ideElement = new XElement(ns + "ide");

            ideElement.Add(new XElement(ns + "cUF", ide.CodigoUF));
            ideElement.Add(new XElement(ns + "cMun", ide.CodigoMunicipio));
            ideElement.Add(new XElement(ns + "natOp", ide.NaturezaOperacao));
            ideElement.Add(new XElement(ns + "regEsp", ide.RegimeEspecialTributacao));
            ideElement.Add(new XElement(ns + "optSimpNac", ide.OptanteSimplesNacional));
            ideElement.Add(new XElement(ns + "incCult", ide.IncentivadorCultural));
            ideElement.Add(new XElement(ns + "nNFSe", ide.NumeroNFSe));
            ideElement.Add(new XElement(ns + "cVerif", codigoVerificacao));
            ideElement.Add(new XElement(ns + "dhEmi", FormatarDataHoraUTC(ide.DataEmissao)));
            ideElement.Add(new XElement(ns + "tpAmb", ide.Ambiente));

            if (!string.IsNullOrEmpty(ide.CodigoMunicipioFGIBS))
            {
                ideElement.Add(new XElement(ns + "cMunFGIBS", ide.CodigoMunicipioFGIBS));
            }

            if (!string.IsNullOrEmpty(ide.IndicadorPresenca))
            {
                ideElement.Add(new XElement(ns + "indPres", ide.IndicadorPresenca));
            }

            return ideElement;
        }

        private XElement CriarPrestador(NFSeViewModel model, XNamespace ns)
        {
            var prest = model.Prestador;
            var prestElement = new XElement(ns + "prest");

            if (!string.IsNullOrEmpty(prest.CNPJ))
            {
                prestElement.Add(new XElement(ns + "CNPJ", RemoverFormatacao(prest.CNPJ)));
            }
            else if (!string.IsNullOrEmpty(prest.CPF))
            {
                prestElement.Add(new XElement(ns + "CPF", RemoverFormatacao(prest.CPF)));
            }

            prestElement.Add(new XElement(ns + "xNome", prest.RazaoSocial));

            if (!string.IsNullOrEmpty(prest.NomeFantasia))
            {
                prestElement.Add(new XElement(ns + "xFant", prest.NomeFantasia));
            }

            prestElement.Add(new XElement(ns + "IM", prest.InscricaoMunicipal));

            if (!string.IsNullOrEmpty(prest.InscricaoEstadual))
            {
                prestElement.Add(new XElement(ns + "IE", prest.InscricaoEstadual));
            }

            var enderPrest = CriarEndereco(prest.Endereco, ns, "enderPrest");
            prestElement.Add(enderPrest);

            if (!string.IsNullOrEmpty(prest.Telefone))
            {
                prestElement.Add(new XElement(ns + "fone", RemoverFormatacao(prest.Telefone)));
            }

            if (!string.IsNullOrEmpty(prest.Email))
            {
                prestElement.Add(new XElement(ns + "email", prest.Email));
            }

            return prestElement;
        }

        private XElement CriarTomador(NFSeViewModel model, XNamespace ns)
        {
            var tom = model.Tomador;
            var tomElement = new XElement(ns + "tom");

            if (tom.Tipo == "PJ")
            {
                tomElement.Add(new XElement(ns + "CNPJ", RemoverFormatacao(tom.Documento)));
            }
            else if (tom.Tipo == "PF")
            {
                tomElement.Add(new XElement(ns + "CPF", RemoverFormatacao(tom.Documento)));
            }
            else if (tom.Tipo == "Estrangeiro")
            {
                tomElement.Add(new XElement(ns + "NIF", tom.Documento));
            }

            tomElement.Add(new XElement(ns + "xNome", tom.NomeRazaoSocial));

            var enderTom = CriarEndereco(tom.Endereco, ns, "enderTom");
            tomElement.Add(enderTom);

            if (!string.IsNullOrEmpty(tom.Telefone))
            {
                tomElement.Add(new XElement(ns + "fone", RemoverFormatacao(tom.Telefone)));
            }

            if (!string.IsNullOrEmpty(tom.Email))
            {
                tomElement.Add(new XElement(ns + "email", tom.Email));
            }

            if (!string.IsNullOrEmpty(tom.InscricaoEstadual))
            {
                tomElement.Add(new XElement(ns + "IE", tom.InscricaoEstadual));
            }

            if (!string.IsNullOrEmpty(tom.InscricaoMunicipal))
            {
                tomElement.Add(new XElement(ns + "IM", tom.InscricaoMunicipal));
            }

            return tomElement;
        }

        private XElement CriarEndereco(EnderecoNFSeViewModel endereco, XNamespace ns, string nomeElemento)
        {
            var ender = new XElement(ns + nomeElemento);
            ender.Add(new XElement(ns + "xLgr", endereco.Logradouro));
            ender.Add(new XElement(ns + "nro", endereco.Numero));
            
            if (!string.IsNullOrEmpty(endereco.Complemento))
            {
                ender.Add(new XElement(ns + "xCpl", endereco.Complemento));
            }
            
            ender.Add(new XElement(ns + "xBairro", endereco.Bairro));
            ender.Add(new XElement(ns + "cMun", endereco.CodigoMunicipio));
            ender.Add(new XElement(ns + "xMun", endereco.NomeMunicipio));
            ender.Add(new XElement(ns + "UF", endereco.UF));
            ender.Add(new XElement(ns + "CEP", RemoverFormatacao(endereco.CEP)));
            ender.Add(new XElement(ns + "cPais", "1058"));
            ender.Add(new XElement(ns + "xPais", "BRASIL"));

            return ender;
        }

        private XElement CriarServico(ServicoViewModel servico, XNamespace ns, int nItem)
        {
            var serv = new XElement(ns + "serv", new XAttribute("nItem", nItem));

            serv.Add(new XElement(ns + "cServ", servico.Codigo));
            serv.Add(new XElement(ns + "xServ", servico.Descricao));
            serv.Add(new XElement(ns + "cClassServ", servico.CodigoClassificacao));
            serv.Add(new XElement(ns + "cTribMun", servico.CodigoTributacaoMunicipal));
            serv.Add(new XElement(ns + "discriminacao", servico.Discriminacao));
            serv.Add(new XElement(ns + "cMunPrest", servico.CodigoMunicipioPrestacao));
            serv.Add(new XElement(ns + "uCom", servico.Unidade));
            serv.Add(new XElement(ns + "qCom", FormatarValor(servico.Quantidade, 4)));
            serv.Add(new XElement(ns + "vUnCom", FormatarValor(servico.ValorUnitario, 4)));
            serv.Add(new XElement(ns + "vServ", FormatarValor(servico.ValorTotal)));

            if (servico.ValorDeducoes > 0)
            {
                serv.Add(new XElement(ns + "vDeducoes", FormatarValor(servico.ValorDeducoes)));
            }

            if (servico.ValorDescontoIncondicionado > 0)
            {
                serv.Add(new XElement(ns + "vDescIncond", FormatarValor(servico.ValorDescontoIncondicionado)));
            }

            if (servico.ValorDescontoCondicionado > 0)
            {
                serv.Add(new XElement(ns + "vDescCond", FormatarValor(servico.ValorDescontoCondicionado)));
            }

            if (servico.ValorOutrasRetencoes > 0)
            {
                serv.Add(new XElement(ns + "vOutrasRet", FormatarValor(servico.ValorOutrasRetencoes)));
            }

            serv.Add(new XElement(ns + "vLiq", FormatarValor(servico.ValorLiquido > 0 ? servico.ValorLiquido : servico.ValorTotal)));

            // Tributação (ISS atual)
            if (servico.Tributacao != null)
            {
                var imposto = CriarTributacao(servico.Tributacao, ns);
                serv.Add(imposto);
            }

            // IBS/CBS (Reforma Tributária 2026)
            if (servico.IBSCBS != null)
            {
                var ibscbs = CriarIBSCBSServico(servico.IBSCBS, ns);
                serv.Add(ibscbs);
            }

            // Imposto Seletivo
            if (servico.ImpostoSeletivo != null)
            {
                var isElement = CriarImpostoSeletivoServico(servico.ImpostoSeletivo, ns);
                serv.Add(isElement);
            }

            return serv;
        }

        private XElement CriarTributacao(TributacaoServicoViewModel tributacao, XNamespace ns)
        {
            var imposto = new XElement(ns + "imposto");
            var iss = new XElement(ns + "ISS");

            iss.Add(new XElement(ns + "sitTrib", tributacao.SituacaoTributaria));

            if (tributacao.Aliquota.HasValue)
            {
                iss.Add(new XElement(ns + "aliq", FormatarValor(tributacao.Aliquota.Value, 4)));
            }

            if (tributacao.ValorBaseCalculo.HasValue)
            {
                iss.Add(new XElement(ns + "vBC", FormatarValor(tributacao.ValorBaseCalculo.Value)));
            }

            if (tributacao.ValorISS.HasValue)
            {
                iss.Add(new XElement(ns + "vISS", FormatarValor(tributacao.ValorISS.Value)));
            }

            imposto.Add(iss);

            // Outros impostos
            if (tributacao.ValorPIS.HasValue || tributacao.ValorCOFINS.HasValue || 
                tributacao.ValorINSS.HasValue || tributacao.ValorIR.HasValue || 
                tributacao.ValorCSLL.HasValue)
            {
                var outros = new XElement(ns + "outros");
                
                if (tributacao.ValorPIS.HasValue)
                {
                    outros.Add(new XElement(ns + "vPIS", FormatarValor(tributacao.ValorPIS.Value)));
                }
                
                if (tributacao.ValorCOFINS.HasValue)
                {
                    outros.Add(new XElement(ns + "vCOFINS", FormatarValor(tributacao.ValorCOFINS.Value)));
                }
                
                if (tributacao.ValorINSS.HasValue)
                {
                    outros.Add(new XElement(ns + "vINSS", FormatarValor(tributacao.ValorINSS.Value)));
                }
                
                if (tributacao.ValorIR.HasValue)
                {
                    outros.Add(new XElement(ns + "vIR", FormatarValor(tributacao.ValorIR.Value)));
                }
                
                if (tributacao.ValorCSLL.HasValue)
                {
                    outros.Add(new XElement(ns + "vCSLL", FormatarValor(tributacao.ValorCSLL.Value)));
                }
                
                imposto.Add(outros);
            }

            return imposto;
        }

        private XElement CriarIBSCBSServico(IBSCBSServicoViewModel ibscbs, XNamespace ns)
        {
            var ibscbsElement = new XElement(ns + "IBSCBS");
            ibscbsElement.Add(new XElement(ns + "CST", ibscbs.CST));
            ibscbsElement.Add(new XElement(ns + "cClassTrib", ibscbs.CodigoClassificacaoTributaria));

            // Reutilizar lógica similar à NFe para grupos IBS/CBS
            if (ibscbs.GrupoIBSCBS != null)
            {
                // Implementar criação de grupo IBS/CBS (similar à NFe)
                var grupo = CriarGrupoIBSCBS(ibscbs.GrupoIBSCBS, ns);
                ibscbsElement.Add(grupo);
            }

            return ibscbsElement;
        }

        private XElement CriarGrupoIBSCBS(IBSCBSGrupoViewModel grupo, XNamespace ns)
        {
            // Reutilizar implementação similar à NFe
            var gElement = new XElement(ns + "gIBSCBS");
            gElement.Add(new XElement(ns + "vBC", FormatarValor(grupo.ValorBaseCalculo)));

            // Implementar grupos IBS UF, Município e CBS (similar à NFe)
            // Por brevidade, implementação básica
            var gIBSUF = new XElement(ns + "gIBSUF");
            gIBSUF.Add(new XElement(ns + "pIBSUF", FormatarValor(grupo.GrupoIBSUF.AliquotaIBSUF, 4)));
            gIBSUF.Add(new XElement(ns + "vIBSUF", FormatarValor(grupo.GrupoIBSUF.ValorIBSUF)));
            gElement.Add(gIBSUF);

            var gIBSMun = new XElement(ns + "gIBSMun");
            gIBSMun.Add(new XElement(ns + "pIBSMun", FormatarValor(grupo.GrupoIBSMunicipio.AliquotaIBSMunicipio, 4)));
            gIBSMun.Add(new XElement(ns + "vIBSMun", FormatarValor(grupo.GrupoIBSMunicipio.ValorIBSMunicipio)));
            gElement.Add(gIBSMun);

            gElement.Add(new XElement(ns + "vIBS", FormatarValor(grupo.ValorIBS)));

            var gCBS = new XElement(ns + "gCBS");
            gCBS.Add(new XElement(ns + "pCBS", FormatarValor(grupo.GrupoCBS.AliquotaCBS, 4)));
            gCBS.Add(new XElement(ns + "vCBS", FormatarValor(grupo.GrupoCBS.ValorCBS)));
            gElement.Add(gCBS);

            return gElement;
        }

        private XElement CriarImpostoSeletivoServico(ISServicoViewModel isModel, XNamespace ns)
        {
            var isElement = new XElement(ns + "IS");
            isElement.Add(new XElement(ns + "CSTIS", isModel.CSTIS));
            isElement.Add(new XElement(ns + "cClassTribIS", isModel.CodigoClassificacaoTributariaIS));

            if (isModel.ValorBaseCalculoIS.HasValue)
            {
                isElement.Add(new XElement(ns + "vBCIS", FormatarValor(isModel.ValorBaseCalculoIS.Value)));
            }

            if (isModel.AliquotaIS.HasValue)
            {
                isElement.Add(new XElement(ns + "pIS", FormatarValor(isModel.AliquotaIS.Value, 4)));
            }

            if (isModel.ValorIS.HasValue)
            {
                isElement.Add(new XElement(ns + "vIS", FormatarValor(isModel.ValorIS.Value)));
            }

            return isElement;
        }

        private XElement CriarTotal(NFSeViewModel model, XNamespace ns)
        {
            var total = new XElement(ns + "total");
            var servTot = new XElement(ns + "servTot");

            var valorServicos = model.Servicos.Sum(s => s.ValorTotal);
            var valorDeducoes = model.Servicos.Sum(s => s.ValorDeducoes);
            var valorDescontoIncond = model.Servicos.Sum(s => s.ValorDescontoIncondicionado);
            var valorDescontoCond = model.Servicos.Sum(s => s.ValorDescontoCondicionado);
            var valorOutrasRet = model.Servicos.Sum(s => s.ValorOutrasRetencoes);
            var valorLiquido = model.Servicos.Sum(s => s.ValorLiquido > 0 ? s.ValorLiquido : s.ValorTotal);

            servTot.Add(new XElement(ns + "vServ", FormatarValor(valorServicos)));
            servTot.Add(new XElement(ns + "vDeducoes", FormatarValor(valorDeducoes)));
            servTot.Add(new XElement(ns + "vDescIncond", FormatarValor(valorDescontoIncond)));
            servTot.Add(new XElement(ns + "vDescCond", FormatarValor(valorDescontoCond)));
            servTot.Add(new XElement(ns + "vOutrasRet", FormatarValor(valorOutrasRet)));
            servTot.Add(new XElement(ns + "vLiq", FormatarValor(valorLiquido)));

            // Totais de impostos (ISS atual)
            var valorISS = model.Servicos
                .Where(s => s.Tributacao?.ValorISS.HasValue == true)
                .Sum(s => s.Tributacao!.ValorISS!.Value);
            
            servTot.Add(new XElement(ns + "vISS", FormatarValor(valorISS)));

            // Totais IBS/CBS (Reforma Tributária 2026)
            if (model.IBSCBSTot != null)
            {
                var ibscbsTot = CriarIBSCBSTot(model.IBSCBSTot, ns);
                servTot.Add(ibscbsTot);
            }

            // Valor total com impostos por fora
            if (model.ValorNFSeTot.HasValue)
            {
                servTot.Add(new XElement(ns + "vNFSeTot", FormatarValor(model.ValorNFSeTot.Value)));
            }

            total.Add(servTot);
            return total;
        }

        private XElement CriarIBSCBSTot(IBSCBSTotNFSeViewModel tot, XNamespace ns)
        {
            var totElement = new XElement(ns + "IBSCBSTot");
            totElement.Add(new XElement(ns + "vBCIBSCBS", FormatarValor(tot.ValorBaseCalculoIBSCBS)));

            if (tot.GrupoIBSTot != null)
            {
                var gIBS = new XElement(ns + "gIBS");
                gIBS.Add(new XElement(ns + "vIBS", FormatarValor(tot.GrupoIBSTot.ValorIBS)));
                totElement.Add(gIBS);
            }

            if (tot.GrupoCBSTot != null)
            {
                var gCBS = new XElement(ns + "gCBS");
                gCBS.Add(new XElement(ns + "vCBS", FormatarValor(tot.GrupoCBSTot.ValorCBS)));
                totElement.Add(gCBS);
            }

            return totElement;
        }

        private XElement CriarPagamento(PagamentoNFSeViewModel pagamento, XNamespace ns)
        {
            var pag = new XElement(ns + "pag");

            foreach (var forma in pagamento.FormasPagamento)
            {
                var detPag = new XElement(ns + "detPag");
                detPag.Add(new XElement(ns + "tPag", forma.MeioPagamento));
                detPag.Add(new XElement(ns + "vPag", FormatarValor(forma.Valor)));

                if (forma.DataVencimento.HasValue)
                {
                    detPag.Add(new XElement(ns + "dVenc", forma.DataVencimento.Value.ToString("yyyy-MM-dd")));
                }

                pag.Add(detPag);
            }

            return pag;
        }

        private string GerarCodigoVerificacao()
        {
            // Gera código de verificação de 8 dígitos
            Random rnd = new Random();
            return rnd.Next(10000000, 99999999).ToString();
        }

        private string RemoverFormatacao(string? valor)
        {
            if (string.IsNullOrEmpty(valor))
                return string.Empty;
            
            return Regex.Replace(valor, @"[^\d]", "");
        }

        public async Task<bool> ValidarXmlAsync(string xml)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Validação básica - verifica se o XML está bem formado
                    XDocument.Parse(xml);
                    _logger.LogInformation("XML está bem formado");
                    
                    // TODO: Implementar validação XSD quando schemas estiverem disponíveis
                    // Por enquanto, apenas validação básica
                    
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao validar XML");
                    return false;
                }
            });
        }
    }
}

