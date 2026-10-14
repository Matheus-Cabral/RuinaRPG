// Diz se o usuário está com o cursor num campo de digitação agora. As fichas adiam a recarga disparada por
// uma Condição enquanto isso, para não trocar o valor do campo que está sendo editado. Só conta campo de texto
// editável: checkbox, rádio, botão e o input somente leitura dos selects/switches do MudBlazor mantêm o foco
// depois do clique e não são digitação.
window.ruinaFoco = {
    campoEmEdicao: function () {
        const e = document.activeElement;
        if (!e || e.readOnly || e.disabled) return false;
        if (e.isContentEditable === true || e.tagName === 'TEXTAREA') return true;
        if (e.tagName !== 'INPUT') return false;
        const tipo = (e.getAttribute('type') || 'text').toLowerCase();
        return ['text', 'number', 'search', 'tel', 'email', 'url', 'password'].includes(tipo);
    }
};
