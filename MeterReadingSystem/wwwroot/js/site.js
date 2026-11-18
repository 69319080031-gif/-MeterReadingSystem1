// Shared UI behaviour: mobile sidebar, delete confirmation modal, auto-hide flash messages.
(function () {
    const sidebar = document.getElementById('sidebar');
    const toggle = document.getElementById('sidebarToggle');
    const backdrop = document.getElementById('sidebarBackdrop');
    if (toggle && sidebar) {
        toggle.addEventListener('click', () => sidebar.classList.toggle('open'));
        backdrop?.addEventListener('click', () => sidebar.classList.remove('open'));
    }

    // Any element with data-delete-url opens the shared confirm modal; the modal form POSTs with an anti-forgery token.
    const modalEl = document.getElementById('confirmDeleteModal');
    if (modalEl) {
        const modal = new bootstrap.Modal(modalEl);
        document.addEventListener('click', e => {
            const btn = e.target.closest('[data-delete-url]');
            if (!btn) return;
            e.preventDefault();
            document.getElementById('confirmDeleteForm').action = btn.dataset.deleteUrl;
            document.getElementById('confirmDeleteMessage').textContent = btn.dataset.deleteMessage || 'ต้องการลบรายการนี้หรือไม่?';
            document.getElementById('confirmDeleteReturn').value = btn.dataset.returnUrl || (location.pathname + location.search);
            modal.show();
        });
    }

    document.querySelectorAll('.alert.flash.alert-success').forEach(a =>
        setTimeout(() => bootstrap.Alert.getOrCreateInstance(a).close(), 7000));

    // Auto-submit filter selects marked with data-autosubmit.
    document.querySelectorAll('[data-autosubmit]').forEach(el =>
        el.addEventListener('change', () => el.form?.requestSubmit()));
})();

// Number formatting helper shared by pages (Gregorian / en-US style separators).
window.fmtNum = function (v, digits = 3) {
    return Number(v).toLocaleString('en-US', { maximumFractionDigits: digits });
};
