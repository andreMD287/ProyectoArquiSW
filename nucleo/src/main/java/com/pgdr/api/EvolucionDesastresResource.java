package com.pgdr.api;
import com.pgdr.application.*;
import jakarta.ejb.EJB;import jakarta.json.*;import jakarta.ws.rs.*;import jakarta.ws.rs.core.*;import jakarta.servlet.http.HttpServletRequest;import java.util.*;
@Path("/eventos") @Produces(MediaType.APPLICATION_JSON) @Consumes(MediaType.APPLICATION_JSON)
public class EvolucionDesastresResource {
 @EJB RegistroIntegrado registro;@Context HttpServletRequest req;
 Identidad actor(){return (Identidad)req.getAttribute("pgdr.identidad");}
 @GET @Path("{id}/historial") public JsonArray historial(@PathParam("id") String id){return registro.historial(actor(),id);}
 @PATCH @Path("{id}/gravedad") public Response cambiar(@PathParam("id") String id,JsonObject b){
  if(!actor().puedeRegistrarDesastre())return Response.status(403).build();
  try{return Response.ok(registro.cambiar(actor(),id,UUID.fromString(b.getString("cambioId")),b.getJsonNumber("versionBase").longValueExact(),b.getString("severidad"),b.getString("motivo"))).build();}
  catch(RegistroIntegrado.CambioConflicto e){return Response.status(409).entity(Map.of("error","VERSION_O_CAMBIO_EN_CONFLICTO")).build();}
  catch(ValidacionException | IllegalArgumentException | NullPointerException | ClassCastException | ArithmeticException e){return Response.status(400).entity(Map.of("error","Datos del cambio inválidos")).build();}
 }
 /** Contrato C2: caracterización del desastre (COORDINADOR o EVALUADOR del organismo). */
 @PATCH @Path("{id}/caracterizacion") public Response caracterizar(@PathParam("id") String id,JsonObject b){
  if(!actor().puedeRegistrarAfectacion())return Response.status(403).entity(Map.of("error","SIN_PERMISO")).build();
  try{
   String clave=b.containsKey("claveIdempotencia")?b.getString("claveIdempotencia"):b.getString("cambioId");
   Integer nivel=b.containsKey("nivel")&&!b.isNull("nivel")?b.getInt("nivel"):null;
   String descripcion=b.containsKey("descripcion")&&!b.isNull("descripcion")?b.getString("descripcion"):null;
   return Response.ok(registro.caracterizar(actor(),id,UUID.fromString(clave),b.getJsonNumber("versionBase").longValueExact(),
    b.getJsonNumber("latitud").doubleValue(),b.getJsonNumber("longitud").doubleValue(),b.getJsonNumber("radioKm").doubleValue(),nivel,descripcion)).build();
  }
  catch(RegistroIntegrado.VersionDesactualizada e){return Response.status(409).entity(Map.of("error","VERSION_DESACTUALIZADA","versionActual",e.actual)).build();}
  catch(RegistroIntegrado.CambioConflicto e){return Response.status(409).entity(Map.of("error","CLAVE_REUTILIZADA")).build();}
  catch(jakarta.ejb.EJBException e){
   if(e.getCause() instanceof NotFoundException)return Response.status(404).entity(Map.of("error","DESASTRE_NO_ENCONTRADO")).build();
   throw e;
  }
  catch(NotFoundException e){return Response.status(404).entity(Map.of("error","DESASTRE_NO_ENCONTRADO")).build();}
  catch(ValidacionException | IllegalArgumentException | NullPointerException | ClassCastException | ArithmeticException e){return Response.status(400).entity(Map.of("error",e.getMessage()==null?"Datos de la caracterización inválidos":e.getMessage())).build();}
 }
 @PATCH @Path("{id}/fase") public Response cambiarFase(@PathParam("id") String id,JsonObject b){
  if(!actor().puedeRegistrarDesastre())return Response.status(403).build();
  try{return Response.ok(registro.cambiarFase(actor(),id,UUID.fromString(b.getString("cambioId")),b.getJsonNumber("versionBase").longValueExact(),b.getString("fase"),b.getString("motivo"))).build();}
  catch(RegistroIntegrado.CambioConflicto e){return Response.status(409).entity(Map.of("error","VERSION_O_CAMBIO_EN_CONFLICTO")).build();}
  catch(ValidacionException | IllegalArgumentException | NullPointerException | ClassCastException | ArithmeticException e){return Response.status(400).entity(Map.of("error",e.getMessage()==null?"Datos del cambio inválidos":e.getMessage())).build();}
 }
}
