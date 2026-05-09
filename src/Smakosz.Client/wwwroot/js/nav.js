window.smakoszNav = {
    closeMobileMenu(selector) {
        const el = document.querySelector(selector);
        if (!el || !el.classList.contains('show')) return;
        if (typeof bootstrap === 'undefined') return;
        const instance = bootstrap.Collapse.getInstance(el)
            || new bootstrap.Collapse(el, { toggle: false });
        instance.hide();
    }
};
