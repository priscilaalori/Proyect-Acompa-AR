namespace AcompañAR.Proyecto.DTO
{
    public class VinculacionRequest
    {
        public int IdMayor { get; set; }
        public int IdAcompanante { get; set; }
        public string? TipoVinculo { get; set; }
        public string? Estado { get; set; }
    }
}
