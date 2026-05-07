var pendingReload = false;
var pageLoadController = navigator.serviceWorker.controller;
navigator.serviceWorker.addEventListener('controllerchange', function () {
    if (pendingReload) location.reload();
});

navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' })
    .then(function (reg) {
        if (navigator.serviceWorker.controller) reg.update();
        setInterval(function () { reg.update(); }, 60 * 60 * 1000);
        if (reg.waiting) { showUpdateToast(reg.waiting); return; }
        reg.addEventListener('updatefound', function () {
            var w = reg.installing;
            w.addEventListener('statechange', function () {
                if (w.state === 'installed' && navigator.serviceWorker.controller) showUpdateToast(w);
            });
        });
    });

function showUpdateToast(worker) {
    if (document.getElementById('pwa-update-toast')) return;
    var t = document.createElement('div');
    t.id = 'pwa-update-toast';
    t.className = 'toast show animate-in';
    t.style.cssText = 'position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);z-index:1060;min-width:320px;max-width:90vw;border-radius:0.75rem;box-shadow:0 1rem 3rem rgba(0,0,0,0.25);overflow:hidden;background:#F2EDE6;border:1px solid #D4A574;';
    t.innerHTML = '<div style="background:linear-gradient(135deg,#D4A574,#B8860B);color:white;padding:0.6rem 1rem;display:flex;justify-content:space-between;align-items:center;">'
        + '<strong><i class="fa-solid fa-arrows-rotate me-2"></i>Aktualizacja</strong>'
        + '<button onclick="this.closest(\'#pwa-update-toast\').remove()" style="background:none;border:none;color:white;cursor:pointer;font-size:1.2rem;padding:0 0.25rem;">&times;</button></div>'
        + '<div style="padding:1rem;color:#4A3428;">'
        + '<p style="margin:0 0 0.75rem;">Nowa wersja aplikacji jest dostepna.</p>'
        + '<button id="pwa-update-btn" class="pwa-update-btn">Zaktualizuj</button></div>';
    document.body.appendChild(t);
    document.getElementById('pwa-update-btn').addEventListener('click', function () {
        if (navigator.serviceWorker.controller !== pageLoadController) {
            location.reload();
            return;
        }
        if (worker && worker.state !== 'activated') {
            pendingReload = true;
            worker.postMessage({ type: 'SKIP_WAITING' });
        } else {
            location.reload();
        }
    });
}
