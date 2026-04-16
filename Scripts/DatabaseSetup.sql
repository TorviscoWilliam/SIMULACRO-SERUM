-- ============================================================
--  SIMULACRO SERUM  –  Script de Creación Completa de BD
--  SQL Server 2019 / 2022 / Azure SQL
--  Collation: Modern_Spanish_CI_AS
--  Versión : 5.0
--
--  Cambios respecto a v3.0
--  ─────────────────────────────────────────────────────────
--  · Herencia TPH en Usuarios: columna Discriminador
--    ("Administrador" | "Estudiante")
--  · Columnas de suscripción/trial: EsTrial, FechaVencimiento,
--    IntentosExtra, NotaPonderada  (solo Estudiante)
--  · Columnas personales: PrimerNombre, SegundoNombre,
--    PrimerApellido, SegundoApellido, Celular, Dni
--  · Seguridad: SessionToken, PasswordResetToken,
--    PasswordResetExpiry, EmailVerificado, EmailVerificacionToken
--  · Examenes: columna DuracionSegundos
--  · PreguntasExamen: eliminados EsCorrecta y OrdenAlternativas
--  · Nueva tabla OrdenAlternativaExamen (orden aleatorio por fila)
--  · Nuevas tablas: Noticias, AnunciosGlobales,
--    PlanesSuscripcion, CaracteristicasPlan,
--    Sugerencias, LogsActividad
--
--  INSTRUCCIONES DE USO
--  ─────────────────────────────────────────────────────────
--  OPCIÓN A – BD nueva desde cero:
--    1. Descomentar y ejecutar la SECCIÓN 0 (DROP).
--    2. Ejecutar todo el script.
--    3. Iniciar la aplicación → Program.cs siembra el SuperAdmin,
--       Admin y TiposExamen automáticamente.
--
--  OPCIÓN B – BD existente (actualización incremental):
--    - Ignorar la SECCIÓN 0.
--    - Cada bloque usa IF NOT EXISTS; es seguro ejecutar todo.
--
--  CONTRASEÑAS
--  ─────────────────────────────────────────────────────────
--  Se usa BCrypt (work factor 11). Nunca texto plano.
--  Program.cs inserta los usuarios admin al arrancar la app.
-- ============================================================


-- ============================================================
--  SECCIÓN 0 – DROP COMPLETO (descomentar para re-creación)
-- ============================================================
/*
USE SimulacroExamenDB;
GO
DROP TABLE IF EXISTS OrdenAlternativaExamen;
DROP TABLE IF EXISTS PreguntasExamen;
DROP TABLE IF EXISTS Examenes;
DROP TABLE IF EXISTS Alternativas;
DROP TABLE IF EXISTS Preguntas;
DROP TABLE IF EXISTS UsuarioTiposExamen;
DROP TABLE IF EXISTS TiposExamen;
DROP TABLE IF EXISTS CaracteristicasPlan;
DROP TABLE IF EXISTS PlanesSuscripcion;
DROP TABLE IF EXISTS AnunciosGlobales;
DROP TABLE IF EXISTS Noticias;
DROP TABLE IF EXISTS Sugerencias;
DROP TABLE IF EXISTS LogsActividad;
DROP TABLE IF EXISTS Usuarios;
GO
USE master;
GO
DROP DATABASE IF EXISTS SimulacroExamenDB;
GO
*/


-- ============================================================
--  SECCIÓN 1 – CREAR BASE DE DATOS
-- ============================================================
USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'SimulacroExamenDB')
BEGIN
    CREATE DATABASE SimulacroExamenDB COLLATE Modern_Spanish_CI_AS;
    PRINT '>>> Base de datos SimulacroExamenDB creada.';
END
ELSE
    PRINT '>>> Base de datos SimulacroExamenDB ya existe.';
GO

USE SimulacroExamenDB;
GO


