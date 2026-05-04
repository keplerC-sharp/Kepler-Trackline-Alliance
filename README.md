# KEPLER / TRACKLINE ALLIANCE
### Sistema de Gestión de Cola con Prioridades — Documentación Completa

## Autore
- Camilo
- Daniela
- Juan Eduardo
- Ismael

**Stack:** ASP.NET Core 10 · Entity Framework Core 9 · MySQL 8 · Vanilla JS  
**Base de datos:** `kepler_core`  
**Versión:** 2.0

---

## ¿Qué es este sistema?

KEPLER es una aplicación web para gestión de turnos en tiempo real. Un operador autenticado abre una sesión, registra participantes con su Grid ID y nivel de licencia, y los asigna a una cola FIFO con prioridad automática según su grade. Desde el panel de cola puede avanzar turnos, ver quién está en pista y quién es el siguiente.

---

## Requisitos

| Requisito | Versión mínima |
|---|---|
| .NET SDK | 10.0 |
| MySQL | 8.0+ |
| Navegador moderno | Chrome / Firefox / Edge |

---

## Instalación y ejecución

### 1. Clonar / descomprimir el proyecto

```bash
cd kepler/
```

### 2. Configurar la base de datos

Editar `appsettings.json` con los datos de tu servidor MySQL:

```json
{
  "ConnectionStrings": {
    "Default": "server=TU_HOST;port=3306;database=kepler_core;user=TU_USUARIO;password=TU_PASSWORD"
  },
  "SMTP": {
    "Host": "",
    "Port": "587",
    "User": "",
    "Pass": ""
  }
}
```

> El SMTP es opcional. Si se deja vacío, el sistema funciona igual y omite el envío de emails.

### 3. Crear la base de datos

```bash
mysql -u root -p -e "CREATE DATABASE IF NOT EXISTS kepler_core CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
```

### 4. Aplicar migraciones (Entity Framework)

```bash
dotnet ef database update
```

> Si no tienes las herramientas de EF instaladas:
> ```bash
> dotnet tool install --global dotnet-ef
> ```

### 5. Insertar un operador inicial

Dado que el login compara contraseñas en texto plano, inserta el primer operador directamente en MySQL:

```sql
INSERT INTO operators (identifier, full_name, password_hash, role, created_at)
VALUES ('ADMIN_01', 'Administrador', 'admin123', 'OPERATOR', NOW());
```

### 6. Ejecutar la aplicación

```bash
dotnet run
```

La app estará disponible en `https://localhost:5001` o `http://localhost:5000`.

---

## Flujo de uso

```
Login → Cola de Turnos → Iniciar Sesión
                              ↓
                    Driver Registry → Registrar participante
                              ↓
                    Buscar & Asignar → Asignar a la cola
                              ↓
                    Cola de Turnos → Avanzar / Cancelar turnos
```

1. **Login** — Inicia sesión con tu Marshal ID y contraseña.
2. **Cola de Turnos** — Haz clic en **Iniciar Sesión** para abrir una sesión activa.
3. **Driver Registry** — Registra participantes con nombre, Grid ID y License Grade.
4. **Buscar & Asignar** — Busca un participante ya registrado y asígnalo a la cola.
5. **Cola de Turnos** — Usa **Avanzar Cola** para pasar al siguiente turno. El timer muestra el tiempo en pista del turno activo.
6. **Finalizar Sesión** — Cierra la sesión activa cuando termines.

---

## Estructura del proyecto

