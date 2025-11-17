using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;
using System.Xml.Linq;


namespace NFE.Services
{
    public class AssinaturaDigital
    {
        private readonly ILogger<AssinaturaDigital> _logger;

        public AssinaturaDigital(ILogger<AssinaturaDigital> logger)
        {
            _logger = logger;
        }

        public X509Certificate2 CarregarCertificadoBase64(string certificadoBase64, string senha)
        {
            try
            {
                _logger.LogInformation("Carregando certificado digital");

                byte[] certBytes = Convert.FromBase64String(certificadoBase64);
                
                #pragma warning disable SYSLIB0057
                var certificado = new X509Certificate2(
                    certBytes,
                    senha,
                    X509KeyStorageFlags.MachineKeySet | 
                    X509KeyStorageFlags.PersistKeySet | 
                    X509KeyStorageFlags.Exportable
                );
                #pragma warning restore SYSLIB0057

                ValidarCertificado(certificado);

                _logger.LogInformation(
                    "Certificado carregado - Válido até: {NotAfter}",
                    certificado.NotAfter
                );

                return certificado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao carregar certificado");
                throw new Exception($"Erro ao carregar certificado: {ex.Message}", ex);
            }
        }

        private void ValidarCertificado(X509Certificate2 certificado)
        {
            if (DateTime.Now < certificado.NotBefore)
                throw new Exception($"Certificado ainda não está válido. Válido a partir de: {certificado.NotBefore}");

            if (DateTime.Now > certificado.NotAfter)
                throw new Exception($"Certificado expirado. Válido até: {certificado.NotAfter}");

            if (!certificado.HasPrivateKey)
                throw new Exception("Certificado não possui chave privada");

            _logger.LogInformation("Certificado validado com sucesso");
        }

        public string AssinarXml(string xml, X509Certificate2 certificado)
        {
            try
            {
                _logger.LogInformation("Iniciando assinatura do XML");

                xml = NormalizarXML(xml);

                // Carregar XML
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = false;
                doc.LoadXml(xml);

                // Namespace da NFe
                XmlNamespaceManager nsManager = new XmlNamespaceManager(doc.NameTable);
                nsManager.AddNamespace("nfe", "http://www.portalfiscal.inf.br/nfe");

                // Encontrar elemento infNFe
                XmlNode? infNFeNode = doc.SelectSingleNode("//nfe:infNFe", nsManager);
                
                if (infNFeNode == null)
                    throw new Exception("Elemento infNFe não encontrado no XML");

                // Obter ID
                string? refUri = infNFeNode.Attributes?["Id"]?.Value;
                if (string.IsNullOrEmpty(refUri))
                    throw new Exception("Atributo Id não encontrado no elemento infNFe");

                _logger.LogInformation("Assinando elemento: {RefUri}", refUri);

                // Obter chave privada
                var privateKey = certificado.GetRSAPrivateKey();
                if (privateKey == null)
                    throw new Exception("Não foi possível obter chave privada do certificado");

                // Criar SignedXml
                SignedXml signedXml = new SignedXml(doc);
                signedXml.SigningKey = privateKey;

                // ✅ ALTERAÇÃO CRÍTICA: NFe 4.00 exige SHA-1, não SHA-256
                signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;

                // Configurar referência
                Reference reference = new Reference("#" + refUri);
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                reference.AddTransform(new XmlDsigC14NTransform(false));
                
                // ✅ ALTERAÇÃO CRÍTICA: DigestMethod também deve ser SHA-1
                reference.DigestMethod = SignedXml.XmlDsigSHA1Url;
                
                signedXml.AddReference(reference);

                // Adicionar informações do certificado
                KeyInfo keyInfo = new KeyInfo();
                keyInfo.AddClause(new KeyInfoX509Data(certificado));
                signedXml.KeyInfo = keyInfo;

                // Computar assinatura
                signedXml.ComputeSignature();

                // Obter elemento de assinatura
                XmlElement xmlSignature = signedXml.GetXml();

                // Inserir assinatura após infNFe
                if (doc.DocumentElement == null)
                    throw new Exception("Elemento raiz não encontrado");

                doc.DocumentElement.AppendChild(doc.ImportNode(xmlSignature, true));

                _logger.LogInformation("XML assinado com sucesso");

                return doc.OuterXml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao assinar XML");
                throw new Exception($"Erro ao assinar XML: {ex.Message}", ex);
            }
        }

        private string NormalizarXML(string xml)
        {
            try
            {
                // Parse mantendo estrutura e namespaces
                var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
                
                // Normalizar APENAS o conteúdo de texto dos elementos
                foreach (var element in doc.Descendants())
                {
                    // Verificar se o elemento tem APENAS texto (não tem elementos filhos)
                    if (element.HasElements == false && !string.IsNullOrWhiteSpace(element.Value))
                    {
                        // Remove espaços no início e fim
                        string normalizado = element.Value.Trim();
                        
                        // Remove quebras de linha e múltiplos espaços
                        normalizado = System.Text.RegularExpressions.Regex.Replace(
                            normalizado, 
                            @"\s+", 
                            " "
                        ).Trim();
                        
                        // Substitui caracteres especiais problemáticos
                        normalizado = normalizado
                            .Replace("ª", "a")
                            .Replace("º", "o")
                            .Replace("²", "2")
                            .Replace("³", "3")
                            .Replace("¹", "1");
                        
                        // Atualizar o valor
                        element.Value = normalizado;
                    }
                }
                
                // Retorna XML sem formatação, MAS preservando namespaces
                return doc.ToString(SaveOptions.DisableFormatting);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Erro ao normalizar XML, retornando original");
                return xml;
            }
        }

        
    }
}
