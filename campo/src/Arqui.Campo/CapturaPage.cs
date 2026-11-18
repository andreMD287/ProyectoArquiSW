using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Storage;
using System.Globalization;
using System.Security.Cryptography;

namespace Arqui.Campo;

public sealed class CapturaPage : ContentPage
{
    private readonly Entry usuarioLogin=new(){Placeholder="Usuario",MaxLength=80};
    private readonly Entry claveLogin=new(){Placeholder="Contraseña",IsPassword=true,MaxLength=256};
    private readonly Button entrar=new(){Text="Iniciar sesión"};
    private readonly Button salir=new(){Text="Cerrar sesión"};
    private readonly Button legado=new(){Text="Asignar capturas anteriores a mi organismo"};
    private readonly Label estadoSesion=new();
    private const int MaxFotoBytes = 5 * 1024 * 1024;

    private readonly AlmacenCampo almacen;
    private readonly Picker nivel = new()
    {
        Title = "Nivel de afectación (1–5)",
        ItemsSource = new[] { "1", "2", "3", "4", "5" }
    };
    private readonly Entry activo=new(){Placeholder="Nombre del activo · ej. Puente La Esperanza",MaxLength=120};
    private readonly Picker elegirTipoActivo = new() { Title = "Tipo de activo" };
    private List<TipoActivoLocal> tiposActivo = new();
    private readonly Picker desastreCaracterizacion = new() { Title = "Desastre para caracterizar" };
    private readonly Entry latCaracterizacion = new() { Placeholder = "Latitud · ej. 4.71" };
    private readonly Entry lonCaracterizacion = new() { Placeholder = "Longitud · ej. -74.07" };
    private readonly Entry radioCaracterizacion = new() { Placeholder = "Radio en kilómetros · ej. 2,5" };
    private readonly Picker nivelCaracterizacion = new() { Title = "Nivel del desastre (opcional, 1–4)", ItemsSource = new[] { "1", "2", "3", "4" } };
    private readonly Editor descCaracterizacion = new() { Placeholder = "Descripción (opcional)", MaxLength = 2000, HeightRequest = 90 };
    private readonly Button gpsCaracterizacion = new() { Text = "Usar GPS para caracterización" };
    private readonly Button guardarCaracterizacion = new() { Text = "Guardar caracterización sin conexión" };
    private readonly Label estadoCaracterizacion = new();
    private readonly VerticalStackLayout listaCaracterizaciones = new() { Spacing = 8 };
    private readonly Entry evento = new() { Placeholder = "Referencia del desastre (opcional)", MaxLength = 100 };
    private readonly Picker elegirDesastre = new() { Title = "Seleccionar un desastre descargado" };
    private readonly Button descargarDesastres = new() { Text = "Actualizar catálogo de desastres" };
    private readonly Label estadoCatalogo = new();
    private List<DesastreLocal> catalogo = new();
    private bool cargandoCatalogo;
    private readonly Entry servidor = new() { Placeholder = "URL del sistema central", Keyboard = Keyboard.Url };
    private readonly Button sincronizar = new() { Text = "Sincronizar ahora" };
    private readonly Label estadoSync = new();
    private readonly IDispatcherTimer timer;
    private bool sincronizando;
    private readonly Editor descripcion = new() { Placeholder = "Describe el daño", MaxLength = 1000, HeightRequest = 90 };
    private readonly Entry latitud = new() { Placeholder = "Latitud, ej. 4.6097" };
    private readonly Entry longitud = new() { Placeholder = "Longitud, ej. -74.0817" };
    private readonly Button ubicar = new() { Text = "Usar ubicación del teléfono" };
    private (string Latitud, string Longitud)? ubicacionDetectada;
    private readonly Button tomarFoto = new() { Text = "Tomar foto" };
    private readonly Button guardar = new() { Text = "Guardar en este teléfono" };
    private readonly Label estado = new();
    private readonly Label estadoFoto = new() { Text = "Sin foto adjunta" };
    private readonly VerticalStackLayout pendientes = new() { Spacing = 8 };
    private byte[]? foto;
    private string? fotoTipo;