-- ============================================================
--  SECCIÓN 2 – TABLA Usuarios  (TPH: Administrador / Estudiante)
--
--  Discriminador = 'Administrador'  → Rol Admin o SuperAdmin
--  Discriminador = 'Estudiante'     → Rol Usuario
--
--  Campos de suscripción (solo Estudiante):
--    EsTrial          true = modo prueba (1 examen de 20 preguntas)
--    FechaVencimiento null = sin límite de tiempo
--    IntentosExtra    intentos diarios adicionales otorgados por admin
--    NotaPonderada    nota personal del estudiante (0-20) para calc. final
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Usuarios') AND type='U')
BEGIN
    CREATE TABLE Usuarios (
        -- ── Identidad ──────────────────────────────────────
        Id              INT           NOT NULL IDENTITY(1,1),
        NombreUsuario   NVARCHAR(100) NOT NULL,
        Correo          NVARCHAR(200) NOT NULL,
        Contrasena      NVARCHAR(255) NOT NULL,   -- Hash BCrypt
        Rol             NVARCHAR(20)  NOT NULL,   -- 'Admin'|'SuperAdmin'|'Usuario'
        FechaCreacion   DATETIME2     NOT NULL DEFAULT GETDATE(),
        Activo          BIT           NOT NULL DEFAULT 1,

        -- ── Herencia TPH ───────────────────────────────────
        Discriminador   NVARCHAR(50)  NOT NULL DEFAULT 'Estudiante',

        -- ── Datos personales ───────────────────────────────
        PrimerNombre    NVARCHAR(100) NULL,
        SegundoNombre   NVARCHAR(100) NULL,
        PrimerApellido  NVARCHAR(100) NULL,
        SegundoApellido NVARCHAR(100) NULL,
        Celular         NVARCHAR(20)  NULL,
        Dni             NVARCHAR(8)   NULL,

        -- ── Seguridad de sesión ────────────────────────────
        SessionToken            NVARCHAR(36)  NULL,
        PasswordResetToken      NVARCHAR(100) NULL,
        PasswordResetExpiry     DATETIME2     NULL,
        EmailVerificado         BIT           NOT NULL DEFAULT 0,
        EmailVerificacionToken  NVARCHAR(100) NULL,

        -- ── Suscripción / trial  (solo Estudiante) ─────────
        NotaPonderada    FLOAT         NULL,
        IntentosExtra    INT           NOT NULL DEFAULT 0,
        EsTrial          BIT           NOT NULL DEFAULT 1,
        FechaVencimiento DATETIME2     NULL,
        PlanSuscripcionId INT          NULL,   -- FK → PlanesSuscripcion (se agrega después)

        CONSTRAINT PK_Usuarios        PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Usuarios_Nombre UNIQUE (NombreUsuario),
        CONSTRAINT UQ_Usuarios_Correo UNIQUE (Correo)
    );
    PRINT '>>> Tabla Usuarios creada.';
