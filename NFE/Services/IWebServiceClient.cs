namespace NFE.Services
{
    public interface IWebServiceClient
    {
        Task<WebServiceResponse> EnviarNFeAsync(string xml, string ambiente);
    }

    public class WebServiceResponse
    {
        public bool Sucesso { get; set; }
        public string? Mensagem { get; set; }
        public string? XmlRetorno { get; set; }
        public string? Protocolo { get; set; }
        public string? ChaveAcesso { get; set; }
        public int? CodigoStatus { get; set; }
        public string? Motivo { get; set; }
        public Dictionary<string, string>? Erros { get; set; }
    }
}

