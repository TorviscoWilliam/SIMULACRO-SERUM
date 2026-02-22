-- ============================================================
--  SIMULACRO SERUM  –  Diseño Completo de Base de Datos
--  SQL Server 2022  |  Codificación: UTF-8
--  Autor   : Sistema (generado desde modelos EF Core)
--  Versión : 2.0
-- ============================================================
--
--  DIAGRAMA ENTIDAD-RELACIÓN (ERD)
--  ================================
--
--   ┌───────────────────┐         ┌──────────────────────────┐
--   │     Usuarios      │         │        Preguntas         │
--   ├───────────────────┤         ├──────────────────────────┤
--   │ PK  Id            │         │ PK  Id                   │
--   │     NombreUsuario │         │     TextoPregunta (MAX)  │
--   │     Correo        │         │     FechaCreacion        │
--   │     Contrasena    │         │     Activo               │
--   │     Rol           │         └─────────────┬────────────┘
--   │     FechaCreacion │               1        │
--   │     Activo        │               │        │ CASCADE
--   └────────┬──────────┘               │        │ DELETE
--            │ 1                        │        ▼ N
--            │ RESTRICT                 │  ┌───────────────────────┐
--            │ DELETE                   │  │      Alternativas     │
--            │                          │  ├───────────────────────┤
--            ▼ N                        │  │ PK  Id                │
--   ┌─────────────────────┐             │  │ FK  PreguntaId        │
--   │       Examenes      │             │  │     TextoAlternativa  │
--   ├─────────────────────┤             │  │     EsCorrecta (BIT)  │
--   │ PK  Id              │             │  └───────────┬───────────┘
--   │ FK  UsuarioId       │             │              │ 1
--   │     FechaInicio     │             │              │ SET NULL
--   │     FechaFin (NULL) │             │              │ (AlternativaSeleccionadaId)
--   │     Puntaje         │             │              │
--   │     TotalPreguntas  │             │              │
--   │     Completado (BIT)│             │              │
--   └──────────┬──────────┘             │              │
--              │ 1                      │              │
--              │ CASCADE                │              │
--              │ DELETE                 │              │
--              ▼ N                      │              ▼
--   ┌──────────────────────────────────────────────────────────┐
--   │                     PreguntasExamen                      │
--   │         (tabla puente / junction table)                  │
--   ├──────────────────────────────────────────────────────────┤
--   │ PK  Id                                                   │
--   │ FK  ExamenId                ──────────────► Examenes     │
--   │ FK  PreguntaId              ──────────────► Preguntas    │
--   │ FK  AlternativaSeleccionadaId (NULL) ──────► Alternativas│
--   │     EsCorrecta (BIT)   → true si respondió correctamente │
--   │     Orden (INT)        → posición aleatoria en el examen  │
--   │     OrdenAlternativas  → "3,1,4,2" IDs mezclados en orden│
--   └──────────────────────────────────────────────────────────┘
--
--  REGLAS DE INTEGRIDAD
--  ─────────────────────
--  • Usuarios.NombreUsuario  → UNIQUE (no duplicados)
--  • Usuarios.Correo         → UNIQUE (no duplicados)
--  • Alternativas.PreguntaId → CASCADE DELETE (al borrar pregunta, se borran sus opciones)
--  • Examenes.UsuarioId      → RESTRICT DELETE (no se puede borrar un usuario con exámenes)
--  • PreguntasExamen.ExamenId→ CASCADE DELETE (al borrar examen, se borran sus líneas)
--  • PreguntasExamen.PreguntaId → RESTRICT DELETE (una pregunta usada no se puede borrar físicamente)
--  • PreguntasExamen.AlternativaSeleccionadaId → SET NULL (si se borra la alternativa, la FK queda NULL)
--  • Soft Delete en Usuarios y Preguntas: campo Activo=0 en lugar de borrado físico
--
-- ============================================================

USE master;
GO

-- ── 1. Crear la base de datos ────────────────────────────────
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'SimulacroExamenDB')
BEGIN
    CREATE DATABASE SimulacroExamenDB
        COLLATE Modern_Spanish_CI_AS;   -- Mayúsculas/minúsculas insensible, acentos correctos
    PRINT 'Base de datos SimulacroExamenDB creada.';
END
ELSE
    PRINT 'Base de datos SimulacroExamenDB ya existe.';
GO

USE SimulacroExamenDB;
GO

-- ============================================================
--  TABLAS
-- ============================================================