END
ELSE
BEGIN
    PRINT '>>> Tabla Usuarios ya existe – aplicando cambios incrementales...';

    -- Discriminador (TPH)
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='Discriminador')
    BEGIN
        ALTER TABLE Usuarios ADD Discriminador NVARCHAR(50) NOT NULL DEFAULT 'Estudiante';
        UPDATE Usuarios SET Discriminador = 'Administrador' WHERE Rol IN ('Admin','SuperAdmin');
        PRINT '    >>> Discriminador agregado.';
    END

    -- Datos personales
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='PrimerNombre')
        ALTER TABLE Usuarios ADD PrimerNombre NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='SegundoNombre')
        ALTER TABLE Usuarios ADD SegundoNombre NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='PrimerApellido')
        ALTER TABLE Usuarios ADD PrimerApellido NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='SegundoApellido')
        ALTER TABLE Usuarios ADD SegundoApellido NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='Celular')
        ALTER TABLE Usuarios ADD Celular NVARCHAR(20) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='Dni')
        ALTER TABLE Usuarios ADD Dni NVARCHAR(8) NULL;

    -- Seguridad
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='SessionToken')
        ALTER TABLE Usuarios ADD SessionToken NVARCHAR(36) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='PasswordResetToken')
        ALTER TABLE Usuarios ADD PasswordResetToken NVARCHAR(100) NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='PasswordResetExpiry')
        ALTER TABLE Usuarios ADD PasswordResetExpiry DATETIME2 NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='EmailVerificado')
    BEGIN
        ALTER TABLE Usuarios ADD EmailVerificado BIT NOT NULL DEFAULT 0;
        UPDATE Usuarios SET EmailVerificado = 1 WHERE FechaCreacion < GETDATE();
    END
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='EmailVerificacionToken')
        ALTER TABLE Usuarios ADD EmailVerificacionToken NVARCHAR(100) NULL;

    -- Suscripción / trial
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='NotaPonderada')
        ALTER TABLE Usuarios ADD NotaPonderada FLOAT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='IntentosExtra')
        ALTER TABLE Usuarios ADD IntentosExtra INT NOT NULL DEFAULT 0;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='EsTrial')
        ALTER TABLE Usuarios ADD EsTrial BIT NOT NULL DEFAULT 0;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='FechaVencimiento')
        ALTER TABLE Usuarios ADD FechaVencimiento DATETIME2 NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Usuarios') AND name='PlanSuscripcionId')
    BEGIN
        ALTER TABLE Usuarios ADD PlanSuscripcionId INT NULL;
        IF OBJECT_ID('PlanesSuscripcion') IS NOT NULL
            ALTER TABLE Usuarios ADD CONSTRAINT FK_Usuarios_PlanSuscripcion
                FOREIGN KEY (PlanSuscripcionId) REFERENCES PlanesSuscripcion(Id) ON DELETE SET NULL;
    END

    -- AdminNombre en LogsActividad era NOT NULL sin default → convertir a NULL
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME='LogsActividad' AND COLUMN_NAME='AdminNombre' AND IS_NULLABLE='NO')
    BEGIN
        ALTER TABLE LogsActividad ALTER COLUMN AdminNombre NVARCHAR(100) NULL;
        PRINT '    >>> LogsActividad.AdminNombre convertido a NULL.';
    END
END
GO


-- ============================================================
--  SECCIÓN 3 – TABLA TiposExamen
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'TiposExamen') AND type='U')
BEGIN
    CREATE TABLE TiposExamen (
        Id               INT           NOT NULL IDENTITY(1,1),
        Nombre           NVARCHAR(100) NOT NULL,
        NumeroPreguntas  INT           NOT NULL DEFAULT 4,
        Activo           BIT           NOT NULL DEFAULT 1,

        CONSTRAINT PK_TiposExamen        PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_TiposExamen_Nombre UNIQUE (Nombre),
        CONSTRAINT CK_TiposExamen_NumPre CHECK (NumeroPreguntas BETWEEN 1 AND 200)
    );
    PRINT '>>> Tabla TiposExamen creada.';
END
ELSE
BEGIN
    PRINT '>>> Tabla TiposExamen ya existe.';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('TiposExamen') AND name='NumeroPreguntas')
    BEGIN
        ALTER TABLE TiposExamen ADD NumeroPreguntas INT NOT NULL
            CONSTRAINT DF_TiposExamen_NumPre DEFAULT 4;
        PRINT '    >>> NumeroPreguntas agregado.';
    END
END
GO


