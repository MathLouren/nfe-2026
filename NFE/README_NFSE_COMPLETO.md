# API NFS-e - Sistema Nacional 2026 - Implementação Completa

## 📋 Visão Geral

Sistema completo para emissão, consulta, cancelamento e substituição de Notas Fiscais de Serviço Eletrônicas (NFS-e) conforme o padrão nacional 2026 e leiautes-NSF-e.

## ✅ Funcionalidades Implementadas

### 1. ✅ Emissão de NFS-e
- **Endpoint:** `POST /api/nfse/emitir`
- Recebe dados em JSON
- Gera XML DPS conforme `DPS_v1.00.xsd`
- Assina digitalmente com certificado A1/A3
- Envia para Sistema Nacional NFS-e
- Modo simulação automático quando Sistema Nacional não está disponível

### 2. ✅ Consulta de NFS-e
- **Endpoint:** `GET /api/nfse/consulta/{chaveAcesso}`
- Consulta NFS-e pela chave de acesso (50 caracteres)
- Retorna dados da NFS-e
- Modo simulação quando Sistema Nacional não está disponível

### 3. ✅ Cancelamento de NFS-e
- **Endpoint:** `POST /api/nfse/cancelar`
- Cancela NFS-e através de evento
- Tipos de cancelamento:
  - `101101` - Cancelamento simples
  - `105102` - Cancelamento por substituição
- Gera XML de evento conforme `evento_v1.00.xsd`
- Assina e envia para Sistema Nacional

### 4. ✅ Substituição de NFS-e
- **Endpoint:** `POST /api/nfse/substituir`
- Emite nova NFS-e
- Cancela NFS-e original por substituição
- Tudo em uma única operação

### 5. ✅ Validação XSD
- **Endpoint:** `POST /api/nfse/validar-xsd`
- Valida XML contra schemas XSD
- Tipos suportados: DPS e Evento
- Retorna lista de erros detalhados

### 6. ✅ Geração de XML
- **Endpoint:** `POST /api/nfse/gerar-xml`
- Gera apenas o XML DPS sem enviar
- Útil para testes e validação

### 7. ✅ Validação Básica
- **Endpoint:** `POST /api/nfse/validar-xml`
- Valida estrutura básica do XML
- Verifica se está bem formado

### 8. ✅ Status do Serviço
- **Endpoint:** `GET /api/nfse/status`
- Retorna status da API
- Lista funcionalidades disponíveis

## 🏗️ Arquitetura

```
NFE/
├── Controllers/
│   └── NFSeController.cs          # Endpoints da API
├── Models/
│   ├── NFSeViewModel.cs            # ViewModels de entrada
│   ├── NFSeResponseViewModel.cs    # ViewModels de resposta
│   └── NFSeEventoViewModel.cs      # ViewModels de eventos
├── Services/
│   ├── DPSService.cs               # Geração de XML DPS
│   ├── EventoNFSeService.cs        # Geração de XML de eventos
│   ├── SistemaNacionalNFSeClient.cs # Cliente REST Sistema Nacional
│   ├── ValidadorXSDService.cs      # Validação XSD
│   ├── NFSeService.cs              # Serviço principal
│   └── AssinaturaDigital.cs        # Assinatura digital (DPS e Eventos)
└── leiautes-NSF-e/                 # Schemas XSD oficiais
```

## 📡 Endpoints da API

### Emissão

#### POST /api/nfse/emitir
Emite NFS-e com certificado digital.

**Request:**
```json
{
  "dadosNFSe": {
    "identificacao": { ... },
    "prestador": { ... },
    "tomador": { ... },
    "servicos": [ ... ]
  },
  "certificadoBase64": "BASE64...",
  "senhaCertificado": "senha",
  "ambiente": "homologacao"
}
```

**Response:**
```json
{
  "sucesso": true,
  "mensagem": "DPS processada com sucesso",
  "numeroNFSe": "35503080000000000000000000000000000000000001",
  "codigoVerificacao": "12345678",
  "protocolo": "20251218123456",
  "xmlEnviado": "...",
  "xmlRetorno": "..."
}
```

### Consulta

#### GET /api/nfse/consulta/{chaveAcesso}
Consulta NFS-e pela chave de acesso.

**Response:**
```json
{
  "sucesso": true,
  "mensagem": "NFS-e consultada com sucesso",
  "numeroNFSe": "...",
  "codigoVerificacao": "...",
  "xmlRetorno": "..."
}
```

### Cancelamento

#### POST /api/nfse/cancelar
Cancela uma NFS-e.