```
kepler/
├── Controllers/
│   ├── AuthController.cs         # Login, Logout, Crear operador, /Auth/Me
│   ├── DashboardController.cs    # Driver Registry, Tiquetes, API participantes
│   ├── QueueController.cs        # Cola, stats, avanzar, cancelar, sesión activa
│   ├── SessionController.cs      # Iniciar y finalizar sesión
│   └── HomeController.cs         # Redirección raíz
│
├── Models/
│   ├── Operator.cs               # Usuario del sistema
│   ├── Session.cs                # Sesión de atención
│   ├── Participant.cs            # Persona registrada en el sistema
│   ├── QueueEntry.cs             # Turno en la cola
│   ├── StintSlot.cs              # Slots físicos de atención
│   └── SessionLog.cs             # Auditoría de acciones
│
├── ViewModels/
│   ├── LoginViewModel.cs
│   ├── RegisterViewModel.cs
│   └── QueueViewModel.cs
│
├── Services/
│   ├── QueueService.cs           # Lógica de cola (add, advance, prioridad)
│   ├── SessionService.cs         # Crear sesiones
│   └── EmailService.cs           # Notificaciones (opcional)
│
├── Data/
│   └── AppDbContext.cs           # EF Core DbContext + mapeo de tablas
│
├── Views/
│   ├── Auth/
│   │   ├── Login.cshtml          # Pantalla de login
│   │   └── Register.cshtml       # Crear nuevo operador (requiere auth)
│   ├── Dashboard/
│   │   ├── Index.cshtml          # Driver Registry (registro de participantes)
│   │   └── Tickets.cshtml        # Historial de tiquetes (localStorage)
│   ├── Queue/
│   │   └── Index.cshtml          # Cola de turnos en tiempo real
│   └── Home/
│       └── Index.cshtml          # Redirección automática
│
├── wwwroot/
│   ├── js/
│   │   ├── shared.js             # Sidebar, topbar, toasts, print — todas las páginas
│   │   ├── login.js              # UX del login
│   │   ├── registro.js           # Lógica Driver Registry
│   │   ├── cola.js               # Lógica Cola de Turnos
│   │   └── tiquetes.js           # Lógica Tiquetes (localStorage)
│   └── css/
│       └── styles.css            # Estilos globales
│
├── appsettings.json              # Cadena de conexión y SMTP
├── Program.cs                    # Configuración de la app
└── Kepler-Trackline-Alliance.csproj
```

---

## Rutas de la aplicación

| Ruta | Método | Auth | Descripción |
|---|---|---|---|
| `/` | GET | No | Redirige al login o a la cola |
| `/Auth/Login` | GET/POST | No | Login de operadores |
| `/Auth/Logout` | GET | No | Cerrar sesión |
| `/Auth/Register` | GET/POST | Sí | Crear nuevo operador |
| `/Auth/Me` | GET | Sí | Info del operador actual (JSON) |
| `/Queue/Index` | GET | Sí | Cola de turnos |
| `/Queue/GetQueue` | GET | Sí | Estado de la cola (JSON) |
| `/Queue/GetStats` | GET | Sí | Estadísticas de la sesión (JSON) |
| `/Queue/GetActiveSession` | GET | Sí | Sesión activa actual (JSON) |
| `/Queue/Advance` | POST | Sí | Avanzar la cola |
| `/Queue/Cancel` | POST | Sí | Cancelar un turno |
| `/Queue/AddParticipant` | POST | Sí | Agregar participante a la cola |
| `/Dashboard/Index` | GET | Sí | Driver Registry |
| `/Dashboard/Tickets` | GET | Sí | Historial de tiquetes |
| `/Dashboard/GetParticipants` | GET | Sí | Lista de participantes (JSON) |
| `/Dashboard/RegisterParticipant` | POST | Sí | Registrar nuevo participante |
| `/Dashboard/AssignToQueue` | POST | Sí | Asignar participante a cola activa |
| `/Session/Start` | POST | Sí | Iniciar sesión de atención |
| `/Session/End` | POST | Sí | Finalizar sesión de atención |

---

## Modelo de base de datos

### Diagrama de relaciones

```
operators ──< sessions ──< queue_entries >── participants
                │
                └──< session_log
```

### Tablas

#### `operators`
Usuarios del sistema que abren sesiones y gestionan la cola.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `identifier` | VARCHAR(50) UNIQUE | Código de acceso (`MARSHAL_01`) |
| `full_name` | VARCHAR(120) | Nombre completo |
| `password_hash` | VARCHAR(255) | Contraseña (texto plano en esta versión) |
| `role` | VARCHAR(20) | `OPERATOR` |
| `created_at` | DATETIME | Fecha de registro |

#### `sessions`
Una sesión representa una jornada de atención. Toda la cola pertenece a una sesión `LIVE`.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `operator_id` | INT UNSIGNED FK | Operador que la abrió |
| `session_code` | VARCHAR(50) | Código generado automáticamente |
| `status` | VARCHAR(20) | `STANDBY` · `LIVE` · `COMPLETED` |
| `started_at` | DATETIME | Inicio real |
| `ended_at` | DATETIME | Cierre |
| `created_at` | DATETIME | Fecha de creación |