-- ============================================================
--  SECCIÓN 4 – TABLA UsuarioTiposExamen
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'UsuarioTiposExamen') AND type='U')
BEGIN
    CREATE TABLE UsuarioTiposExamen (
        Id               INT          NOT NULL IDENTITY(1,1),
        UsuarioId        INT          NOT NULL,
        TipoExamenId     INT          NOT NULL,
        FechaAsignacion  DATETIME2    NOT NULL DEFAULT GETDATE(),

        CONSTRAINT PK_UsuarioTiposExamen PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_UsuTipo_Par        UNIQUE (UsuarioId, TipoExamenId),
        CONSTRAINT FK_UsuTipo_Usuario
            FOREIGN KEY (UsuarioId)    REFERENCES Usuarios(Id)   ON DELETE CASCADE,
        CONSTRAINT FK_UsuTipo_Tipo
            FOREIGN KEY (TipoExamenId) REFERENCES TiposExamen(Id) ON DELETE CASCADE
    );
    PRINT '>>> Tabla UsuarioTiposExamen creada.';
END
ELSE
    PRINT '>>> Tabla UsuarioTiposExamen ya existe.';
GO


-- ============================================================
--  SECCIÓN 5 – TABLA Preguntas
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Preguntas') AND type='U')
BEGIN
    CREATE TABLE Preguntas (
        Id              INT           NOT NULL IDENTITY(1,1),
        TipoExamenId    INT           NULL,
        TextoPregunta   NVARCHAR(MAX) NOT NULL,
        FechaCreacion   DATETIME2     NOT NULL DEFAULT GETDATE(),
        Activo          BIT           NOT NULL DEFAULT 1,

        CONSTRAINT PK_Preguntas PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Preg_TipoExamen
            FOREIGN KEY (TipoExamenId) REFERENCES TiposExamen(Id) ON DELETE SET NULL
    );
    PRINT '>>> Tabla Preguntas creada.';
END
ELSE
BEGIN
    PRINT '>>> Tabla Preguntas ya existe.';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Preguntas') AND name='TipoExamenId')
    BEGIN
        ALTER TABLE Preguntas ADD TipoExamenId INT NULL
            CONSTRAINT FK_Preg_TipoExamen FOREIGN KEY REFERENCES TiposExamen(Id) ON DELETE SET NULL;
        PRINT '    >>> TipoExamenId agregado a Preguntas.';
    END
END
GO


-- ============================================================
--  SECCIÓN 6 – TABLA Alternativas
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Alternativas') AND type='U')
BEGIN
    CREATE TABLE Alternativas (
        Id                INT           NOT NULL IDENTITY(1,1),
        PreguntaId        INT           NOT NULL,
        TextoAlternativa  NVARCHAR(MAX) NOT NULL,
        EsCorrecta        BIT           NOT NULL DEFAULT 0,

        CONSTRAINT PK_Alternativas PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Alt_Pregunta
            FOREIGN KEY (PreguntaId) REFERENCES Preguntas(Id) ON DELETE CASCADE
    );
    PRINT '>>> Tabla Alternativas creada.';
END
ELSE
    PRINT '>>> Tabla Alternativas ya existe.';
GO


-- ============================================================
--  SECCIÓN 7 – TABLA Examenes
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Examenes') AND type='U')
BEGIN
    CREATE TABLE Examenes (
        Id               INT          NOT NULL IDENTITY(1,1),
        UsuarioId        INT          NOT NULL,
        TipoExamenId     INT          NULL,
        FechaInicio      DATETIME2    NOT NULL DEFAULT GETDATE(),
        FechaFin         DATETIME2    NULL,
        Puntaje          INT          NOT NULL DEFAULT 0,
        TotalPreguntas   INT          NOT NULL DEFAULT 0,
        Completado       BIT          NOT NULL DEFAULT 0,
        DuracionSegundos INT          NULL,     -- NULL = sin límite de tiempo

        CONSTRAINT PK_Examenes      PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Exam_Usuario
            FOREIGN KEY (UsuarioId)    REFERENCES Usuarios(Id),    -- RESTRICT
        CONSTRAINT FK_Exam_TipoExamen
            FOREIGN KEY (TipoExamenId) REFERENCES TiposExamen(Id)  ON DELETE SET NULL,
        CONSTRAINT CK_Examenes_Puntaje CHECK (Puntaje >= 0),
        CONSTRAINT CK_Examenes_Total   CHECK (TotalPreguntas >= 0)
    );
    PRINT '>>> Tabla Examenes creada.';
