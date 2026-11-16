package com.pgdr.portal.web;

import java.math.BigDecimal;
import java.text.NumberFormat;
import java.util.Locale;
import org.springframework.stereotype.Component;

/** Formatos para las vistas (es-CO): pesos con separador de miles y decimales con coma. Uso: ${@formato.dinero(x)}. */
@Component("formato")
public class Formato {
    private static final Locale COLOMBIA = Locale.forLanguageTag("es-CO");

    /** "1000000.00" → "$ 1.000.000". Vacío o inválido → "—". */
    public String dinero(Object valor) {
        BigDecimal numero = numero(valor);
        if (numero == null) return "—";
        NumberFormat f = NumberFormat.getNumberInstance(COLOMBIA);
        f.setMaximumFractionDigits(numero.stripTrailingZeros().scale() > 0 ? 2 : 0);
        f.setMinimumFractionDigits(0);
        return "$ " + f.format(numero);
    }

    /** 3.0 → "3,0" con los decimales indicados. */
    public String decimal(Object valor, int decimales) {
        BigDecimal numero = numero(valor);
        if (numero == null) return "—";
        NumberFormat f = NumberFormat.getNumberInstance(COLOMBIA);
        f.setMinimumFractionDigits(decimales);
        f.setMaximumFractionDigits(decimales);
        return f.format(numero);
    }

    private static BigDecimal numero(Object valor) {
        if (valor == null) return null;
        try {
            String texto = valor.toString().trim();
            return texto.isEmpty() ? null : new BigDecimal(texto);
        } catch (NumberFormatException e) {
            return null;
        }
    }
}
