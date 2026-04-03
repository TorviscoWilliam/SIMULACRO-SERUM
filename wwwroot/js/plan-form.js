// Vista previa en tiempo real del formulario de plan
(function () {
    function qs(id) { return document.getElementById(id); }

    function boldToHtml(text) {
        return text.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
    }

    function updateGradient(c1, c2) {
        var grad = 'linear-gradient(135deg,' + c1 + ',' + c2 + ')';
        var header = qs('previewHeader');
        var boton  = qs('previewBoton');
        if (header) header.style.background = grad;
        if (boton)  boton.style.background  = grad;
        var precioEl = qs('previewPrecioCard');
        if (precioEl) precioEl.style.color = c2;
    }

    function updateFeatures(text) {
        var list = qs('previewFeaturesList');
        if (!list) return;
        var lines = text.split('\n').filter(function(l){ return l.trim().length > 0; });
        list.innerHTML = lines.map(function(l) {
            return '<li class="d-flex align-items-start gap-2 mb-1">' +
                '<i class="bi bi-check-circle-fill text-success mt-1 flex-shrink-0"></i>' +
                '<span>' + boldToHtml(l.trim()) + '</span></li>';
        }).join('');
    }

    // Nombre
    var nombre = qs('previewNombre');
    if (nombre) nombre.addEventListener('input', function () {
        var el = qs('previewNombreCard');
        if (el) el.textContent = this.value || 'Nombre del plan';
    });

    // Etiqueta
    var etiqueta = qs('previewEtiqueta');
    if (etiqueta) etiqueta.addEventListener('input', function () {
        var el = qs('previewEtiquetaCard');
        if (el) el.textContent = this.value || 'ETIQUETA';
    });

    // Precio
    var precio = qs('previewPrecio');
    if (precio) precio.addEventListener('input', function () {
        var el = qs('previewPrecioCard');
        if (el) el.textContent = parseFloat(this.value) > 0 ? parseFloat(this.value).toFixed(0) : '0';
    });

    // TextoPrecio
    var textoPrecio = qs('previewTextoPrecio');
    if (textoPrecio) textoPrecio.addEventListener('input', function () {
        var el = qs('previewTextoPrecioCard');
        if (el) el.textContent = ' ' + this.value;
    });

    // Colores
    var c1Input = qs('previewColor1');
    var c2Input = qs('previewColor2');

    function getColor(id) {
        var el = qs(id);
        return el ? el.value : '#74c0fc';
    }

    if (c1Input) c1Input.addEventListener('input', function () {
        // sync text input
        var txt = document.querySelector('.color-text[data-target="previewColor1"]');
        if (txt) txt.value = this.value;
        updateGradient(this.value, getColor('previewColor2'));
    });
    if (c2Input) c2Input.addEventListener('input', function () {
        var txt = document.querySelector('.color-text[data-target="previewColor2"]');
        if (txt) txt.value = this.value;
        updateGradient(getColor('previewColor1'), this.value);
    });

    // Sync text inputs → color pickers
    document.querySelectorAll('.color-text').forEach(function (txt) {
        txt.addEventListener('input', function () {
            var target = qs(this.dataset.target);
            if (target && /^#[0-9a-fA-F]{6}$/.test(this.value)) {
                target.value = this.value;
                updateGradient(getColor('previewColor1'), getColor('previewColor2'));
            }
        });
    });

    // Características
    var caract = qs('previewCaracteristicas');
    if (caract) caract.addEventListener('input', function () {
        updateFeatures(this.value);
    });

    // Badge
    var badge = qs('previewBadge');
    if (badge) badge.addEventListener('input', function () {
        var box  = qs('previewBadgeBox');
        var text = qs('previewBadgeText');
        if (!box || !text) return;
        if (this.value.trim().length > 0) {
            box.style.display = '';
            text.textContent = this.value;
        } else {
            box.style.display = 'none';
        }
    });

    // Texto botón
    var textoBoton = qs('previewTextoBoton');
    if (textoBoton) textoBoton.addEventListener('input', function () {
        var el = qs('previewTextoBotonCard');
        if (el) el.textContent = this.value || '¡Suscribirme ya!';
    });

    // Popular switch
    var popularSwitch = qs('esPopular');
    if (popularSwitch) popularSwitch.addEventListener('change', function () {
        var el = qs('previewPopularBadge');
        if (!el) return;
        el.style.display = this.checked ? '' : 'none';
    });
})();
