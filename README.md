# KEPLER / TRACKLINE ALLIANCE — Documentación de Base de Datos

**Versión:** 1.0  
**Motor:** MySQL 8.0+  
**Charset:** utf8mb4 / utf8mb4_unicode_ci  
**Base de datos:** `kepler_core`

---

## Propósito del sistema

KEPLER es una aplicación de **gestión de cola con prioridades**, controlada por un operador. Los participantes se registran y son asignados a una cola FIFO que respeta niveles de prioridad (Grade S, A, B). El operador controla en tiempo real quién está siendo atendido, quién es el siguiente y el estado de cada turno.

---

## Diagrama de relaciones

```
operators ──< sessions ──< queue_entries >── participants
                │                │
                │                └──< stint_slots
                │
                └──< session_log
```

---

## Tablas

### 1. `operators`

Usuarios del sistema que abren sesiones y gestionan la cola.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `identifier` | VARCHAR(50) UNIQUE | Código de operador (`ID_ALPHA_00`) |
| `full_name` | VARCHAR(120) | Nombre completo |
| `password_hash` | VARCHAR(255) | Contraseña hasheada (bcrypt) |
| `role` | ENUM | `ADMIN` · `OPERATOR` |
| `created_at` | DATETIME | Fecha de registro |

**Notas:**
- El rol `ADMIN` puede crear sesiones y gestionar participantes.
- El rol `OPERATOR` puede mover entradas en la cola y actualizar estados.

---

### 2. `sessions`

Una sesión representa una jornada de atención abierta por un operador. Toda la cola y sus movimientos pertenecen a una sesión activa.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `operator_id` | INT UNSIGNED FK | Operador que la gestiona |
| `session_code` | VARCHAR(30) UNIQUE | Código legible (`SESSION_01`) |
| `status` | ENUM | `STANDBY` · `LIVE` · `COMPLETED` · `TERMINATED` |
| `started_at` | DATETIME | Momento de inicio real |
| `ended_at` | DATETIME | Momento de cierre |
| `created_at` | DATETIME | Fecha de creación |

**Ciclo de vida de una sesión:**

```
STANDBY → LIVE → COMPLETED
                ↘ TERMINATED  (cierre forzado)
```

**FK:** `operator_id` → `operators.id` (RESTRICT)

---

### 3. `participants`

Personas registradas en el sistema que pueden ser añadidas a una cola. Su `grade` determina la prioridad con la que ingresan.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `full_name` | VARCHAR(120) | Nombre completo |
| `grid_id` | VARCHAR(20) UNIQUE | Código único del participante (`KR-460-THX`) |
| `grade` | ENUM | `S` (alta prioridad) · `A` (media) · `B` (estándar) |
| `season_points` | SMALLINT UNSIGNED | Puntaje acumulado en la temporada |
| `registered_at` | DATETIME | Fecha de registro en el sistema |

**Regla de prioridad por grade:**

| Grade | `priority` en cola | Comportamiento |
|---|---|---|
| `S` | `HIGH` | Entra antes que todos los `NORMAL` |
| `A` | `NORMAL` | Orden de llegada entre A y B |
| `B` | `NORMAL` | Orden de llegada entre A y B |

---

### 4. `queue_entries`

**Tabla principal del sistema.** Cada fila representa un turno en la cola para una sesión específica.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `session_id` | INT UNSIGNED FK | Sesión a la que pertenece |
| `participant_id` | INT UNSIGNED FK | Participante en turno |
| `position` | SMALLINT UNSIGNED | Posición actual en la cola (1 = primero) |
| `priority` | ENUM | `HIGH` · `NORMAL` |
| `status` | ENUM | Ver ciclo de vida abajo |
| `estimated_start_s` | DECIMAL(9,2) | Tiempo estimado de inicio en segundos |
| `session_time_s` | DECIMAL(9,2) | Duración real de la atención en segundos |
| `entered_at` | DATETIME | Cuando se añadió a la cola |
| `started_at` | DATETIME | Cuando pasó a `ON_TRACK` |
| `completed_at` | DATETIME | Cuando finalizó la atención |

