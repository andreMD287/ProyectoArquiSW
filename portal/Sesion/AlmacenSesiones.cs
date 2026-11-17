using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace Portal.Sesion;

/// <summary>
/// Tokens del núcleo de las sesiones abiertas, en la memoria del portal y con la misma vigencia
/// que el token. El navegador solo conoce el id de sesión. Limitación conocida: si el portal se
/// reinicia, las sesiones se pierden y hay que entrar de nuevo.
/// </summary>
public sealed class AlmacenSesiones(IMemoryCache memoria)
{
    public string Guardar(string token, DateTimeOffset expiraEn)
    {
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        memoria.Set(Clave(id), token, expiraEn);
        return id;
    }

    public string? Token(string sesionId) => memoria.TryGetValue(Clave(sesionId), out string? token) ? token : null;

    public void Quitar(string sesionId) => memoria.Remove(Clave(sesionId));

    private static string Clave(string id) => "sesion:" + id;
}
