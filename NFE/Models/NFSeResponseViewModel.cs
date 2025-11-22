namespace NFE.Models
{
    /// <summary>
    /// Resposta da emissão de NFS-e
    /// </summary>
    public class NFSeResponseViewModel
    {
        public bool Sucesso { get; set; }
        
        public string? Mensagem { get; set; }
        
        public string? XmlEnviado { get; set; }
        
        public string? XmlRetorno { get; set; }
        
        public string? Protocolo { get; set; }
        
        public string? NumeroNFSe { get; set; }
        
        public string? CodigoVerificacao { get; set; }
        
        public string? CodigoStatus { get; set; }
        
        public string? Motivo { get; set; }
        
        public Dictionary<string, string[]>? Erros { get; set; }
        
        public DateTime DataProcessamento { get; set; }
        
        /// <summary>
        /// Link para consulta/visualização da NFS-e
        /// </summary>
        public string? LinkConsulta { get; set; }
    }
}

