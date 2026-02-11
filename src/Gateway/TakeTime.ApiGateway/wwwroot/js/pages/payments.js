const Pages = window.Pages || {};
Pages.payments = {
    title: 'การเงิน & ชำระเงิน',
    async render() {
        const stats = renderStats([
            { label: 'รายได้วันนี้', value: '฿45,200', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8V7m0 1v8m0 0v1"/></svg>' },
            { label: 'รายได้เดือนนี้', value: '฿687,500', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6"/></svg>' },
            { label: 'รอชำระ', value: '3', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>' },
            { label: 'คืนเงิน', value: '1', color: 'red', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6"/></svg>' },
        ]);

        const payments = [
            { id: 'PAY-001', reservation: 'RES-001', guest: 'คุณสมชาย ใจดี', method: 'PromptPay', amount: 4500, vat: 315, total: 4815, status: 'Paid', date: '2026-02-11' },
            { id: 'PAY-002', reservation: 'RES-002', guest: 'John Smith', method: 'Credit Card', amount: 12000, vat: 840, total: 12840, status: 'Paid', date: '2026-02-11' },
            { id: 'PAY-003', reservation: 'RES-003', guest: 'คุณวิภา แสงทอง', method: '-', amount: 1500, vat: 105, total: 1605, status: 'Pending', date: '2026-02-12' },
            { id: 'PAY-004', reservation: 'RES-006', guest: 'คุณกมล ดีงาม', method: 'Bank Transfer', amount: 3500, vat: 245, total: 3745, status: 'Refunded', date: '2026-02-09' },
        ];

        const table = renderTable(
            [
                { label: 'รหัส', key: 'id' },
                { label: 'การจอง', key: 'reservation' },
                { label: 'แขก', key: 'guest' },
                { label: 'วิธีชำระ', key: 'method' },
                { label: 'ยอด', render: r => formatMoney(r.amount) },
                { label: 'VAT 7%', render: r => formatMoney(r.vat) },
                { label: 'รวม', render: r => `<span class="font-semibold">${formatMoney(r.total)}</span>` },
                { label: 'สถานะ', render: r => statusBadge(r.status) },
                { label: 'วันที่', render: r => formatDate(r.date) },
            ],
            payments,
            { actions: (r) => {
                let btns = `<button class="btn btn-sm btn-secondary" onclick="Pages.payments.receipt('${r.id}')">ใบเสร็จ</button>`;
                if (r.status === 'Paid') btns += ` <button class="btn btn-sm btn-danger" onclick="Pages.payments.refund('${r.id}')">คืนเงิน</button>`;
                return btns;
            }}
        );

        return stats + renderCard('รายการชำระเงิน', table, '<button class="btn btn-sm btn-primary" onclick="Pages.payments.newPayment()">+ บันทึกการชำระ</button>');
    },
    newPayment() {
        formModal('บันทึกการชำระเงิน', [
            { name: 'reservationId', label: 'รหัสการจอง', required: true },
            { name: 'method', label: 'วิธีชำระ', type: 'select', options: [
                { value: 'Cash', label: 'เงินสด' }, { value: 'PromptPay', label: 'PromptPay' },
                { value: 'CreditCard', label: 'บัตรเครดิต' }, { value: 'BankTransfer', label: 'โอนเงิน' },
            ]},
            { name: 'amount', label: 'จำนวนเงิน (฿)', type: 'number', required: true },
            { name: 'notes', label: 'หมายเหตุ', type: 'textarea' },
        ], async (data) => { alert('บันทึกการชำระเงินสำเร็จ (Demo)'); });
    },
    receipt(id) { showModal('ใบเสร็จ ' + id, '<p class="text-gray-500">แสดงใบเสร็จรับเงิน / ใบกำกับภาษี</p>'); },
    refund(id) { confirmDialog('คืนเงิน', `ยืนยันการคืนเงิน ${id}?`, () => alert('คืนเงินสำเร็จ (Demo)')); },
};
window.Pages = Pages;
