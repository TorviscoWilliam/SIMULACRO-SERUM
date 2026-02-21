// ── Simulacro SERUM – scripts globales ──────────────────────

// Auto-ocultar alertas después de 4 segundos
document.addEventListener('DOMContentLoaded', function () {
    const alerts = document.querySelectorAll('.alert.alert-success, .alert.alert-danger');
    alerts.forEach(function (alert) {
        setTimeout(function () {
            const bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            if (bsAlert) bsAlert.close();
        }, 4000);
    });

    // Activar tooltips de Bootstrap
    const tooltipEls = document.querySelectorAll('[data-bs-toggle="tooltip"]');
    tooltipEls.forEach(function (el) {
        new bootstrap.Tooltip(el);
    });

    // Marcar nav-link activo según la URL
    const path = window.location.pathname.toLowerCase();
    document.querySelectorAll('.navbar .nav-link').forEach(function (link) {
        const href = (link.getAttribute('href') || '').toLowerCase();
        if (href && path.startsWith(href) && href !== '/') {
            link.classList.add('active');
        }
    });
});
