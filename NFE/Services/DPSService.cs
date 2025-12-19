using NFE.Models;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NFE.Services
{
    /// <summary>
    /// Serviço para gerar XML DPS (Declaração de Prestação de Serviços) conforme leiautes-NSF-e
    /// </summary>
    public class DPSService
    {
        private readonly ILogger<DPSService> _logger;
        
        // Namespace padrão NFS-e Nacional conforme leiautes
        private static readonly XNamespace NsNFSe = "http://www.sped.fazenda.gov.br/nfse";
        private static readonly XNamespace NsDsig = "http://www.w3.org/2000/09/xmldsig#";
        private static readonly CultureInfo InvariantCulture = CultureInfo.InvariantCulture;

        public DPSService(ILogger<DPSService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Gera XML DPS a partir do modelo JSON
        /// </summary>
        public string GerarDPS(NFSeViewModel model)
        {
            try
            {
                _logger.LogInformation("Iniciando geração de DPS - Número: {Numero}", 
                    model.Identificacao.NumeroNFSe);

                // Criar elemento raiz DPS
                var dps = new XElement(NsNFSe + "DPS",
                    new XAttribute("versao", "1.00")
                );

                // Criar infDPS
                var infDPS = new XElement(NsNFSe + "infDPS",
                    new XAttribute("Id", $"DPS{model.Identificacao.NumeroNFSe:D15}")
                );

                // 1. Ambiente
                infDPS.Add(new XElement(NsNFSe + "tpAmb", model.Identificacao.Ambiente));

                // 2. Data/Hora de emissão (formato UTC)
                infDPS.Add(new XElement(NsNFSe + "dhEmi", FormatarDataHoraUTC(model.Identificacao.DataEmissao)));

                // 3. Versão do aplicativo
                infDPS.Add(new XElement(NsNFSe + "verAplic", "1.0.0"));

                // 4. Série do DPS (geralmente "1" ou código do equipamento)
                infDPS.Add(new XElement(NsNFSe + "serie", "1"));

                // 5. Número do DPS
                infDPS.Add(new XElement(NsNFSe + "nDPS", model.Identificacao.NumeroNFSe.ToString("D15")));

                // 6. Data de competência (data de início da prestação)
                infDPS.Add(new XElement(NsNFSe + "dCompet", 
                    model.Identificacao.DataEmissao.ToString("yyyyMMdd")));

                // 7. Tipo de emitente (1=Prestador, 2=Tomador, 3=Intermediário)
                infDPS.Add(new XElement(NsNFSe + "tpEmit", "1"));

                // 8. Código do município emissor
                infDPS.Add(new XElement(NsNFSe + "cLocEmi", model.Identificacao.CodigoMunicipio));

                // 9. Prestador
                infDPS.Add(CriarPrestador(model.Prestador));

                // 10. Tomador (se informado)
                if (model.Tomador != null)
                {
                    infDPS.Add(CriarTomador(model.Tomador));
                }

                // 11. Serviço
                if (model.Servicos != null && model.Servicos.Any())
                {
                    infDPS.Add(CriarServico(model.Servicos.First(), model.Identificacao.CodigoMunicipio));
                }

                // 12. Valores
                infDPS.Add(CriarValores(model));

                dps.Add(infDPS);

                // Criar documento XML
                var xmlDocument = new XDocument(
                    new XDeclaration("1.0", "UTF-8", null),
                    dps
                );

                string xmlString = xmlDocument.ToString(SaveOptions.DisableFormatting);
                
                _logger.LogInformation("DPS gerado com sucesso - Tamanho: {Size} bytes", xmlString.Length);
                
                return xmlString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar DPS");
                throw;
            }
        }

        private XElement CriarPrestador(PrestadorViewModel prestador)
        {
            var prest = new XElement(NsNFSe + "prest");

            // CNPJ ou CPF
            if (!string.IsNullOrEmpty(prestador.CNPJ))
            {
                prest.Add(new XElement(NsNFSe + "CNPJ", RemoverFormatacao(prestador.CNPJ)));
            }
            else if (!string.IsNullOrEmpty(prestador.CPF))
            {
                prest.Add(new XElement(NsNFSe + "CPF", RemoverFormatacao(prestador.CPF)));
            }

            // CAEPF (opcional)
            // IM (Inscrição Municipal)
            if (!string.IsNullOrEmpty(prestador.InscricaoMunicipal))
            {
                prest.Add(new XElement(NsNFSe + "IM", prestador.InscricaoMunicipal));
            }

            // Nome/Razão Social
            if (!string.IsNullOrEmpty(prestador.RazaoSocial))
            {
                prest.Add(new XElement(NsNFSe + "xNome", prestador.RazaoSocial));
            }

            // Endereço
            if (prestador.Endereco != null)
            {
                prest.Add(CriarEndereco(prestador.Endereco));
            }

            // Telefone
            if (!string.IsNullOrEmpty(prestador.Telefone))
            {
                prest.Add(new XElement(NsNFSe + "fone", RemoverFormatacao(prestador.Telefone)));
            }

            // Email
            if (!string.IsNullOrEmpty(prestador.Email))
            {
                prest.Add(new XElement(NsNFSe + "email", prestador.Email));
            }

            // Regime Tributário
            var regTrib = new XElement(NsNFSe + "regTrib");
            
            // Optante Simples Nacional
            regTrib.Add(new XElement(NsNFSe + "opSimpNac", 
                prestador.InscricaoMunicipal != null ? "3" : "1")); // 1=Não, 2=MEI, 3=ME/EPP
            
            // Regime Especial de Tributação
            regTrib.Add(new XElement(NsNFSe + "regEspTrib", "0")); // 0=Nenhum
            
            prest.Add(regTrib);

            return prest;
        }

        private XElement CriarTomador(TomadorViewModel tomador)
        {
            var toma = new XElement(NsNFSe + "toma");

            // CNPJ, CPF ou NIF
            if (tomador.Tipo == "PJ" && !string.IsNullOrEmpty(tomador.Documento))
            {
                toma.Add(new XElement(NsNFSe + "CNPJ", RemoverFormatacao(tomador.Documento)));
            }
            else if (tomador.Tipo == "PF" && !string.IsNullOrEmpty(tomador.Documento))
            {
                toma.Add(new XElement(NsNFSe + "CPF", RemoverFormatacao(tomador.Documento)));
            }
            else if (tomador.Tipo == "Estrangeiro" && !string.IsNullOrEmpty(tomador.Documento))
            {
                toma.Add(new XElement(NsNFSe + "NIF", tomador.Documento));
            }

            // Inscrição Municipal
            if (!string.IsNullOrEmpty(tomador.InscricaoMunicipal))
            {
                toma.Add(new XElement(NsNFSe + "IM", tomador.InscricaoMunicipal));
            }

            // Nome/Razão Social
            toma.Add(new XElement(NsNFSe + "xNome", tomador.NomeRazaoSocial));

            // Endereço
            if (tomador.Endereco != null)
            {
                toma.Add(CriarEndereco(tomador.Endereco));
            }

            // Telefone
            if (!string.IsNullOrEmpty(tomador.Telefone))
            {
                toma.Add(new XElement(NsNFSe + "fone", RemoverFormatacao(tomador.Telefone)));
            }

            // Email
            if (!string.IsNullOrEmpty(tomador.Email))
            {
                toma.Add(new XElement(NsNFSe + "email", tomador.Email));
            }

            return toma;
        }

        private XElement CriarEndereco(EnderecoNFSeViewModel endereco)
        {
            var end = new XElement(NsNFSe + "end");

            // Endereço Nacional
            var endNac = new XElement(NsNFSe + "endNac");
            endNac.Add(new XElement(NsNFSe + "cMun", endereco.CodigoMunicipio));
            endNac.Add(new XElement(NsNFSe + "CEP", RemoverFormatacao(endereco.CEP)));
            end.Add(endNac);

            // Logradouro
            end.Add(new XElement(NsNFSe + "xLgr", endereco.Logradouro));

            // Número
            end.Add(new XElement(NsNFSe + "nro", endereco.Numero));

            // Complemento (opcional)
            if (!string.IsNullOrEmpty(endereco.Complemento))
            {
                end.Add(new XElement(NsNFSe + "xCpl", endereco.Complemento));
            }

            // Bairro
            end.Add(new XElement(NsNFSe + "xBairro", endereco.Bairro));

            return end;
        }

        private XElement CriarServico(ServicoViewModel servico, string codigoMunicipio)
        {
            var serv = new XElement(NsNFSe + "serv");

            // Local de Prestação
            var locPrest = new XElement(NsNFSe + "locPrest");
            locPrest.Add(new XElement(NsNFSe + "cLocPrestacao", 
                servico.CodigoMunicipioPrestacao ?? codigoMunicipio));
            serv.Add(locPrest);

            // Código do Serviço
            var cServ = new XElement(NsNFSe + "cServ");
            
            // Código de tributação nacional (extrair do código de classificação)
            string codTribNac = ExtrairCodigoTributacaoNacional(servico.CodigoClassificacao);
            cServ.Add(new XElement(NsNFSe + "cTribNac", codTribNac));

            // Código de tributação municipal (opcional)
            if (!string.IsNullOrEmpty(servico.CodigoTributacaoMunicipal))
            {
                cServ.Add(new XElement(NsNFSe + "cTribMun", servico.CodigoTributacaoMunicipal));
            }

            // Descrição do serviço
            cServ.Add(new XElement(NsNFSe + "xDescServ", servico.Discriminacao));

            serv.Add(cServ);

            // Informações Complementares
            if (!string.IsNullOrEmpty(servico.Discriminacao))
            {
                var infoCompl = new XElement(NsNFSe + "infoCompl");
                infoCompl.Add(new XElement(NsNFSe + "xInfComp", servico.Discriminacao));
                serv.Add(infoCompl);
            }

            return serv;
        }

        private XElement CriarValores(NFSeViewModel model)
        {
            var valores = new XElement(NsNFSe + "valores");

            // Valores do Serviço Prestado
            var vServPrest = new XElement(NsNFSe + "vServPrest");
            
            decimal valorServicos = model.Servicos?.Sum(s => s.ValorTotal) ?? 0;
            vServPrest.Add(new XElement(NsNFSe + "vServ", FormatarValor(valorServicos)));
            
            valores.Add(vServPrest);

            // Descontos Condicionados e Incondicionados
            decimal vDescIncond = model.Servicos?.Sum(s => s.ValorDescontoIncondicionado) ?? 0;
            decimal vDescCond = model.Servicos?.Sum(s => s.ValorDescontoCondicionado) ?? 0;

            if (vDescIncond > 0 || vDescCond > 0)
            {
                var vDescCondIncond = new XElement(NsNFSe + "vDescCondIncond");
                if (vDescIncond > 0)
                {
                    vDescCondIncond.Add(new XElement(NsNFSe + "vDescIncond", FormatarValor(vDescIncond)));
                }
                if (vDescCond > 0)
                {
                    vDescCondIncond.Add(new XElement(NsNFSe + "vDescCond", FormatarValor(vDescCond)));
                }
                valores.Add(vDescCondIncond);
            }

            // Dedução/Redução (se houver)
            decimal vDeducoes = model.Servicos?.Sum(s => s.ValorDeducoes) ?? 0;
            if (vDeducoes > 0)
            {
                var vDedRed = new XElement(NsNFSe + "vDedRed");
                vDedRed.Add(new XElement(NsNFSe + "vDR", FormatarValor(vDeducoes)));
                valores.Add(vDedRed);
            }

            // Tributação
            var trib = new XElement(NsNFSe + "trib");

            // Tributação Municipal (ISSQN)
            var tribMun = new XElement(NsNFSe + "tribMun");
            
            var servico = model.Servicos?.FirstOrDefault();
            if (servico?.Tributacao != null)
            {
                var tribISSQN = servico.Tributacao.SituacaoTributaria switch
                {
                    "00" => "1", // Operação tributável
                    "01" => "2", // Imunidade
                    "02" => "3", // Exportação de serviço
                    "03" => "4", // Não Incidência
                    _ => "1"
                };
                
                tribMun.Add(new XElement(NsNFSe + "tribISSQN", tribISSQN));

                // Alíquota (se informada)
                if (servico.Tributacao.Aliquota.HasValue)
                {
                    tribMun.Add(new XElement(NsNFSe + "pAliq", 
                        FormatarValor(servico.Tributacao.Aliquota.Value, 4)));
                }

                // Tipo de retenção (1=Não Retido, 2=Retido pelo Tomador, 3=Retido pelo Intermediário)
                tribMun.Add(new XElement(NsNFSe + "tpRetISSQN", "1"));
            }
            else
            {
                // Valores padrão
                tribMun.Add(new XElement(NsNFSe + "tribISSQN", "1"));
                tribMun.Add(new XElement(NsNFSe + "tpRetISSQN", "1"));
            }

            trib.Add(tribMun);

            // Tributação Federal (PIS/COFINS, etc.)
            if (servico?.Tributacao != null && 
                (servico.Tributacao.ValorPIS.HasValue || servico.Tributacao.ValorCOFINS.HasValue))
            {
                var tribFed = new XElement(NsNFSe + "tribFed");
                
                var piscofins = new XElement(NsNFSe + "piscofins");
                piscofins.Add(new XElement(NsNFSe + "CST", "01")); // Operação Tributável com Alíquota Básica
                
                if (servico.Tributacao.ValorPIS.HasValue)
                {
                    piscofins.Add(new XElement(NsNFSe + "vPis", 
                        FormatarValor(servico.Tributacao.ValorPIS.Value)));
                }
                
                if (servico.Tributacao.ValorCOFINS.HasValue)
                {
                    piscofins.Add(new XElement(NsNFSe + "vCofins", 
                        FormatarValor(servico.Tributacao.ValorCOFINS.Value)));
                }
                
                tribFed.Add(piscofins);
                trib.Add(tribFed);
            }

            // Totais de Tributos
            var totTrib = new XElement(NsNFSe + "totTrib");
            var vTotTrib = new XElement(NsNFSe + "vTotTrib");
            
            decimal vTotTribFed = servico?.Tributacao?.ValorPIS ?? 0 + 
                                 servico?.Tributacao?.ValorCOFINS ?? 0;
            decimal vTotTribEst = 0;
            decimal vTotTribMun = servico?.Tributacao?.ValorISS ?? 0;
            
            vTotTrib.Add(new XElement(NsNFSe + "vTotTribFed", FormatarValor(vTotTribFed)));
            vTotTrib.Add(new XElement(NsNFSe + "vTotTribEst", FormatarValor(vTotTribEst)));
            vTotTrib.Add(new XElement(NsNFSe + "vTotTribMun", FormatarValor(vTotTribMun)));
            
            totTrib.Add(vTotTrib);
            trib.Add(totTrib);

            valores.Add(trib);

            // Valor Líquido
            decimal vLiq = valorServicos - vDescIncond - vDescCond - vDeducoes - vTotTribMun;
            valores.Add(new XElement(NsNFSe + "vLiq", FormatarValor(vLiq)));

            return valores;
        }

        private string ExtrairCodigoTributacaoNacional(string codigoClassificacao)
        {
            // Código de classificação vem no formato XXXX-XX (ex: 0101-01)
            // Código tributação nacional são 6 dígitos: 2 para Item + 2 para Subitem + 2 para Desdobro
            if (string.IsNullOrEmpty(codigoClassificacao))
                return "010101"; // Padrão

            // Remover hífen e garantir 6 dígitos
            string codigo = codigoClassificacao.Replace("-", "");
            if (codigo.Length >= 6)
            {
                return codigo.Substring(0, 6).PadRight(6, '0');
            }
            
            return codigo.PadRight(6, '0');
        }

        private string FormatarValor(decimal valor, int casasDecimais = 2)
        {
            return valor.ToString($"F{casasDecimais}", InvariantCulture);
        }

        private string FormatarDataHoraUTC(DateTime data)
        {
            var offset = TimeZoneInfo.Local.GetUtcOffset(data);
            var offsetString = $"{(offset.Hours >= 0 ? "+" : "-")}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
            return data.ToString("yyyy-MM-ddTHH:mm:ss", InvariantCulture) + offsetString;
        }

        private string RemoverFormatacao(string? valor)
        {
            if (string.IsNullOrEmpty(valor))
                return string.Empty;
            
            return Regex.Replace(valor, @"[^\d]", "");
        }
    }
}