-- ── 2. Usuarios ──────────────────────────────────────────────
--  Almacena tanto administradores como examinados.
--  La contraseña NUNCA se guarda en texto plano: se usa hash BCrypt.
--  Soft delete: Activo=0 impide el login sin borrar el historial.
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Usuarios') AND type = 'U')
BEGIN
    CREATE TABLE Usuarios (
        Id              INT            NOT NULL IDENTITY(1,1),
        NombreUsuario   NVARCHAR(100)  NOT NULL,
        Correo          NVARCHAR(200)  NOT NULL,
        Contrasena      NVARCHAR(255)  NOT NULL,           -- Hash BCrypt (~60 chars, max 255)
        Rol             NVARCHAR(20)   NOT NULL DEFAULT 'Usuario',  -- 'Admin' | 'Usuario'
        FechaCreacion   DATETIME2(0)   NOT NULL DEFAULT GETDATE(),
        Activo          BIT            NOT NULL DEFAULT 1,

        CONSTRAINT PK_Usuarios PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Usuarios_NombreUsuario UNIQUE (NombreUsuario),
        CONSTRAINT UQ_Usuarios_Correo        UNIQUE (Correo),
        CONSTRAINT CK_Usuarios_Rol           CHECK (Rol IN ('Admin', 'Usuario'))
    );
    PRINT 'Tabla Usuarios creada.';
END
ELSE
    PRINT 'Tabla Usuarios ya existe.';
GO

-- ── 3. Preguntas ─────────────────────────────────────────────
--  Banco de preguntas del simulacro.
--  Soft delete: Activo=0 excluye la pregunta de nuevos exámenes
--  pero conserva el historial de los exámenes ya realizados.
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Preguntas') AND type = 'U')
BEGIN
    CREATE TABLE Preguntas (
        Id              INT            NOT NULL IDENTITY(1,1),
        TextoPregunta   NVARCHAR(MAX)  NOT NULL,           -- Sin límite de longitud
        FechaCreacion   DATETIME2(0)   NOT NULL DEFAULT GETDATE(),
        Activo          BIT            NOT NULL DEFAULT 1,

        CONSTRAINT PK_Preguntas PRIMARY KEY CLUSTERED (Id)
    );
    PRINT 'Tabla Preguntas creada.';
END
ELSE
    PRINT 'Tabla Preguntas ya existe.';
GO

-- ── 4. Alternativas ──────────────────────────────────────────
--  Opciones de respuesta de cada pregunta (2 a 4 por pregunta).
--  Exactamente UNA alternativa por pregunta debe tener EsCorrecta=1.
--  CASCADE DELETE: al desactivar físicamente una pregunta,
--  sus alternativas también se eliminan.
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Alternativas') AND type = 'U')
BEGIN
    CREATE TABLE Alternativas (
        Id                  INT            NOT NULL IDENTITY(1,1),
        PreguntaId          INT            NOT NULL,
        TextoAlternativa    NVARCHAR(MAX)  NOT NULL,
        EsCorrecta          BIT            NOT NULL DEFAULT 0,

        CONSTRAINT PK_Alternativas PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Alternativas_Preguntas
            FOREIGN KEY (PreguntaId)
            REFERENCES Preguntas(Id)
            ON DELETE CASCADE       -- Borrar pregunta → borra sus alternativas
    );
    PRINT 'Tabla Alternativas creada.';
END
ELSE
    PRINT 'Tabla Alternativas ya existe.';
GO

-- ── 5. Examenes ──────────────────────────────────────────────
--  Cada fila = un intento de examen de un usuario.
--  Completado=0 → examen en progreso (no aparece en historial).
--  Completado=1 → examen enviado y calificado.
--  FechaFin - FechaInicio = duración total del examen.
--  RESTRICT DELETE: no se puede borrar un usuario con exámenes
--  para proteger el historial (usar Activo=0 en Usuarios).
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Examenes') AND type = 'U')
BEGIN
    CREATE TABLE Examenes (
        Id              INT            NOT NULL IDENTITY(1,1),
        UsuarioId       INT            NOT NULL,
        FechaInicio     DATETIME2(0)   NOT NULL DEFAULT GETDATE(),
        FechaFin        DATETIME2(0)   NULL,               -- NULL mientras está en progreso
        Puntaje         INT            NOT NULL DEFAULT 0,  -- Respuestas correctas (1 pto c/u)
        TotalPreguntas  INT            NOT NULL DEFAULT 0,  -- Preguntas asignadas en este intento
        Completado      BIT            NOT NULL DEFAULT 0,

        CONSTRAINT PK_Examenes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Examenes_Usuarios
            FOREIGN KEY (UsuarioId)
            REFERENCES Usuarios(Id),                        -- RESTRICT por defecto (sin ON DELETE)
        CONSTRAINT CK_Examenes_Puntaje
            CHECK (Puntaje >= 0),
        CONSTRAINT CK_Examenes_TotalPreguntas
            CHECK (TotalPreguntas >= 0),
        CONSTRAINT CK_Examenes_PuntajeLeTotal
            CHECK (Puntaje <= TotalPreguntas)
    );
    PRINT 'Tabla Examenes creada.';
