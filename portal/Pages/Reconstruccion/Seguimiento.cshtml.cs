using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Pgdr.Reconstruccion.V1;
using Portal.Servicios;

namespace Portal.Pages.Reconstruccion;

/// <summary>
/// Reconstrucción física (mínimo b) de un desastre: seguimiento, inicio y cierre, y creación de
/// proyectos desde las evaluaciones de daño. Todo por gRPC hacia Reconstrucción; las reglas (RN-xx)
/// las aplica Reconstrucción y el portal muestra su mensaje.
/// </summary>
public class SeguimientoModel(ReconstruccionCliente reconstruccion, NucleoCliente nucleo) : PaginaBase
{
    [BindProperty(SupportsGet = true)] public string Referencia { get; set; } = "";
    public JsonElement Desastre { get; private set; }
    public SeguimientoResponse Seguimiento { get; private set; } = new();
    public IReadOnlyList<EvaluacionMsg>? Evaluaciones { get; private set; }
    public string? AvisoEvaluaciones { get; private set; }
    public string Fase => Desastre.Texto("fase", "REGISTRO");

    public async Task OnGetAsync()
    {
        var token = Token;
        var desastre = nucleo.Desastre(token, Referencia);
        var seguimiento = reconstruccion.Seguimiento(token, Referencia);
        Desastre = await desastre;
        Seguimiento = await seguimiento;
        if (!Sesion.EsCoordinador) return;
        try
        {
            Evaluaciones = (await reconstruccion.Evaluaciones(token, Referencia)).Evaluaciones;
        }
        catch (ErrorServicio e) when (e.Estado != 401)
        {
            AvisoEvaluaciones = e.ParaUsuario();
        }
    }

    public Task<IActionResult> OnPostIniciarAsync() =>
        Ejecutar(Destino, "El desastre pasó a Reconstrucción.", () => reconstruccion.IniciarReconstruccion(Token, Referencia));

    public Task<IActionResult> OnPostCerrarAsync() =>
        Ejecutar(Destino, "El desastre quedó en Cierre.", () => reconstruccion.Cerrar(Token, Referencia));

    public Task<IActionResult> OnPostProyectosAsync(string nombre, string? tipo, List<string>? evaluaciones) =>
        Ejecutar(Destino, "Proyecto creado en estado Propuesto.",
            () => reconstruccion.Crear(Token, Referencia, (nombre ?? "").Trim(), tipo, evaluaciones ?? []));

    private string Destino => "/reconstruccion/" + Referencia;
}
