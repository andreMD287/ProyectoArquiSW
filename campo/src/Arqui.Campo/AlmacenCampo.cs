using SQLite;

namespace Arqui.Campo;

public sealed class EvaluacionPendiente
{
    [System.Text.Json.Serialization.JsonIgnore] public long VersionServidor {get;set;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public string? Activo {get;set;}
    public string TipoActivo { get; set; } = "OTRO";
    [System.Text.Json.Serialization.JsonIgnore] public string? OrganizacionLocal {get;set;}
    [System.Text.Json.Serialization.JsonIgnore] public string? UsuarioLocal {get;set;}
    [PrimaryKey]
    public string EvaluacionId { get; set; } = Guid.NewGuid().ToString();
    public string OperacionId { get; set; } = Guid.NewGuid().ToString();
    public string EventoId { get; set; } = "DEMO-01";
    public int? NivelAfectacion { get; set; }
    public string? Estado { get; set; }
    public string? Motivo { get; set; }
    public string Descripcion { get; set; } = "";
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public string? FuenteUbicacion { get; set; }
    public string CapturadaEnUtc { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string? EvidenciaId { get; set; }
    public string? FotoTipo { get; set; }
    public string? FotoSha256 { get; set; }
    public byte[]? Foto { get; set; }
}

public sealed class ResumenPendiente
{
    public string EvaluacionId { get; set; } = "";
    public int? NivelAfectacion { get; set; }
    public string? Estado { get; set; }
    public string? Motivo { get; set; }
    public string Descripcion { get; set; } = "";
    public string? EvidenciaId { get; set; }
}

public sealed class AlmacenCampo
{
    private readonly SQLiteAsyncConnection baseLocal;
    private readonly Lazy<Task<CreateTableResult>> inicializacion;

    public AlmacenCampo(string ruta)
    {
        baseLocal = new SQLiteAsyncConnection(ruta);
        inicializacion = new(async()=>{
            var result=await baseLocal.CreateTableAsync<EvaluacionPendiente>();
            await baseLocal.CreateTableAsync<RevisionLocal>();
            await baseLocal.CreateTableAsync<CaracterizacionPendiente>();
            await baseLocal.CreateTableAsync<TipoActivoLocal>();
            // Registros anteriores a C1 no se descartan: quedan normalizados como OTRO.
            await baseLocal.ExecuteAsync("UPDATE EvaluacionPendiente SET TipoActivo='OTRO' WHERE TipoActivo IS NULL OR TRIM(TipoActivo)=''");
            return result;
        });
    }

    public async Task<List<DesastreLocal>> DesastresAsync()
    {
        await baseLocal.CreateTableAsync<DesastreLocal>();
        return await baseLocal.Table<DesastreLocal>().Where(x=>x.OrganizacionLocal==SesionCampo.Organizacion).OrderBy(x => x.Id).ToListAsync();
    }

    public async Task GuardarCatalogoAsync(IEnumerable<DesastreLocal> desastres)
    {
        await baseLocal.CreateTableAsync<DesastreLocal>();
        var copia = desastres.ToArray();
        foreach(var d in copia)d.OrganizacionLocal=SesionCampo.Organizacion;
        await baseLocal.RunInTransactionAsync(db => {
            db.Execute("DELETE FROM DesastreLocal WHERE OrganizacionLocal = ?",SesionCampo.Organizacion);
            foreach(var d in copia)db.InsertOrReplace(d);
        });
    }

    public async Task<List<TipoActivoLocal>> TiposActivoAsync()
    {
        await inicializacion.Value;
        var locales = await baseLocal.Table<TipoActivoLocal>().OrderBy(x => x.Nombre).ToListAsync();
        if (locales.Count > 0) return locales;
        // Referencia provisional de la versión C1. GET /api/catalogos la reemplaza.
        return new List<TipoActivoLocal>
        {
            new(){Codigo="VIVIENDA",Nombre="Vivienda"},
            new(){Codigo="EDIFICIO_PUBLICO",Nombre="Edificio público"},
            new(){Codigo="ESCUELA",Nombre="Escuela"},
            new(){Codigo="CENTRO_SALUD",Nombre="Centro de salud"},
            new(){Codigo="VIA",Nombre="Vía"},
            new(){Codigo="PUENTE",Nombre="Puente"},
            new(){Codigo="RED_AGUA",Nombre="Red de agua"},
            new(){Codigo="RED_ENERGIA",Nombre="Red de energía"},
            new(){Codigo="OTRO",Nombre="Otro"}
        };
    }

    public async Task GuardarTiposActivoAsync(IEnumerable<TipoActivoLocal> tipos)
    {
        await inicializacion.Value;
        var copia = tipos.Where(t => !string.IsNullOrWhiteSpace(t.Codigo))
            .GroupBy(t => t.Codigo, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First()).ToArray();
        if (copia.Length == 0) throw new InvalidDataException("El servidor no devolvió tipos de activo.");
        await baseLocal.RunInTransactionAsync(db => {
            db.Execute("DELETE FROM TipoActivoLocal");
            foreach (var tipo in copia) db.InsertOrReplace(tipo);
        });
    }

    public async Task GuardarCaracterizacionAsync(CaracterizacionPendiente c)
    {
        await inicializacion.Value;
        if (string.IsNullOrWhiteSpace(c.DesastreId) || c.RadioKm <= 0 || c.RadioKm > 500 ||
            !double.IsFinite(c.Latitud) || Math.Abs(c.Latitud) > 90 ||
            !double.IsFinite(c.Longitud) || Math.Abs(c.Longitud) > 180 ||
            (c.Nivel.HasValue && (c.Nivel < 1 || c.Nivel > 4)) || c.Descripcion.Length > 2000)
            throw new ArgumentException("Caracterización inválida.");
        await baseLocal.InsertAsync(c);
    }

    public async Task<List<CaracterizacionPendiente>> CaracterizacionesAsync()
    {
        await inicializacion.Value;
        return await baseLocal.QueryAsync<CaracterizacionPendiente>(
            "SELECT * FROM CaracterizacionPendiente WHERE OrganizacionLocal=? AND UsuarioLocal=? ORDER BY CapturadaEnUtc DESC",
            SesionCampo.Organizacion, SesionCampo.Usuario);
    }

    public async Task ResultadoCaracterizacionAsync(CaracterizacionPendiente c, string estado,
        string? motivo = null, long? versionActual = null)
    {
        await inicializacion.Value;
        c.Estado = estado;
        c.Motivo = motivo;
        c.VersionActual = versionActual;
        await baseLocal.UpdateAsync(c);
    }

    // Cada rebased crea una captura NUEVA, con una clave nueva. La anterior se archiva.
    // Se llama exclusivamente después de una decisión explícita del usuario.
    public async Task NuevaVersionCaracterizacionAsync(CaracterizacionPendiente anterior, long version)
    {
        await inicializacion.Value;
        var nueva = new CaracterizacionPendiente {
            OrganizacionLocal=anterior.OrganizacionLocal, UsuarioLocal=anterior.UsuarioLocal,
            DesastreId=anterior.DesastreId, Latitud=anterior.Latitud, Longitud=anterior.Longitud,
            RadioKm=anterior.RadioKm, Nivel=anterior.Nivel, Descripcion=anterior.Descripcion,
            VersionBase=version
        };
        await baseLocal.RunInTransactionAsync(db => {
            anterior.Estado="archivada";
            db.Update(anterior);
            db.Insert(nueva);
        });
    }

    public async Task<int> LegadoPendienteAsync(){await inicializacion.Value;return await baseLocal.ExecuteScalarAsync<int>("SELECT count(*) FROM EvaluacionPendiente WHERE OrganizacionLocal IS NULL");}
    public async Task AsignarLegadoAsync(){await inicializacion.Value;await baseLocal.ExecuteAsync("UPDATE EvaluacionPendiente SET OrganizacionLocal=?,UsuarioLocal=? WHERE OrganizacionLocal IS NULL",SesionCampo.Organizacion,SesionCampo.Usuario);}
    public async Task GuardarAsync(EvaluacionPendiente evaluacion)
    {
        await inicializacion.Value;
        // Una sola inserción confirma evaluación, operación pendiente y evidencia juntas.
        await baseLocal.InsertAsync(evaluacion);
    }

    public async Task<List<ResumenPendiente>> ListarAsync()
    {
        await inicializacion.Value;
        return await baseLocal.QueryAsync<ResumenPendiente>(
            """
            SELECT EvaluacionId, NivelAfectacion, Estado, Motivo, Descripcion, EvidenciaId
            FROM EvaluacionPendiente
            WHERE OrganizacionLocal = ? AND UsuarioLocal = ?
            ORDER BY CapturadaEnUtc DESC
            """, SesionCampo.Organizacion, SesionCampo.Usuario);
    }

    public async Task<List<RevisionLocal>> RevisionesAsync(string? id=null){await inicializacion.Value;
        return await baseLocal.QueryAsync<RevisionLocal>("SELECT * FROM RevisionLocal WHERE OrganizacionLocal=? AND UsuarioLocal=? AND (? IS NULL OR EvaluacionId=?) ORDER BY rowid DESC",SesionCampo.Organizacion,SesionCampo.Usuario,id,id);
    }
    public async Task GuardarRevisionAsync(RevisionLocal r){await inicializacion.Value;await baseLocal.InsertAsync(r);}
    public async Task ResultadoRevisionAsync(RevisionLocal r,long? version=null){await inicializacion.Value;await baseLocal.RunInTransactionAsync(db=>{db.Update(r);if(version is {} v)db.Execute("UPDATE EvaluacionPendiente SET VersionServidor=? WHERE EvaluacionId=? AND OrganizacionLocal=? AND UsuarioLocal=?",v,r.EvaluacionId,SesionCampo.Organizacion,SesionCampo.Usuario);});}
    public async Task ResolverRevisionAsync(RevisionLocal anterior,RevisionLocal nueva,long version){await inicializacion.Value;await baseLocal.RunInTransactionAsync(db=>{anterior.Estado="archivada";db.Update(anterior);db.Insert(nueva);db.Execute("UPDATE EvaluacionPendiente SET VersionServidor=? WHERE EvaluacionId=? AND OrganizacionLocal=? AND UsuarioLocal=?",version,nueva.EvaluacionId,SesionCampo.Organizacion,SesionCampo.Usuario);});}
    public async Task VersionAsync(string id,long v){await inicializacion.Value;await baseLocal.ExecuteAsync("UPDATE EvaluacionPendiente SET VersionServidor=? WHERE EvaluacionId=? AND OrganizacionLocal=? AND UsuarioLocal=?",v,id,SesionCampo.Organizacion,SesionCampo.Usuario);}
    public async Task ActualizarResultadoAsync(string id, string estado, string? motivo)
    {
        await inicializacion.Value;
        await baseLocal.ExecuteAsync(
            "UPDATE EvaluacionPendiente SET Estado = ?, Motivo = ? WHERE EvaluacionId = ?", estado, motivo, id);
    }

    public async Task AsignarNivelAnteriorAsync(string id, int nivel)
    {
        await inicializacion.Value;
        await baseLocal.ExecuteAsync(
            "UPDATE EvaluacionPendiente SET NivelAfectacion = ? WHERE EvaluacionId = ? AND NivelAfectacion IS NULL",
            nivel, id);
    }

    public async Task<EvaluacionPendiente> ObtenerAsync(string evaluacionId)
    {
        await inicializacion.Value;
        return await baseLocal.GetAsync<EvaluacionPendiente>(evaluacionId);
    }
}

public sealed class DesastreLocal
{
    public string? OrganizacionLocal {get;set;}
    [PrimaryKey] public string Id { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Ubicacion { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public string Severidad { get; set; } = "";
    public string Fase { get; set; } = "";
    public long Version { get; set; }
    public double? Latitud { get; set; }
    public double? Longitud { get; set; }
    public double? RadioKm { get; set; }
    public int? Nivel { get; set; }
    public string Fecha { get; set; } = "";
}

public sealed class RevisionLocal {
 [PrimaryKey] public string OperacionId {get;set;}=Guid.NewGuid().ToString();
 public string EvaluacionId {get;set;}="";
 public string OrganizacionLocal {get;set;}="";
 public string UsuarioLocal {get;set;}="";
 public long VersionBase {get;set;}
 public int NivelAfectacion {get;set;}
 public string Descripcion {get;set;}="";
 public string? Activo {get;set;}
 public double Latitud {get;set;} public double Longitud {get;set;}
 public string Motivo {get;set;}="";
 public string Estado {get;set;}="pendiente";
 public string? ServidorJson {get;set;}
 public string Fecha {get;set;}=DateTimeOffset.UtcNow.ToString("O");
}
