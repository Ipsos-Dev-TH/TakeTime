const Pages = window.Pages || {};
Pages.affiliates = {
    title: 'พันธมิตร',
    async render() {
        const stats = renderStats([
            { label: 'พันธมิตรทั้งหมด', value: '15', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101"/></svg>' },
            { label: 'ใช้งาน', value: '12', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>' },
            { label: 'การจองจากพันธมิตร', value: '89', color: 'purple', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>' },
            { label: 'คอมมิชชัน', value: '฿45,600', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2z"/></svg>' },
        ]);
        const affiliates = [
            { id: 1, name: 'Travel Agency A', contact: 'คุณสุรชัย', commission: '10%', bookings: 35, revenue: 225000, payout: 22500, status: 'Active' },
            { id: 2, name: 'Hotel Booking Co.', contact: 'Ms. Lisa', commission: '8%', bookings: 28, revenue: 180000, payout: 14400, status: 'Active' },
            { id: 3, name: 'Thai Tours', contact: 'คุณนภา', commission: '12%', bookings: 15, revenue: 95000, payout: 11400, status: 'Active' },
            { id: 4, name: 'Beach Resort Partners', contact: 'Mr. James', commission: '10%', bookings: 11, revenue: 78000, payout: 7800, status: 'Inactive' },
        ];
        const table = renderTable(
            [
                { label: 'พันธมิตร', render: r => `<div><p class="font-medium">${r.name}</p><p class="text-xs text-gray-400">${r.contact}</p></div>` },
                { label: 'คอมมิชชัน', key: 'commission' },
                { label: 'การจอง', key: 'bookings' },
                { label: 'รายได้', render: r => formatMoney(r.revenue) },
                { label: 'จ่ายแล้ว', render: r => formatMoney(r.payout) },
                { label: 'สถานะ', render: r => statusBadge(r.status) },
            ],
            affiliates,
            { actions: (r) => `<button class="btn btn-sm btn-secondary" onclick="Pages.affiliates.view(${r.id})">ดู</button> <button class="btn btn-sm btn-secondary" onclick="Pages.affiliates.payout(${r.id})">จ่ายเงิน</button>` }
        );
        return stats + renderCard('รายชื่อพันธมิตร', table, '<button class="btn btn-sm btn-primary" onclick="Pages.affiliates.add()">+ เพิ่มพันธมิตร</button>');
    },
    add() {
        formModal('เพิ่มพันธมิตร', [
            { name: 'name', label: 'ชื่อบริษัท', required: true },
            { name: 'contact', label: 'ผู้ติดต่อ', required: true },
            { name: 'email', label: 'อีเมล', type: 'email', required: true },
            { name: 'commission', label: 'คอมมิชชัน (%)', type: 'number', required: true },
        ], async (data) => { alert('เพิ่มพันธมิตรสำเร็จ (Demo)'); });
    },
    view(id) { showModal('ข้อมูลพันธมิตร', '<p class="text-gray-500">ประวัติการจอง, รายงานคอมมิชชัน</p>'); },
    payout(id) { confirmDialog('จ่ายคอมมิชชัน', 'ยืนยันการจ่ายคอมมิชชัน?', () => alert('จ่ายเงินสำเร็จ (Demo)')); },
};
window.Pages = Pages;