    public CapturaPage(AlmacenCampo almacen)
    {
        this.almacen = almacen;
        Title = "Arqui Campo";
        entrar.Clicked+=async(_,_)=>{
            if(!SincronizadorCampo.TryCrearDestino(servidor.Text,out var destino)){estadoSesion.Text="Indica un servidor válido.";return;}
            if(sincronizando || !descargarDesastres.IsEnabled){estadoSesion.Text="Espera que termine la sincronización.";return;}
            entrar.IsEnabled=false;
            try{await SesionCampo.Entrar(destino,usuarioLogin.Text??"",claveLogin.Text??"");claveLogin.Text="";Preferences.Default.Set("ServidorCampo",servidor.Text.Trim());ActualizarSesion();legado.IsVisible=SesionCampo.Rol=="COORDINADOR" && await almacen.LegadoPendienteAsync()>0;await ActualizarPendientesAsync();await CargarCatalogoLocalAsync();}
            catch(Exception e){estadoSesion.Text=e is InvalidOperationException?e.Message:"No se pudo iniciar sesión. Comprueba la conexión.";}
            finally{entrar.IsEnabled=true;}
        };
        salir.Clicked+=async(_,_)=>{
            if(sincronizando || !descargarDesastres.IsEnabled)return;
            try{if(!SincronizadorCampo.TryCrearDestino(servidor.Text,out var destino))return;await SesionCampo.Salir(destino);ActualizarSesion();await ActualizarPendientesAsync();await CargarCatalogoLocalAsync();}
            catch(Exception){estadoSesion.Text="No se pudo cerrar sesión en el servidor. Reintenta con conexión.";}
        };
        legado.Clicked+=async(_,_)=>{
            if(SesionCampo.Rol!="COORDINADOR")return;
            if(await DisplayAlertAsync("Capturas anteriores","¿Confirmas que las capturas sin organismo de este teléfono pertenecen a tu organización?","Asignar","Cancelar")){
                await almacen.AsignarLegadoAsync();await ActualizarPendientesAsync();legado.IsVisible=false;
            }
        };
        ubicar.Clicked += UbicarAsync;
        gpsCaracterizacion.Clicked += UbicarCaracterizacionAsync;
        guardarCaracterizacion.Clicked += GuardarCaracterizacionAsync;
        tomarFoto.Clicked += TomarFotoAsync;
        guardar.Clicked += GuardarAsync;
        descargarDesastres.Clicked += async (_, _) => await DescargarDesastresAsync();
        elegirDesastre.SelectedIndexChanged += (_, _) => {
            if (cargandoCatalogo || elegirDesastre.SelectedIndex < 0) return;
            evento.Text = elegirDesastre.SelectedIndex == 0 ? "" : catalogo[elegirDesastre.SelectedIndex - 1].Id;
        };
        servidor.Text = Preferences.Default.Get("ServidorCampo", "http://127.0.0.1:8084");
        sincronizar.Clicked += async (_, _) => await SincronizarAsync(manual: true);
        timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromSeconds(8);
        timer.Tick += async (_, _) => await SincronizarAsync(manual: false);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label {Text="Acceso del organismo",FontSize=24},
                    servidor,usuarioLogin,claveLogin,entrar,salir,estadoSesion,legado,
                    new Label { Text = "Evaluación de daños", FontSize = 24 },
                    elegirDesastre, evento,
                    new Label { Text = "Sin referencia se guarda una evaluación preliminar. El catálogo descargado funciona sin conexión." },
                    nivel, elegirTipoActivo, activo, descripcion, latitud, longitud, ubicar, tomarFoto, estadoFoto,
                    guardar, estado,
                    new Label { Text = "Caracterización del desastre", FontSize = 24 },
                    new Label { Text = "Se guarda primero en SQLite. Requiere seleccionar un desastre del catálogo local." },
                    desastreCaracterizacion, latCaracterizacion, lonCaracterizacion,
                    gpsCaracterizacion, radioCaracterizacion, nivelCaracterizacion,
                    descCaracterizacion, guardarCaracterizacion, estadoCaracterizacion,
                    new Label { Text = "Caracterizaciones del teléfono", FontSize = 20 },
                    listaCaracterizaciones,
                    new Label { Text = "Sistema central", FontSize = 20 },
                    descargarDesastres, estadoCatalogo, sincronizar, estadoSync,
                    new Label { Text = "Evaluaciones del teléfono", FontSize = 20 },
                    pendientes
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try{await SesionCampo.Restaurar();}catch(Exception){estadoSesion.Text="No se pudo restaurar la sesión. Inicia sesión nuevamente.";}
        ActualizarSesion();
        legado.IsVisible=SesionCampo.Rol=="COORDINADOR" && await almacen.LegadoPendienteAsync()>0;
        await ActualizarPendientesAsync();
        await CargarCatalogoLocalAsync();
        timer.Start();
    }