**Ciclo de vida de un turno:**

```
QUEUED → UP_NEXT → ON_TRACK → COMPLETED
   ↓                   ↓
CANCELLED          CANCELLED
```

| Status | Pantalla equivalente | Descripción |
|---|---|---|
| `ON_TRACK` | Panel "EN PISTA" | Siendo atendido ahora |
| `UP_NEXT` | Panel "PRÓXIMO" | El siguiente en entrar |
| `QUEUED` | Panel "COLA" | En espera con posición asignada |
| `COMPLETED` | — | Atención finalizada |
| `CANCELLED` | — | Retirado de la cola |

**Restricciones:**
- `UNIQUE (session_id, participant_id)` — un participante no puede estar dos veces en la misma sesión.

**FK:**
- `session_id` → `sessions.id` (CASCADE)
- `participant_id` → `participants.id` (RESTRICT)

---

### 5. `stint_slots`

Representa los **espacios físicos de atención** que el operador visualiza y gestiona en pantalla (panel de Stint Strategy). El operador asigna una `queue_entry` a un slot mediante drag & drop.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `session_id` | INT UNSIGNED FK | Sesión a la que pertenece |
| `queue_entry_id` | INT UNSIGNED FK | Turno asignado (`NULL` = slot vacío) |
| `slot_order` | TINYINT UNSIGNED | Orden del slot (1 = principal) |
| `slot_status` | ENUM | `ACTIVE` · `QUEUED` · `HOLD` · `BLOCKED` · `EMPTY` |
| `assigned_at` | DATETIME | Cuando se asignó el turno al slot |

**Estados de un slot:**

| Status | Color en UI | Descripción |
|---|---|---|
| `ACTIVE` | Verde (`GREEN_LIT`) | Atención en curso |
| `QUEUED` | Amarillo | En cola, listo para activar |
| `HOLD` | Naranja (`HOLD_BOX`) | Pausado, esperando instrucción |
| `BLOCKED` | Rojo | Bloqueado por el operador |
| `EMPTY` | Gris | Sin asignar |

**FK:**
- `session_id` → `sessions.id` (CASCADE)
- `queue_entry_id` → `queue_entries.id` (SET NULL al eliminar)

---

### 6. `session_log`

Registro cronológico de todas las acciones del operador durante una sesión. Equivale al "Race Control Log" del diseño. Permite auditoría y trazabilidad completa.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `session_id` | INT UNSIGNED FK | Sesión relacionada |
| `operator_id` | INT UNSIGNED FK | Operador que ejecutó la acción |
| `action_type` | ENUM | Tipo de acción (ver tabla abajo) |
| `notes` | TEXT | Detalle libre de la acción |
| `created_at` | DATETIME(3) | Timestamp con milisegundos |

**Tipos de acción registrados:**

| `action_type` | Descripción |
|---|---|
| `SESSION_STARTED` | La sesión fue abierta |
| `SESSION_ENDED` | La sesión fue cerrada normalmente |
| `ENTRY_ADDED` | Nuevo participante añadido a la cola |
| `ENTRY_PROMOTED` | Participante subió de prioridad |
| `ENTRY_MOVED` | Participante cambió de posición |
| `ENTRY_ON_TRACK` | Participante pasó a ser atendido |
| `ENTRY_COMPLETED` | Atención finalizada |
| `ENTRY_CANCELLED` | Participante retirado de la cola |
| `SLOT_ASSIGNED` | Un turno fue asignado a un slot |
| `SLOT_BLOCKED` | Un slot fue bloqueado por el operador |

**FK:**
- `session_id` → `sessions.id` (CASCADE)
- `operator_id` → `operators.id` (SET NULL)

---

## Índices

| Índice | Tabla | Columnas | Propósito |
|---|---|---|---|
| `idx_queue_session_status` | `queue_entries` | `session_id, status` | Filtrar cola por estado |
| `idx_queue_session_position` | `queue_entries` | `session_id, position` | Ordenar cola por posición |
| `idx_stint_session` | `stint_slots` | `session_id, slot_order` | Listar slots en orden |
| `idx_log_session_time` | `session_log` | `session_id, created_at` | Log cronológico por sesión |

