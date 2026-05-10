window.smakoszScroll = {
    getY: function () {
        return window.scrollY || window.pageYOffset || 0;
    },
    setY: function (y) {
        window.scrollTo({ top: y, behavior: 'instant' });
    },
    scrollToElement: function (id) {
        var el = document.getElementById(id);
        if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
};
