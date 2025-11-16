namespace NFE.Models
{
    /// <summary>
    /// ViewModel para resposta da API de NFe
    /// </summary>
    public class NFeResponseViewModel
    {
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public string? XmlEnviado { get; set; }
        public string? XmlRetorno { get; set; }
        public string? Protocolo { get; set; }
        public string? ChaveAcesso { get; set; }
        public int? CodigoStatus { get; set; }
        public string? Motivo { get; set; }
        public Dictionary<string, string[]>? Erros { get; set; }
        public DateTime DataProcessamento { get; set; } = DateTime.Now;
    }
}

