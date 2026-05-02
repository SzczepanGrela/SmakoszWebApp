window.smakoszCropper = (function () {
    const instances = {};

    function init(imgId, ratio) {
        destroy(imgId);
        const img = document.getElementById(imgId);
        if (!img || typeof Cropper === 'undefined') return false;
        instances[imgId] = new Cropper(img, {
            aspectRatio: ratio,
            viewMode: 1,
            autoCropArea: 1,
            background: false,
            responsive: true,
            zoomable: true,
            scalable: false,
            rotatable: false
        });
        return true;
    }

    function getDataUrl(imgId, mime, maxWidth) {
        const c = instances[imgId];
        if (!c) return null;
        const canvas = c.getCroppedCanvas({
            maxWidth: maxWidth || 2400,
            imageSmoothingEnabled: true,
            imageSmoothingQuality: 'high'
        });
        return canvas ? canvas.toDataURL(mime || 'image/jpeg', 0.92) : null;
    }

    function destroy(imgId) {
        if (instances[imgId]) {
            instances[imgId].destroy();
            delete instances[imgId];
        }
    }

    function readFile(inputId) {
        return new Promise(function (resolve) {
            const input = document.getElementById(inputId);
            if (!input || !input.files || input.files.length === 0) { resolve(null); return; }
            const file = input.files[0];
            const reader = new FileReader();
            reader.onload = function (e) {
                resolve({ dataUrl: e.target.result, name: file.name, size: file.size });
            };
            reader.onerror = function () { resolve(null); };
            reader.readAsDataURL(file);
        });
    }

    return { init: init, getDataUrl: getDataUrl, destroy: destroy, readFile: readFile };
})();
