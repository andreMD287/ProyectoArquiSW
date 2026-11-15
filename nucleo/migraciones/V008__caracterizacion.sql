-- Caracterización del desastre (contrato C2): fase, ubicación y radio del área afectada como
-- columnas con restricciones, en lugar de vivir solo dentro del texto JSON.
ALTER TABLE desastres_integrados ADD COLUMN IF NOT EXISTS fase VARCHAR(20) NOT NULL DEFAULT 'REGISTRO';
ALTER TABLE desastres_integrados ADD COLUMN IF NOT EXISTS latitud DOUBLE PRECISION;
ALTER TABLE desastres_integrados ADD COLUMN IF NOT EXISTS longitud DOUBLE PRECISION;
ALTER TABLE desastres_integrados ADD COLUMN IF NOT EXISTS radio_km NUMERIC(8,2);

-- Las fases que ya estaban guardadas en el JSON pasan a la columna.
UPDATE desastres_integrados
   SET fase = datos_json::jsonb->>'fase'
 WHERE datos_json::jsonb->>'fase' IN ('REGISTRO','CARACTERIZACION','RECONSTRUCCION','CIERRE')
   AND fase <> datos_json::jsonb->>'fase';

DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_desastre_fase') THEN
  ALTER TABLE desastres_integrados ADD CONSTRAINT ck_desastre_fase
   CHECK (fase IN ('REGISTRO','CARACTERIZACION','RECONSTRUCCION','CIERRE'));
 END IF;
 IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_desastre_ubicacion') THEN
  ALTER TABLE desastres_integrados ADD CONSTRAINT ck_desastre_ubicacion CHECK (
   (latitud IS NULL AND longitud IS NULL AND radio_km IS NULL) OR
   (latitud BETWEEN -90 AND 90 AND longitud BETWEEN -180 AND 180 AND radio_km > 0 AND radio_km <= 500));
 END IF;
END $$;
