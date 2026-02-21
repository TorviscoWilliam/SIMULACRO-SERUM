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
    }
}