    protected override void OnDisappearing()
    {
        timer.Stop();
        base.OnDisappearing();
    }

    private void ActualizarSesion(){
        bool activa=!string.IsNullOrEmpty(SesionCampo.Organizacion);
        estadoSesion.Text=activa?$"{SesionCampo.Usuario} · {SesionCampo.Rol}. Puedes capturar sin conexión; sincronizar requiere una sesión válida.":"Inicia sesión antes de capturar o sincronizar.";
        guardar.IsEnabled=activa && SesionCampo.Rol!="CONSULTA";
        guardarCaracterizacion.IsEnabled=activa && (SesionCampo.Rol is "COORDINADOR" or "EVALUADOR");
        salir.IsVisible=activa;
        if (SesionCampo.SesionVencida)
            estadoSesion.Text="Sesión vencida. Vuelve a iniciar sesión para sincronizar; nada se borró de SQLite.";
        legado.IsVisible=false;
    }
    private async Task CargarCatalogoLocalAsync()
    {
        try
        {
            cargandoCatalogo = true;
            catalogo = await almacen.DesastresAsync();
            elegirDesastre.ItemsSource = new[] { "Evaluación preliminar · sin desastre" }
                .Concat(catalogo.Select(d => $"{d.Id} · {d.Ubicacion}")).ToArray();
            elegirDesastre.SelectedIndex = -1;
            desastreCaracterizacion.ItemsSource = catalogo.Select(d => $"{d.Id} · {d.Ubicacion} (v{d.Version}, {d.Fase})").ToArray();
            desastreCaracterizacion.SelectedIndex = -1;
            tiposActivo = await almacen.TiposActivoAsync();
            elegirTipoActivo.ItemsSource = tiposActivo.Select(t => $"{t.Nombre} ({t.Codigo})").ToArray();
            elegirTipoActivo.SelectedIndex = tiposActivo.FindIndex(t => t.Codigo == "OTRO");
            if (string.IsNullOrEmpty(estadoCatalogo.Text))
                estadoCatalogo.Text = $"{catalogo.Count} desastres guardados para consulta sin conexión.";
        }
        catch (Exception) { estadoCatalogo.Text = "No se pudo leer el catálogo. Puedes conservar una referencia manual o capturar una evaluación preliminar."; }
        finally { cargandoCatalogo = false; }
    }

    private async Task DescargarDesastresAsync()
    {
        if (!SincronizadorCampo.TryCrearDestino(servidor.Text, out var destino))
        { estadoCatalogo.Text = "Indica una URL HTTPS o la dirección local de prueba."; return; }
        descargarDesastres.IsEnabled = false;
        try
        {
            var resultado = await SincronizadorCampo.DescargarCatalogoAsync(almacen, destino);
            estadoCatalogo.Text = resultado.Disponible ? $"Catálogo actualizado: {resultado.Desastres.Length} desastres disponibles sin conexión." :
                "El registro no responde. Se descargó la última copia disponible; puedes seguir capturando.";
            Preferences.Default.Set("ServidorCampo", servidor.Text.Trim());
        }
        catch (SesionVencidaException) { estadoCatalogo.Text = "Sesión vencida: inicia sesión nuevamente. Catálogo local conservado."; ActualizarSesion(); }
        catch (Exception) { estadoCatalogo.Text = "Sin conexión o catálogo incompleto. Se conserva la copia local."; }
        finally { await CargarCatalogoLocalAsync(); descargarDesastres.IsEnabled = true; }
    }