END
ELSE
BEGIN
    PRINT '>>> Tabla Examenes ya existe.';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Examenes') AND name='TipoExamenId')
    BEGIN
        ALTER TABLE Examenes ADD TipoExamenId INT NULL
            CONSTRAINT FK_Exam_TipoExamen FOREIGN KEY REFERENCES TiposExamen(Id) ON DELETE SET NULL;
        PRINT '    >>> TipoExamenId agregado.';
    END
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Examenes') AND name='DuracionSegundos')
    BEGIN
        ALTER TABLE Examenes ADD DuracionSegundos INT NULL;
        PRINT '    >>> DuracionSegundos agregado.';
    END
END
GO


-- ============================================================
--  SECCIÓN 8 – TABLA PreguntasExamen
--  Sin EsCorrecta ni OrdenAlternativas (columnas eliminadas).
--  El orden de alternativas se persiste en OrdenAlternativaExamen.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'PreguntasExamen') AND type='U')
BEGIN
    CREATE TABLE PreguntasExamen (
        Id                        INT  NOT NULL IDENTITY(1,1),
        ExamenId                  INT  NOT NULL,
        PreguntaId                INT  NOT NULL,
        AlternativaSeleccionadaId INT  NULL,
        Orden                     INT  NOT NULL DEFAULT 0,

        CONSTRAINT PK_PreguntasExamen PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PregEx_Examen
            FOREIGN KEY (ExamenId)   REFERENCES Examenes(Id)     ON DELETE CASCADE,
        CONSTRAINT FK_PregEx_Pregunta
            FOREIGN KEY (PreguntaId) REFERENCES Preguntas(Id),   -- RESTRICT
        CONSTRAINT FK_PregEx_AltSel
            FOREIGN KEY (AlternativaSeleccionadaId)
            REFERENCES Alternativas(Id) ON DELETE SET NULL
    );
    PRINT '>>> Tabla PreguntasExamen creada.';
END
ELSE
    PRINT '>>> Tabla PreguntasExamen ya existe.';
GO


-- ============================================================
--  SECCIÓN 9 – TABLA OrdenAlternativaExamen
--  Persiste el orden aleatorio de las alternativas por pregunta
--  dentro de un examen concreto (reemplaza OrdenAlternativas CSV).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'OrdenAlternativaExamen') AND type='U')
BEGIN
    CREATE TABLE OrdenAlternativaExamen (
        Id               INT  NOT NULL IDENTITY(1,1),
        PreguntaExamenId INT  NOT NULL,
        AlternativaId    INT  NOT NULL,
        Orden            INT  NOT NULL DEFAULT 0,

        CONSTRAINT PK_OrdenAltExamen PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_OrdAlt_PregExamen
            FOREIGN KEY (PreguntaExamenId) REFERENCES PreguntasExamen(Id) ON DELETE CASCADE,
        CONSTRAINT FK_OrdAlt_Alternativa
            FOREIGN KEY (AlternativaId)    REFERENCES Alternativas(Id)    -- RESTRICT
    );
    PRINT '>>> Tabla OrdenAlternativaExamen creada.';
END
ELSE
    PRINT '>>> Tabla OrdenAlternativaExamen ya existe.';
GO


-- ============================================================
--  SECCIÓN 10 – TABLA Noticias
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Noticias') AND type='U')
BEGIN
    CREATE TABLE Noticias (
        Id               INT            NOT NULL IDENTITY(1,1),
        Titulo           NVARCHAR(200)  NOT NULL,
        Contenido        NVARCHAR(MAX)  NOT NULL,
        ImagenRuta       NVARCHAR(500)  NULL,
        EnlaceUrl        NVARCHAR(1000) NULL,
        FechaPublicacion DATETIME2      NOT NULL DEFAULT GETDATE(),
        AdminId          INT            NOT NULL,
        Activo           BIT            NOT NULL DEFAULT 1,

        CONSTRAINT PK_Noticias PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Noticias_Admin
            FOREIGN KEY (AdminId) REFERENCES Usuarios(Id)   -- RESTRICT
    );
    PRINT '>>> Tabla Noticias creada.';
