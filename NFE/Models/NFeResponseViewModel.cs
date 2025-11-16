namespace NFE.Models
{
    /// <summary>
    /// Resposta da emissão de NFe (modelo novo)
    /// </summary>
    public class NFeResponseViewModel
    {
        public bool Sucesso { get; set; }
        
        public string? Mensagem { get; set; }
        
        public string? XmlEnviado { get; set; }
        
        public string? XmlRetorno { get; set; }
        
        public string? Protocolo { get; set; }
        
        public string? ChaveAcesso { get; set; }
        
        public string? CodigoStatus { get; set; }
        
        public string? Motivo { get; set; }
        
        public Dictionary<string, string[]>? Erros { get; set; }
        
        public DateTime DataProcessamento { get; set; }
        
        /// <summary>
        /// Número do recibo (quando lote é recebido mas ainda não processado)
        /// </summary>
        public string? NumeroRecibo { get; set; }
        
        /// <summary>
        /// Indica se deve consultar o recibo posteriormente
        /// </summary>
        public bool RequerConsultaRecibo { get; set; }
    }
}