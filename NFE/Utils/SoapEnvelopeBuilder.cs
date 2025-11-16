namespace NFE.Utils
{
    public static class SoapEnvelopeBuilder
    {
        /// <summary>
        /// Cria envelope SOAP SEM formatação (SEFAZ não aceita \r\n)
        /// </summary>
        public static string CriarEnvelopeAutorizacao(string xmlNFeAssinado, string uf = "33", string versao = "4.00")
        {
            // Remover declaração XML se existir
            string nfeContent = xmlNFeAssinado;
            if (nfeContent.StartsWith("<?xml"))
            {
                int startIndex = nfeContent.IndexOf("<NFe");
                if (startIndex > 0)
                {
                    nfeContent = nfeContent.Substring(startIndex);
                }
            }

            // ✅ IMPORTANTE: Remover TODAS as quebras de linha e espaços desnecessários
            nfeContent = RemoverFormatacao(nfeContent);

            // ✅ Criar envelope SEM quebras de linha
            return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   $"<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:nfe=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4\">" +
                   $"<soap:Header>" +
                   $"<nfeCabecMsg xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4\">" +
                   $"<cUF>{uf}</cUF>" +
                   $"<versaoDados>{versao}</versaoDados>" +
                   $"</nfeCabecMsg>" +
                   $"</soap:Header>" +
                   $"<soap:Body>" +
                   $"<nfeDadosMsg xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeAutorizacao4\">" +
                   $"<enviNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"{versao}\">" +
                   $"<idLote>1</idLote>" +
                   $"<indSinc>1</indSinc>" +
                   $"{nfeContent}" +
                   $"</enviNFe>" +
                   $"</nfeDadosMsg>" +
                   $"</soap:Body>" +
                   $"</soap:Envelope>";
        }

        /// <summary>
        /// Remove formatação (quebras de linha e espaços entre tags)
        /// </summary>
        private static string RemoverFormatacao(string xml)
        {
            // Remover \r\n, \n, \r
            xml = xml.Replace("\r\n", "").Replace("\n", "").Replace("\r", "");
            
            // Remover espaços entre tags (> <)
            xml = System.Text.RegularExpressions.Regex.Replace(xml, @">\s+<", "><");
            
            // Remover espaços no início/fim
            xml = xml.Trim();

            return xml;
        }

        /// <summary>
        /// Cria envelope para consulta de protocolo
        /// </summary>
        public static string CriarEnvelopeConsultaProtocolo(string chaveAcesso, string uf = "33", string ambiente = "2", string versao = "4.00")
        {
            return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   $"<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:nfe=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4\">" +
                   $"<soap:Header>" +
                   $"<nfeCabecMsg xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4\">" +
                   $"<cUF>{uf}</cUF>" +
                   $"<versaoDados>{versao}</versaoDados>" +
                   $"</nfeCabecMsg>" +
                   $"</soap:Header>" +
                   $"<soap:Body>" +
                   $"<nfeDadosMsg xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeConsultaProtocolo4\">" +
                   $"<consSitNFe xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"{versao}\">" +
                   $"<tpAmb>{ambiente}</tpAmb>" +
                   $"<xServ>CONSULTAR</xServ>" +
                   $"<chNFe>{chaveAcesso}</chNFe>" +
                   $"</consSitNFe>" +
                   $"</nfeDadosMsg>" +
                   $"</soap:Body>" +
                   $"</soap:Envelope>";
        }

        /// <summary>
        /// Cria envelope para status do serviço
        /// </summary>
        public static string CriarEnvelopeStatusServico(string uf = "33", string ambiente = "2", string versao = "4.00")
        {
            return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   $"<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:nfe=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4\">" +
                   $"<soap:Header>" +
                   $"<nfeCabecMsg xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4\">" +
                   $"<cUF>{uf}</cUF>" +
                   $"<versaoDados>{versao}</versaoDados>" +
                   $"</nfeCabecMsg>" +
                   $"</soap:Header>" +
                   $"<soap:Body>" +
                   $"<nfeDadosMsg xmlns=\"http://www.portalfiscal.inf.br/nfe/wsdl/NFeStatusServico4\">" +
                   $"<consStatServ xmlns=\"http://www.portalfiscal.inf.br/nfe\" versao=\"{versao}\">" +
                   $"<tpAmb>{ambiente}</tpAmb>" +
                   $"<cUF>{uf}</cUF>" +
                   $"<xServ>STATUS</xServ>" +
                   $"</consStatServ>" +
                   $"</nfeDadosMsg>" +
                   $"</soap:Body>" +
                   $"</soap:Envelope>";
        }
    }
}
