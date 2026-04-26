window.smakoszTurnstile = {
    render: function (elementId, siteKey) {
        return new Promise(function (resolve) {
            var attempts = 0;
            var tryRender = function () {
                if (typeof turnstile !== 'undefined') {
                    turnstile.render('#' + elementId, {
                        sitekey: siteKey,
                        callback: function (token) { resolve(token); },
                        'error-callback': function () { resolve(''); },
                        'expired-callback': function () { resolve(''); }
                    });
                    return;
                }
                if (++attempts > 50) { resolve(''); return; }
                setTimeout(tryRender, 100);
            };
            tryRender();
        });
    },
    reset: function (elementId) {
        if (typeof turnstile !== 'undefined') {
            turnstile.reset('#' + elementId);
        }
    }
};