    private async Task SincronizarAsync(bool manual)
    {
        if (sincronizando || string.IsNullOrEmpty(SesionCampo.Token) || SesionCampo.SesionVencida)
        {
            if (manual && SesionCampo.SesionVencida)
                estadoSync.Text="Sesión vencida. Inicia sesión otra vez; los registros pendientes están conservados.";
            return;
        }
        if (!SincronizadorCampo.TryCrearDestino(servidor.Text, out var destino))
        {
            if (manual)
                estadoSync.Text = "Usa una URL HTTPS o la dirección local de prueba.";
            return;
        }

        sincronizando = true;
        sincronizar.IsEnabled = servidor.IsEnabled = false;
        try
        {
            if (manual)
                Preferences.Default.Set("ServidorCampo", servidor.Text.Trim());
            var conteo = await SincronizadorCampo.EnviarAsync(almacen, destino);
            if (conteo > 0 || manual)
                estadoSync.Text = conteo == 0 ? "No hay operaciones listas para enviar." :
                    $"Resultados recibidos para {conteo} operaciones.";
        }
        catch (SesionVencidaException)
        {
            estadoSync.Text = "Sesión vencida. Inicia sesión de nuevo para continuar, sin borrar capturas.";
            ActualizarSesion();
        }
        catch (Exception)
        {
            estadoSync.Text = "Sin respuesta del sistema central. Se volverá a intentar sin perder capturas.";
        }
        finally
        {
            sincronizando = false;
            sincronizar.IsEnabled = servidor.IsEnabled = true;
            await ActualizarPendientesAsync();
        }
    }

