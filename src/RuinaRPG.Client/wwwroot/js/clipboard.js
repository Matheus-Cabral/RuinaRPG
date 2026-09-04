window.ruinaClipboard = {
    // navigator.clipboard only exists in a secure context (HTTPS, or http://localhost) — a
    // plain-HTTP LAN deployment (no TLS in front of it) has no such API, so writeText would throw
    // on `undefined` there. Falls back to the legacy execCommand('copy') technique in that case.
    copy: function (text) {
        if (window.isSecureContext && navigator.clipboard) {
            return navigator.clipboard.writeText(text).then(function () {
                return true;
            }).catch(function () {
                return window.ruinaClipboard.legacyCopy(text);
            });
        }
        return Promise.resolve(window.ruinaClipboard.legacyCopy(text));
    },

    legacyCopy: function (text) {
        var textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.appendChild(textarea);
        textarea.focus();
        textarea.select();

        var succeeded = false;
        try {
            succeeded = document.execCommand('copy');
        } catch (e) {
            succeeded = false;
        }

        document.body.removeChild(textarea);
        return succeeded;
    }
};