---

## Vista: `vw_queue_status`

Muestra el estado completo de la cola activa, ordenado por estado y posición.

```sql
SELECT * FROM vw_queue_status;
```

**Columnas devueltas:**

| Columna | Descripción |
|---|---|
| `position` | Posición en la cola |
| `full_name` | Nombre del participante |
| `grid_id` | Código único |
| `grade` | Grado S / A / B |
| `season_points` | Puntos acumulados |
| `priority` | HIGH / NORMAL |
| `status` | Estado actual del turno |
| `estimated_start_s` | Tiempo estimado de inicio |
| `session_time_s` | Tiempo real de atención |
| `entered_at` | Cuándo ingresó a la cola |
| `started_at` | Cuándo comenzó a ser atendido |

**Orden de resultados:** `ON_TRACK → UP_NEXT → QUEUED → COMPLETED → CANCELLED`

---

## Lógica de negocio clave

### Insertar un participante en la cola

```sql
-- 1. Determinar la prioridad según el grade del participante
-- 2. Si priority = HIGH → insertar antes de los NORMAL
--    Si priority = NORMAL → insertar al final de la cola FIFO

INSERT INTO queue_entries (session_id, participant_id, position, priority, status)
VALUES (
  :session_id,
  :participant_id,
  (SELECT COALESCE(MAX(position), 0) + 1 FROM queue_entries
   WHERE session_id = :session_id AND status = 'QUEUED'),
  :priority,   -- HIGH o NORMAL según grade del participante
  'QUEUED'
);
```

### Avanzar la cola (llamar al siguiente)

```sql
-- Completar el ON_TRACK actual
UPDATE queue_entries
SET status = 'COMPLETED', completed_at = NOW()
WHERE session_id = :session_id AND status = 'ON_TRACK';

-- Promover UP_NEXT a ON_TRACK
UPDATE queue_entries
SET status = 'ON_TRACK', started_at = NOW()
WHERE session_id = :session_id AND status = 'UP_NEXT';

-- Promover el primero en QUEUED a UP_NEXT
UPDATE queue_entries
SET status = 'UP_NEXT'
WHERE session_id = :session_id AND status = 'QUEUED'
ORDER BY priority DESC, position ASC
LIMIT 1;
```

### Consultar estado del panel de pits (On Track / Up Next / Cola)

```sql
-- EN PISTA
SELECT p.full_name, qe.session_time_s
FROM queue_entries qe JOIN participants p ON qe.participant_id = p.id
WHERE qe.session_id = :session_id AND qe.status = 'ON_TRACK';

-- PRÓXIMO
SELECT p.full_name, p.grade, qe.estimated_start_s
FROM queue_entries qe JOIN participants p ON qe.participant_id = p.id
WHERE qe.session_id = :session_id AND qe.status = 'UP_NEXT';

-- COLA (con tiempo estimado de inicio)
SELECT qe.position, p.full_name, qe.estimated_start_s
FROM queue_entries qe JOIN participants p ON qe.participant_id = p.id
WHERE qe.session_id = :session_id AND qe.status = 'QUEUED'
ORDER BY qe.position ASC;
```

---

## Instrucciones de instalación

```bash
# Crear la base de datos y ejecutar el script
mysql -u root -p < kepler_core_database.sql

# Verificar las tablas creadas
mysql -u root -p kepler_core -e "SHOW TABLES;"
```

**Tablas esperadas:**

```
+------------------------+
| Tables_in_kepler_core  |
+------------------------+
| operators              |
| sessions               |
| participants           |
| queue_entries          |
| stint_slots            |
| session_log            |
+------------------------+
```

---

## Requisitos del servidor

| Requisito | Mínimo |
|---|---|
| MySQL | 8.0+ |
| Almacenamiento inicial | ~5 MB |
| Charset | utf8mb4 |
| Engine | InnoDB (requerido para FK) |

---

*KEPLER / TRACKLINE ALLIANCE — Database Docs v1.0*