END
ELSE
BEGIN
    PRINT '>>> Tabla Noticias ya existe.';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Noticias') AND name='EnlaceUrl')
    BEGIN
        ALTER TABLE Noticias ADD EnlaceUrl NVARCHAR(1000) NULL;
        PRINT '    >>> EnlaceUrl agregado a Noticias.';
    END
END
GO


-- ============================================================
--  SECCIÓN 11 – TABLA AnunciosGlobales
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'AnunciosGlobales') AND type='U')
BEGIN
    CREATE TABLE AnunciosGlobales (
        Id                 INT            NOT NULL IDENTITY(1,1),
        Mensaje            NVARCHAR(500)  NOT NULL,
        Tipo               NVARCHAR(20)   NOT NULL DEFAULT 'warning',
        Activo             BIT            NOT NULL DEFAULT 0,
        FechaActualizacion DATETIME2      NOT NULL DEFAULT GETDATE(),
        AdminId            INT            NOT NULL,

        CONSTRAINT PK_AnunciosGlobales PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Anuncios_Admin
            FOREIGN KEY (AdminId) REFERENCES Usuarios(Id)  -- RESTRICT
    );
    PRINT '>>> Tabla AnunciosGlobales creada.';
END
ELSE
    PRINT '>>> Tabla AnunciosGlobales ya existe.';
GO


-- ============================================================
--  SECCIÓN 12 – TABLA PlanesSuscripcion + CaracteristicasPlan
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'PlanesSuscripcion') AND type='U')
BEGIN
    CREATE TABLE PlanesSuscripcion (
        Id              INT            NOT NULL IDENTITY(1,1),
        Nombre          NVARCHAR(100)  NOT NULL,
        Etiqueta        NVARCHAR(100)  NOT NULL DEFAULT 'PLAN MENSUAL',
        Precio          DECIMAL(10,2)  NOT NULL DEFAULT 0,
        TextoPrecio     NVARCHAR(50)   NOT NULL DEFAULT 'mensuales',
        ColorPrimario   NVARCHAR(20)   NOT NULL DEFAULT '#74c0fc',
        ColorSecundario NVARCHAR(20)   NOT NULL DEFAULT '#4dabf7',
        EsPopular       BIT            NOT NULL DEFAULT 0,
        TextoBadge      NVARCHAR(200)  NULL,
        EnlaceBoton     NVARCHAR(500)  NOT NULL DEFAULT 'https://wa.me/51936037152',
        TextoBoton      NVARCHAR(80)   NOT NULL DEFAULT '¡Suscribirme ya!',
        Activo          BIT            NOT NULL DEFAULT 1,
        Orden           INT            NOT NULL DEFAULT 0,
        FechaCreacion   DATETIME2      NOT NULL DEFAULT GETDATE(),

        CONSTRAINT PK_PlanesSuscripcion PRIMARY KEY CLUSTERED (Id)
    );
    PRINT '>>> Tabla PlanesSuscripcion creada.';
END
ELSE
    PRINT '>>> Tabla PlanesSuscripcion ya existe.';
GO

-- FK Usuarios.PlanSuscripcionId → PlanesSuscripcion (se declara aquí porque
-- PlanesSuscripcion debe existir antes que la FK que referencia a ella)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Usuarios_PlanSuscripcion')
    ALTER TABLE Usuarios ADD CONSTRAINT FK_Usuarios_PlanSuscripcion
        FOREIGN KEY (PlanSuscripcionId) REFERENCES PlanesSuscripcion(Id) ON DELETE SET NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'CaracteristicasPlan') AND type='U')