END
ELSE
    PRINT 'Tabla Examenes ya existe.';
GO

-- ── 6. PreguntasExamen ───────────────────────────────────────
--  Tabla puente entre Examenes y Preguntas.
--  Registra qué preguntas se asignaron a cada intento,
--  en qué orden aleatorio aparecieron, qué respondió el
--  usuario y si acertó.
--
--  OrdenAlternativas: string "id1,id2,id3,id4" que persiste el
--  orden aleatorio de las opciones para ese intento, evitando
--  que se re-aleatoricen si el usuario recarga la página.
--
--  AlternativaSeleccionadaId: NULL si la pregunta no fue
--  respondida antes de enviar el formulario.
-- ─────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'PreguntasExamen') AND type = 'U')
BEGIN
    CREATE TABLE PreguntasExamen (
        Id                          INT            NOT NULL IDENTITY(1,1),
        ExamenId                    INT            NOT NULL,
        PreguntaId                  INT            NOT NULL,
        AlternativaSeleccionadaId   INT            NULL,       -- NULL = sin responder
        EsCorrecta                  BIT            NOT NULL DEFAULT 0,
        Orden                       INT            NOT NULL DEFAULT 0,  -- Posición 1-based
        OrdenAlternativas           NVARCHAR(500)  NOT NULL DEFAULT '', -- "3,1,4,2"

        CONSTRAINT PK_PreguntasExamen PRIMARY KEY CLUSTERED (Id),

        -- Al borrar un Examen, sus líneas se borran en cascada
        CONSTRAINT FK_PreguntasExamen_Examenes
            FOREIGN KEY (ExamenId)
            REFERENCES Examenes(Id)
            ON DELETE CASCADE,

        -- No se puede borrar una Pregunta que ya fue usada en un examen
        CONSTRAINT FK_PreguntasExamen_Preguntas
            FOREIGN KEY (PreguntaId)
            REFERENCES Preguntas(Id),                          -- RESTRICT por defecto

        -- Si se borra la Alternativa referenciada, la FK queda en NULL
        CONSTRAINT FK_PreguntasExamen_Alternativas
            FOREIGN KEY (AlternativaSeleccionadaId)
            REFERENCES Alternativas(Id)
            ON DELETE SET NULL,

        CONSTRAINT CK_PreguntasExamen_Orden
            CHECK (Orden >= 0)
    );
    PRINT 'Tabla PreguntasExamen creada.';
END
ELSE
    PRINT 'Tabla PreguntasExamen ya existe.';
GO

-- ============================================================
--  ÍNDICES DE RENDIMIENTO
--  (Los UNIQUE ya crean índices implícitos)
-- ============================================================

-- Búsquedas frecuentes: historial y panel de usuario
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Examenes_UsuarioId' AND object_id = OBJECT_ID('Examenes'))
    CREATE NONCLUSTERED INDEX IX_Examenes_UsuarioId
        ON Examenes (UsuarioId)
        INCLUDE (Completado, Puntaje, TotalPreguntas, FechaFin);
GO

-- Filtro de exámenes completados (muy usado en consultas del historial)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Examenes_UsuarioId_Completado' AND object_id = OBJECT_ID('Examenes'))
    CREATE NONCLUSTERED INDEX IX_Examenes_UsuarioId_Completado
        ON Examenes (UsuarioId, Completado)
        INCLUDE (Puntaje, TotalPreguntas, FechaFin);
GO

-- Carga de preguntas de un examen (Tomar / Resultado)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PreguntasExamen_ExamenId' AND object_id = OBJECT_ID('PreguntasExamen'))
    CREATE NONCLUSTERED INDEX IX_PreguntasExamen_ExamenId
        ON PreguntasExamen (ExamenId)
        INCLUDE (PreguntaId, AlternativaSeleccionadaId, EsCorrecta, Orden, OrdenAlternativas);
