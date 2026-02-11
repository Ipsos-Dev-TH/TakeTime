// Reusable table component
function renderTable(columns, rows, options = {}) {
    if (!rows || rows.length === 0) {
        return `<div class="text-center py-12 text-gray-400">
            <svg class="w-12 h-12 mx-auto mb-3 text-gray-300" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4"/></svg>
            <p>${options.emptyText || 'ไม่มีข้อมูล'}</p>
        </div>`;
    }

    let html = '<div class="overflow-x-auto"><table class="data-table"><thead><tr>';
    columns.forEach(col => {
        html += `<th>${col.label}</th>`;
    });
    if (options.actions) html += '<th class="text-right">การดำเนินการ</th>';
    html += '</tr></thead><tbody>';

    rows.forEach(row => {
        html += '<tr>';
        columns.forEach(col => {
            const val = col.render ? col.render(row) : (row[col.key] || '-');
            html += `<td>${val}</td>`;
        });
        if (options.actions) {
            html += `<td class="text-right">${options.actions(row)}</td>`;
        }
        html += '</tr>';
    });

    html += '</tbody></table></div>';
    return html;
}

function badge(text, type = 'gray') {
    return `<span class="badge badge-${type}">${text}</span>`;
}

function statusBadge(status) {
    const map = {
        'Confirmed': ['ยืนยันแล้ว', 'green'],
        'Pending': ['รอดำเนินการ', 'yellow'],
        'CheckedIn': ['เช็คอินแล้ว', 'blue'],
        'CheckedOut': ['เช็คเอาท์แล้ว', 'gray'],
        'Cancelled': ['ยกเลิก', 'red'],
        'Active': ['ใช้งาน', 'green'],
        'Inactive': ['ปิดใช้งาน', 'gray'],
        'Completed': ['เสร็จสิ้น', 'green'],
        'InProgress': ['กำลังดำเนินการ', 'blue'],
        'Assigned': ['มอบหมายแล้ว', 'purple'],
        'Open': ['เปิด', 'yellow'],
        'Paid': ['ชำระแล้ว', 'green'],
        'Refunded': ['คืนเงินแล้ว', 'red'],
        'Available': ['ว่าง', 'green'],
        'Occupied': ['ไม่ว่าง', 'red'],
        'Cleaning': ['กำลังทำความสะอาด', 'yellow'],
        'Maintenance': ['ซ่อมบำรุง', 'red'],
    };
    const [label, color] = map[status] || [status, 'gray'];
    return badge(label, color);
}

function formatDate(dateStr) {
    if (!dateStr) return '-';
    const d = new Date(dateStr);
    return d.toLocaleDateString('th-TH', { day: '2-digit', month: 'short', year: 'numeric' });
}

function formatMoney(amount, currency = '฿') {
    if (amount == null) return '-';
    return `${currency}${Number(amount).toLocaleString('th-TH', { minimumFractionDigits: 2 })}`;
}
