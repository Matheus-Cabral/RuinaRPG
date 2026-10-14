// Diz se o usuário está com o cursor num campo de digitação agora. As fichas adiam a recarga disparada por
// uma Condição enquanto isso, para não trocar o valor do campo que está sendo editado.
window.ruinaFoco = {
    campoEmEdicao: function () {
        const e = document.activeElement;
        if (!e) return false;
        return e.tagName === 'INPUT' || e.tagName === 'TEXTAREA' || e.tagName === 'SELECT' || e.isContentEditable === true;
    }
};
