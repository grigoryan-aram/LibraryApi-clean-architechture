// Small helpers the chat circuit calls over JS interop.
window.athenaeum = {
    scrollToEnd: function (elementId) {
        const el = document.getElementById(elementId);
        if (el) {
            el.scrollTop = el.scrollHeight;
        }
    }
};
