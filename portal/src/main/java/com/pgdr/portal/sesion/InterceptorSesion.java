package com.pgdr.portal.sesion;

import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import org.springframework.web.servlet.HandlerInterceptor;

/** Sin sesión válida, toda página del portal lleva al inicio de sesión (y vuelve después). */
public class InterceptorSesion implements HandlerInterceptor {
    @Override
    public boolean preHandle(HttpServletRequest request, HttpServletResponse response, Object handler) throws Exception {
        Sesion sesion = Sesion.de(request.getSession(false));
        if (sesion != null) {
            request.setAttribute(Sesion.ATRIBUTO, sesion);
            return true;
        }
        String destino = request.getRequestURI() + (request.getQueryString() == null ? "" : "?" + request.getQueryString());
        response.sendRedirect("/login?siguiente=" + URLEncoder.encode(destino, StandardCharsets.UTF_8));
        return false;
    }
}
