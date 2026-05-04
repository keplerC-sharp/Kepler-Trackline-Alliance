-- =============================================================
-- KEPLER / TRACKLINE ALLIANCE — Base de datos esencial
-- App de gestión de cola con prioridades
-- 6 tablas fundamentales
-- =============================================================

CREATE DATABASE IF NOT EXISTS kepler_core
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE kepler_core;

-- =============================================================
-- 1. OPERATORS
--    Persona que gestiona y controla la cola desde el sistema
-- =============================================================
CREATE TABLE operators (
  id          INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  identifier  VARCHAR(50)  NOT NULL UNIQUE,   -- ID_ALPHA_00
  full_name   VARCHAR(120) NOT NULL,
  password_hash VARCHAR(255) NOT NULL,
  role        ENUM('ADMIN', 'OPERATOR') NOT NULL DEFAULT 'OPERATOR',
  created_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;


-- =============================================================
-- 2. SESSIONS
--    Una sesión es una jornada de atención abierta por el operador.
--    Agrupa toda la cola y el flujo de un período de trabajo.
-- =============================================================
CREATE TABLE sessions (
  id           INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  operator_id  INT UNSIGNED NOT NULL,
  session_code VARCHAR(30)  NOT NULL UNIQUE,  -- SESSION_01, KP-SESS-2024
  status       ENUM('STANDBY','LIVE','COMPLETED','TERMINATED')
                 NOT NULL DEFAULT 'STANDBY',
  started_at   DATETIME,
  ended_at     DATETIME,
  created_at   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (operator_id) REFERENCES operators(id) ON DELETE RESTRICT
) ENGINE=InnoDB;


-- =============================================================
-- 3. PARTICIPANTS
--    Personas que se registran para ingresar a la cola.
--    Equivale a "pilotos" en el diseño — Race Grid & Entry List.
-- =============================================================
CREATE TABLE participants (
  id             INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  full_name      VARCHAR(120) NOT NULL,
  grid_id        VARCHAR(20)  NOT NULL UNIQUE,  -- KR-460-THX
  grade          ENUM('S', 'A', 'B') NOT NULL DEFAULT 'B',  -- Prioridad base
  season_points  SMALLINT UNSIGNED DEFAULT 0,               -- Puntaje acumulado
  registered_at  DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;


-- =============================================================
-- 4. QUEUE_ENTRIES
--    Corazón del sistema. Cada fila es un turno en la cola.
--
--    Estados del turno:
--      ON_TRACK  → Participante actualmente siendo atendido (EN PISTA)
--      UP_NEXT   → El siguiente en la cola (PRÓXIMO)
--      QUEUED    → En espera (COLA)
--      COMPLETED → Atención finalizada
--      CANCELLED → Retirado de la cola
--
--    Prioridades:
--      HIGH  → Grade S — entra primero (superpasa la FIFO normal)
--      NORMAL → Grade A/B — orden de llegada
-- =============================================================
CREATE TABLE queue_entries (
  id                  INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  session_id          INT UNSIGNED NOT NULL,
  participant_id      INT UNSIGNED NOT NULL,
  position            SMALLINT UNSIGNED NOT NULL,            -- Lugar en la cola
  priority            ENUM('HIGH', 'NORMAL') NOT NULL DEFAULT 'NORMAL',
  status              ENUM('ON_TRACK','UP_NEXT','QUEUED','COMPLETED','CANCELLED')
                        NOT NULL DEFAULT 'QUEUED',
  estimated_start_s   DECIMAL(9,2),                         -- Tiempo estimado de inicio (seg)
  session_time_s      DECIMAL(9,2),                         -- Tiempo real de atención (seg)
  entered_at          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  started_at          DATETIME,                             -- Cuando pasó a ON_TRACK
  completed_at        DATETIME,                             -- Cuando terminó
  FOREIGN KEY (session_id)     REFERENCES sessions(id)     ON DELETE CASCADE,
  FOREIGN KEY (participant_id) REFERENCES participants(id) ON DELETE RESTRICT,
  UNIQUE KEY uq_session_participant (session_id, participant_id)
) ENGINE=InnoDB;


-- =============================================================
-- 5. STINT_SLOTS
--    Los slots de la pantalla "Stint Strategy / Stint Queue".
--    Son los espacios físicos de atención que el operador gestiona
--    (máx. N slots activos al mismo tiempo: ON_TRACK, UP_NEXT, etc.)
--
--    El operador arrastra (drop) un queue_entry a un slot.
-- =============================================================
CREATE TABLE stint_slots (
  id              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  session_id      INT UNSIGNED NOT NULL,
  queue_entry_id  INT UNSIGNED,                             -- NULL = slot vacío
  slot_order      TINYINT UNSIGNED NOT NULL,               -- 1=ON_TRACK, 2=UP_NEXT, 3+
  slot_status     ENUM('ACTIVE','QUEUED','HOLD','BLOCKED','EMPTY')
                    NOT NULL DEFAULT 'EMPTY',
  assigned_at     DATETIME,
  FOREIGN KEY (session_id)     REFERENCES sessions(id)      ON DELETE CASCADE,
  FOREIGN KEY (queue_entry_id) REFERENCES queue_entries(id) ON DELETE SET NULL
) ENGINE=InnoDB;


-- =============================================================
-- 6. SESSION_LOG
--    Registro cronológico de acciones del operador sobre la sesión.
--    Equivale al "Chronological Race Log" del diseño.
-- =============================================================
CREATE TABLE session_log (
  id          INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
  session_id  INT UNSIGNED NOT NULL,
  operator_id INT UNSIGNED,
  action_type ENUM(
    'SESSION_STARTED',
    'SESSION_ENDED',
    'ENTRY_ADDED',
    'ENTRY_PROMOTED',    -- Subió prioridad
    'ENTRY_MOVED',       -- Cambió de posición
    'ENTRY_ON_TRACK',    -- Pasó a atención
    'ENTRY_COMPLETED',
    'ENTRY_CANCELLED',
    'SLOT_ASSIGNED',
    'SLOT_BLOCKED'
  ) NOT NULL,
  notes       TEXT,
  created_at  DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  FOREIGN KEY (session_id)  REFERENCES sessions(id)   ON DELETE CASCADE,
  FOREIGN KEY (operator_id) REFERENCES operators(id)  ON DELETE SET NULL
) ENGINE=InnoDB;


-- =============================================================
-- ÍNDICES
-- =============================================================
CREATE INDEX idx_queue_session_status   ON queue_entries(session_id, status);
CREATE INDEX idx_queue_session_position ON queue_entries(session_id, position);
CREATE INDEX idx_stint_session          ON stint_slots(session_id, slot_order);
CREATE INDEX idx_log_session_time       ON session_log(session_id, created_at);


-- =============================================================
-- DATOS SEMILLA
-- =============================================================

INSERT INTO operators (identifier, full_name, password_hash, role) VALUES
('ID_ALPHA_00', 'Control Principal', '$2b$12$placeholder_hash', 'ADMIN'),
('ID_BETA_01',  'Operador Trackline','$2b$12$placeholder_hash', 'OPERATOR');

INSERT INTO participants (full_name, grid_id, grade, season_points) VALUES
('V. Rossi',      'KR-460-THX', 'S', 382),
('S. Perez',      'KR-110-RBX', 'A', 254),
('L. Hamilton',   'KR-440-MBZ', 'S', 341),
('M. Verstappen', 'KR-330-RBX', 'S', 450),
('K. Sainz',      'KR-550-FER', 'B', 198),
('S. Volkov',     'KR-002-VOL', 'S', 180),
('A. Chen',       'KR-003-CHE', 'A', 156),
('J. Webster',    'KR-004-WEB', 'B', 132),
('M. Rossi',      'KR-005-ROS', 'A', 220);

INSERT INTO sessions (operator_id, session_code, status, started_at) VALUES
(1, 'SESSION_01', 'LIVE', NOW());

-- Cola de ejemplo: 1 en atención, 1 próximo, 3 en espera
INSERT INTO queue_entries (session_id, participant_id, position, priority, status, session_time_s, estimated_start_s) VALUES
(1, 4, 1, 'HIGH',   'ON_TRACK', 494.22, NULL),   -- Verstappen — atendiendo ahora
(1, 6, 2, 'HIGH',   'UP_NEXT',  NULL,   150.0),   -- Volkov — próximo (est. 2:30)
(1, 7, 3, 'NORMAL', 'QUEUED',   NULL,   645.0),   -- Chen — cola pos 03 (10:45)
(1, 8, 4, 'NORMAL', 'QUEUED',   NULL,   1270.0),  -- Webster — cola pos 04 (21:10)
(1, 9, 5, 'NORMAL', 'QUEUED',   NULL,   2100.0);  -- M. Rossi — cola pos 05 (35:00)

-- Slots de la pantalla Stint
INSERT INTO stint_slots (session_id, queue_entry_id, slot_order, slot_status, assigned_at) VALUES
(1, 1, 1, 'ACTIVE',  NOW()),   -- Slot 1: ON_TRACK  → Verstappen
(1, 2, 2, 'QUEUED',  NOW()),   -- Slot 2: UP_NEXT   → Volkov
(1, NULL, 3, 'EMPTY', NULL),   -- Slot 3: vacío
(1, 5, 4, 'BLOCKED', NOW());   -- Slot 4: BLOCKED   → Hamilton (posición 5 → aquí por prioridad S)

-- Log inicial
INSERT INTO session_log (session_id, operator_id, action_type, notes) VALUES
(1, 1, 'SESSION_STARTED',  'Sesión iniciada — MISSION_CONTROL // GRID_SYNC_01'),
(1, 1, 'ENTRY_ADDED',      'Participante KR-330-RBX registrado en cola'),
(1, 1, 'ENTRY_ON_TRACK',   'KR-330-RBX pasó a ON_TRACK — atención iniciada'),
(1, 1, 'ENTRY_PROMOTED',   'KR-002-VOL promovido a UP_NEXT'),
(1, 1, 'SLOT_BLOCKED',     'Slot 4 bloqueado — KR-440-MBZ en espera de autorización');

-- =============================================================
-- VISTA ÚTIL: Estado actual de la cola en una sesión
-- =============================================================
CREATE VIEW vw_queue_status AS
SELECT
  qe.position,
  p.full_name,
  p.grid_id,
  p.grade,
  p.season_points,
  qe.priority,
  qe.status,
  qe.estimated_start_s,
  qe.session_time_s,
  qe.entered_at,
  qe.started_at
FROM queue_entries qe
JOIN participants p ON qe.participant_id = p.id
ORDER BY
  FIELD(qe.status, 'ON_TRACK','UP_NEXT','QUEUED','COMPLETED','CANCELLED'),
  qe.position;

-- =============================================================
-- FIN — KEPLER CORE v1.0
-- =============================================================