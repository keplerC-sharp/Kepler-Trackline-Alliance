# Sistema de Gestión de Turnos

## Descripción

Se requiere desarrollar un sistema de software para la gestión de turnos en diferentes contextos (hospitales, bancos, supermercados, estaciones de transporte, entre otros).  

El sistema debe permitir registrar usuarios, asignar turnos, gestionar salas de espera y facilitar la atención por parte de asesores, optimizando el flujo de atención y mejorando la experiencia del usuario.

Cada equipo podrá definir el enfoque del sistema según el contexto elegido (por ejemplo: atención médica, atención bancaria, servicio al cliente, etc.).

---

## Planteamiento del problema

En muchos entornos de atención al cliente, la gestión de turnos se realiza de manera ineficiente, generando problemas como:

- Largas filas y desorganización
- Falta de control sobre el orden de atención
- Pérdida de información de los usuarios
- Mala experiencia del cliente
- Dificultad para que los asesores gestionen los turnos

Se necesita una solución tecnológica que permita organizar el flujo de atención, registrar usuarios de manera básica y gestionar turnos en tiempo real, incluyendo una sala de espera dinámica y herramientas para los asesores.

---

## Objetivo general

Desarrollar un sistema de gestión de turnos que permita registrar usuarios, asignar y administrar turnos, y facilitar la atención mediante una interfaz para asesores y una sala de espera interactiva.

---

## Objetivos específicos

1. Diseñar un módulo de registro de usuarios:
   - Permitir registrar usuarios con:
     - ID
     - Documento
   - Validar si el usuario ya existe antes de crear un nuevo registro

2. Implementar la generación de turnos:
   - Asignar un ticket único al usuario
   - Asociar el turno a un estado (pendiente, en espera, atendido)

3. Desarrollar la sala de espera:
   - Mostrar los turnos en cola
   - Indicar el turno actual en atención
   - Incluir un componente visual (pantalla con lista de turnos)
   - Integrar reproducción de video o contenido visual
   - Implementar notificación sonora o por voz para el llamado de turnos

4. Crear el flujo de atención:
   - Permitir que el usuario sea llamado desde la sala de espera
   - Cambiar el estado del turno a “en atención”
   - Finalizar el turno al terminar la atención

5. Desarrollar la interfaz del asesor:
   - Visualizar:
     - Turnos pendientes
     - Turnos en espera
     - Turnos atendidos
   - Llamar al siguiente turno
   - Registrar comentarios sobre la atención
   - Actualizar información del usuario si es necesario

6. Gestionar estados del sistema:
   - Pendiente
   - En espera
   - En atención
   - Finalizado

---

## Reglas del sistema (lógica clave)

- Un usuario no puede tener múltiples turnos activos al mismo tiempo
- Si el usuario no existe, debe registrarse antes de generar el turno
- El turno debe avanzar en orden (FIFO)
- Cada turno debe tener trazabilidad (historial de estados y comentarios)
- El asesor es quien controla el flujo de atención

---

## Alcance técnico sugerido

- Backend
- Base de datos relacional (MySQL, PostgreSQL, etc.)
- Frontend:
  - Pantalla de sala de espera
  - Panel del asesor
  - panel de turno
- Opcional:
  - Sonido (alerta de turno)
  - Texto a voz (TTS)

---

## Extra (reto avanzado)

- Priorización de turnos (VIP, urgencias)
- Estadísticas:
  - Tiempo promedio de atención
  - Número de turnos atendidos por asesor
- Pantalla en tiempo real (WebSockets)
- Integración con códigos QR