window.ksCopy = async function (text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (e) {
        const area = document.createElement('textarea');
        area.value = text;
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        const ok = document.execCommand('copy');
        document.body.removeChild(area);
        return ok;
    }
};

window.ksInsert = function (id, snippet) {
    const el = document.getElementById(id);
    if (!el) return null;

    const start = el.selectionStart ?? el.value.length;
    const end = el.selectionEnd ?? start;
    const value = el.value.slice(0, start) + snippet + el.value.slice(end);

    el.value = value;
    el.focus();
    el.selectionStart = el.selectionEnd = start + snippet.length;
    return value;
};

(function () {
    function handleTab(event) {
        const target = event.target;
        if (!target || target.id !== 'latex-editor' || event.key !== 'Tab') return;

        event.preventDefault();
        const indent = '  ';
        const start = target.selectionStart ?? target.value.length;
        const end = target.selectionEnd ?? start;

        target.setRangeText(indent, start, end, 'end');
        target.dispatchEvent(new Event('input', { bubbles: true }));
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            document.addEventListener('keydown', handleTab);
        });
    } else {
        document.addEventListener('keydown', handleTab);
    }
})();
