(function () {
    var STORAGE_PENDING = 'push-prompt.pending';
    var STORAGE_DISMISSED_UNTIL = 'push-prompt.dismissed-until';
    var STORAGE_SUBSCRIBED = 'push-prompt.subscribed';
    var COOLDOWN_MS = 7 * 24 * 60 * 60 * 1000;
    var SHOW_DELAY_MS = 30 * 1000;
    var COORD_RETRY_MS = 30 * 1000;
    var COORD_MAX_RETRIES = 3;

    if (!('serviceWorker' in navigator) || !('PushManager' in window) || !('Notification' in window)) return;
    if (Notification.permission === 'denied') {
        localStorage.removeItem(STORAGE_PENDING);
        return;
    }
    if (Notification.permission === 'granted' && localStorage.getItem(STORAGE_SUBSCRIBED) === '1') return;

    if (localStorage.getItem(STORAGE_PENDING) !== '1') return;

    var dismissedUntil = parseInt(localStorage.getItem(STORAGE_DISMISSED_UNTIL), 10) || 0;
    if (dismissedUntil > Date.now()) return;

    var shownThisLoad = false;
    var retryCount = 0;

    function setDismissCooldown() {
        localStorage.setItem(STORAGE_DISMISSED_UNTIL, String(Date.now() + COOLDOWN_MS));
        localStorage.removeItem(STORAGE_PENDING);
    }

    function clearPending() {
        localStorage.removeItem(STORAGE_PENDING);
    }

    function isCompetingBannerVisible() {
        if (document.getElementById('pwa-install-toast')) return true;
        if (document.getElementById('pwa-update-toast')) return true;
        return false;
    }

    function passesLateGuards() {
        if (shownThisLoad) return false;
        if (document.getElementById('push-prompt-toast')) return false;
        if (Notification.permission === 'denied') {
            clearPending();
            return false;
        }
        if (localStorage.getItem(STORAGE_SUBSCRIBED) === '1') return false;
        var laterUntil = parseInt(localStorage.getItem(STORAGE_DISMISSED_UNTIL), 10) || 0;
        if (laterUntil > Date.now()) return false;
        return true;
    }

    async function urlBase64ToUint8Array(base64String) {
        var padding = '='.repeat((4 - base64String.length % 4) % 4);
        var base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        var rawData = window.atob(base64);
        var outputArray = new Uint8Array(rawData.length);
        for (var i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }

    async function subscribeAndPost() {
        var keyResponse = await fetch('/api/me/push-public-key', { credentials: 'include' });
        if (!keyResponse.ok) return false;
        var keyJson = await keyResponse.json();
        var publicKey = keyJson && keyJson.publicKey;
        if (!publicKey) return false;

        var registration = await navigator.serviceWorker.ready;
        var subscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey: await urlBase64ToUint8Array(publicKey)
        });
        var json = subscription.toJSON();

        var postResponse = await fetch('/api/me/push-subscriptions', {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                endpoint: json.endpoint,
                p256dh: json.keys.p256dh,
                auth: json.keys.auth
            })
        });
        return postResponse.ok;
    }

    function tryShow() {
        if (!passesLateGuards()) return;
        if (isCompetingBannerVisible()) {
            if (retryCount >= COORD_MAX_RETRIES) return;
            retryCount++;
            setTimeout(tryShow, COORD_RETRY_MS);
            return;
        }
        showBanner();
    }

    function showBanner() {
        if (!passesLateGuards()) return;
        shownThisLoad = true;

        var t = document.createElement('div');
        t.id = 'push-prompt-toast';
        t.className = 'toast show animate-in';
        t.style.cssText = 'position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);z-index:1060;min-width:320px;max-width:90vw;border-radius:0.75rem;box-shadow:0 1rem 3rem rgba(0,0,0,0.25);overflow:hidden;background:#F2EDE6;border:1px solid #D4A574;';
        t.innerHTML = '<div style="background:linear-gradient(135deg,#D4A574,#B8860B);color:white;padding:0.6rem 1rem;display:flex;justify-content:space-between;align-items:center;">'
            + '<strong><i class="fa-solid fa-bell me-2"></i>Wlacz powiadomienia</strong>'
            + '<button id="push-prompt-close" style="background:none;border:none;color:white;cursor:pointer;font-size:1.2rem;padding:0 0.25rem;">&times;</button></div>'
            + '<div style="padding:1rem;color:#4A3428;">'
            + '<p style="margin:0 0 0.75rem;">Bedziemy informowac Cie o polubieniach, obserwujacych i nowych rekomendacjach.</p>'
            + '<button id="push-prompt-enable" class="pwa-update-btn">Wlacz</button>'
            + '<button id="push-prompt-later" class="pwa-update-btn" style="background:transparent;color:#4A3428;border:1px solid #D4A574;margin-left:0.5rem;">Moze pozniej</button></div>';
        document.body.appendChild(t);

        document.getElementById('push-prompt-close').addEventListener('click', function () {
            setDismissCooldown();
            t.remove();
        });

        document.getElementById('push-prompt-later').addEventListener('click', function () {
            setDismissCooldown();
            t.remove();
        });

        document.getElementById('push-prompt-enable').addEventListener('click', async function () {
            var btn = document.getElementById('push-prompt-enable');
            btn.disabled = true;
            try {
                var ok = await subscribeAndPost();
                if (ok) {
                    localStorage.setItem(STORAGE_SUBSCRIBED, '1');
                    clearPending();
                } else {
                    setDismissCooldown();
                }
            } catch (e) {
                if (Notification.permission === 'denied') {
                    clearPending();
                } else {
                    setDismissCooldown();
                }
            } finally {
                t.remove();
            }
        });
    }

    setTimeout(tryShow, SHOW_DELAY_MS);
})();
