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

        fetch(apiUrl, { method: 'HEAD', mode: 'no-cors' })
            .then(function () {
                msgEl.textContent = 'Wystąpił nieoczekiwany błąd w aplikacji.';
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
