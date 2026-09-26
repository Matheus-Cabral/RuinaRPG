// Aba História — Jodit (wwwroot/lib/jodit, MIT) behind a tiny API for RichTextEditor.razor.
// Jodit itself (~900 KB) is only fetched the first time an editor is created.
window.ruinaRichText = {
    _loading: null,
    _instances: new Map(),
    DEBOUNCE_MS: 1000,

    _ensureLoaded: function () {
        if (window.Jodit) return Promise.resolve();
        if (!this._loading) {
            this._loading = new Promise(function (resolve, reject) {
                var css = document.createElement('link');
                css.rel = 'stylesheet';
                css.href = 'lib/jodit/jodit.min.css';
                document.head.appendChild(css);
                var script = document.createElement('script');
                script.src = 'lib/jodit/jodit.min.js';
                script.onload = resolve;
                script.onerror = function (err) {
                    // Forget the failed attempt so the next editor created can try again.
                    window.ruinaRichText._loading = null;
                    reject(err);
                };
                document.head.appendChild(script);
            });
        }
        return this._loading;
    },

    _isDark: function () {
        return document.documentElement.getAttribute('data-theme') === 'dark';
    },

    create: async function (element, dotNetRef, initialHtml) {
        await this._ensureLoaded();
        var self = this;
        var editor = Jodit.make(element, {
            language: 'pt_br',
            theme: this._isDark() ? 'dark' : 'default',
            minHeight: 500,
            height: 'auto',
            toolbarAdaptive: false,
            showCharsCounter: false,
            showWordsCounter: false,
            showXPathInStatusbar: false,
            askBeforePasteHTML: false,
            askBeforePasteFromWord: false,
            defaultActionOnPaste: 'insert_clear_html',
            // Jodit's default paragraph list minus pre ("Code"): the server allowlist has no <pre>.
            // Jodit.atom stops Jodit merging this over the default list (which would bring "pre" back).
            controls: {
                paragraph: {
                    list: Jodit.atom({ p: 'Paragraph', h1: 'Heading 1', h2: 'Heading 2', h3: 'Heading 3', h4: 'Heading 4', blockquote: 'Quote' })
                }
            },
            disablePlugins: ['image', 'video', 'file', 'media', 'source', 'print', 'about', 'speech-recognize', 'ai-assistant'],
            buttons: [
                'undo', 'redo', '|',
                'paragraph', 'font', 'fontsize', '|',
                'bold', 'italic', 'underline', 'strikethrough', 'superscript', 'subscript', '|',
                'brush', '|',
                'ul', 'ol', 'outdent', 'indent', 'align', '|',
                'link', 'table', 'hr', '|',
                'find', 'eraser', 'fullsize'
            ]
        });
        editor.value = initialHtml || '';

        var state = { editor: editor, timer: null, observer: null };

        var send = function () {
            state.timer = null;
            dotNetRef.invokeMethodAsync('OnEditorChanged', editor.value);
        };
        state.flush = function () {
            if (state.timer !== null) {
                clearTimeout(state.timer);
                send();
            }
        };

        editor.events.on('change', function () {
            if (state.timer !== null) clearTimeout(state.timer);
            state.timer = setTimeout(send, self.DEBOUNCE_MS);
        });
        editor.events.on('blur', function () { state.flush(); });

        // Follow the app's light/dark toggle (theme.js sets data-theme on <html>).
        state.observer = new MutationObserver(function () {
            var dark = self._isDark();
            editor.container.classList.toggle('jodit_theme_dark', dark);
            editor.container.classList.toggle('jodit_theme_default', !dark);
        });
        state.observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });

        this._instances.set(element, state);
    },

    destroy: function (element) {
        var state = this._instances.get(element);
        if (!state) return;
        state.flush();
        state.observer.disconnect();
        state.editor.destruct();
        this._instances.delete(element);
    }
};
