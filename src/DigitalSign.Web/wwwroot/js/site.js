// BT Digital Sign — site.js

$(function () {

    // ── Auto-dismiss alerts หลัง 5 วินาที ────────────────────────────────────
    setTimeout(function () {
        $('.alert-dismissible').fadeOut('slow');
    }, 5000);

    // ── PDF file input — แสดงชื่อไฟล์และ auto-fill DocumentName ──────────────
    $('#pdfFileInput').on('change', function () {
        const file = this.files[0];
        if (!file) return;

        // แสดงชื่อไฟล์
        const label = $(this).next('.form-text');
        label.text('ไฟล์ที่เลือก: ' + file.name + ' (' + (file.size / 1024).toFixed(0) + ' KB)');

        // Auto-fill DocumentName ถ้ายังว่างอยู่
        const docNameInput = $('#DocumentName');
        if (docNameInput.length && !docNameInput.val()) {
            const nameWithoutExt = file.name.replace(/\.pdf$/i, '');
            docNameInput.val(nameWithoutExt);
        }
    });

    // ── Confirm ก่อน Sign PDF ─────────────────────────────────────────────────
    $('form').on('submit', function () {
        const btn = $(this).find('[type=submit]');
        if (btn.data('confirming')) return true;

        btn.html('<span class="spinner-border spinner-border-sm me-1"></span>กำลังดำเนินการ...')
           .prop('disabled', true);
    });

    // ── Copy to clipboard ─────────────────────────────────────────────────────
    window.copyToClipboard = function (text) {
        navigator.clipboard.writeText(text).then(function () {
            showToast('Copy แล้ว!', 'success');
        });
    };

    // ── Toast notification ────────────────────────────────────────────────────
    window.showToast = function (message, type = 'info') {
        const toast = $('<div>')
            .addClass('toast align-items-center text-bg-' + type + ' border-0 position-fixed bottom-0 end-0 m-3')
            .attr('role', 'alert')
            .html('<div class="d-flex"><div class="toast-body">' + message + '</div>' +
                  '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button></div>');

        $('body').append(toast);
        const bsToast = new bootstrap.Toast(toast[0], { delay: 3000 });
        bsToast.show();
        toast.on('hidden.bs.toast', function () { $(this).remove(); });
    };
});
