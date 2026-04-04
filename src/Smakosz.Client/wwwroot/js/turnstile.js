window.smakoszTurnstile = {
    render: function (elementId, siteKey) {
        return new Promise(function (resolve) {
            if (typeof turnstile === 'undefined') { resolve(''); return; }
            turnstile.render('#' + elementId, {
                sitekey: siteKey,
                callback: function (token) { resolve(token); },
                'error-callback': function () { resolve(''); },
                'expired-callback': function () { resolve(''); }
            });
        });
    },
    reset: function (elementId) {
        if (typeof turnstile !== 'undefined') {
            turnstile.reset('#' + elementId);
        }
    }
};
