# API NFS-e - Nota Fiscal de Serviço Eletrônica

API ASP.NET Core para recebimento e processamento de NFS-e (Nota Fiscal de Serviço Eletrônica) seguindo o padrão MVC, compatível com o novo modelo nacional 2026.

## 📋 Sobre NFS-e 2026

A partir de 2026, a NFS-e será padronizada nacionalmente, substituindo os sistemas municipais existentes. As principais mudanças incluem:

- **IBS (Imposto sobre Bens e Serviços):** Substituirá o ISS e o ICMS
- **CBS (Contribuição sobre Bens e Serviços):** Unificará o PIS e a COFINS
- **Padrão Nacional:** Unificação dos sistemas municipais em um padrão único

## 🏗️ Estrutura do Projeto

```
NFE/
├── Controllers/
│   └── NFSeController.cs          # Controller para NFS-e
├── Models/
│   ├── NFSeViewModel.cs           # ViewModels de entrada
│   └── NFSeResponseViewModel.cs   # ViewModels de resposta
├── Services/
│   ├── INFSeService.cs           # Interface do serviço
│   ├── NFSeService.cs            # Implementação do serviço
│   ├── INFSeWebServiceClient.cs  # Interface do cliente webservice
│   └── NFSeWebServiceClient.cs   # Cliente webservice
└── Utils/
    └── SoapEnvelopeBuilderNFSe.cs # Builder de envelopes SOAP
```

## 🚀 Endpoints

### POST /api/nfse
Endpoint LEGADO - SEM certificado (modo simulação)

**Request:**
```json
{
  "identificacao": {
    "codigoUF": "35",
    "codigoMunicipio": "3550308",
    "naturezaOperacao": "PRESTAÇÃO DE SERVIÇOS",
    "regimeEspecialTributacao": "1",
    "optanteSimplesNacional": "2",
    "incentivadorCultural": "2",
    "numeroNFSe": 1,
    "dataEmissao": "2026-01-15T10:30:00-03:00",
    "ambiente": "2"
  },
  "prestador": {
    "cnpj": "12345678000195",
    "razaoSocial": "EMPRESA DE SOFTWARE LTDA",
    "inscricaoMunicipal": "12345678",
    "endereco": { ... }
  },
  "tomador": {
    "tipo": "PJ",
    "documento": "11222333000181",
    "nomeRazaoSocial": "CLIENTE EXEMPLO LTDA",
    "endereco": { ... }
  },
  "servicos": [
    {
      "codigo": "001",
      "descricao": "Desenvolvimento de Software",
      "codigoClassificacao": "0101-01",
      "codigoTributacaoMunicipal": "1401",
      "discriminacao": "Desenvolvimento de sistema...",
      "quantidade": 1.0,
      "valorUnitario": 5000.00,
      "valorTotal": 5000.00,
      "tributacao": {
        "situacaoTributaria": "00",
        "aliquota": 5.0,
        "valorISS": 250.00
      }
    }
  ]
}
```

**Response:**
```json
{
  "sucesso": true,
  "mensagem": "NFS-e autorizada com sucesso",
  "xmlEnviado": "<?xml version=\"1.0\"...",
  "xmlRetorno": "<?xml version=\"1.0\"...",
  "protocolo": "123456789012345",
  "numeroNFSe": "1",
  "codigoVerificacao": "12345678",
  "codigoStatus": "100",
  "motivo": "NFS-e autorizada",
  "linkConsulta": "https://nfse.gov.br/consulta/...",
  "dataProcessamento": "2026-01-15T10:00:00"
}
```

### POST /api/nfse/emitir
Endpoint NOVO - COM certificado digital (recomendado)

**Request:**
```json
{
  "dadosNFSe": { ... },
  "certificadoBase64": "BASE64_DO_CERTIFICADO",
  "senhaCertificado": "SENHA_DO_CERTIFICADO",
  "ambiente": "homologacao"
}
```

### POST /api/nfse/gerar-xml
Gera apenas o XML da NFS-e sem enviar

### POST /api/nfse/validar-xml
Valida um XML de NFS-e

### GET /api/nfse/status
Consulta status do serviço

## 📝 Campos Principais

