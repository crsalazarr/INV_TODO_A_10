using MySqlConnector;

namespace INV_TODO_A_10.Models
{
    public class ConexionBD
    {
        private readonly string _connectionString;

        public ConexionBD(string connectionString)
        {
            _connectionString = connectionString;
        }

        public MySqlConnection ObtenerConexion()
        {
            var conexion = new MySqlConnection(_connectionString);
            conexion.Open();
            return conexion;
        }

        public void CerrarConexion(MySqlConnection conexion)
        {
            if (conexion?.State == System.Data.ConnectionState.Open)
            {
                conexion.Close();
            }
        }
    }
}