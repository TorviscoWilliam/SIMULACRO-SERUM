-- ============================================================
-- Simulacro SERUM - Script de Inicialización de Base de Datos
-- SQL Server 2022
-- ============================================================
-- Nota: Este script es ALTERNATIVO al modo Code First automático.
-- La aplicación crea la BD automáticamente con Database.EnsureCreated().
-- Úselo solo si prefiere crear la BD manualmente.
-- ============================================================

USE master;
GO

-- Crear base de datos si no existe
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'SimulacroExamenDB')
BEGIN
    CREATE DATABASE SimulacroExamenDB;
    PRINT 'Base de datos SimulacroExamenDB creada.';
END
GO

USE SimulacroExamenDB;
GO

-- ── Tabla: Usuarios ──────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Usuarios' AND xtype='U')
BEGIN
    CREATE TABLE Usuarios (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        NombreUsuario NVARCHAR(100)  NOT NULL,
        Correo        NVARCHAR(200)  NOT NULL,
        Contrasena    NVARCHAR(255)  NOT NULL,
        Rol           NVARCHAR(20)   NOT NULL DEFAULT 'Usuario',
        FechaCreacion DATETIME2      NOT NULL DEFAULT GETDATE(),
        Activo        BIT            NOT NULL DEFAULT 1,
        CONSTRAINT UQ_Usuarios_NombreUsuario UNIQUE (NombreUsuario),
        CONSTRAINT UQ_Usuarios_Correo        UNIQUE (Correo)
    );
    PRINT 'Tabla Usuarios creada.';
END
GO

-- ── Tabla: Preguntas ─────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Preguntas' AND xtype='U')
BEGIN
    CREATE TABLE Preguntas (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        TextoPregunta NVARCHAR(MAX)  NOT NULL,
        FechaCreacion DATETIME2      NOT NULL DEFAULT GETDATE(),
        Activo        BIT            NOT NULL DEFAULT 1
    );
    PRINT 'Tabla Preguntas creada.';
END
GO

-- ── Tabla: Alternativas ──────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Alternativas' AND xtype='U')
BEGIN
    CREATE TABLE Alternativas (
        Id               INT IDENTITY(1,1) PRIMARY KEY,
        PreguntaId       INT           NOT NULL,
        TextoAlternativa NVARCHAR(MAX) NOT NULL,
        EsCorrecta       BIT           NOT NULL DEFAULT 0,
        CONSTRAINT FK_Alternativas_Preguntas
            FOREIGN KEY (PreguntaId) REFERENCES Preguntas(Id) ON DELETE CASCADE
    );
    PRINT 'Tabla Alternativas creada.';
END
GO

-- ── Tabla: Examenes ──────────────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Examenes' AND xtype='U')
BEGIN
    CREATE TABLE Examenes (
        Id             INT IDENTITY(1,1) PRIMARY KEY,
        UsuarioId      INT      NOT NULL,
        FechaInicio    DATETIME2 NOT NULL DEFAULT GETDATE(),
        FechaFin       DATETIME2 NULL,
        Puntaje        INT      NOT NULL DEFAULT 0,
        TotalPreguntas INT      NOT NULL DEFAULT 0,
        Completado     BIT      NOT NULL DEFAULT 0,
        CONSTRAINT FK_Examenes_Usuarios
            FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)
    );
    PRINT 'Tabla Examenes creada.';
END
GO

-- ── Tabla: PreguntasExamen ───────────────────────────────────
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PreguntasExamen' AND xtype='U')
BEGIN
    CREATE TABLE PreguntasExamen (
        Id                       INT IDENTITY(1,1) PRIMARY KEY,
        ExamenId                 INT           NOT NULL,
        PreguntaId               INT           NOT NULL,
        AlternativaSeleccionadaId INT          NULL,
        EsCorrecta               BIT           NOT NULL DEFAULT 0,
        Orden                    INT           NOT NULL DEFAULT 0,
        OrdenAlternativas        NVARCHAR(500) NOT NULL DEFAULT '',
        CONSTRAINT FK_PreguntasExamen_Examenes
            FOREIGN KEY (ExamenId)   REFERENCES Examenes(Id) ON DELETE CASCADE,
        CONSTRAINT FK_PreguntasExamen_Preguntas
            FOREIGN KEY (PreguntaId) REFERENCES Preguntas(Id),
        CONSTRAINT FK_PreguntasExamen_Alternativas
            FOREIGN KEY (AlternativaSeleccionadaId) REFERENCES Alternativas(Id)
    );
    PRINT 'Tabla PreguntasExamen creada.';
END
GO

-- ── Datos iniciales: Administrador ───────────────────────────
-- Contraseña: Admin123! (hash BCrypt)
-- NOTA: La aplicación crea este usuario automáticamente al iniciar.
-- Solo ejecute este bloque si crea la BD manualmente.
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Rol = 'Admin')
BEGIN
    PRINT 'Para crear el administrador, inicie la aplicación por primera vez.';
    PRINT 'Se creará automáticamente con usuario: admin / contraseña: Admin123!';
END
GO

-- ── Índices para optimización ────────────────────────────────
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Examenes_UsuarioId')
    CREATE INDEX IX_Examenes_UsuarioId ON Examenes (UsuarioId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_PreguntasExamen_ExamenId')
    CREATE INDEX IX_PreguntasExamen_ExamenId ON PreguntasExamen (ExamenId);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name='IX_Alternativas_PreguntaId')
    CREATE INDEX IX_Alternativas_PreguntaId ON Alternativas (PreguntaId);
GO

PRINT '====================================================';
PRINT 'Base de datos SimulacroExamenDB lista.';
PRINT 'Inicie la aplicación para que el administrador';
PRINT 'se cree automáticamente.';
PRINT '====================================================';
GO
