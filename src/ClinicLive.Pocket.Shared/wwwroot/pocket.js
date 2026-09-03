// The browser's answers to the device capabilities. Each returns honestly when the
// browser can't do the thing — desktop Chrome has no vibration motor, Safari has
// no navigator.vibrate at all — and the shared UI is written to cope.
window.pocketDevice = {
    vibrate(pattern) {
        return typeof navigator.vibrate === "function" ? navigator.vibrate(pattern) : false;
    },

    async requestNotifications() {
        if (!("Notification" in window)) return false;
        if (Notification.permission === "granted") return true;
        if (Notification.permission === "denied") return false;
        return (await Notification.requestPermission()) === "granted";
    },

    notify(title, body) {
        if (!("Notification" in window) || Notification.permission !== "granted") return false;
        new Notification(title, { body, tag: "cliniclive-queue" });
        return true;
    },

    // Geolocation needs a secure context (https, or localhost) and a user gesture
    // in most browsers; the browser shows its own permission bar.
    locate() {
        return new Promise(resolve => {
            if (!("geolocation" in navigator)) return resolve(null);
            navigator.geolocation.getCurrentPosition(
                p => resolve({ latitude: p.coords.latitude, longitude: p.coords.longitude }),
                () => resolve(null),
                { enableHighAccuracy: false, timeout: 10000, maximumAge: 60000 });
        });
    },

    openDirections(lat, lng) {
        const w = window.open(`https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`, "_blank", "noopener");
        return w !== null;
    },
};
