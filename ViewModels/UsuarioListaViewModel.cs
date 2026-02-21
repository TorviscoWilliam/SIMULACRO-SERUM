namespace SimulacroExamen.ViewModels
{
    public class UsuarioListaViewModel
    {
        public int      Id                  { get; set; }
        public string   NombreUsuario       { get; set; } = string.Empty;
        public string   Correo              { get; set; } = string.Empty;
        public string   Rol                 { get; set; } = string.Empty;
        public DateTime FechaCreacion       { get; set; }
        public bool     Activo              { get; set; }
        public int      TotalExamenes       { get; set; }
        public int      MejorPuntaje        { get; set; }

        // Tiempo transcurrido desde la creación
        public TimeSpan TiempoDesdeCreacion => DateTime.Now - FechaCreacion;

        public string TiempoFormateado
        {
            get
            {
                var t = TiempoDesdeCreacion;
                if (t.TotalDays >= 365)
                    return $"{(int)(t.TotalDays / 365)} año(s), {(int)(t.TotalDays % 365 / 30)} mes(es)";
                if (t.TotalDays >= 30)
                    return $"{(int)(t.TotalDays / 30)} mes(es), {(int)(t.TotalDays % 30)} día(s)";
                if (t.TotalDays >= 1)
                    return $"{(int)t.TotalDays} día(s)";
                if (t.TotalHours >= 1)
                    return $"{(int)t.TotalHours} hora(s)";
                return $"{(int)t.TotalMinutes} minuto(s)";
            }
        }
    }
}
