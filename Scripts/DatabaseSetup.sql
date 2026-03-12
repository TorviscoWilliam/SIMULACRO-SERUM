-- ============================================================
--  SIMULACRO SERUM  –  Script de Creación Completa de BD
--  SQL Server 2019/2022  |  Collation: Modern_Spanish_CI_AS
--  Versión : 3.0  (incluye TiposExamen, UsuarioTiposExamen,
--                  TipoExamenId en Preguntas/Examenes,
--                  NumeroPreguntas en TiposExamen)
--
--  INSTRUCCIONES DE USO
--  ─────────────────────────────────────────────────────────
--  OPCIÓN A – BD nueva o re-creación total (desde cero):
--    1. Descomentar y ejecutar la SECCIÓN 0 (DROP).
--    2. Ejecutar desde la SECCIÓN 1 hasta el final.
--    3. Iniciar la aplicación → Program.cs siembra el admin
--       automáticamente con BCrypt.
--
--  OPCIÓN B – BD ya existente (solo cambios incrementales):
--    - Ignorar la SECCIÓN 0.
--    - Ejecutar solo las secciones de tablas que falten.
--    - Para agregar NumeroPreguntas a TiposExamen existente,
--      ver el bloque ALTER TABLE en la SECCIÓN 3.
--
--  CONTRASEÑAS  ──  MUY IMPORTANTE
--  ─────────────────────────────────────────────────────────
--  NUNCA se almacenan contraseñas en texto plano.
--  Se usa hash BCrypt (work factor 11, ≈60 caracteres).
--  La columna Contrasena guarda el hash, no la clave.
--
--  El dato semilla del administrador lo inserta la
--  aplicación automáticamente (Program.cs) usando:
--      BCrypt.Net.BCrypt.HashPassword("Admin123!", 11)
--
--  Para insertar manualmente, vea la SECCIÓN 11.
-- ============================================================