#### `participants`
Personas registradas. Su `grade` determina la prioridad en la cola.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `full_name` | VARCHAR(120) | Nombre completo |
| `grid_id` | VARCHAR(20) UNIQUE | Código único (`KR-460-THX`) |
| `grade` | VARCHAR(2) | `S` (alta prioridad) · `A` · `B` |
| `season_points` | INT | Puntaje acumulado |
| `registered_at` | DATETIME | Fecha de registro |

**Regla de prioridad:**

| Grade | Priority en cola | Comportamiento |
|---|---|---|
| `S` | `HIGH` | Entra antes que todos los NORMAL |
| `A` | `NORMAL` | Orden de llegada |
| `B` | `NORMAL` | Orden de llegada |

#### `queue_entries`
Tabla principal. Cada fila es un turno en la cola de una sesión.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `session_id` | INT UNSIGNED FK | Sesión a la que pertenece |
| `participant_id` | INT UNSIGNED FK | Participante en turno |
| `position` | INT | Posición en la cola |
| `priority` | VARCHAR(10) | `HIGH` · `NORMAL` |
| `status` | VARCHAR(20) | Ver ciclo de vida |
| `estimated_start_s` | DECIMAL | Tiempo estimado de inicio (segundos) |
| `session_time_s` | DECIMAL | Duración real de atención (segundos) |
| `entered_at` | DATETIME | Cuando ingresó a la cola |
| `started_at` | DATETIME | Cuando pasó a ON_TRACK |
| `completed_at` | DATETIME | Cuando finalizó |

**Ciclo de vida de un turno:**

```
QUEUED → UP_NEXT → ON_TRACK → COMPLETED
   ↓                   ↓
CANCELLED          CANCELLED
```

**Restricción:** `UNIQUE (session_id, participant_id)` — un participante no puede estar dos veces en la misma sesión activa.

#### `session_log`
Auditoría completa de todas las acciones por sesión.

| Columna | Tipo | Descripción |
|---|---|---|
| `id` | INT UNSIGNED PK | Identificador único |
| `session_id` | INT UNSIGNED FK | Sesión relacionada |
| `operator_id` | INT UNSIGNED FK | Operador que ejecutó la acción |
| `action_type` | VARCHAR(50) | Tipo de acción |
| `notes` | TEXT | Detalle de la acción |
| `created_at` | DATETIME | Timestamp |

**Tipos de acción registrados:** `ENTRY_ADDED` · `ENTRY_ON_TRACK` · `ENTRY_PROMOTED` · `ENTRY_COMPLETED` · `ENTRY_CANCELLED`

---

## Lógica de negocio

### Insertar participante con prioridad

```
Si grade == "S" → priority = HIGH
  → Buscar la primera posición QUEUED/NORMAL
  → Desplazar todas las posiciones >= ese punto +1
  → Insertar en esa posición
Si grade != "S" → priority = NORMAL
  → Insertar al final (MAX(position) + 1)
```

### Avanzar la cola

```
1. Completar ON_TRACK actual → status = COMPLETED
2. Promover UP_NEXT → status = ON_TRACK
   (si no hay UP_NEXT, promover el primero de QUEUED)
3. Promover el siguiente QUEUED → status = UP_NEXT
```

---

## Dependencias NuGet

| Paquete | Versión |
|---|---|
| `Microsoft.EntityFrameworkCore` | 9.0.0 |
| `Pomelo.EntityFrameworkCore.MySql` | 9.0.0 |

---

## Notas de la versión 2.0

- Login sin hasheo de contraseñas (comparación directa)
- Registro de participantes independiente de la sesión activa
- Endpoint `/Dashboard/AssignToQueue` separado del registro
- Serialización JSON en camelCase explícito en todos los endpoints
- Manejo global de errores — ninguna excepción cae al usuario sin respuesta controlada
- Nombre del operador visible en el sidebar via `/Auth/Me`
- Botón "Finalizar Sesión" en la Cola de Turnos
- Panel "Próximo en Cola" en tiempo real

---

*KEPLER / TRACKLINE ALLIANCE — v2.0*