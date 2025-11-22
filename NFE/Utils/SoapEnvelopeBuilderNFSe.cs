namespace NFE.Utils
{
    /// <summary>
    /// Builder de envelopes SOAP para NFS-e
    /// </summary>
    public static class SoapEnvelopeBuilderNFSe
    {
        /// <summary>
        /// Cria envelope SOAP SEM formatação para NFS-e
        /// </summary>
        public static string CriarEnvelopeAutorizacao(string xmlNFSeAssinado, string codigoMunicipio = "3550308", string versao = "1.00")
        {
            // Remover declaração XML se existir
            string nfseContent = xmlNFSeAssinado;
            if (nfseContent.StartsWith("<?xml"))
            {
                int startIndex = nfseContent.IndexOf("<NFSe");
                if (startIndex > 0)
                {
                    nfseContent = nfseContent.Substring(startIndex);
                }
            }

            // Remover TODAS as quebras de linha e espaços desnecessários
            nfseContent = RemoverFormatacao(nfseContent);

            // Criar envelope SEM quebras de linha
            return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   $"<soap:Envelope xmlns:soap=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:nfse=\"http://www.portalfiscal.inf.br/nfse/wsdl/NFSeAutorizacao\">" +
                   $"<soap:Header>" +
                   $"<nfseCabecMsg xmlns=\"http://www.portalfiscal.inf.br/nfse/wsdl/NFSeAutorizacao\">" +
                   $"<cMun>{codigoMunicipio}</cMun>" +
                   $"<versaoDados>{versao}</versaoDados>" +
                   $"</nfseCabecMsg>" +
                   $"</soap:Header>" +
                   $"<soap:Body>" +
                   $"<nfseDadosMsg xmlns=\"http://www.portalfiscal.inf.br/nfse/wsdl/NFSeAutorizacao\">" +
                   $"<enviNFSe xmlns=\"http://www.portalfiscal.inf.br/nfse\" versao=\"{versao}\">" +
                   $"<idLote>1</idLote>" +
                   $"<indSinc>1</indSinc>" +
                   $"{nfseContent}" +
                   $"</enviNFSe>" +
                   $"</nfseDadosMsg>" +
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
    }
}