GO

-- Join de alternativas por pregunta
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Alternativas_PreguntaId' AND object_id = OBJECT_ID('Alternativas'))
    CREATE NONCLUSTERED INDEX IX_Alternativas_PreguntaId
        ON Alternativas (PreguntaId)
        INCLUDE (TextoAlternativa, EsCorrecta);
GO

-- Filtro de preguntas activas (banco de preguntas y nuevos exámenes)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Preguntas_Activo' AND object_id = OBJECT_ID('Preguntas'))
    CREATE NONCLUSTERED INDEX IX_Preguntas_Activo
        ON Preguntas (Activo)
        INCLUDE (TextoPregunta, FechaCreacion);
GO

-- Filtro de usuarios activos por rol (dashboard admin y login)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Usuarios_Rol_Activo' AND object_id = OBJECT_ID('Usuarios'))
    CREATE NONCLUSTERED INDEX IX_Usuarios_Rol_Activo
        ON Usuarios (Rol, Activo)
        INCLUDE (NombreUsuario, Correo, FechaCreacion);
GO

PRINT 'Índices de rendimiento creados.';
GO

-- ============================================================
--  DATO SEMILLA: ADMINISTRADOR
--  La aplicación crea este usuario automáticamente al iniciar
--  (Program.cs → EnsureCreated + BCrypt.HashPassword).
--  Solo ejecute el bloque de INSERT si crea la BD de forma
--  completamente manual y no iniciará la aplicación antes.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Rol = 'Admin')
BEGIN
    PRINT '';
    PRINT '>>> ATENCIÓN: No existe ningún administrador.';
    PRINT '>>> Inicie la aplicación una vez para crearlo automáticamente.';
    PRINT '>>> Credenciales iniciales: admin / Admin123!';
    PRINT '';
    PRINT '>>> Si prefiere crearlo manualmente, ejecute el INSERT comentado';
    PRINT '>>> en la sección de datos semilla de este script.';
END
ELSE
    PRINT 'Administrador ya existe en la base de datos.';
GO

-- INSERT manual (descomentar SOLO si no se usará EnsureCreated):
-- El hash corresponde a "Admin123!" generado con BCrypt (work factor 11).
-- IMPORTANTE: No reutilice este hash en producción. Genere uno nuevo con:
--   BCrypt.Net.BCrypt.HashPassword("SuNuevaContrasena")
/*
INSERT INTO Usuarios (NombreUsuario, Correo, Contrasena, Rol, FechaCreacion, Activo)
VALUES (
    'admin',
    'admin@simulacro.com',
    '$2a$11$examplehashgeneratedbybcrypt.replacethiswithrealobcrypt',
    'Admin',
    GETDATE(),
    1
);
*/

-- ============================================================
--  RESUMEN DEL MODELO RELACIONAL
-- ============================================================
--
--  Usuarios (1) ─── (N) Examenes
--      Un usuario puede realizar múltiples intentos de examen.
--      No se puede borrar un usuario que tenga exámenes (RESTRICT).
--      Para inhabilitar: Usuarios.Activo = 0 (soft delete).
--
--  Preguntas (1) ─── (N) Alternativas
--      Cada pregunta tiene entre 2 y 4 alternativas.
--      Exactamente una tiene EsCorrecta = 1.
--      Si se borra físicamente la pregunta, sus alternativas
--      se borran en cascada (CASCADE DELETE).
--
--  Preguntas (1) ─── (N) PreguntasExamen
--      Una pregunta puede aparecer en múltiples exámenes.
--      No se puede borrar una pregunta usada en exámenes (RESTRICT).
--      Para quitar del banco: Preguntas.Activo = 0 (soft delete).
--
--  Examenes (1) ─── (N) PreguntasExamen
--      Cada examen tiene entre 1 y N preguntas asignadas.
--      Al borrar un examen, sus líneas se borran (CASCADE DELETE).
--
--  Alternativas (1) ─── (N) PreguntasExamen [FK AlternativaSeleccionadaId]
--      La alternativa que eligió el usuario en ese intento.
--      Si se borra la alternativa, la FK queda en NULL (SET NULL).
--      EsCorrecta en PreguntaExamen guarda si fue correcta
--      en el momento de responder (histórico inmutable).
--
-- ============================================================
PRINT '====================================================';
PRINT '  SimulacroExamenDB - Diseño de BD completado OK   ';
PRINT '====================================================';
GO
