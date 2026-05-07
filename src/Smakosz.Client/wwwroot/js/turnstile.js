window.smakoszTurnstile = {
    isTestKey: function (siteKey) {
        return /^[123]x0{20}A[AB]$/.test(siteKey) || /^3x0{20}FF$/.test(siteKey);
    },
    render: function (elementId, siteKey, dotNetRef) {
        if (window.smakoszTurnstile.isTestKey(siteKey)) {
            setTimeout(function () { dotNetRef.invokeMethodAsync('OnTokenChanged', 'XXXX.DUMMY.TOKEN.XXXX'); }, 0);
            return;
        }
        var attempts = 0;
        var tryRender = function () {
            if (typeof turnstile !== 'undefined') {
                turnstile.render('#' + elementId, {
                    sitekey: siteKey,
                    theme: 'light',
                    callback: function (token) { dotNetRef.invokeMethodAsync('OnTokenChanged', token); },
                    'error-callback': function () { dotNetRef.invokeMethodAsync('OnTokenChanged', ''); },
                    'expired-callback': function () { dotNetRef.invokeMethodAsync('OnTokenChanged', ''); }
                });
                return;
            }
            if (++attempts > 50) { dotNetRef.invokeMethodAsync('OnTokenChanged', ''); return; }
            setTimeout(tryRender, 100);
        };
        tryRender();
    },
    reset: function (elementId) {
        if (typeof turnstile === 'undefined') return;
        try { turnstile.reset('#' + elementId); } catch (_) { /* widget not rendered (test-key bypass) */ }
    }
};
