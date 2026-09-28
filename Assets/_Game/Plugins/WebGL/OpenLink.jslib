// Opens an external link in a new tab from a player's tap.
// Unity delivers input a frame after the DOM event, so the browser may already consider the
// gesture spent and block window.open. If the direct call is blocked, the link is armed on the
// player's next pointerup/click and expires after a few seconds; nothing ever opens on its own.
mergeInto(LibraryManager.library, {

    OpenLink_Open: function (urlPtr) {
        var url = UTF8ToString(urlPtr);
        if (!/^https?:\/\//i.test(url)) {
            return;
        }

        var state = window.__bpOpenLink || (window.__bpOpenLink = { disarm: null });
        if (state.disarm) {
            state.disarm();
        }

        // "noopener" would make window.open return null even on success, hiding a blocked popup,
        // so the opener is cut by hand instead.
        function openNow() {
            var w = null;
            try {
                w = window.open(url, "_blank");
                if (w) {
                    try { w.opener = null; } catch (e) { }
                }
            } catch (e) { }
            return !!w;
        }

        if (openNow()) {
            return;
        }

        var timer = 0;
        function disarm() {
            document.removeEventListener("pointerup", handler, true);
            document.removeEventListener("click", handler, true);
            clearTimeout(timer);
            state.disarm = null;
        }
        function handler() {
            disarm();
            openNow();
        }

        document.addEventListener("pointerup", handler, true);
        document.addEventListener("click", handler, true);
        timer = setTimeout(disarm, 3000);
        state.disarm = disarm;
    }
});
