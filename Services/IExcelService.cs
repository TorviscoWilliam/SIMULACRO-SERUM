using SimulacroExamen.ViewModels;

namespace SimulacroExamen.Services
{
    public interface IExcelService
    {
        /// <summary>
        /// Lee preguntas de un archivo Excel.
        /// Formato: Columna A = Pregunta | B = Respuesta Correcta | C-F = Opciones incorrectas
        /// </summary>
        List<PreguntaFormViewModel> ImportarPreguntas(Stream stream);

        /// <summary>
        /// Genera un Excel con la lista de usuarios.
        /// </summary>
        byte[] ExportarUsuarios(List<UsuarioListaViewModel> usuarios);

        /// <summary>
        /// Genera el archivo de plantilla Excel para importar preguntas.
        /// </summary>
        byte[] GenerarPlantillaPreguntas();

        /// <summary>
        /// Genera un Excel con el listado de preguntas (con alternativas y tipo de examen).
        /// </summary>
        byte[] ExportarPreguntas(List<SimulacroExamen.Models.Pregunta> preguntas);

        /// <summary>
        /// Lee usuarios desde un archivo Excel (columnas: Usuario, Correo, Contraseña,
        /// PrimerNombre, PrimerApellido, Celular, DNI).
        /// </summary>
        List<(string Usuario, string Correo, string Contrasena, string? PrimerNombre,
              string? PrimerApellido, string? Celular, string? Dni)> ImportarUsuarios(Stream stream);

        /// <summary>
        /// Genera plantilla Excel vacía para carga masiva de usuarios.
        /// </summary>
        byte[] GenerarPlantillaUsuarios();
    }
}
