window.smakoszTurnstile = {
    render: function (elementId, siteKey, dotNetRef) {
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
        if (typeof turnstile !== 'undefined') {
            turnstile.reset('#' + elementId);
        }
    }
};
