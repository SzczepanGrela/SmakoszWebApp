window.smakoszCarousel = {
    scrollLeft: function (trackId) {
        var t = document.getElementById(trackId);
        if (!t) return;
        var d = window.innerWidth <= 480 ? (t.querySelector('.carousel-item-wrapper').offsetWidth + 16) : 600;
        t.scrollBy({ left: -d, behavior: 'smooth' });
    },
    scrollRight: function (trackId) {
        var t = document.getElementById(trackId);
        if (!t) return;
        var d = window.innerWidth <= 480 ? (t.querySelector('.carousel-item-wrapper').offsetWidth + 16) : 600;
        t.scrollBy({ left: d, behavior: 'smooth' });
    },
    installScrollListener: function (trackId, dotNetRef) {
        var t = document.getElementById(trackId);
        if (!t) return;
        var lastCanLeft = null, lastCanRight = null, throttleTimer = null;
        var handler = function () {
            if (throttleTimer) return;
            throttleTimer = setTimeout(function () {
                throttleTimer = null;
                var canLeft = t.scrollLeft > 10;
                var canRight = t.scrollLeft < t.scrollWidth - t.clientWidth - 10;
                if (canLeft !== lastCanLeft || canRight !== lastCanRight) {
                    lastCanLeft = canLeft;
                    lastCanRight = canRight;
                    dotNetRef.invokeMethodAsync('OnScrollStateChanged', canLeft, canRight);
                }
            }, 50);
        };
        t.addEventListener('scroll', handler, { passive: true });
        t._smakoszScrollHandler = handler;
        handler();
    },
    uninstallScrollListener: function (trackId) {
        var t = document.getElementById(trackId);
        if (!t || !t._smakoszScrollHandler) return;
        t.removeEventListener('scroll', t._smakoszScrollHandler);
        t._smakoszScrollHandler = null;
    }
};
