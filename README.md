# Simulacro de Examen SERUM

Aplicación web ASP.NET Core MVC para simulacro de exámenes con roles de **Administrador** y **Usuario**.

## Tecnologías

- **ASP.NET Core 8 MVC** + C#
- **Entity Framework Core 8** (Code First)
- **SQL Server 2022**
- **Bootstrap 5** + Bootstrap Icons
- **ClosedXML** – exportar/importar Excel
- **BCrypt.Net** – hash de contraseñas

---

## Funcionalidades

### Administrador
| Función | Descripción |
|---|---|
| Dashboard | Estadísticas globales: usuarios, preguntas, exámenes, promedio |
| Gestión de usuarios | Crear usuarios, activar/desactivar, ver tiempo desde creación |
| Exportar usuarios | Exportar lista completa a Excel (.xlsx) |
| Gestión de preguntas | Agregar manualmente o importar desde Excel |
| Descargar plantilla | Plantilla Excel para carga masiva de preguntas |

### Usuario
| Función | Descripción |
|---|---|
| Tomar examen | Preguntas y alternativas en orden **aleatorio** |
| Ver resultado | Puntaje, porcentaje y revisión detallada de respuestas |
| Historial | Todos los intentos registrados con estadísticas |
| Revisar errores | Ver respuesta seleccionada vs respuesta correcta |

---

## Configuración rápida

### 1. Requisitos
- Visual Studio 2022 con workload **ASP.NET and web development**
- SQL Server 2022 (local o remoto)
- .NET 8 SDK

### 2. Cadena de conexión
Edite `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=SimulacroExamenDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 3. Ejecutar
1. Abra `SimulacroExamen.sln` en Visual Studio
2. Presione **F5** — la base de datos se crea automáticamente
3. Credenciales de administrador: usuario `admin` / contraseña `Admin123!`

### 4. Número de preguntas por examen
Configure en `appsettings.json` → `AppSettings:NumeroPreguntas` (por defecto: 20)

---

## Formato Excel para importar preguntas

| Col A | Col B | Col C | Col D | Col E |
|---|---|---|---|---|
| Pregunta | Respuesta correcta | Opción 2 incorrecta | Opción 3 *(opcional)* | Opción 4 *(opcional)* |

Fila 1 = encabezados. Datos desde fila 2. Descargue la plantilla desde el panel admin.