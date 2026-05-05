namespace KeplerPrint.Models
{
    public record PrintPilotoRequest(
        string Nombre,
        string DriverId,
        string Licencia,
        string Email,
        string Fecha
    );

    public record PrintVehiculoRequest(
        string Modelo,
        string Categoria,
        string Vin,
        string Garage,
        string Fecha
    );

    public record PrintTurnoRequest(
        string Turno,
        string Nombre,
        string Vehiculo,
        int    Duracion,
        string CreatedAt,
        string Fecha
    );

    public record PrintRegistroCompletoRequest(
        // Piloto
        string Nombre,
        string DriverId,
        string Licencia,
        string Email,
        // Vehiculo
        string Modelo,
        string Categoria,
        string Vin,
        string Garage,
        // Turno
        string Turno,
        int    Duracion,
        string Fecha
    );
}
