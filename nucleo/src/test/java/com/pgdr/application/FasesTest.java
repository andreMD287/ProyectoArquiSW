package com.pgdr.application;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import org.junit.jupiter.api.Test;

/** Transiciones de fase que solo puede pedir el módulo de Reconstrucción (RN-01, RN-02 y RN-04). */
class FasesTest {
    @Test
    void pasarAReconstruccionYCerrarDesdeReconstruccionExigenAlModulo() {
        assertTrue(RegistroIntegrado.exigeReconstruccion("CARACTERIZACION", "RECONSTRUCCION"));
        assertTrue(RegistroIntegrado.exigeReconstruccion("RECONSTRUCCION", "CIERRE"));
    }

    @Test
    void lasDemasTransicionesLasHaceElCoordinador() {
        assertFalse(RegistroIntegrado.exigeReconstruccion("REGISTRO", "CARACTERIZACION"));
        assertFalse(RegistroIntegrado.exigeReconstruccion("REGISTRO", "CIERRE"));
        assertFalse(RegistroIntegrado.exigeReconstruccion("CARACTERIZACION", "CIERRE"));
    }
}
