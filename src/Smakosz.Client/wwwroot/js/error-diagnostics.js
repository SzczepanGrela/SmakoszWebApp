(function () {
    var errorEl = document.getElementById('blazor-error-ui');
    if (!errorEl) return;

    var observer = new MutationObserver(function () {
        if (errorEl.style.display !== 'none') diagnose();
    });
    observer.observe(errorEl, { attributes: true, attributeFilter: ['style'] });

    function diagnose() {
        var msgEl = document.getElementById('blazor-error-msg');
        if (!msgEl) return;

        var apiUrl = window.__smakoszApiUrl || 'https://localhost:5001';

        fetch(apiUrl + '/health', { method: 'GET', mode: 'cors' })
            .then(function (r) {
                if (r.ok) {
                    msgEl.textContent = 'Wystąpił nieoczekiwany błąd w aplikacji.';
                } else {
                    throw new Error('API returned ' + r.status);
                }
            })
            .catch(function () {
                return fetch('https://cloudflare.com/cdn-cgi/trace', { mode: 'no-cors' })
                    .then(function () {
                        msgEl.textContent = 'Nie można połączyć się z serwerem. Serwer może być tymczasowo niedostępny.';
                        document.getElementById('blazor-error-content').className = 'alert alert-warning m-3';
                    })
                    .catch(function () {
                        msgEl.textContent = 'Brak połączenia z internetem. Sprawdź swoje połączenie sieciowe.';
                        document.getElementById('blazor-error-content').className = 'alert alert-warning m-3';
                    });
            });
    }
})();