BEGIN
    CREATE TABLE CaracteristicasPlan (
        Id                INT           NOT NULL IDENTITY(1,1),
        PlanSuscripcionId INT           NOT NULL,
        Texto             NVARCHAR(300) NOT NULL,
        Orden             INT           NOT NULL DEFAULT 0,

        CONSTRAINT PK_CaracteristicasPlan PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_CaracPlan_Plan
            FOREIGN KEY (PlanSuscripcionId)
            REFERENCES PlanesSuscripcion(Id) ON DELETE CASCADE
    );
    PRINT '>>> Tabla CaracteristicasPlan creada.';
END
ELSE
    PRINT '>>> Tabla CaracteristicasPlan ya existe.';
GO


-- ============================================================
--  SECCIÓN 13 – TABLA Sugerencias
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Sugerencias') AND type='U')
BEGIN
    CREATE TABLE Sugerencias (
        Id         INT            NOT NULL IDENTITY(1,1),
        UsuarioId  INT            NOT NULL,
        Asunto     NVARCHAR(100)  NOT NULL,
        Mensaje    NVARCHAR(2000) NOT NULL,
        FechaEnvio DATETIME2      NOT NULL DEFAULT GETDATE(),
        Leida      BIT            NOT NULL DEFAULT 0,

        CONSTRAINT PK_Sugerencias PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Suger_Usuario
            FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)  -- RESTRICT
    );
    PRINT '>>> Tabla Sugerencias creada.';
END
ELSE
    PRINT '>>> Tabla Sugerencias ya existe.';
GO


-- ============================================================
--  SECCIÓN 14 – TABLA LogsActividad
--  AdminNombre eliminado (se obtiene por navegación a Usuarios).
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'LogsActividad') AND type='U')
BEGIN
    CREATE TABLE LogsActividad (
        Id          INT            NOT NULL IDENTITY(1,1),
        Fecha       DATETIME2      NOT NULL DEFAULT GETDATE(),
        AdminId     INT            NOT NULL,
        Accion      NVARCHAR(100)  NOT NULL,
        Descripcion NVARCHAR(500)  NOT NULL,

        CONSTRAINT PK_LogsActividad PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Logs_Admin
            FOREIGN KEY (AdminId) REFERENCES Usuarios(Id)   -- RESTRICT
    );
    PRINT '>>> Tabla LogsActividad creada.';
END
ELSE
BEGIN
    PRINT '>>> Tabla LogsActividad ya existe.';
    -- Columna legacy AdminNombre → convertir a NULL para no romper INSERT de EF Core
    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_NAME='LogsActividad' AND COLUMN_NAME='AdminNombre' AND IS_NULLABLE='NO')
    BEGIN
        ALTER TABLE LogsActividad ALTER COLUMN AdminNombre NVARCHAR(100) NULL;
        PRINT '    >>> LogsActividad.AdminNombre convertido a NULL.';
    END
END
GO


-- ============================================================
--  SECCIÓN 15 – ÍNDICES DE RENDIMIENTO
-- ============================================================

-- Examenes: búsquedas por usuario y estado
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Examenes_UsuarioId_Completado' AND object_id=OBJECT_ID('Examenes'))
    CREATE NONCLUSTERED INDEX IX_Examenes_UsuarioId_Completado
        ON Examenes (UsuarioId, Completado)
        INCLUDE (TipoExamenId, Puntaje, TotalPreguntas, FechaInicio, FechaFin);
GO

-- PreguntasExamen: carga de preguntas de un examen
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_PreguntasExamen_ExamenId' AND object_id=OBJECT_ID('PreguntasExamen'))
    CREATE NONCLUSTERED INDEX IX_PreguntasExamen_ExamenId
        ON PreguntasExamen (ExamenId)
        INCLUDE (PreguntaId, AlternativaSeleccionadaId, Orden);
GO

