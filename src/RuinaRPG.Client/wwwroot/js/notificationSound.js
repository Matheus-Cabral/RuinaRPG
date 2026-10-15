window.ruinaNotificationSound = {
    // Browsers reject play() until the user has interacted with the page (autoplay policy). The
    // visual notification is shown regardless, so a blocked sound is swallowed rather than surfaced.
    play: function () {
        var audio = new Audio('audio/harp_notification.mp3');
        var playing = audio.play();
        if (playing && playing.catch) {
            playing.catch(function () { });
        }
    }
};
