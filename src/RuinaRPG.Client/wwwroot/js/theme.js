window.ruinaTheme = {
    KEY: 'rr-theme',

    resolveInitial: function () {
        var stored = localStorage.getItem(this.KEY);
        if (stored === 'light' || stored === 'dark') return stored;
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    },

    set: function (theme) {
        localStorage.setItem(this.KEY, theme);
        document.documentElement.setAttribute('data-theme', theme);
    }
};