-- ============================================================
--  SECCIÓN 0 – DROP COMPLETO (descomentar para re-creación)
-- ============================================================
/*
USE SimulacroExamenDB;
GO
-- Eliminar en orden inverso por dependencias de FK
DROP TABLE IF EXISTS PreguntasExamen;
DROP TABLE IF EXISTS Alternativas;
DROP TABLE IF EXISTS Examenes;
DROP TABLE IF EXISTS Preguntas;
DROP TABLE IF EXISTS UsuarioTiposExamen;
DROP TABLE IF EXISTS TiposExamen;
DROP TABLE IF EXISTS Usuarios;
GO

-- Para eliminar la BD completa:
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
--  DIAGRAMA ENTIDAD-RELACIÓN (ERD v3.0)
-- ════════════════════════════════════════════════════════════
--
--  ┌──────────────────────┐    ┌───────────────────────────┐
--  │      Usuarios        │    │       TiposExamen         │
--  ├──────────────────────┤    ├───────────────────────────┤
--  │PK Id                 │    │PK Id                      │
--  │   NombreUsuario UNIQ │    │   Nombre         UNIQUE   │
--  │   Correo        UNIQ │    │   NumeroPreguntas INT=4   │
--  │   Contrasena (BCrypt)│    │   Activo         BIT=1    │
--  │   Rol  Admin|Usuario │    └──────────┬────────────────┘
--  │   FechaCreacion      │              │1
--  │   Activo BIT=1       │              │ CASCADE→UsuarioTiposExamen
--  └───┬──────────────────┘              │ SET NULL→Preguntas / Examenes
--      │1                               N│
--      │         ┌──────────────────────►┘
--      │         │  ┌───────────────────────────────┐
--      │         └─►│     UsuarioTiposExamen        │
--      │            ├───────────────────────────────┤
--      │            │PK Id                          │
--      │            │FK UsuarioId   → Usuarios      │
--      │            │FK TipoExamenId→ TiposExamen   │
--      │            │   FechaAsignacion             │
--      │            └───────────────────────────────┘
--      │
--      │  RESTRICT DELETE (no borrar usuario con exámenes)
--      │
--      ▼N
--  ┌────────────────────────────────────────────────────┐
--  │                    Examenes                        │
--  ├────────────────────────────────────────────────────┤
--  │PK Id                                               │
--  │FK UsuarioId       → Usuarios    (RESTRICT)         │
--  │FK TipoExamenId    → TiposExamen (SET NULL)         │
--  │   FechaInicio   FechaFin(NULL)                     │
--  │   Puntaje  TotalPreguntas  Completado              │
--  └─────────────────────────┬──────────────────────────┘
--                            │1  CASCADE DELETE
--                            ▼N
--  ┌────────────────────────────────────────────────────┐
--  │                 PreguntasExamen                    │
--  ├────────────────────────────────────────────────────┤
--  │PK Id                                               │
--  │FK ExamenId               → Examenes (CASCADE)      │
--  │FK PreguntaId             → Preguntas (RESTRICT)    │
--  │FK AlternativaSeleccionadaId → Alternativas (SNULL) │
--  │   EsCorrecta  Orden  OrdenAlternativas             │
--  └────────────────────────────────────────────────────┘
--
--  ┌───────────────────────┐  1:N CASCADE  ┌──────────────────┐
--  │       Preguntas       │──────────────►│   Alternativas   │
--  ├───────────────────────┤               ├──────────────────┤
--  │PK Id                  │               │PK Id             │
--  │FK TipoExamenId (NULL) │               │FK PreguntaId     │
--  │   TextoPregunta  MAX  │               │   TextoAlt  MAX  │
--  │   FechaCreacion       │               │   EsCorrecta BIT │
--  │   Activo BIT=1        │               └──────────────────┘
--  └───────────────────────┘
--
-- ============================================================


-- ============================================================
--  SECCIÓN 2 – TABLA Usuarios
--  Contrasena guarda SIEMPRE un hash BCrypt, nunca texto plano.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Usuarios') AND type='U')
BEGIN
    CREATE TABLE Usuarios (
        Id              INT           NOT NULL IDENTITY(1,1),
        NombreUsuario   NVARCHAR(100) NOT NULL,
        Correo          NVARCHAR(200) NOT NULL,
        Contrasena      NVARCHAR(255) NOT NULL,   -- Hash BCrypt (~60 chars)
        Rol             NVARCHAR(20)  NOT NULL DEFAULT 'Usuario',
        FechaCreacion   DATETIME2(0)  NOT NULL DEFAULT GETDATE(),
        Activo          BIT           NOT NULL DEFAULT 1,

        CONSTRAINT PK_Usuarios           PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Usuarios_Nombre    UNIQUE (NombreUsuario),
        CONSTRAINT UQ_Usuarios_Correo    UNIQUE (Correo),
        CONSTRAINT CK_Usuarios_Rol       CHECK (Rol IN ('Admin','Usuario'))
    );
    PRINT '>>> Tabla Usuarios creada.';
END
ELSE
    PRINT '>>> Tabla Usuarios ya existe.';
GO


-- ============================================================
--  SECCIÓN 3 – TABLA TiposExamen
--  NumeroPreguntas: cuántas preguntas aleatorias se asignan
--  cuando el usuario inicia un examen de este tipo.
--  (Configurable en Admin → Tipos de Examen)
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

    -- Migración incremental: agregar NumeroPreguntas si no existe
    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE  object_id = OBJECT_ID('TiposExamen') AND name = 'NumeroPreguntas'
    )
    BEGIN
        ALTER TABLE TiposExamen
            ADD NumeroPreguntas INT NOT NULL
            CONSTRAINT DF_TiposExamen_NumPre DEFAULT 4,
            CONSTRAINT CK_TiposExamen_NumPre CHECK (NumeroPreguntas BETWEEN 1 AND 200);
        PRINT '    >>> Columna NumeroPreguntas agregada a TiposExamen.';
    END
END
GO


-- ============================================================
--  SECCIÓN 4 – TABLA UsuarioTiposExamen
--  Controla a qué tipos de examen tiene acceso cada usuario.
--  Sin una fila aquí, el usuario no ve ese tipo en su panel.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'UsuarioTiposExamen') AND type='U')
BEGIN
    CREATE TABLE UsuarioTiposExamen (
        Id               INT          NOT NULL IDENTITY(1,1),
        UsuarioId        INT          NOT NULL,
        TipoExamenId     INT          NOT NULL,
        FechaAsignacion  DATETIME2(0) NOT NULL DEFAULT GETDATE(),

        CONSTRAINT PK_UsuarioTiposExamen  PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_UsuTipo_Par         UNIQUE (UsuarioId, TipoExamenId),
        CONSTRAINT FK_UsuTipo_Usuario
            FOREIGN KEY (UsuarioId)   REFERENCES Usuarios(Id),    -- RESTRICT
        CONSTRAINT FK_UsuTipo_Tipo
            FOREIGN KEY (TipoExamenId) REFERENCES TiposExamen(Id) -- RESTRICT
    );
    PRINT '>>> Tabla UsuarioTiposExamen creada.';
END
ELSE
    PRINT '>>> Tabla UsuarioTiposExamen ya existe.';
GO


-- ============================================================
--  SECCIÓN 5 – TABLA Preguntas
--  Soft delete: Activo=0 saca la pregunta del banco sin
--  borrar el historial de exámenes ya realizados.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Preguntas') AND type='U')
BEGIN
    CREATE TABLE Preguntas (
        Id              INT           NOT NULL IDENTITY(1,1),
        TipoExamenId    INT           NULL,
        TextoPregunta   NVARCHAR(MAX) NOT NULL,
        FechaCreacion   DATETIME2(0)  NOT NULL DEFAULT GETDATE(),
        Activo          BIT           NOT NULL DEFAULT 1,

        CONSTRAINT PK_Preguntas      PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Preg_TipoExamen
            FOREIGN KEY (TipoExamenId)
            REFERENCES TiposExamen(Id)
            ON DELETE SET NULL  -- Al borrar el tipo, la pregunta queda sin tipo
    );
    PRINT '>>> Tabla Preguntas creada.';
END
ELSE
BEGIN
    PRINT '>>> Tabla Preguntas ya existe.';

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE  object_id = OBJECT_ID('Preguntas') AND name = 'TipoExamenId'
    )
    BEGIN
        ALTER TABLE Preguntas ADD TipoExamenId INT NULL
            CONSTRAINT FK_Preg_TipoExamen
                FOREIGN KEY REFERENCES TiposExamen(Id) ON DELETE SET NULL;
        PRINT '    >>> Columna TipoExamenId agregada a Preguntas.';
    END
END
GO


-- ============================================================
--  SECCIÓN 6 – TABLA Alternativas
--  2 a 4 alternativas por pregunta; exactamente una con
--  EsCorrecta = 1.  CASCADE DELETE: si la pregunta se borra
--  físicamente, sus alternativas desaparecen también.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Alternativas') AND type='U')
BEGIN
    CREATE TABLE Alternativas (
        Id                INT           NOT NULL IDENTITY(1,1),
        PreguntaId        INT           NOT NULL,
        TextoAlternativa  NVARCHAR(MAX) NOT NULL,
        EsCorrecta        BIT           NOT NULL DEFAULT 0,

        CONSTRAINT PK_Alternativas   PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Alt_Pregunta
            FOREIGN KEY (PreguntaId)
            REFERENCES Preguntas(Id)
            ON DELETE CASCADE
    );
    PRINT '>>> Tabla Alternativas creada.';
END
ELSE
    PRINT '>>> Tabla Alternativas ya existe.';
GO


-- ============================================================
--  SECCIÓN 7 – TABLA Examenes
--  RESTRICT en UsuarioId: no se puede borrar un usuario con
--  exámenes → use Activo=0 en Usuarios para inhabilitarlo.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'Examenes') AND type='U')
BEGIN
    CREATE TABLE Examenes (
        Id              INT          NOT NULL IDENTITY(1,1),
        UsuarioId       INT          NOT NULL,
        TipoExamenId    INT          NULL,
        FechaInicio     DATETIME2(0) NOT NULL DEFAULT GETDATE(),
        FechaFin        DATETIME2(0) NULL,        -- NULL = examen en progreso
        Puntaje         INT          NOT NULL DEFAULT 0,
        TotalPreguntas  INT          NOT NULL DEFAULT 0,
        Completado      BIT          NOT NULL DEFAULT 0,

        CONSTRAINT PK_Examenes         PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Exam_Usuario
            FOREIGN KEY (UsuarioId)
            REFERENCES Usuarios(Id),              -- RESTRICT por defecto
        CONSTRAINT FK_Exam_TipoExamen
            FOREIGN KEY (TipoExamenId)
            REFERENCES TiposExamen(Id)
            ON DELETE SET NULL,
        CONSTRAINT CK_Examenes_Puntaje CHECK (Puntaje >= 0),
        CONSTRAINT CK_Examenes_Total   CHECK (TotalPreguntas >= 0),
        CONSTRAINT CK_Examenes_PxleT   CHECK (Puntaje <= TotalPreguntas)
    );
    PRINT '>>> Tabla Examenes creada.';
END
ELSE
BEGIN
    PRINT '>>> Tabla Examenes ya existe.';

    IF NOT EXISTS (
        SELECT 1 FROM sys.columns
        WHERE  object_id = OBJECT_ID('Examenes') AND name = 'TipoExamenId'
    )
    BEGIN
        ALTER TABLE Examenes ADD TipoExamenId INT NULL
            CONSTRAINT FK_Exam_TipoExamen
                FOREIGN KEY REFERENCES TiposExamen(Id) ON DELETE SET NULL;
        PRINT '    >>> Columna TipoExamenId agregada a Examenes.';
    END
END
GO


-- ============================================================
--  SECCIÓN 8 – TABLA PreguntasExamen
--  Registra qué preguntas se asignaron a cada intento,
--  en qué orden, qué respondió el usuario y si acertó.
--
--  OrdenAlternativas: "id1,id2,id3" → orden aleatorio
--  persistido para que no cambie si se recarga la página.
--  EsCorrecta: snapshot histórico inmutable.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'PreguntasExamen') AND type='U')
BEGIN
    CREATE TABLE PreguntasExamen (
        Id                        INT          NOT NULL IDENTITY(1,1),
        ExamenId                  INT          NOT NULL,
        PreguntaId                INT          NOT NULL,
        AlternativaSeleccionadaId INT          NULL,     -- NULL = no respondida
        EsCorrecta                BIT          NOT NULL DEFAULT 0,
        Orden                     INT          NOT NULL DEFAULT 0,
        OrdenAlternativas         NVARCHAR(500) NOT NULL DEFAULT '',

        CONSTRAINT PK_PreguntasExamen  PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_PregEx_Orden     CHECK (Orden >= 0),

        CONSTRAINT FK_PregEx_Examen
            FOREIGN KEY (ExamenId)   REFERENCES Examenes(Id)    ON DELETE CASCADE,
        CONSTRAINT FK_PregEx_Pregunta
            FOREIGN KEY (PreguntaId) REFERENCES Preguntas(Id),  -- RESTRICT
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
--  SECCIÓN 9 – ÍNDICES DE RENDIMIENTO
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name='IX_Examenes_UsuarioId_Completado' AND object_id=OBJECT_ID('Examenes'))
    CREATE NONCLUSTERED INDEX IX_Examenes_UsuarioId_Completado
        ON Examenes (UsuarioId, Completado)
        INCLUDE (TipoExamenId, Puntaje, TotalPreguntas, FechaInicio, FechaFin);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name='IX_PreguntasExamen_ExamenId' AND object_id=OBJECT_ID('PreguntasExamen'))
    CREATE NONCLUSTERED INDEX IX_PreguntasExamen_ExamenId
        ON PreguntasExamen (ExamenId)
        INCLUDE (PreguntaId, AlternativaSeleccionadaId, EsCorrecta, Orden, OrdenAlternativas);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name='IX_Alternativas_PreguntaId' AND object_id=OBJECT_ID('Alternativas'))
    CREATE NONCLUSTERED INDEX IX_Alternativas_PreguntaId
        ON Alternativas (PreguntaId) INCLUDE (TextoAlternativa, EsCorrecta);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name='IX_Preguntas_TipoActivo' AND object_id=OBJECT_ID('Preguntas'))
    CREATE NONCLUSTERED INDEX IX_Preguntas_TipoActivo
        ON Preguntas (TipoExamenId, Activo) INCLUDE (TextoPregunta, FechaCreacion);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name='IX_Usuarios_NombreActivo' AND object_id=OBJECT_ID('Usuarios'))
    CREATE NONCLUSTERED INDEX IX_Usuarios_NombreActivo
        ON Usuarios (NombreUsuario, Activo) INCLUDE (Contrasena, Rol, Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name='IX_Usuarios_RolActivo' AND object_id=OBJECT_ID('Usuarios'))
    CREATE NONCLUSTERED INDEX IX_Usuarios_RolActivo
        ON Usuarios (Rol, Activo) INCLUDE (NombreUsuario, Correo, FechaCreacion);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name='IX_UsuTipo_UsuarioId' AND object_id=OBJECT_ID('UsuarioTiposExamen'))
    CREATE NONCLUSTERED INDEX IX_UsuTipo_UsuarioId
        ON UsuarioTiposExamen (UsuarioId) INCLUDE (TipoExamenId, FechaAsignacion);
GO

PRINT '>>> Índices creados.';
GO


-- ============================================================
--  SECCIÓN 10 – SEMILLA: 17 TIPOS DE EXAMEN
-- ============================================================
INSERT INTO TiposExamen (Nombre, NumeroPreguntas, Activo)
SELECT n, 4, 1
FROM (VALUES
    ('BIOLOGÍA'),
    ('ENFERMERÍA'),
    ('FARMACIA Y BIOQUÍMICA'),
    ('ING.SANITARIA'),
    ('MEDICINA VETERINARIA'),
    ('ODONTOLOGÍA'),
    ('PSICOLOGÍA'),
    ('TRABAJO SOCIAL'),
    ('TM. LABORATORIO'),
    ('MEDICINA'),
    ('NUTRICIÓN'),
    ('TM. OPTOMETRÍA'),
    ('TM. RADIOLOGÍA'),
    ('TM. T. FÍSICA'),
    ('TM. T. LENGUAJE'),
    ('TM.T.OCUPACIONAL'),
    ('OBSTETRICIA')
) AS src(n)
WHERE NOT EXISTS (SELECT 1 FROM TiposExamen WHERE Nombre = src.n);

PRINT '>>> Tipos de examen sembrados.';
GO


-- ============================================================
--  SECCIÓN 11 – SEMILLA: ADMINISTRADOR  (contraseña cifrada)
--
--  CONTRASEÑA  ──  BCrypt, NUNCA texto plano
--  ─────────────────────────────────────────────────────────
--  La aplicación inserta este usuario automáticamente al
--  arrancar si no existe ningún Admin (Program.cs).
--  Usa:  BCrypt.Net.BCrypt.HashPassword("Admin123!", 11)
--
--  Si prefiere insertarlo MANUAL:
--    1. Genere el hash en .NET / C# / PowerShell:
--
--       [Opción C#]
--       using BCrypt.Net;
--       Console.WriteLine(BCrypt.HashPassword("Admin123!", 11));
--
--       [Opción PowerShell con el paquete BCrypt.Net-Next]
--       Add-Type -Path ".\BCrypt.Net-Next.dll"
--       [BCrypt.Net.BCrypt]::HashPassword("Admin123!", 11)
--
--    2. El resultado tiene la forma:
--       $2a$11$<22 chars salt><31 chars hash>
--       Ejemplo (NO usar este hash):
--       $2a$11$abcdefghijklmnopqrstuvABCDEFGHIJKLMNOPQRST0123456789a
--
--    3. Reemplace el placeholder y descomente el INSERT.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Rol = 'Admin')
BEGIN
    PRINT '>>> Sin administrador.  Inicie la app o inserte manualmente.';

    -- Descomentar tras generar el hash BCrypt:
    /*
    INSERT INTO Usuarios (NombreUsuario, Correo, Contrasena, Rol, FechaCreacion, Activo)
    VALUES (
        'admin',
        'admin@simulacro.com',
        '$2a$11$REEMPLAZAR_CON_HASH_GENERADO_POR_BCrypt.HashPassword',
        'Admin',
        GETDATE(),
        1
    );
    */
END
ELSE
    PRINT '>>> Administrador ya existe.';
GO


-- ============================================================
--  SECCIÓN 12 – VERIFICACIÓN FINAL
-- ============================================================
SELECT
    t.name          AS Tabla,
    p.rows          AS Filas_Actuales,
    SUM(a.total_pages) * 8 AS KB_Usado
FROM
    sys.tables     t
    JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0,1)
    JOIN sys.allocation_units a ON p.partition_id = a.container_id
GROUP BY t.name, p.rows
ORDER BY t.name;
GO

PRINT '==============================================================';
PRINT '  SimulacroExamenDB v3.0 – Script ejecutado correctamente    ';
PRINT '  Tablas: Usuarios, TiposExamen, UsuarioTiposExamen,         ';
PRINT '          Preguntas, Alternativas, Examenes, PreguntasExamen ';
PRINT '  Contraseñas: BCrypt hash (work factor 11) – nunca texto    ';
PRINT '==============================================================';
GO
