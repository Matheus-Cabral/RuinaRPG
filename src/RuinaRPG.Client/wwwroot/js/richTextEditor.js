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

    // "Inserir imagem": pick a file, let .NET upload it (RichTextEditor.UploadImageFromJs — the authenticated
    // HttpClient lives there, so no token ever reaches this script) and insert the app URL it returns
    // ("/images/{file}") where the cursor was. Nothing else is ever put in src: no blob:, no data:.
    _pickImage: function (editor, dotNetRef, state) {
        // The file dialog takes the focus away; remember where the cursor was.
        var range = null;
        var sel = editor.s.sel;
        if (sel && sel.rangeCount > 0 && editor.editor.contains(sel.getRangeAt(0).commonAncestorContainer)) {
            range = sel.getRangeAt(0).cloneRange();
        }

        var input = document.createElement('input');
        input.type = 'file';
        input.accept = 'image/png,image/jpeg,image/gif,image/webp';
        input.style.display = 'none';
        input.addEventListener('change', async function () {
            var file = input.files && input.files[0];
            input.remove();
            if (!file) return;

            var url = null;
            try {
                url = await dotNetRef.invokeMethodAsync('UploadImageFromJs', DotNet.createJSStreamReference(file), file.name);
            } catch (err) {
                url = null;
            }
            // null = the upload failed (the page already shows why) or the editor is gone.
            if (!url || editor.isDestructed) return;

            if (range) editor.s.selectRange(range);
            editor.s.insertImage(url, null, null);
            editor.synchronizeValues();
            // Save right away instead of waiting for the debounce: the image is already on the server.
            if (state.timer !== null) clearTimeout(state.timer);
            state.timer = null;
            dotNetRef.invokeMethodAsync('OnEditorChanged', editor.value);
        });
        input.addEventListener('cancel', function () { input.remove(); });
        document.body.appendChild(input);
        input.click();
    },

    // Only "Inserir imagem" may add an image. A dropped file, or an image file pasted from the clipboard, would
    // otherwise be inserted by the browser itself (blob:/data: src) or sent to Jodit's unconfigured uploader.
    // Runs in the capture phase so it wins over Jodit's handlers; text and HTML paste/drag pass untouched.
    _blockFileInsertion: function (editor) {
        var hasFiles = function (dt) { return !!dt && ((dt.files && dt.files.length > 0) || Array.prototype.some.call(dt.items || [], function (i) { return i.kind === 'file'; })); };
        var block = function (e) { e.preventDefault(); e.stopImmediatePropagation(); };
        editor.editor.addEventListener('drop', function (e) { if (hasFiles(e.dataTransfer)) block(e); }, true);
        editor.editor.addEventListener('dragover', function (e) { if (hasFiles(e.dataTransfer)) block(e); }, true);
        editor.editor.addEventListener('paste', function (e) {
            var dt = e.clipboardData;
            // A clipboard that also carries text/html (copied from a page or Word) is a normal paste: Jodit
            // sanitizes the markup, and the server drops any foreign image. Only a files-only clipboard is blocked.
            if (hasFiles(dt) && !dt.getData('text/html') && !dt.getData('text/plain')) block(e);
        }, true);
    },

    create: async function (element, dotNetRef, initialHtml, options) {
        await this._ensureLoaded();
        var self = this;
        var allowImages = !!(options && options.imagens);
        var state = { editor: null, timer: null, observer: null };
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
                },
                inserirImagem: {
                    icon: 'image',
                    tooltip: 'Inserir imagem',
                    exec: function (ed) { self._pickImage(ed, dotNetRef, state); }
                }
            },
            // Jodit's own image plugins stay off: they insert by URL (any site) or as base64, and the server
            // only keeps images hosted by the app. Our button above is the one way to add an image.
            // Dropping a file: with this off Jodit cancels the drop instead of handing the file to its uploader
            // (which has no URL configured here). Pasted files are cancelled by blockFileInsertion below.
            enableDragAndDropFileToEditor: false,
            disablePlugins: ['image', 'image-properties', 'image-processor', 'video', 'file', 'media', 'source', 'print', 'about', 'speech-recognize', 'ai-assistant'],
            buttons: [
                'undo', 'redo', '|',
                'paragraph', 'font', 'fontsize', '|',
                'bold', 'italic', 'underline', 'strikethrough', 'superscript', 'subscript', '|',
                'brush', '|',
                'ul', 'ol', 'outdent', 'indent', 'align', '|',
                'link'].concat(allowImages ? ['inserirImagem'] : [], ['table', 'hr', '|',
                'find', 'eraser', 'fullsize'
            ])
        });
        editor.value = initialHtml || '';
        this._blockFileInsertion(editor);

        state.editor = editor;

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
