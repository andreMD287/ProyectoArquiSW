#if CAMPO_PRUEBAS
using Android.App;
using Android.OS;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Maui.Storage;
namespace Arqui.Campo;
[Instrumentation(Name="co.edu.arqui.campo.PruebasCampo",TargetPackage="co.edu.arqui.campo")]
[Android.Runtime.Preserve(AllMembers=true)]
public class PruebasCampo:Instrumentation {
 public PruebasCampo(){}
 protected PruebasCampo(nint handle,Android.Runtime.JniHandleOwnership ownership):base(handle,ownership){}
 string usuario="",clave="";
 public override void OnCreate(Bundle? args){base.OnCreate(args);usuario=args?.GetString("usuario")??"";clave=args?.GetString("clave")??"";Start();}
 void Estado(string text){var b=new Bundle();b.PutString("fase",text);SendStatus(0,b);}
 void Check(bool ok,string msg){if(!ok)throw new Exception(msg);Estado("OK: "+msg);}
 public override void OnStart(){var result=new Bundle();try{Ejecutar().GetAwaiter().GetResult();result.PutString("stream","APROBADO: SQLite, login, desconexión real, resolución de conflictos, tipo de activo (C1), caracterización (C2), 409 y 401 en Android\n");Finish(Result.Ok,result);}catch(Exception e){result.PutString("stream","FALLO: "+e.GetType().Name+" "+e.Message+"\n");Finish(Result.Canceled,result);}}
 async Task<JsonObject> Http(HttpMethod method,Uri uri,JsonObject? body=null){using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(3)};using var req=new HttpRequestMessage(method,uri);SesionCampo.Autorizar(req);if(body is not null)req.Content=new StringContent(body.ToJsonString(),System.Text.Encoding.UTF8,"application/json");using var res=await http.SendAsync(req);res.EnsureSuccessStatusCode();return JsonNode.Parse(await res.Content.ReadAsStringAsync())!.AsObject();}
 // La copia del catálogo en servidor se actualiza por eventos: se espera a que llegue la versión pedida.
 static async Task<DesastreLocal?> DesastreServidor(AlmacenCampo almacen,Uri destino,string id,long version){
  for(int n=0;n<20;n++){await SincronizadorCampo.DescargarCatalogoAsync(almacen,destino);var d=(await almacen.DesastresAsync()).FirstOrDefault(x=>x.Id==id);if(d is not null&&d.Version>=version)return d;await Task.Delay(500);}
  return (await almacen.DesastresAsync()).FirstOrDefault(x=>x.Id==id);
 }
 async Task Ejecutar(){
  var respaldo=await SecureStorage.Default.GetAsync("arqui-sesion");var destino=new Uri("http://127.0.0.1:8084/api/campo/lotes");
  try{
   var existente=System.IO.Path.Combine(FileSystem.AppDataDirectory,"campo.db3");
   if(System.IO.File.Exists(existente)){
    var originalDb=new SQLite.SQLiteAsyncConnection(existente,SQLite.SQLiteOpenFlags.ReadOnly|SQLite.SQLiteOpenFlags.FullMutex);
    var cantidad=await originalDb.ExecuteScalarAsync<int>("SELECT count(*) FROM EvaluacionPendiente");var copia=System.IO.Path.Combine(FileSystem.CacheDirectory,"migracion-prueba-"+Guid.NewGuid().ToString("N")+".sqlite");
    await originalDb.BackupAsync(copia);var migrada=new AlmacenCampo(copia);await migrada.LegadoPendienteAsync();
    var comprobar=new SQLite.SQLiteAsyncConnection(copia);Check(await comprobar.ExecuteScalarAsync<int>("SELECT count(*) FROM EvaluacionPendiente")==cantidad,"migración conserva capturas de una copia de la base anterior");
   }
   await SesionCampo.Entrar(destino,usuario,clave);Check(!string.IsNullOrEmpty(SesionCampo.Token),"login real del APK");
   var id="ANDROID-"+Guid.NewGuid().ToString("N");
   await Http(HttpMethod.Post,new Uri(destino,"/api/desastres"),new JsonObject{{"id",id},{"tipo","INUNDACION"},{"ubicacion","Zona sintética Android"},{"descripcion","Prueba instrumentada"},{"severidad","BAJA"},{"fecha",DateTimeOffset.UtcNow.ToString("O")}});
   var ruta=System.IO.Path.Combine(FileSystem.CacheDirectory,"prueba-evolucion-"+Guid.NewGuid().ToString("N")+".sqlite");var almacen=new AlmacenCampo(ruta);
   await SincronizadorCampo.DescargarCatalogoAsync(almacen,destino);

   // C1: el catálogo de tipos de activo baja a SQLite y la evaluación lleva el código y el texto libre.
   var tiposEnSqlite=await new SQLite.SQLiteAsyncConnection(ruta).ExecuteScalarAsync<int>("SELECT count(*) FROM TipoActivoLocal");
   Check(tiposEnSqlite>=9 && (await almacen.TiposActivoAsync()).Any(t=>t.Codigo=="PUENTE"),"C1: los tipos de activo del servidor quedan en SQLite ("+tiposEnSqlite+")");
   var conTipo=new EvaluacionPendiente{OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,EventoId=id,NivelAfectacion=3,Activo="Puente La Esperanza",TipoActivo="PUENTE",Descripcion="Captura C1 Android",Latitud=4.61,Longitud=-74.08};await almacen.GuardarAsync(conTipo);

   var original=new EvaluacionPendiente{OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,EventoId=id,NivelAfectacion=2,Activo="Escuela",Descripcion="Captura original Android",Latitud=4.6,Longitud=-74.1};await almacen.GuardarAsync(original);
   await SincronizadorCampo.EnviarAsync(almacen,destino);Check((await almacen.ObtenerAsync(original.EvaluacionId)).Estado=="incorporada","captura Android sincronizada");
   var leida=await Http(HttpMethod.Get,new Uri(destino,"/api/campo/evaluaciones/"+conTipo.EvaluacionId));
   Check(leida["tipoActivo"]?.GetValue<string>()=="PUENTE" && leida["activo"]?.GetValue<string>()=="Puente La Esperanza","C1: el servidor guarda el tipo PUENTE y conserva el texto libre «Puente La Esperanza»");
   Check((await almacen.ObtenerAsync(original.EvaluacionId)).TipoActivo=="OTRO","C1: una captura sin tipo elegido queda como OTRO");

   var revision=new RevisionLocal{EvaluacionId=original.EvaluacionId,OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,VersionBase=0,NivelAfectacion=5,Descripcion="Propuesta offline Android",Activo="Escuela",Latitud=4.6,Longitud=-74.1,Motivo="Visita de campo"};
   Estado("DETENER_API");bool desconectado=false;
   for(int n=0;n<30;n++){try{await Http(HttpMethod.Get,new Uri(destino,"/api/auth/me"));}catch(HttpRequestException){desconectado=true;break;}catch(TaskCanceledException){desconectado=true;break;}catch(System.Net.WebException){desconectado=true;break;}await Task.Delay(250);}
   Check(desconectado,"servidor desconectado de verdad");await almacen.GuardarRevisionAsync(revision);

   // C2 sin conexión: se guarda en SQLite sobre la versión del catálogo descargado.
   var local=(await almacen.DesastresAsync()).First(d=>d.Id==id);
   var caracterizacion=new CaracterizacionPendiente{OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,DesastreId=id,VersionBase=local.Version,Latitud=4.6097,Longitud=-74.0817,RadioKm=3.5,Nivel=2,Descripcion="Caracterización sin conexión desde Android"};
   await almacen.GuardarCaracterizacionAsync(caracterizacion);

   var reabierto=new AlmacenCampo(ruta);Check((await reabierto.RevisionesAsync(original.EvaluacionId)).Any(r=>r.Estado=="pendiente" && r.Descripcion==revision.Descripcion),"corrección persiste al reabrir SQLite sin conexión");
   Check((await reabierto.CaracterizacionesAsync()).Any(c=>c.ClaveIdempotencia==caracterizacion.ClaveIdempotencia && c.Estado=="pendiente" && c.RadioKm==3.5),"C2: la caracterización sin conexión persiste al reabrir SQLite (base v"+local.Version+")");
   try{await SincronizadorCampo.EnviarAsync(reabierto,destino);}catch(HttpRequestException){}catch(TaskCanceledException){}catch(System.Net.WebException){}
   Check((await reabierto.RevisionesAsync(original.EvaluacionId))[0].Estado=="pendiente","fallo de red conserva propuesta pendiente");
   Check((await reabierto.CaracterizacionesAsync()).First(c=>c.ClaveIdempotencia==caracterizacion.ClaveIdempotencia).Estado=="pendiente","C2: el fallo de red conserva la caracterización pendiente");
   Check(!SesionCampo.SesionVencida,"la falta de red no se confunde con sesión vencida");
   Estado("RESTAURAR_API");bool conectado=false;
   for(int n=0;n<30;n++){try{await Http(HttpMethod.Get,new Uri(destino,"/api/auth/me"));conectado=true;break;}catch(HttpRequestException){}catch(TaskCanceledException){}catch(System.Net.WebException){}await Task.Delay(250);}
   Check(conectado,"conexión restaurada");
   var patch=new JsonObject{{"operacionId",Guid.NewGuid().ToString()},{"versionBase",0},{"nivelAfectacion",3},{"descripcion","Cambio desde otro dispositivo"},{"activo","Escuela"},{"latitud",4.6},{"longitud",-74.1},{"motivo","Otra inspección"}};
   await Http(HttpMethod.Put,new Uri(destino,"/api/campo/evaluaciones/"+original.EvaluacionId),patch);
   await SincronizadorCampo.EnviarAsync(reabierto,destino);
   var conflicto=(await reabierto.RevisionesAsync(original.EvaluacionId))[0];Check(conflicto.Estado=="conflicto" && conflicto.Descripcion==revision.Descripcion && conflicto.ServidorJson is not null,"conflicto conserva servidor y propuesta local");

   // C2 al volver la red: el desastre queda caracterizado en el servidor.
   Check((await reabierto.CaracterizacionesAsync()).First(c=>c.ClaveIdempotencia==caracterizacion.ClaveIdempotencia).Estado=="incorporada","C2: al volver la red la caracterización se sincroniza");
   var caracterizado=await DesastreServidor(reabierto,destino,id,local.Version+1);
   Check(caracterizado is not null && caracterizado.Version==local.Version+1 && caracterizado.Fase=="CARACTERIZACION" && caracterizado.Latitud==4.6097 && caracterizado.RadioKm==3.5 && caracterizado.Nivel==2,
    "C2: el servidor muestra fase CARACTERIZACION, v"+caracterizado?.Version+", ubicación, radio 3,5 km y nivel 2");

   var remoto=JsonSerializer.Deserialize(conflicto.ServidorJson!,CampoJson.Default.VersionCampo)!;Check(remoto.Version==1,"versión remota identificada");
   var resuelta=new RevisionLocal{EvaluacionId=original.EvaluacionId,OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,VersionBase=remoto.Version,NivelAfectacion=5,Descripcion=revision.Descripcion,Activo="Escuela",Latitud=4.6,Longitud=-74.1,Motivo="Propuesta revisada explícitamente"};
   await reabierto.ResolverRevisionAsync(conflicto,resuelta,remoto.Version);await SincronizadorCampo.EnviarAsync(reabierto,destino);
   var final=await Http(HttpMethod.Get,new Uri(destino,"/api/campo/evaluaciones/"+original.EvaluacionId));Check(final["version"]!.GetValue<long>()==2 && final["descripcion"]!.GetValue<string>()==revision.Descripcion,"resolución sincronizada como nueva versión");
   Check((await reabierto.ObtenerAsync(original.EvaluacionId)).Descripcion==original.Descripcion,"captura original local inmutable");

   // 409: otro teléfono caracteriza con el catálogo viejo. Nada se sobrescribe y el usuario decide.
   var vieja=new CaracterizacionPendiente{OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,DesastreId=id,VersionBase=local.Version,Latitud=4.70,Longitud=-74.05,RadioKm=7,Nivel=3,Descripcion="Segundo teléfono con catálogo viejo"};
   await reabierto.GuardarCaracterizacionAsync(vieja);await SincronizadorCampo.EnviarAsync(reabierto,destino);
   var enConflicto=(await reabierto.CaracterizacionesAsync()).First(c=>c.ClaveIdempotencia==vieja.ClaveIdempotencia);
   Check(enConflicto.Estado=="conflicto" && enConflicto.Motivo=="VERSION_DESACTUALIZADA" && enConflicto.VersionActual==caracterizado!.Version,"409: la caracterización sobre v"+local.Version+" queda en conflicto VERSION_DESACTUALIZADA (servidor en v"+enConflicto.VersionActual+")");
   var sinCambio=await DesastreServidor(reabierto,destino,id,caracterizado.Version);
   Check(sinCambio!.Version==caracterizado.Version && sinCambio.RadioKm==3.5 && sinCambio.Latitud==4.6097,"409: el servidor no se sobrescribe (sigue en radio 3,5 km)");
   Check(enConflicto.RadioKm==7 && enConflicto.Latitud==4.70,"409: la propuesta local se conserva intacta");
   await reabierto.NuevaVersionCaracterizacionAsync(enConflicto,sinCambio.Version);await SincronizadorCampo.EnviarAsync(reabierto,destino);
   var lista=await reabierto.CaracterizacionesAsync();
   Check(lista.Any(c=>c.ClaveIdempotencia==vieja.ClaveIdempotencia && c.Estado=="archivada") && lista.Any(c=>c.ClaveIdempotencia!=vieja.ClaveIdempotencia && c.VersionBase==sinCambio.Version && c.Estado=="incorporada"),"409: la decisión explícita crea una operación nueva y archiva la anterior");
   var resuelto=await DesastreServidor(reabierto,destino,id,sinCambio.Version+1);
   Check(resuelto!.Version==sinCambio.Version+1 && resuelto.RadioKm==7,"409: la versión resuelta llega al servidor (v"+resuelto.Version+", radio 7 km)");

   // 401: la sesión se revoca en el servidor. La app lo distingue de la falta de red y no borra nada.
   using(var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)}){using var r=new HttpRequestMessage(HttpMethod.Post,new Uri(destino,"/api/auth/logout"));SesionCampo.Autorizar(r);r.Content=new StringContent("{}",System.Text.Encoding.UTF8,"application/json");using var res=await http.SendAsync(r);res.EnsureSuccessStatusCode();}
   var trasVencer=new EvaluacionPendiente{OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,EventoId=id,NivelAfectacion=4,Activo="Escuela Rural El Carmen",TipoActivo="ESCUELA",Descripcion="Captura con la sesión vencida",Latitud=4.62,Longitud=-74.09};await reabierto.GuardarAsync(trasVencer);
   var otraCaracterizacion=new CaracterizacionPendiente{OrganizacionLocal=SesionCampo.Organizacion,UsuarioLocal=SesionCampo.Usuario,DesastreId=id,VersionBase=resuelto.Version,Latitud=4.71,Longitud=-74.06,RadioKm=8,Descripcion="Caracterización con la sesión vencida"};await reabierto.GuardarCaracterizacionAsync(otraCaracterizacion);
   bool vencida=false;try{await SincronizadorCampo.EnviarAsync(reabierto,destino);}catch(SesionVencidaException){vencida=true;}
   Check(vencida && SesionCampo.SesionVencida,"401: la app marca la sesión vencida, distinta de la falta de red");
   var tras401=new AlmacenCampo(ruta);
   Check((await tras401.ObtenerAsync(trasVencer.EvaluacionId)).Estado is null && (await tras401.CaracterizacionesAsync()).First(c=>c.ClaveIdempotencia==otraCaracterizacion.ClaveIdempotencia).Estado=="pendiente","401: la evaluación y la caracterización pendientes siguen en SQLite");
   bool bloqueada=false;try{await SincronizadorCampo.EnviarAsync(tras401,destino);}catch(SesionVencidaException){bloqueada=true;}
   Check(bloqueada,"401: no se reintenta con el token vencido hasta iniciar sesión otra vez");
   await SesionCampo.Entrar(destino,usuario,clave);Check(!SesionCampo.SesionVencida && !string.IsNullOrEmpty(SesionCampo.Token),"401: nuevo inicio de sesión");
   await SincronizadorCampo.EnviarAsync(tras401,destino);
   Check((await tras401.ObtenerAsync(trasVencer.EvaluacionId)).Estado=="incorporada" && (await tras401.CaracterizacionesAsync()).First(c=>c.ClaveIdempotencia==otraCaracterizacion.ClaveIdempotencia).Estado=="incorporada","401: tras iniciar sesión otra vez se sincroniza lo pendiente, sin pérdida");

   await SesionCampo.Salir(destino);Check(SesionCampo.Token is null,"logout del APK");
  }finally{if(respaldo is null)SecureStorage.Default.Remove("arqui-sesion");else await SecureStorage.Default.SetAsync("arqui-sesion",respaldo);}
 }
}
#endif
