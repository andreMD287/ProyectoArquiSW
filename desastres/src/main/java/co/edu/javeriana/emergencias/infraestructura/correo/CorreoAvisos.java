package co.edu.javeriana.emergencias.infraestructura.correo;

import jakarta.enterprise.context.ApplicationScoped;
import jakarta.mail.*;
import jakarta.mail.internet.*;
import java.util.Properties;

/** Correo de un aviso de Reconstrucción por SMTP (Mailpit en el laboratorio). */
@ApplicationScoped
public class CorreoAvisos {
    public void enviar(String para, String asunto, String texto, String idEvento) {
        try {
            Properties p = new Properties();
            p.setProperty("mail.smtp.host", System.getenv().getOrDefault("SMTP_HOST", "mailpit"));
            p.setProperty("mail.smtp.port", System.getenv().getOrDefault("SMTP_PORT", "1025"));
            p.setProperty("mail.smtp.connectiontimeout", "4000");
            p.setProperty("mail.smtp.timeout", "4000");
            p.setProperty("mail.smtp.writetimeout", "4000");
            MimeMessage mensaje = new MimeMessage(Session.getInstance(p));
            mensaje.setFrom(new InternetAddress("reconstruccion@emergencias.local"));
            mensaje.setRecipient(Message.RecipientType.TO, new InternetAddress(para, true));
            mensaje.setSubject(asunto, "UTF-8");
            mensaje.setText(texto, "UTF-8");
            mensaje.setHeader("X-Evento-ID", idEvento);
            Transport.send(mensaje);
        } catch (MessagingException e) { throw new IllegalStateException("No se pudo enviar el correo del aviso", e); }
    }
}
