using Npgsql;

namespace Mapas.Api.Infrastructure;

public sealed record TipoActivo(string Codigo, string Nombre);

/// <summary>
/// Contrato C1: catálogo de tipos de activo físico, en una tabla configurable (RF-ED-04).
/// La evaluación guarda el código tal como llegó (así un lote reenviado sin conexión sigue siendo
/// idéntico y la idempotencia no se rompe). Al leer se normaliza: un código desconocido o ausente
/// queda como OTRO y se conserva el valor original recibido, para reclasificarlo después.
/// </summary>
public sealed class TiposActivo(NpgsqlDataSource db)
{
    private static readonly TipoActivo[] Iniciales =
    [
        new("VIVIENDA", "Vivienda"), new("EDIFICIO_PUBLICO", "Edificio público"), new("ESCUELA", "Escuela"),
        new("CENTRO_SALUD", "Centro de salud"), new("VIA", "Vía"), new("PUENTE", "Puente"),
        new("RED_AGUA", "Red de agua"), new("RED_ENERGIA", "Red de energía"), new("OTRO", "Otro"),
    ];

    private volatile TipoActivo[] vigentes = Iniciales;

    public async Task Inicializar(CancellationToken ct)
    {
        await using var crear = db.CreateCommand("""
            CREATE TABLE IF NOT EXISTS tipos_activo (
                codigo text PRIMARY KEY CHECK (codigo ~ '^[A-Z_]{2,40}$'),
                nombre text NOT NULL,
                orden int NOT NULL
            );
            """);
        await crear.ExecuteNonQueryAsync(ct);
        for (var i = 0; i < Iniciales.Length; i++)
        {
            await using var semilla = db.CreateCommand(
                "INSERT INTO tipos_activo(codigo, nombre, orden) VALUES (@c, @n, @o) ON CONFLICT (codigo) DO NOTHING");
            semilla.Parameters.AddWithValue("c", Iniciales[i].Codigo);
            semilla.Parameters.AddWithValue("n", Iniciales[i].Nombre);
            semilla.Parameters.AddWithValue("o", (i + 1) * 10);
            await semilla.ExecuteNonQueryAsync(ct);
        }
        await Recargar(ct);
    }

    /// <summary>Lee la tabla de nuevo: un tipo agregado por configuración aparece sin reiniciar.</summary>
    public async Task<TipoActivo[]> Recargar(CancellationToken ct)
    {
        await using var leer = db.CreateCommand("SELECT codigo, nombre FROM tipos_activo ORDER BY orden, codigo");
        var lista = new List<TipoActivo>();
        await using (var filas = await leer.ExecuteReaderAsync(ct))
            while (await filas.ReadAsync(ct))
                lista.Add(new(filas.GetString(0), filas.GetString(1)));
        if (lista.Count > 0) vigentes = lista.ToArray();
        return vigentes;
    }

    /// <summary>Código válido → el mismo, sin original. Desconocido o ausente → OTRO, con el valor recibido.</summary>
    public (string Codigo, string Nombre, string? Original) Normalizar(string? recibido)
    {
        var codigo = recibido?.Trim().ToUpperInvariant();
        var tipo = vigentes.FirstOrDefault(t => t.Codigo == codigo);
        if (tipo is not null) return (tipo.Codigo, tipo.Nombre, null);
        var otro = vigentes.FirstOrDefault(t => t.Codigo == "OTRO") ?? new TipoActivo("OTRO", "Otro");
        return (otro.Codigo, otro.Nombre, string.IsNullOrWhiteSpace(recibido) ? null : recibido);
    }
}
