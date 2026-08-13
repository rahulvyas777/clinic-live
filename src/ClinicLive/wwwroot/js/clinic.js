// Tiny interop helpers — Part 9 of From Prompt to Polish.
// A chat that doesn't follow its own conversation is a bug, not a feature.
window.clinic = {
    scrollToEnd: (el) => {
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    },
};
