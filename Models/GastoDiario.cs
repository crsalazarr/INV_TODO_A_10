// Models/GastoDiario.cs
namespace INV_TODO_A_10.Models
{
    public class GastoDiario
    {
        public int id { get; set; }
        public int local_id { get; set; }
        public DateTime fecha { get; set; } = DateTime.Now;
        public decimal nomina { get; set; } = 0;
        public decimal arriendo { get; set; } = 0;
        public decimal bolsa { get; set; } = 0;
        public decimal otros { get; set; } = 0;
        
        // Propiedad calculada
        public decimal total_gastos => nomina + arriendo + bolsa + otros;
    }
}