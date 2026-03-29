public class Usuario
{
    public int id { get; set; }
    public string nombre { get; set; } = string.Empty;
    public string apellido { get; set; } = string.Empty;
    public string usuario { get; set; } = string.Empty;
    public string contraseña { get; set; } = string.Empty;
    public string cargo { get; set; } = string.Empty;
    public int local_id { get; set; } // <-- Nuevo: local asignado al usuario
}
