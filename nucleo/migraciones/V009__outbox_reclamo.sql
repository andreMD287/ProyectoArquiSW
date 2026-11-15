-- Bandeja de salida: cada aviso se reclama por un tiempo antes de enviarlo, fuera de la
-- transacción. Si el envío falla o la instancia cae, el reclamo vence y otro ciclo lo reintenta.
ALTER TABLE outbox_eventos ADD COLUMN IF NOT EXISTS reclamado_hasta TIMESTAMPTZ;

-- Sesiones vencidas: índice para purgarlas sin recorrer toda la tabla.
CREATE INDEX IF NOT EXISTS idx_sesiones_expira ON sesiones(expira_en);