-- OrdenAlternativaExamen: carga del orden por preguntaExamen
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_OrdenAlt_PreguntaExamenId' AND object_id=OBJECT_ID('OrdenAlternativaExamen'))
    CREATE NONCLUSTERED INDEX IX_OrdenAlt_PreguntaExamenId
        ON OrdenAlternativaExamen (PreguntaExamenId)
        INCLUDE (AlternativaId, Orden);
GO

-- Alternativas: carga de alternativas por pregunta
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Alternativas_PreguntaId' AND object_id=OBJECT_ID('Alternativas'))
    CREATE NONCLUSTERED INDEX IX_Alternativas_PreguntaId
        ON Alternativas (PreguntaId) INCLUDE (TextoAlternativa, EsCorrecta);
GO

-- Preguntas: filtro por tipo y estado activo
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Preguntas_TipoActivo' AND object_id=OBJECT_ID('Preguntas'))
    CREATE NONCLUSTERED INDEX IX_Preguntas_TipoActivo
        ON Preguntas (TipoExamenId, Activo) INCLUDE (TextoPregunta, FechaCreacion);
GO

-- Usuarios: login y listado
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Usuarios_NombreActivo' AND object_id=OBJECT_ID('Usuarios'))
    CREATE NONCLUSTERED INDEX IX_Usuarios_NombreActivo
        ON Usuarios (NombreUsuario, Activo) INCLUDE (Contrasena, Rol, Id, Discriminador);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Usuarios_RolActivo' AND object_id=OBJECT_ID('Usuarios'))
    CREATE NONCLUSTERED INDEX IX_Usuarios_RolActivo
        ON Usuarios (Rol, Activo, Discriminador) INCLUDE (NombreUsuario, Correo, FechaCreacion);
GO

-- UsuarioTiposExamen: acceso por usuario
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_UsuTipo_UsuarioId' AND object_id=OBJECT_ID('UsuarioTiposExamen'))
    CREATE NONCLUSTERED INDEX IX_UsuTipo_UsuarioId
        ON UsuarioTiposExamen (UsuarioId) INCLUDE (TipoExamenId, FechaAsignacion);
GO

-- Noticias: listado activo ordenado por fecha
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Noticias_ActivoFecha' AND object_id=OBJECT_ID('Noticias'))
    CREATE NONCLUSTERED INDEX IX_Noticias_ActivoFecha
        ON Noticias (Activo, FechaPublicacion DESC);
GO

-- LogsActividad: listado por fecha
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_Logs_Fecha' AND object_id=OBJECT_ID('LogsActividad'))
    CREATE NONCLUSTERED INDEX IX_Logs_Fecha
        ON LogsActividad (Fecha DESC) INCLUDE (AdminId, Accion, Descripcion);
GO

PRINT '>>> Índices creados / verificados.';
GO


-- ============================================================
--  SECCIÓN 16 – VERIFICACIÓN FINAL
-- ============================================================
SELECT
    t.name                     AS Tabla,
    p.rows                     AS Filas,
    SUM(a.total_pages) * 8     AS KB_Usado
FROM
    sys.tables t
    JOIN sys.partitions        p ON t.object_id = p.object_id AND p.index_id IN (0,1)
    JOIN sys.allocation_units  a ON p.partition_id = a.container_id
GROUP BY t.name, p.rows
ORDER BY t.name;
GO

PRINT '================================================================';
PRINT '  SimulacroExamenDB v5.0 – Script ejecutado correctamente      ';
PRINT '  Tablas: Usuarios (TPH), TiposExamen, UsuarioTiposExamen,     ';
PRINT '          Preguntas, Alternativas, Examenes, PreguntasExamen,  ';
PRINT '          OrdenAlternativaExamen, Noticias, AnunciosGlobales,  ';
PRINT '          PlanesSuscripcion, CaracteristicasPlan,              ';
PRINT '          Sugerencias, LogsActividad                           ';
PRINT '  Contraseñas: BCrypt hash (work factor 11)                    ';
PRINT '  Admin/SuperAdmin: sembrado automáticamente por Program.cs    ';
PRINT '================================================================';
GO