**Request:**
```json
{
  "evento": {
    "chaveAcesso": "35503080000000000000000000000000000000000001",
    "tipoEvento": "101101",
    "codigoJustificativa": "01",
    "motivo": "Erro na emissão",
    "documentoAutor": "59282800000195"
  },
  "certificadoBase64": "BASE64...",
  "senhaCertificado": "senha",
  "ambiente": "homologacao"
}
```

### Substituição

#### POST /api/nfse/substituir
Substitui uma NFS-e por outra.

**Request:**
```json
{
  "chaveSubstituida": "35503080000000000000000000000000000000000001",
  "codigoJustificativa": "01",
  "motivo": "Correção de dados",
  "dadosNFSeNova": { ... },
  "certificadoBase64": "BASE64...",
  "senhaCertificado": "senha",
  "ambiente": "homologacao"
}
```

### Validação XSD

#### POST /api/nfse/validar-xsd
Valida XML contra schemas XSD.

**Request:**
```json
{
  "xml": "<DPS>...</DPS>",
  "tipo": "DPS"
}
```

**Response:**
```json
{
  "sucesso": true,
  "valido": true,
  "erros": [],
  "quantidadeErros": 0
}
```

## 🔐 Segurança

- Assinatura digital obrigatória para emissão, cancelamento e substituição
- Validação de certificado (validade, chave privada)
- Comunicação HTTPS
- Validação de dados de entrada

## 📦 Schemas XSD Suportados

- `DPS_v1.00.xsd` - Declaração de Prestação de Serviços
- `evento_v1.00.xsd` - Eventos (cancelamento, substituição)
- `pedRegEvento_v1.00.xsd` - Pedido de Registro de Evento
- `tiposComplexos_v1.00.xsd` - Tipos complexos
- `tiposSimples_v1.00.xsd` - Tipos simples
- `xmldsig-core-schema.xsd` - Assinatura digital

## 🎯 Modo Simulação

O sistema detecta automaticamente quando o Sistema Nacional NFS-e não está disponível e entra em modo simulação:

- ✅ Gera respostas simuladas
- ✅ Retorna dados no formato esperado
- ✅ Permite testar todo o fluxo
- ✅ Logs indicam claramente quando está em simulação

## 📝 Exemplos de Uso

### Emissão Simples
```bash
POST /api/nfse/emitir
Content-Type: application/json

{
  "dadosNFSe": { ... },
  "certificadoBase64": "...",
  "senhaCertificado": "12345678",
  "ambiente": "homologacao"
}
```

### Consulta
```bash
GET /api/nfse/consulta/35503080000000000000000000000000000000000001?ambiente=homologacao
```

### Cancelamento
```bash
POST /api/nfse/cancelar
Content-Type: application/json

{
  "evento": {
    "chaveAcesso": "35503080000000000000000000000000000000000001",
    "tipoEvento": "101101",
    "codigoJustificativa": "01",
    "motivo": "Erro na emissão",
    "documentoAutor": "59282800000195"
  },
  "certificadoBase64": "...",
  "senhaCertificado": "12345678",
  "ambiente": "homologacao"
}
```

## 🚀 Como Executar

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
- HTTPS: https://localhost:5000/swagger

## 📚 Documentação Swagger

A API inclui documentação Swagger/OpenAPI completa com:
- Descrições de todos os endpoints
- Exemplos de requisição/resposta
- Códigos de status HTTP
- Validações e regras de negócio

## ✅ Checklist de Funcionalidades

- [x] Recebimento de dados em JSON
- [x] Geração de XML DPS conforme leiautes-NSF-e
- [x] Assinatura digital de DPS
- [x] Comunicação com Sistema Nacional NFS-e
- [x] Modo simulação automático
- [x] Consulta de NFS-e por chave de acesso
- [x] Cancelamento de NFS-e
- [x] Substituição de NFS-e
- [x] Validação XSD completa
- [x] Documentação Swagger/OpenAPI
- [x] Tratamento de erros
- [x] Logging completo

## 🔄 Próximos Passos (Quando Sistema Nacional Estiver Disponível)

1. Testar com ambiente real de homologação
2. Ajustar URLs conforme publicação oficial
3. Implementar outros tipos de eventos (confirmação, rejeição, etc.)
4. Adicionar cache de consultas
5. Implementar retry automático em caso de falhas temporárias

## 📞 Suporte

Para dúvidas ou problemas, consulte:
- Documentação oficial: Manual Contribuintes Emissor Público API
- Schemas XSD: `leiautes-NSF-e/`
- Logs da aplicação