### Identificação
- `codigoUF`: Código da UF (2 dígitos)
- `codigoMunicipio`: Código do município (7 dígitos - IBGE)
- `naturezaOperacao`: Natureza da operação (máx. 60 caracteres)
- `regimeEspecialTributacao`: 1=Microempresa, 2=Estimativa, 3=Sociedade, 4=Cooperativa, 5=MEI, 6=ME EPP
- `optanteSimplesNacional`: 1=Sim, 2=Não
- `numeroNFSe`: Número sequencial da NFS-e

### Prestador
- `cnpj` ou `cpf`: Documento do prestador
- `razaoSocial`: Razão social (máx. 150 caracteres)
- `inscricaoMunicipal`: Inscrição municipal (máx. 15 caracteres)

### Tomador
- `tipo`: PJ, PF ou Estrangeiro
- `documento`: CNPJ, CPF ou NIF
- `nomeRazaoSocial`: Nome/Razão social (máx. 150 caracteres)

### Serviço
- `codigoClassificacao`: Código de classificação (LC 116) - formato: XXXX-XX
- `codigoTributacaoMunicipal`: Código de tributação municipal
- `discriminacao`: Discriminação detalhada do serviço (máx. 2000 caracteres)
- `quantidade`: Quantidade do serviço
- `valorUnitario`: Valor unitário
- `valorTotal`: Valor total do serviço

### Tributação (ISS atual)
- `situacaoTributaria`: 00=Tributado, 01=Isento, etc.
- `aliquota`: Alíquota do ISS (%)
- `valorISS`: Valor do ISS

### IBS/CBS (Reforma Tributária 2026)
- `ibscbs`: Grupo de tributação IBS/CBS
- `ibscbstot`: Totais de IBS/CBS

## 🔐 Certificado Digital

Para emissão em produção, é necessário:
1. Certificado digital A1 ou A3 (e-CPF ou e-CNPJ)
2. Certificado válido e não expirado
3. Chave privada acessível

## ⚙️ Configuração

As URLs dos webservices são configuradas em `appsettings.json`:

```json
{
  "WebServices": {
    "NFSe": {
      "3550308": {
        "homologacao": {
          "Url": "https://homologacao.nfse.gov.br/ws/nfseautorizacao/nfseautorizacao.asmx"
        },
        "producao": {
          "Url": "https://nfse.gov.br/ws/nfseautorizacao/nfseautorizacao.asmx"
        }
      }
    }
  }
}
```

## 📦 Exemplo de Uso

Veja o arquivo `ExemploNFSe.json` para um exemplo completo de requisição.

## ✅ Validações

A API inclui validações completas usando Data Annotations:
- Validação de campos obrigatórios
- Validação de formatos (CNPJ, CPF, CEP, etc.)
- Validação de valores (ranges, regex, etc.)
- Validação de estrutura (listas, objetos aninhados)

## 🔄 Compatibilidade

- ✅ Modelo Nacional 2026
- ✅ Reforma Tributária (IBS/CBS)
- ✅ Imposto Seletivo (IS)
- ✅ Padrão XML oficial
- ✅ Assinatura digital
- ✅ Validação XSD (quando schemas estiverem disponíveis)

## 📚 Documentação Adicional

- [Relatório de Validação NFe 2026](../RELATORIO_VALIDACAO_NFE_2026.md)
- [Análise de Código NFe 2026](../ANALISE_CODIGO_NFE_2026.md)
- [Correções Aplicadas](../CORRECOES_APLICADAS.md)

## 🛠️ Como Executar

1. Restaurar dependências:
```bash
dotnet restore
```

2. Executar a aplicação:
```bash
dotnet run
```

3. Acessar Swagger:
- HTTP: http://localhost:5000/swagger
- HTTPS: https://localhost:7001/swagger

## 📝 Notas Importantes

- A NFS-e Nacional 2026 ainda está em fase de implementação
- URLs de webservice podem mudar conforme publicação oficial
- Schemas XSD serão disponibilizados quando o padrão for oficializado
- Ambiente de homologação será disponibilizado pela Receita Federal

## 🔗 Links Úteis

- Portal da Receita Federal: https://www.gov.br/receitafederal
- Portal NFS-e Nacional: https://nfse.gov.br (quando disponível)

