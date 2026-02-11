// Modal component
function showModal(title, bodyHtml, options = {}) {
    const existing = document.getElementById('app-modal');
    if (existing) existing.remove();

    const modal = document.createElement('div');
    modal.id = 'app-modal';
    modal.className = 'modal-backdrop';
    modal.innerHTML = `
        <div class="modal-content">
            <div class="flex items-center justify-between p-4 border-b">
                <h3 class="text-lg font-semibold text-gray-800">${title}</h3>
                <button onclick="closeModal()" class="text-gray-400 hover:text-gray-600">
                    <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>
                </button>
            </div>
            <div class="p-4">${bodyHtml}</div>
            ${options.footer ? `<div class="flex justify-end gap-2 p-4 border-t bg-gray-50 rounded-b-xl">${options.footer}</div>` : ''}
        </div>
    `;
    modal.addEventListener('click', (e) => { if (e.target === modal) closeModal(); });
    document.body.appendChild(modal);
}

function closeModal() {
    const modal = document.getElementById('app-modal');
    if (modal) modal.remove();
}

function confirmDialog(title, message, onConfirm) {
    showModal(title, `<p class="text-gray-600">${message}</p>`, {
        footer: `
            <button onclick="closeModal()" class="btn btn-secondary">ยกเลิก</button>
            <button onclick="closeModal(); (${onConfirm.toString()})()" class="btn btn-danger">ยืนยัน</button>
        `
    });
}

function formModal(title, fields, onSubmit) {
    let formHtml = '<form id="modal-form" class="space-y-4">';
    fields.forEach(f => {
        formHtml += `<div>
            <label class="form-label">${f.label}</label>`;
        if (f.type === 'select') {
            formHtml += `<select name="${f.name}" class="form-input" ${f.required ? 'required' : ''}>
                ${f.options.map(o => `<option value="${o.value}">${o.label}</option>`).join('')}
            </select>`;
        } else if (f.type === 'textarea') {
            formHtml += `<textarea name="${f.name}" class="form-input" rows="3" ${f.required ? 'required' : ''} placeholder="${f.placeholder || ''}"></textarea>`;
        } else {
            formHtml += `<input type="${f.type || 'text'}" name="${f.name}" class="form-input" ${f.required ? 'required' : ''} placeholder="${f.placeholder || ''}" value="${f.value || ''}">`;
        }
        formHtml += '</div>';
    });
    formHtml += '</form>';

    showModal(title, formHtml, {
        footer: `
            <button onclick="closeModal()" class="btn btn-secondary">ยกเลิก</button>
            <button onclick="submitModalForm()" class="btn btn-primary">บันทึก</button>
        `
    });

    window._modalSubmit = onSubmit;
}

function submitModalForm() {
    const form = document.getElementById('modal-form');
    if (!form.checkValidity()) { form.reportValidity(); return; }
    const data = Object.fromEntries(new FormData(form));
    closeModal();
    if (window._modalSubmit) window._modalSubmit(data);
}