    private async void UbicarAsync(object? sender, EventArgs args)
    {
        CambiarOcupado(true);
        try
        {
            var solicitud = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(12));
            var posicion = await Geolocation.Default.GetLocationAsync(solicitud);
            if (posicion is null)
            {
                estado.Text = "Ubicación no disponible. Puedes ingresar las coordenadas manualmente.";
                return;
            }

            latitud.Text = posicion.Latitude.ToString("R", CultureInfo.InvariantCulture);
            longitud.Text = posicion.Longitude.ToString("R", CultureInfo.InvariantCulture);
            ubicacionDetectada = (latitud.Text, longitud.Text);
            estado.Text = "Ubicación tomada del teléfono. Puedes ajustarla antes de guardar.";
        }
        catch (Exception)
        {
            estado.Text = "Ubicación no disponible. Puedes ingresar las coordenadas manualmente.";
        }
        finally
        {
            CambiarOcupado(false);
        }
    }

    private async void TomarFotoAsync(object? sender, EventArgs args)
    {
        CambiarOcupado(true);
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                estado.Text = "Este dispositivo no tiene una cámara disponible.";
                return;
            }

            var captura = await MediaPicker.Default.CapturePhotoAsync();
            if (captura is null)
                return;

            await using var archivo = await captura.OpenReadAsync();
            using var datos = new MemoryStream();
            var buffer = new byte[81920];
            int leidos;
            while ((leidos = await archivo.ReadAsync(buffer)) > 0)
            {
                if (datos.Length + leidos > MaxFotoBytes)
                {
                    estado.Text = "La foto supera el límite de 5 MiB.";
                    return;
                }
                datos.Write(buffer, 0, leidos);
            }

            if (datos.Length == 0)
            {
                estado.Text = "La cámara devolvió una foto vacía.";
                return;
            }

            foto = datos.ToArray();
            fotoTipo = captura.ContentType is { } contentType && contentType.StartsWith("image/")
                ? contentType : "image/jpeg";
            estadoFoto.Text = "Foto lista para guardar";
            estado.Text = "";
        }
        catch (Exception)
        {
            estado.Text = "No se pudo adjuntar la foto. Revisa el permiso de cámara y vuelve a intentar.";
        }
        finally
        {
            CambiarOcupado(false);
        }
    }

    private async void GuardarAsync(object? sender, EventArgs args)
    {
        if(string.IsNullOrEmpty(SesionCampo.Organizacion) || SesionCampo.Rol=="CONSULTA"){estado.Text="Inicia sesión con permiso de captura.";return;}
        if (nivel.SelectedIndex < 0)
        {
            estado.Text = "Selecciona un nivel de afectación de 1 a 5. La referencia del desastre puede completarse después.";
            return;
        }

        if (!LeerCoordenada(latitud.Text, 90, out var lat) || !LeerCoordenada(longitud.Text, 180, out var lon))
        {
            estado.Text = "Ingresa latitud y longitud válidas en grados decimales.";
            return;
        }

        CambiarOcupado(true);
        try
        {
            await almacen.GuardarAsync(new EvaluacionPendiente
            {
                OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,
                Activo=activo.Text?.Trim(),
                TipoActivo=elegirTipoActivo.SelectedIndex>=0 ? tiposActivo[elegirTipoActivo.SelectedIndex].Codigo : "OTRO",
                EventoId = evento.Text?.Trim() ?? "",
                NivelAfectacion = nivel.SelectedIndex + 1,
                Descripcion = descripcion.Text?.Trim() ?? "",
                Latitud = lat,
                Longitud = lon,
                FuenteUbicacion = ubicacionDetectada == (latitud.Text, longitud.Text) ? "DISPOSITIVO" : "MANUAL",
                EvidenciaId = foto is null ? null : Guid.NewGuid().ToString(),
                FotoTipo = fotoTipo,
                FotoSha256 = foto is null ? null : Convert.ToHexString(SHA256.HashData(foto)),
                Foto = foto
            });

            activo.Text="";
            nivel.SelectedIndex = -1;
            descripcion.Text = latitud.Text = longitud.Text = "";
            ubicacionDetectada = null;
            foto = null;
            fotoTipo = null;
            estadoFoto.Text = "Sin foto adjunta";
            estado.Text = "Evaluación guardada en el teléfono. Quedó pendiente de sincronizar.";
            await ActualizarPendientesAsync();
        }
        catch (Exception)
        {
            estado.Text = "No se pudo guardar. Revisa el espacio disponible y vuelve a intentar.";
        }
        finally
        {
            CambiarOcupado(false);
        }
    }

    private async void UbicarCaracterizacionAsync(object? sender, EventArgs args)
    {
        gpsCaracterizacion.IsEnabled = false;
        try
        {
            var ubicacion = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Best,TimeSpan.FromSeconds(12)));
            if (ubicacion is null) throw new InvalidOperationException();
            latCaracterizacion.Text = ubicacion.Latitude.ToString("R",CultureInfo.InvariantCulture);
            lonCaracterizacion.Text = ubicacion.Longitude.ToString("R",CultureInfo.InvariantCulture);
            estadoCaracterizacion.Text = "Coordenadas obtenidas. Verifica el radio antes de guardar.";
        }
        catch (Exception) { estadoCaracterizacion.Text = "GPS no disponible. Introduce las coordenadas manualmente."; }
        finally { gpsCaracterizacion.IsEnabled = true; }
    }

    private async void GuardarCaracterizacionAsync(object? sender, EventArgs args)
    {
        if (string.IsNullOrEmpty(SesionCampo.Organizacion) || SesionCampo.Rol is not ("COORDINADOR" or "EVALUADOR"))
        { estadoCaracterizacion.Text = "Inicia sesión como coordinador o evaluador."; return; }
        if (desastreCaracterizacion.SelectedIndex < 0 || desastreCaracterizacion.SelectedIndex >= catalogo.Count)
        { estadoCaracterizacion.Text = "Selecciona un desastre descargado previamente."; return; }
        var d = catalogo[desastreCaracterizacion.SelectedIndex];
        if (string.Equals(d.Fase,"CIERRE",StringComparison.OrdinalIgnoreCase))
        { estadoCaracterizacion.Text = "Este desastre está cerrado en el catálogo local; no se puede caracterizar."; return; }
        if (!LeerCoordenada(latCaracterizacion.Text,90,out var lat) ||
            !LeerCoordenada(lonCaracterizacion.Text,180,out var lon) ||
            !double.TryParse(radioCaracterizacion.Text?.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var radio) ||
            !double.IsFinite(radio) || radio <= 0 || radio > 500)
        { estadoCaracterizacion.Text="Verifica latitud, longitud y radio (mayor que 0 y hasta 500 km)."; return; }
        guardarCaracterizacion.IsEnabled=false;
        try
        {
            var c = new CaracterizacionPendiente {
                OrganizacionLocal=SesionCampo.Organizacion, UsuarioLocal=SesionCampo.Usuario,
                DesastreId=d.Id, VersionBase=d.Version, Latitud=lat, Longitud=lon,
                RadioKm=radio, Nivel=nivelCaracterizacion.SelectedIndex < 0 ? null : nivelCaracterizacion.SelectedIndex+1,
                Descripcion=descCaracterizacion.Text?.Trim() ?? ""
            };
            await almacen.GuardarCaracterizacionAsync(c);
            latCaracterizacion.Text=lonCaracterizacion.Text=radioCaracterizacion.Text=descCaracterizacion.Text="";
            nivelCaracterizacion.SelectedIndex=-1;
            estadoCaracterizacion.Text=$"Caracterización de {d.Id} guardada sin conexión (base v{d.Version}).";
            await ActualizarPendientesAsync();
        }
        catch (Exception e) { estadoCaracterizacion.Text="No se pudo guardar: " + e.Message; }
        finally { guardarCaracterizacion.IsEnabled=true; }
    }

    private async Task ActualizarPendientesAsync()
    {
        try
        {
            var registros = await almacen.ListarAsync();
            pendientes.Children.Clear();
            listaCaracterizaciones.Children.Clear();
            var caracterizaciones = await almacen.CaracterizacionesAsync();
            if (caracterizaciones.Count == 0)
                listaCaracterizaciones.Children.Add(new Label { Text="Aún no hay caracterizaciones guardadas." });
            foreach(var c in caracterizaciones)
            {
                var filaC = new Button { Text=$"{c.DesastreId} · {c.Estado} · v{c.VersionBase}" };
                filaC.Clicked += async (_,_) => await RevisarCaracterizacionAsync(c);
                listaCaracterizaciones.Children.Add(filaC);
            }
            if (registros.Count == 0)
                pendientes.Children.Add(new Label { Text = "Aún no hay evaluaciones guardadas." });

            foreach (var registro in registros)
            {
                var evidencia = registro.EvidenciaId is null ? "sin foto" : "con foto";
                var nivelTexto = registro.NivelAfectacion is int numero ? $"Nivel {numero}" : "Asignar nivel";
                var fila = new Button { Text = $"{nivelTexto} · {Etiqueta(registro.Estado)} · {evidencia}" };
                fila.Clicked += async (_, _) => await AbrirPendienteAsync(registro.EvaluacionId);
                var revisiones=await almacen.RevisionesAsync(registro.EvaluacionId);
                if(revisiones.Any(x=>x.Estado=="conflicto"))fila.Text+=" · Corrección en conflicto";
                else if(revisiones.Any(x=>x.Estado=="pendiente"))fila.Text+=" · Corrección pendiente";
                pendientes.Children.Add(fila);
            }
        }
        catch (Exception)
        {
            estado.Text = "No se pudo leer la lista de pendientes. Vuelve a abrir la pantalla.";
        }
    }

    private async Task RevisarCaracterizacionAsync(CaracterizacionPendiente c)
    {
        if(c.Estado != "conflicto")
        {
            await DisplayAlertAsync("Caracterización local",
                $"Desastre: {c.DesastreId}\nEstado: {c.Estado}\nBase: v{c.VersionBase}\n{c.Motivo}","Cerrar");
            return;
        }
        var actual = (await almacen.DesastresAsync()).FirstOrDefault(d => d.Id == c.DesastreId);
        if(actual is null || actual.Version <= c.VersionBase)
        {
            await DisplayAlertAsync("Conflicto de versión",
                "Actualiza el catálogo de desastres con conexión antes de decidir. La captura sigue intacta.","Entendido");
            return;
        }
        if(string.Equals(actual.Fase,"CIERRE",StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlertAsync("Desastre cerrado",
                "El desastre está cerrado. La propuesta se conserva en conflicto; no se reenvía automáticamente.","Entendido");
            return;
        }
        var confirmado=await DisplayAlertAsync("Resolver conflicto C2",
            $"Tu captura usa v{c.VersionBase}; el servidor informa v{actual.Version}. " +
            $"Descripción actual: {actual.Descripcion}.\n¿Crear una nueva operación con tus datos sobre la versión vigente? " +
            "El registro anterior quedará archivado, sin sobrescritura silenciosa.",
            "Crear nueva versión","Conservar conflicto");
        if(!confirmado)return;
        await almacen.NuevaVersionCaracterizacionAsync(c,actual.Version);
        await ActualizarPendientesAsync();
        estadoCaracterizacion.Text="Nueva operación pendiente con clave de idempotencia diferente.";
    }

    private async Task AbrirPendienteAsync(string id)
    {
        try
        {
            var registro = await almacen.ObtenerAsync(id);
            if (registro.NivelAfectacion is null)
            {
                var opcion = await DisplayActionSheetAsync("Asigna un nivel al registro anterior", "Cancelar", null,
                    "1", "2", "3", "4", "5");
                if (!int.TryParse(opcion, out var elegido))
                    return;
                await almacen.AsignarNivelAnteriorAsync(id, elegido);
                registro.NivelAfectacion = elegido;
                await ActualizarPendientesAsync();
            }

            var revisiones=await almacen.RevisionesAsync(id);
            var ultima=revisiones.FirstOrDefault(x=>x.Estado is "incorporada" or "servidor");
            if(ultima is not null){registro.NivelAfectacion=ultima.NivelAfectacion;registro.Descripcion=ultima.Descripcion;registro.Activo=ultima.Activo;registro.Latitud=ultima.Latitud;registro.Longitud=ultima.Longitud;}
            var detalle = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    new Label { Text = $"{registro.EventoId} · Nivel {registro.NivelAfectacion}/5", FontSize = 20 },
                    new Label { Text = registro.Descripcion },
                    new Label { Text = FormattableString.Invariant($"Coordenadas: {registro.Latitud}, {registro.Longitud}") },
                    new Label { Text = Etiqueta(registro.Estado) },
                    new Label { Text = registro.Motivo ?? "" }
                }
            };

            if (registro.Foto is { } bytes)
            {
                detalle.Children.Add(new Image
                {
                    Source = ImageSource.FromStream(() => new MemoryStream(bytes, writable: false)),
                    HeightRequest = 280,
                    Aspect = Aspect.AspectFit
                });
            }

            var pendiente=revisiones.FirstOrDefault(x=>x.Estado is "pendiente" or "conflicto");
            if(pendiente is not null){
                detalle.Children.Add(new Label{Text=$"Propuesta local: daño {pendiente.NivelAfectacion}/5 · {pendiente.Descripcion}. Estado: {pendiente.Estado}"});
                if(pendiente.Estado=="conflicto" && pendiente.ServidorJson is {} raw){
                    var remoto=System.Text.Json.JsonSerializer.Deserialize(raw,CampoJson.Default.VersionCampo)!;
                    detalle.Children.Add(new Label{Text=$"Servidor v{remoto.Version}: daño {remoto.NivelAfectacion}/5 · {remoto.Descripcion}. Tu propuesta se conserva sin sobrescribir el servidor."});
                    foreach(bool conservar in new[]{false,true}){
                        var resolver=new Button{Text=conservar?"Revisar y reenviar mi propuesta":"Conservar versión del servidor"};
                        resolver.Clicked+=async(_,_)=>{
                            if(!await DisplayAlertAsync("Resolver conflicto",conservar?$"Se enviará tu propuesta sobre la versión {remoto.Version} revisada. Otro cambio posterior volverá a generar conflicto.":"Se conservará la versión del servidor; tu propuesta anterior quedará archivada en el teléfono.","Confirmar","Cancelar"))return;
                            resolver.IsEnabled=false;
                            var nueva=new RevisionLocal{EvaluacionId=id,OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,VersionBase=remoto.Version,NivelAfectacion=conservar?pendiente.NivelAfectacion:remoto.NivelAfectacion,Descripcion=conservar?pendiente.Descripcion:remoto.Descripcion??"",Activo=conservar?pendiente.Activo:remoto.Activo,Latitud=conservar?pendiente.Latitud:remoto.Latitud,Longitud=conservar?pendiente.Longitud:remoto.Longitud,Motivo=conservar?pendiente.Motivo:"Resolución: conservar servidor",Estado=conservar?"pendiente":"servidor"};
                            await almacen.ResolverRevisionAsync(pendiente,nueva,remoto.Version);await Navigation.PopAsync();
                        };detalle.Children.Add(resolver);
                    }
                }
            }else if(registro.Estado=="incorporada" && SesionCampo.Rol!="CONSULTA"){
                var editar=new Button{Text=$"Preparar corrección offline · versión {registro.VersionServidor}"};
                editar.Clicked+=async(_,_)=>{
                    var propuesta=new RevisionLocal{EvaluacionId=id,OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,VersionBase=registro.VersionServidor,NivelAfectacion=registro.NivelAfectacion??1,Descripcion=registro.Descripcion,Activo=registro.Activo,Latitud=registro.Latitud,Longitud=registro.Longitud};
                    var texto=new Editor{Text=propuesta.Descripcion,MaxLength=1000,HeightRequest=100};var tipo=new Entry{Text=propuesta.Activo,Placeholder="Activo",MaxLength=120};var daño=new Picker{Title="Nivel de daño",ItemsSource=new[]{"1","2","3","4","5"},SelectedIndex=propuesta.NivelAfectacion-1};var motivo=new Entry{Placeholder="Motivo de la corrección",MaxLength=500};var guardarCambio=new Button{Text="Guardar corrección en el teléfono"};
                    guardarCambio.Clicked+=async(_,_)=>{if(string.IsNullOrWhiteSpace(motivo.Text)||daño.SelectedIndex<0)return;guardarCambio.IsEnabled=false;propuesta.Descripcion=texto.Text??"";propuesta.Activo=tipo.Text;propuesta.NivelAfectacion=daño.SelectedIndex+1;propuesta.Motivo=motivo.Text.Trim();await almacen.GuardarRevisionAsync(propuesta);await Navigation.PopAsync();await Navigation.PopAsync();};
                    await Navigation.PushAsync(new ContentPage{Title="Corrección offline",Content=new ScrollView{Content=new VerticalStackLayout{Padding=20,Spacing=12,Children={new Label{Text="La versión original y su foto se conservan. Al sincronizar se verificará si alguien más la modificó."},daño,tipo,texto,motivo,guardarCambio}}}});
                };detalle.Children.Add(editar);
            }
            var volver = new Button { Text = "Volver a captura y sincronización" };
            volver.Clicked += async (_, _) => await Navigation.PopAsync();
            detalle.Children.Add(volver);
            await Navigation.PushAsync(new ContentPage
            {
                Title = "Evaluación guardada",
                Content = new ScrollView { Content = detalle }
            });
        }
        catch (Exception)
        {
            estado.Text = "No se pudo abrir la evaluación guardada.";
        }
    }

    private static string Etiqueta(string? estado) => estado switch
    {
        "recibida" => "Recibida · en proceso",
        "incorporada" => "Sincronizada",
        "conflicto" => "En conflicto",
        "rechazada" => "Rechazada",
        _ => "Pendiente"
    };

    private static bool LeerCoordenada(string? texto, double limite, out double valor) =>
        double.TryParse(texto?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out valor)
        && double.IsFinite(valor) && Math.Abs(valor) <= limite;

    private void CambiarOcupado(bool ocupado)
    {
        elegirDesastre.IsEnabled = evento.IsEnabled = nivel.IsEnabled = descripcion.IsEnabled =
            latitud.IsEnabled = longitud.IsEnabled = ubicar.IsEnabled = tomarFoto.IsEnabled = guardar.IsEnabled = !ocupado;
        elegirTipoActivo.IsEnabled = !ocupado;
    }
}
