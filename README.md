# APEX CONTROL — MPA Frontend

System for managing pilots, vehicles, queues, and ticket printing for track events.

---

## Project Structure

```
apex-mpa/
├── index.html # Login / Secure Access Gateway
├── registro.html # Pilot, vehicle, and shift assignment registration
├── cola.html # Real-time shift queue
├── tiquetes.html # History of generated tickets
├── css/
│ └── styles.css # Global styles shared by all pages
└── js/

├── shared.js # Shared utilities: auth, sidebar, toasts, print

├── login.js # Exclusive login logic

├── registro.js # Exclusive logic for registration and shift assignment

├── queue.js # Exclusive logic for the real-time queue

└── tickets.js # Exclusive logic for the ticket history
```

---

## Pages

### `index.html` — Login
- Login with Marshal ID and Access Key
- "Remember Station" option (ID persists in `localStorage`)
- Auth guard: if a session is already active, redirects directly to `registration.html`
- Connection status indicators (Telemetry Link / Encryption)

### `registration.html` — Driver & Vehicle Onboarding
- **Driver Tab:** form with inline validations (name, duplicate ID, email, license)
- **Vehicle Tab:** make/model, VIN, garage, category
- **Search Driver Tab:** autocomplete, driver selection, shift assignment with selector Duration
- Print button appears after each successful registration
- Print server configuration bar with connection test

### `queue.html` — Turn Queue
- List of turns with statuses: `PENDING`, `ON TRACK`, `COMPLETED`
- Real-time countdown of the active turn (updates every second)
- Large timer with color change: cyan → yellow (70%) → flashing red (85%)
- Live statistics: pending / on track / completed
- Manual or automatic turn advancement when time expires
- SignalR simulation using `setInterval`

### `tickets.html` — Ticket History
- Displays all generated tickets: drivers, vehicles, and turns
- Filters by type: All / Turns / Drivers / Vehicles
- Individual reprint button for each ticket
- ​​Individual deletion button and full history clear

---

## Technologies

| Technology | Usage |

|---|---|

| HTML5 | Page Structure |

| CSS3 + CSS Variables | Global Styles, Dark Theme, Responsive Design |

| JavaScript ES6+ | Business Logic, No Frameworks |

| Bootstrap 5.3 | Grid, Responsive Utilities |

| Bootstrap Icons 1.11 | Iconography |

| Google Fonts | Barlow Condensed, Barlow, Share Tech Mono |

No JS frameworks (no React, no Vue, no Angular). No bundler. Opens directly in the browser.

---

## Inter-Page Communication

Data is shared between pages using `localStorage` and `sessionStorage`:

| Key | Type | Content |

|---|---|---|

`apex_user` | `sessionStorage` | Authenticated user `{id, role}` |

`apex_queue` | `localStorage` | Array of queue positions |

`apex_tickets` | `localStorage` | History of generated tickets |

`apex_print_ip` | `localStorage` | IP:Port of the print server |

`apex_remember_id` | `localStorage` | Marshal ID remembered upon login |

---

## Auth Guard

All pages except `index.html` call `requireAuth()` on load. If there is no active session in `sessionStorage`, they are automatically redirected to the login page.

```js
// In shared.js
function requireAuth() {
if (!sessionStorage.getItem('apex_user')) {
window.location.href = 'index.html';

}
}
```

---

## Printing — Xprinter 58mm

The frontend sends print requests to the ASP.NET Core backend, which physically controls the printer.

### Configuration
In `registro.html` and `tiquetes.html`, there is a configuration bar where you enter the server's IP address and port:

```
http://192.168.1.105:3000
```

This address is automatically saved in `localStorage` (`apex_print_ip`).

### Endpoints consumed by the frontend

| Method | Path | Description |

|---|---|---|

| `GET` | `/api/print/health` | Connection test |

| `POST` | `/api/print/pilot` | Print pilot ticket |

| `POST` | `/api/print/vehicle` | Print vehicle ticket |

| `POST` | `/api/print/turno` | Print shift ticket |


### Example Payload — Shift
```json
{
"type": "shift",
"shift": "T-003",
"name": "L. HAMILTON",
"vehicle": "Porsche 911 GT3 RS",
"duration": 20,
"createdAt": "10:45:32",
"date": "29/04/2024 10:45:32"
}
```

### Printer Driver
Install the `.exe` driver file on the PC where the Xprinter is connected via USB. Once installed:
1. Verify that the Xprinter appears in **Devices and Printers**
2. Print a test page to confirm
3. Note the COM port in **Device Manager → Ports (COM & LPT)**
4. Configure the port in `appsettings.json` of the backend:

```json
"Printer": {
"Port": "COM3"
}
```

---

## How to run

1. Clone or extract the project
2. Ensure your PC and mobile device are on the same Wi-Fi network
3. Open `index.html` in your browser (or serve it using any static server)
4. Log in with any Marshal ID and password (mock—connect to the live backend when available)
5. Configure the print server IP address in the field in the top bar

> For production, it is recommended to serve the files as static files using IIS, Nginx, or ASP.NET Core. Do not open the `.html` file directly from the file system, as some browsers restrict access to `localStorage` for the `file://` protocol.

---

## Connecting to the live backend (pending)

Currently, the frontend uses mock data. To connect to the ASP.NET Core backend, replace the functions marked with the comment `// — Here you connect to your actual API —` in each JS file:

- `js/login.js` → `POST /api/auth/login`
- `js/registro.js` → `POST /api/pilotos`, `POST /api/vehiculos`, `POST /api/turnos`
- `js/cola.js` → `GET /api/turnos/cola` (SignalR hub)

---

## Responsive

The design is fully responsive:

- **Desktop:** Visible sidebar, two-column layout
- **Tablet:** Collapsible sidebar with hamburger menu
- **Mobile:** Single-column view, sidebar as a side drawer

Breakpoints inherited from Bootstrap 5 (`sm`, `lg`).