# API NFe - Padrão MVC

API ASP.NET Core para recebimento e processamento de NFe (Nota Fiscal Eletrônica) seguindo o padrão MVC.

## Estrutura do Projeto

```
NFE/
├── Controllers/          # Controllers MVC
│   └── NFeController.cs
├── Models/              # ViewModels seguindo padrão MVC
│   ├── NFeViewModel.cs
│   └── NFeResponseViewModel.cs
├── Services/            # Serviços de negócio
│   ├── INFeService.cs
│   ├── NFeService.cs
│   ├── IWebServiceClient.cs
│   └── WebServiceClient.cs
├── Program.cs           # Configuração da aplicação
├── appsettings.json     # Configurações
└── NFE.csproj          # Arquivo do projeto
```

## Padrão MVC Implementado

### Models (ViewModels)
- `NFeViewModel`: Recebe dados da NFe
- `NFeResponseViewModel`: Retorna resposta da API

### Controllers
- `NFeController`: Gerencia requisições HTTP

### Services
- `NFeService`: Lógica de negócio para processamento de NFe
- `WebServiceClient`: Comunicação com webservices da SEFAZ

## Endpoints

### POST /api/nfe
Recebe dados de NFe, gera XML e envia para webservice.

**Request:**
```json
{
  "identificacao": {
    "codigoUF": "35",
    "naturezaOperacao": "VENDA",
    "modelo": "55",
    "serie": "1",
    "numeroNota": 1,
    "dataEmissao": "2024-01-15T10:00:00",
    "dataSaidaEntrada": "2024-01-15T10:00:00",
    "tipoOperacao": "1",
    "codigoMunicipioFatoGerador": "3550308",
    "tipoImpressao": "1",
    "tipoEmissao": "1",
    "ambiente": "2",
    "finalidade": "1",
    "indicadorConsumidorFinal": "0",
    "indicadorPresenca": "1"
  },
  "emitente": {
    "cnpj": "12345678000195",
    "razaoSocial": "EMPRESA EXEMPLO LTDA",
    "nomeFantasia": "EXEMPLO LTDA",
    "inscricaoEstadual": "123456789012",
    "codigoRegimeTributario": "1",
    "endereco": {
      "logradouro": "RUA DAS FLORES",
      "numero": "123",
      "bairro": "CENTRO",
      "codigoMunicipio": "3550308",
      "nomeMunicipio": "SAO PAULO",
      "uf": "SP",
      "cep": "01000000"
    }
  },
  "destinatario": {
    "tipo": "PJ",
    "documento": "11222333000181",
    "nomeRazaoSocial": "CLIENTE EXEMPLO LTDA",
    "indicadorIE": "1",
    "inscricaoEstadual": "987654321098",
    "endereco": {
      "logradouro": "AVENIDA PRINCIPAL",
      "numero": "456",
      "bairro": "JARDIM AMERICA",
      "codigoMunicipio": "3550308",
      "nomeMunicipio": "SAO PAULO",
      "uf": "SP",
      "cep": "02000000"
    }
  },
  "produtos": [
    {
      "codigo": "001",
      "ean": "7891234567890",
      "descricao": "PRODUTO EXEMPLO",
      "ncm": "84713012",
      "cfop": "5102",
      "unidadeComercial": "UN",
      "quantidadeComercial": 10.0,
      "valorUnitarioComercial": 100.0,
      "valorTotal": 1000.0,
      "indicadorTotal": "1"
    }
  ]
}
```

**Response:**
```json
{
  "sucesso": true,
  "mensagem": "NFe autorizada com sucesso",
  "xmlEnviado": "<?xml version=\"1.0\"...",
  "xmlRetorno": "<?xml version=\"1.0\"...",
  "protocolo": "123456789012345",
  "chaveAcesso": "35200112345678000195550010000000011234567890",
  "codigoStatus": 100,
  "motivo": "Autorizado o uso da NF-e",
  "dataProcessamento": "2024-01-15T10:00:00"
}
```

### POST /api/nfe/gerar-xml
Apenas gera XML sem enviar para webservice.

### POST /api/nfe/validar-xml
Valida um XML de NFe.

### GET /api/nfe/info
Retorna informações sobre a API.

## Como Executar

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

## Validações

A API inclui validações completas usando Data Annotations:
- Validação de campos obrigatórios
- Validação de formatos (CNPJ, CPF, CEP, etc.)
- Validação de valores (ranges, regex, etc.)
- Validação de estrutura (listas, objetos aninhados)

## Características do Padrão MVC

1. **Separação de Responsabilidades**
   - Models: Representam os dados
   - Controllers: Gerenciam requisições HTTP
   - Services: Contêm lógica de negócio

2. **ViewModels**
   - Dados de entrada e saída bem definidos
   - Validações usando Data Annotations

3. **Injeção de Dependência**
   - Services registrados no Program.cs
   - Controllers recebem dependências via construtor

4. **Padrão Repository/Service**
   - Interfaces para serviços
   - Implementações separadas

## Configuração

As URLs dos webservices são configuradas em `appsettings.json`:

```json
{
  "WebServices": {
    "NFe": {
      "SP": {
        "homologacao": {
          "Url": "https://homologacao.nfe.fazenda.sp.gov.br/ws/nfeautorizacao4.asmx"
        }
      }
    }
  }
}
```

## Exemplo de Uso

```bash
curl -X POST "https://localhost:7001/api/nfe?ambiente=homologacao" \
  -H "Content-Type: application/json" \
  -d @dados-nfe.json
```

