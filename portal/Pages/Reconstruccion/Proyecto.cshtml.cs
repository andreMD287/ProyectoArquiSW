using Microsoft.AspNetCore.Mvc;
using Pgdr.Reconstruccion.V1;
using Portal.Servicios;

namespace Portal.Pages.Reconstruccion;

/// <summary>
/// Ciclo de vida de un proyecto de reconstrucción. La autoridad (COORDINADOR) aprueba, ajusta el
/// presupuesto y cancela; el responsable asignado (EVALUADOR) ejecuta. Cada formulario envía la
/// versión del proyecto que vio el usuario: si otro lo cambió, Reconstrucción lo rechaza (409).
/// </summary>
public class ProyectoModel(ReconstruccionCliente reconstruccion) : PaginaBase
{
    [BindProperty(SupportsGet = true)] public string Referencia { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string Id { get; set; } = "";
    public ProyectoMsg Proyecto { get; private set; } = new();
    public IReadOnlyList<AuditoriaMsg>? Auditoria { get; private set; }
    public bool Abierto => Proyecto.Estado is not ("FINALIZADO" or "CANCELADO");

    public async Task OnGetAsync()
    {
        var token = Token;
        var seguimiento = await reconstruccion.Seguimiento(token, Referencia);
        Proyecto = seguimiento.Proyectos.FirstOrDefault(p => p.Id == Id)
                   ?? throw new ErrorServicio(404, "El proyecto no existe en este desastre.");
        if (!Sesion.EsEvaluador) Auditoria = (await reconstruccion.Auditoria(token, Id)).Registros;
    }

    public Task<IActionResult> OnPostAprobarAsync(string? presupuesto, string? fuente, string? responsable, long? version) =>
        Ejecutar(Destino, "Proyecto aprobado.", () => reconstruccion.Aprobar(Token, Id, presupuesto, fuente, responsable, version));

    public Task<IActionResult> OnPostIniciarAsync(string? contratista, string? inicio, string? fin, long? version) =>
        Ejecutar(Destino, "Ejecución iniciada.", () => reconstruccion.IniciarEjecucion(Token, Id, contratista, inicio, fin, version));

    public Task<IActionResult> OnPostAvanceAsync(int? porcentaje, string? descripcion, string? motivo, long? version) =>
        Ejecutar(Destino, "Avance registrado.", () => reconstruccion.Avance(Token, Id, porcentaje ?? -1, descripcion, motivo, version));

    public Task<IActionResult> OnPostCostoAsync(string? valor, string? descripcion, long? version) =>
        Ejecutar(Destino, "Costo registrado.", () => reconstruccion.Costo(Token, Id, valor, descripcion, version));

    public Task<IActionResult> OnPostEvidenciaAsync(string? descripcion, long? version) =>
        Ejecutar(Destino, "Evidencia adjunta.", () => reconstruccion.Evidencia(Token, Id, descripcion, version));

    public Task<IActionResult> OnPostPresupuestoAsync(string? presupuesto, string? motivo, long? version) =>
        Ejecutar(Destino, "Presupuesto modificado.", () => reconstruccion.Presupuesto(Token, Id, presupuesto, motivo, version));

    public Task<IActionResult> OnPostEstadoAsync(string? estado, string? motivo, long? version) =>
        Ejecutar(Destino, $"Estado cambiado a {estado}.", () => reconstruccion.Estado(Token, Id, estado, motivo, version));

    private string Destino => $"/reconstruccion/{Referencia}/proyectos/{Id}";
}
