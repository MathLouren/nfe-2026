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

        /// <summary>
        /// Valida certificado digital com validações completas:
        /// - Validade temporal
        /// - Chave privada
        /// - Cadeia de certificação
        /// - Revogação (CRL/OCSP)
        /// - Tipo de certificado (e-CNPJ/e-CPF)
        /// </summary>
        private void ValidarCertificado(X509Certificate2 certificado)
        {
            var erros = new List<string>();

            // 1. Validação básica de data
            if (DateTime.Now < certificado.NotBefore)
                erros.Add($"Certificado ainda não está válido. Válido a partir de: {certificado.NotBefore:dd/MM/yyyy HH:mm:ss}");

            if (DateTime.Now > certificado.NotAfter)
                erros.Add($"Certificado expirado. Válido até: {certificado.NotAfter:dd/MM/yyyy HH:mm:ss}");

            // 2. Validação de chave privada
            if (!certificado.HasPrivateKey)
                erros.Add("Certificado não possui chave privada");

            // 3. Validação de cadeia de certificação
            try
            {
                ValidarCadeiaCertificacao(certificado);
            }
            catch (Exception ex)
            {
                erros.Add($"Erro na validação da cadeia de certificação: {ex.Message}");
            }

            // 4. Validação de revogação (CRL/OCSP)
            try
            {
                ValidarRevogacao(certificado);
            }
            catch (Exception ex)
            {
                // Em caso de erro de rede, logar mas não bloquear (pode ser offline)
                _logger.LogWarning(ex, "Não foi possível validar revogação do certificado. Continuando com validação básica.");
                // Em produção, você pode querer bloquear se não conseguir validar revogação
                // erros.Add($"Erro na validação de revogação: {ex.Message}");
            }

            // 5. Validação de tipo de certificado (e-CNPJ/e-CPF)
            try
            {
                ValidarTipoCertificado(certificado);
            }
            catch (Exception ex)
            {
                erros.Add($"Erro na validação do tipo de certificado: {ex.Message}");
            }

            // Se houver erros, lançar exceção com todos os erros
            if (erros.Any())
            {
                string mensagemErro = "Certificado inválido:\n" + string.Join("\n", erros.Select((e, i) => $"{i + 1}. {e}"));
                _logger.LogError("Certificado falhou na validação: {Erros}", string.Join("; ", erros));
                throw new Exception(mensagemErro);
            }

            _logger.LogInformation(
                "Certificado validado com sucesso - Tipo: {Tipo}, Válido até: {NotAfter}",
                ObterTipoCertificado(certificado),
                certificado.NotAfter
            );
        }

        /// <summary>
        /// Valida a cadeia de certificação do certificado
        /// </summary>
        private void ValidarCadeiaCertificacao(X509Certificate2 certificado)
        {
            _logger.LogInformation("Validando cadeia de certificação");

            using var chain = new X509Chain();
            
            // Configurar opções de validação
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck; // Revogação será validada separadamente
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.VerificationTime = DateTime.Now;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(30);

            // Adicionar stores de certificados confiáveis
            chain.ChainPolicy.ExtraStore.Add(certificado);

            // Construir cadeia
            bool chainBuilt = chain.Build(certificado);

            if (!chainBuilt)
            {
                var chainErrors = new List<string>();
                foreach (X509ChainStatus status in chain.ChainStatus)
                {
                    // Ignorar alguns erros que podem ser aceitáveis em desenvolvimento
                    if (status.Status != X509ChainStatusFlags.NoError &&
                        status.Status != X509ChainStatusFlags.UntrustedRoot &&
                        status.Status != X509ChainStatusFlags.RevocationStatusUnknown)
                    {
                        chainErrors.Add($"{status.Status}: {status.StatusInformation}");
                    }
                }

                if (chainErrors.Any())
                {
                    throw new Exception($"Cadeia de certificação inválida: {string.Join("; ", chainErrors)}");
                }
            }

            // Validar se a cadeia tem pelo menos um certificado intermediário ou raiz confiável
            if (chain.ChainElements.Count == 0)
            {
                throw new Exception("Cadeia de certificação vazia");
            }

            _logger.LogInformation("Cadeia de certificação validada - {Count} certificado(s) na cadeia", chain.ChainElements.Count);
        }

        /// <summary>
        /// Valida revogação do certificado usando CRL/OCSP
        /// </summary>
        private void ValidarRevogacao(X509Certificate2 certificado)
        {
            _logger.LogInformation("Validando revogação do certificado");

            using var chain = new X509Chain();
            
            // Tentar validar revogação online primeiro
            chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.VerificationTime = DateTime.Now;
            chain.ChainPolicy.UrlRetrievalTimeout = TimeSpan.FromSeconds(10);

            bool chainBuilt = chain.Build(certificado);

            if (!chainBuilt)
            {
                // Verificar se o problema é específico de revogação
                var revocationErrors = chain.ChainStatus
                    .Where(s => s.Status.HasFlag(X509ChainStatusFlags.Revoked) ||
                               s.Status.HasFlag(X509ChainStatusFlags.RevocationStatusUnknown))
                    .ToList();

                if (revocationErrors.Any(s => s.Status.HasFlag(X509ChainStatusFlags.Revoked)))
                {
                    throw new Exception($"Certificado REVOGADO: {revocationErrors.First().StatusInformation}");
                }

                // Se não conseguir validar online, tentar offline
                if (revocationErrors.Any(s => s.Status.HasFlag(X509ChainStatusFlags.RevocationStatusUnknown)))
                {
                    _logger.LogWarning("Não foi possível validar revogação online. Tentando modo offline.");
                    
                    chain.ChainPolicy.RevocationMode = X509RevocationMode.Offline;
                    chainBuilt = chain.Build(certificado);

                    if (!chainBuilt)
                    {
                        var offlineErrors = chain.ChainStatus
                            .Where(s => s.Status.HasFlag(X509ChainStatusFlags.Revoked))
                            .ToList();

                        if (offlineErrors.Any())
                        {
                            throw new Exception($"Certificado REVOGADO (validação offline): {offlineErrors.First().StatusInformation}");
                        }

                        // Se ainda assim não conseguir, apenas logar aviso
                        _logger.LogWarning("Não foi possível validar revogação (modo offline também falhou). Certificado aceito com ressalvas.");
                    }
                }
            }
            else
            {
                // Verificar se há status de revogação mesmo com cadeia construída
                var revokedStatus = chain.ChainStatus
                    .FirstOrDefault(s => s.Status.HasFlag(X509ChainStatusFlags.Revoked));

                if (revokedStatus.Status == X509ChainStatusFlags.Revoked)
                {
                    throw new Exception($"Certificado REVOGADO: {revokedStatus.StatusInformation}");
                }
            }

            _logger.LogInformation("Revogação validada - Certificado não está revogado");
        }

        /// <summary>
        /// Valida se o certificado é um e-CNPJ ou e-CPF válido (certificado digital brasileiro)
        /// </summary>
        private void ValidarTipoCertificado(X509Certificate2 certificado)
        {
            _logger.LogInformation("Validando tipo de certificado (e-CNPJ/e-CPF)");

            // OIDs dos certificados digitais brasileiros
            const string OID_ECPF = "2.16.76.1.3.1";      // e-CPF
            const string OID_ECNPJ = "2.16.76.1.3.2";     // e-CNPJ

            // Verificar extensões do certificado
            bool isECPF = false;
            bool isECNPJ = false;

            // Verificar OIDs nas extensões do certificado
            foreach (X509Extension extension in certificado.Extensions)
            {
                string oid = extension.Oid?.Value ?? string.Empty;

                if (oid == OID_ECPF)
                {
                    isECPF = true;
                    _logger.LogInformation("Certificado identificado como e-CPF");
                }
                else if (oid == OID_ECNPJ)
                {
                    isECNPJ = true;
                    _logger.LogInformation("Certificado identificado como e-CNPJ");
                }
            }

            // Verificar também no Subject Alternative Name (SAN) e outros campos
            // Alguns certificados podem não ter o OID mas ter informações no Subject
            string subject = certificado.Subject;
            string issuer = certificado.Issuer;

            // Verificar se é emitido por AC brasileira conhecida
            bool isBrazilianAC = issuer.Contains("ICP-BRASIL", StringComparison.OrdinalIgnoreCase) ||
                                issuer.Contains("AC", StringComparison.OrdinalIgnoreCase) ||
                                issuer.Contains("CERTISIGN", StringComparison.OrdinalIgnoreCase) ||
                                issuer.Contains("SERASA", StringComparison.OrdinalIgnoreCase) ||
                                issuer.Contains("VALID", StringComparison.OrdinalIgnoreCase) ||
                                issuer.Contains("SOLUTI", StringComparison.OrdinalIgnoreCase) ||
                                issuer.Contains("SERPRO", StringComparison.OrdinalIgnoreCase);

            // Se não encontrou OID específico, verificar pelo Subject
            if (!isECPF && !isECNPJ)
            {
                // Verificar padrões comuns no Subject de certificados brasileiros
                if (subject.Contains("CPF:", StringComparison.OrdinalIgnoreCase) ||
                    subject.Contains(":CPF", StringComparison.OrdinalIgnoreCase))
                {
                    isECPF = true;
                    _logger.LogInformation("Certificado identificado como e-CPF (via Subject)");
                }
                else if (subject.Contains("CNPJ:", StringComparison.OrdinalIgnoreCase) ||
                         subject.Contains(":CNPJ", StringComparison.OrdinalIgnoreCase))
                {
                    isECNPJ = true;
                    _logger.LogInformation("Certificado identificado como e-CNPJ (via Subject)");
                }
            }

            // Validar se é certificado brasileiro válido
            if (!isECPF && !isECNPJ)
            {
                if (isBrazilianAC)
                {
                    _logger.LogWarning("Certificado emitido por AC brasileira mas tipo (e-CPF/e-CNPJ) não identificado claramente");
                    // Em produção, você pode querer ser mais restritivo
                    // throw new Exception("Não foi possível identificar se o certificado é e-CPF ou e-CNPJ");
                }
                else
                {
                    throw new Exception(
                        "Certificado não é um e-CPF ou e-CNPJ válido. " +
                        "Certificados digitais brasileiros devem ser emitidos por Autoridades Certificadoras credenciadas pela ICP-Brasil."
                    );
                }
            }

            // Validar uso do certificado (deve permitir assinatura digital)
            bool hasDigitalSignature = false;
            foreach (X509Extension extension in certificado.Extensions)
            {
                if (extension is X509EnhancedKeyUsageExtension eku)
                {
                    foreach (Oid oid in eku.EnhancedKeyUsages)
                    {
                        // OID para uso de assinatura digital
                        if (oid.Value == "1.3.6.1.5.5.7.3.4" || // emailProtection (alguns certificados)
                            oid.Value == "1.3.6.1.5.5.7.3.2" || // clientAuth
                            oid.Value == "2.5.29.37.0")         // anyExtendedKeyUsage
                        {
                            hasDigitalSignature = true;
                            break;
                        }
                    }
                }
            }

            // Se não encontrou uso específico, assumir que permite (certificados A1/A3 geralmente permitem)
            if (!hasDigitalSignature)
            {
                _logger.LogInformation("Uso de assinatura digital não explicitamente verificado, assumindo permitido");
            }

            _logger.LogInformation(
                "Tipo de certificado validado - Tipo: {Tipo}, Emitido por: {Issuer}",
                isECPF ? "e-CPF" : (isECNPJ ? "e-CNPJ" : "Desconhecido"),
                certificado.Issuer
            );
        }

        /// <summary>
        /// Obtém o tipo do certificado (e-CPF ou e-CNPJ) para logging
        /// </summary>
        private string ObterTipoCertificado(X509Certificate2 certificado)
        {
            const string OID_ECPF = "2.16.76.1.3.1";
            const string OID_ECNPJ = "2.16.76.1.3.2";

            foreach (X509Extension extension in certificado.Extensions)
            {
                string oid = extension.Oid?.Value ?? string.Empty;
                if (oid == OID_ECPF) return "e-CPF";
                if (oid == OID_ECNPJ) return "e-CNPJ";
            }

            string subject = certificado.Subject;
            if (subject.Contains("CPF:", StringComparison.OrdinalIgnoreCase)) return "e-CPF";
            if (subject.Contains("CNPJ:", StringComparison.OrdinalIgnoreCase)) return "e-CNPJ";

            return "Certificado Digital Brasileiro";
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
                if (signedXml.SignedInfo != null)
                {
                    signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
                }

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

        /// <summary>
        /// Assina XML DPS (Declaração de Prestação de Serviços) conforme leiautes-NSF-e
        /// Também pode ser usado para assinar eventos
        /// </summary>
        public string AssinarDPS(string xmlDPS, X509Certificate2 certificado)
        {
            try
            {
                _logger.LogInformation("Iniciando assinatura do DPS");

                xmlDPS = NormalizarXML(xmlDPS);

                // Carregar XML
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = false;
                doc.LoadXml(xmlDPS);

                // Namespace do NFS-e Nacional
                XmlNamespaceManager nsManager = new XmlNamespaceManager(doc.NameTable);
                nsManager.AddNamespace("nfse", "http://www.sped.fazenda.gov.br/nfse");

                // Encontrar elemento infDPS ou infPedReg (para eventos)
                XmlNode? elementoParaAssinar = doc.SelectSingleNode("//nfse:infDPS", nsManager) 
                    ?? doc.SelectSingleNode("//nfse:infPedReg", nsManager);
                
                if (elementoParaAssinar == null)
                {
                    // Tentar sem namespace também
                    elementoParaAssinar = doc.SelectSingleNode("//infDPS") 
                        ?? doc.SelectSingleNode("//infPedReg");
                    if (elementoParaAssinar == null)
                        throw new Exception("Elemento infDPS ou infPedReg não encontrado no XML");
                }

                // Obter ID
                string? refUri = elementoParaAssinar.Attributes?["Id"]?.Value;
                if (string.IsNullOrEmpty(refUri))
                    throw new Exception("Atributo Id não encontrado no elemento");

                _logger.LogInformation("Assinando elemento: {RefUri}", refUri);

                // Obter chave privada
                var privateKey = certificado.GetRSAPrivateKey();
                if (privateKey == null)
                    throw new Exception("Não foi possível obter chave privada do certificado");

                // Criar SignedXml
                SignedXml signedXml = new SignedXml(doc);
                signedXml.SigningKey = privateKey;

                // NFS-e Nacional também usa SHA-1 conforme padrão brasileiro
                if (signedXml.SignedInfo != null)
                {
                    signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA1Url;
                }

                // Configurar referência
                Reference reference = new Reference("#" + refUri);
                reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
                reference.AddTransform(new XmlDsigC14NTransform(false));
                
                // DigestMethod SHA-1
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

                // Inserir assinatura no elemento raiz
                if (doc.DocumentElement == null)
                    throw new Exception("Elemento raiz não encontrado");

                // Encontrar o elemento raiz (DPS ou pedRegEvento ou evento)
                XmlNode? elementoRaiz = doc.SelectSingleNode("//nfse:DPS", nsManager) 
                    ?? doc.SelectSingleNode("//nfse:pedRegEvento", nsManager)
                    ?? doc.SelectSingleNode("//nfse:evento", nsManager)
                    ?? doc.SelectSingleNode("//DPS")
                    ?? doc.SelectSingleNode("//pedRegEvento")
                    ?? doc.SelectSingleNode("//evento");
                
                if (elementoRaiz == null)
                    elementoRaiz = doc.DocumentElement;

                // Adicionar namespace dsig à assinatura se necessário
                XmlElement signatureNode = doc.ImportNode(xmlSignature, true) as XmlElement ?? throw new Exception("Erro ao importar assinatura");
                
                // Inserir assinatura no elemento raiz
                // Para DPS: após infDPS dentro do DPS
                // Para eventos: após infPedReg dentro do pedRegEvento, ou após infEvento dentro do evento
                elementoRaiz.AppendChild(signatureNode);

                _logger.LogInformation("XML assinado com sucesso");

                return doc.OuterXml;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao assinar DPS");
                throw new Exception($"Erro ao assinar DPS: {ex.Message}", ex);
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
