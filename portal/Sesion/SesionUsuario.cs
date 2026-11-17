using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Portal.Sesion;

/// <summary>
/// Quién usa el portal: identidad que entrega el núcleo (usuario, rol y organismo) y el id con el
/// que el portal guarda su token. Viaja en la cookie de autenticación (cifrada y HttpOnly); el
/// token no va en ella.
/// </summary>
public sealed record SesionUsuario(string Usuario, string Rol, string OrganizacionId, string SesionId)
{
    public bool EsCoordinador => Rol == "COORDINADOR";
    public bool EsEvaluador => Rol == "EVALUADOR";

    /// <summary>COORDINADOR y EVALUADOR registran información de campo (evaluaciones y caracterización).</summary>
    public bool PuedeEvaluar => EsCoordinador || EsEvaluador;

    /// <summary>Nombre del rol tal como lo ve Reconstrucción (COORDINADOR = autoridad, EVALUADOR = responsable).</summary>
    public string RolReconstruccion => Rol switch
    {
        "COORDINADOR" => "Autoridad",
        "EVALUADOR" => "Responsable",
        _ => "Auditor",
    };

    public static SesionUsuario? De(ClaimsPrincipal usuario)
    {
        if (usuario.Identity?.IsAuthenticated != true) return null;
        var sesion = usuario.FindFirstValue("sesion");
        if (string.IsNullOrEmpty(sesion)) return null;
        return new SesionUsuario(usuario.FindFirstValue("usuario") ?? "", usuario.FindFirstValue(ClaimTypes.Role) ?? "",
            usuario.FindFirstValue("organizacion") ?? "", sesion);
    }

    public ClaimsPrincipal ComoPrincipal() => new(new ClaimsIdentity(
    [
        new Claim("usuario", Usuario), new Claim(ClaimTypes.Name, Usuario), new Claim(ClaimTypes.Role, Rol),
        new Claim("organizacion", OrganizacionId), new Claim("sesion", SesionId),
    ], CookieAuthenticationDefaults.AuthenticationScheme));
}
