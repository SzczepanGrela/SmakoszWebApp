(function () {
    var STORAGE_VISITS = 'pwa-install.visits';
    var STORAGE_DISMISSED_UNTIL = 'pwa-install.dismissed-until';
    var STORAGE_INSTALLED = 'pwa-install.installed';
    var COOLDOWN_MS = 7 * 24 * 60 * 60 * 1000;
    var FIRST_VISIT_DELAY_MS = 30 * 1000;

    var visits = (parseInt(localStorage.getItem(STORAGE_VISITS), 10) || 0) + 1;
    localStorage.setItem(STORAGE_VISITS, String(visits));

    if (localStorage.getItem(STORAGE_INSTALLED) === '1') return;
    if (window.matchMedia && window.matchMedia('(display-mode: standalone)').matches) return;
    if (window.navigator.standalone === true) return;

    var dismissedUntil = parseInt(localStorage.getItem(STORAGE_DISMISSED_UNTIL), 10) || 0;
    if (dismissedUntil > Date.now()) return;

    var ua = navigator.userAgent;
    var isIos = /iP(hone|od|ad)/.test(ua) || (/Macintosh/.test(ua) && 'ontouchend' in document);
    var isIosSafari = isIos && !/CriOS|FxiOS|EdgiOS/i.test(ua);
    var isAndroidMobile = /Android|Mobi|webOS|BlackBerry|IEMobile|Opera Mini/i.test(ua);
    var isMobile = isIos || isAndroidMobile;
    if (!isMobile) return;

    var deferredPrompt = null;
    var shownThisLoad = false;

    function setDismissCooldown() {
        localStorage.setItem(STORAGE_DISMISSED_UNTIL, String(Date.now() + COOLDOWN_MS));
    }

    function scheduleShow(fn) {
        if (visits >= 2) {
            fn();
        } else {
            setTimeout(fn, FIRST_VISIT_DELAY_MS);
        }
    }

    if (isIosSafari) {
        scheduleShow(showIosBanner);
        return;
    }

    window.addEventListener('beforeinstallprompt', function (e) {
        e.preventDefault();
        deferredPrompt = e;
        scheduleShow(showBanner);
    });

    window.addEventListener('appinstalled', function () {
        localStorage.setItem(STORAGE_INSTALLED, '1');
        var el = document.getElementById('pwa-install-toast');
        if (el) el.remove();
        deferredPrompt = null;
    });

    function passesLateGuards() {
        if (shownThisLoad) return false;
        if (document.getElementById('pwa-install-toast')) return false;
        if (localStorage.getItem(STORAGE_INSTALLED) === '1') return false;
        var laterUntil = parseInt(localStorage.getItem(STORAGE_DISMISSED_UNTIL), 10) || 0;
        if (laterUntil > Date.now()) return false;
        return true;
    }

    function showBanner() {
        if (!deferredPrompt) return;
        if (!passesLateGuards()) return;
        shownThisLoad = true;

        var t = document.createElement('div');
        t.id = 'pwa-install-toast';
        t.className = 'toast show animate-in';
        t.style.cssText = 'position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);z-index:1060;min-width:320px;max-width:90vw;border-radius:0.75rem;box-shadow:0 1rem 3rem rgba(0,0,0,0.25);overflow:hidden;background:#F2EDE6;border:1px solid #D4A574;';
        t.innerHTML = '<div style="background:linear-gradient(135deg,#D4A574,#B8860B);color:white;padding:0.6rem 1rem;display:flex;justify-content:space-between;align-items:center;">'
            + '<strong><i class="fa-solid fa-mobile-screen-button me-2"></i>Zainstaluj aplikacje</strong>'
            + '<button id="pwa-install-close" style="background:none;border:none;color:white;cursor:pointer;font-size:1.2rem;padding:0 0.25rem;">&times;</button></div>'
            + '<div style="padding:1rem;color:#4A3428;">'
            + '<p style="margin:0 0 0.75rem;">Dodaj Smakosz do ekranu glownego, by korzystac szybciej i offline.</p>'
            + '<button id="pwa-install-btn" class="pwa-update-btn">Zainstaluj</button></div>';
        document.body.appendChild(t);

        document.getElementById('pwa-install-close').addEventListener('click', function () {
            setDismissCooldown();
            t.remove();
        });

        document.getElementById('pwa-install-btn').addEventListener('click', function () {
            if (!deferredPrompt) {
                t.remove();
                return;
            }
            deferredPrompt.prompt();
            deferredPrompt.userChoice.then(function (choice) {
                if (choice && choice.outcome === 'accepted') {
                    localStorage.setItem(STORAGE_INSTALLED, '1');
                } else {
                    setDismissCooldown();
                }
                deferredPrompt = null;
                t.remove();
            }).catch(function () {
                deferredPrompt = null;
                t.remove();
            });
        });
    }

    function showIosBanner() {
        if (!passesLateGuards()) return;
        shownThisLoad = true;

        var t = document.createElement('div');
        t.id = 'pwa-install-toast';
        t.className = 'toast show animate-in';
        t.style.cssText = 'position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);z-index:1060;min-width:320px;max-width:90vw;border-radius:0.75rem;box-shadow:0 1rem 3rem rgba(0,0,0,0.25);overflow:hidden;background:#F2EDE6;border:1px solid #D4A574;';
        t.innerHTML = '<div style="background:linear-gradient(135deg,#D4A574,#B8860B);color:white;padding:0.6rem 1rem;display:flex;justify-content:space-between;align-items:center;">'
            + '<strong><i class="fa-solid fa-mobile-screen-button me-2"></i>Zainstaluj aplikacje</strong>'
            + '<button id="pwa-install-close" style="background:none;border:none;color:white;cursor:pointer;font-size:1.2rem;padding:0 0.25rem;">&times;</button></div>'
            + '<div style="padding:1rem;color:#4A3428;">'
            + '<p style="margin:0 0 0.6rem;">Dodaj Smakosz do ekranu glownego:</p>'
            + '<ol style="margin:0 0 0.75rem;padding-left:1.25rem;line-height:1.6;">'
            + '<li>Stuknij ikone Udostepniania <i class="fa-solid fa-arrow-up-from-bracket" style="color:#0a84ff;"></i></li>'
            + '<li>Wybierz <strong>Do ekranu poczatkowego</strong> <i class="fa-solid fa-square-plus" style="color:#0a84ff;"></i></li>'
            + '</ol>'
            + '<button id="pwa-install-btn" class="pwa-update-btn">Rozumiem</button></div>';
        document.body.appendChild(t);

        document.getElementById('pwa-install-close').addEventListener('click', function () {
            setDismissCooldown();
            t.remove();
        });

        document.getElementById('pwa-install-btn').addEventListener('click', function () {
            setDismissCooldown();
            t.remove();
        });
    }
})();
