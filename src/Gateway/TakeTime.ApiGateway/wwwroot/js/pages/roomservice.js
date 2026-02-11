const Pages = window.Pages || {};
Pages.roomservice = {
    title: 'รูมเซอร์วิส',
    async render() {
        const stats = renderStats([
            { label: 'ออเดอร์วันนี้', value: '15', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2"/></svg>' },
            { label: 'กำลังจัดเตรียม', value: '3', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3"/></svg>' },
            { label: 'จัดส่งแล้ว', value: '11', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>' },
            { label: 'รายได้วันนี้', value: '฿4,250', color: 'indigo', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8V7m0 1v8m0 0v1"/></svg>' },
        ]);
        const orders = [
            { id: 'RS-001', room: '301', items: 'ข้าวผัด x1, น้ำส้ม x2', total: 280, status: 'InProgress', time: '09:15', guest: 'John Smith' },
            { id: 'RS-002', room: '201', items: 'กาแฟร้อน x1', total: 80, status: 'Pending', time: '09:30', guest: 'คุณสมชาย' },
            { id: 'RS-003', room: '102', items: 'ผัดไทย x2, น้ำดื่ม x2', total: 340, status: 'Completed', time: '08:45', guest: 'คุณวิภา' },
            { id: 'RS-004', room: '401', items: 'อาหารเช้าชุด x3', total: 750, status: 'Completed', time: '07:30', guest: 'คุณปรีชา' },
        ];
        const table = renderTable(
            [
                { label: 'ออเดอร์', key: 'id' },
                { label: 'ห้อง', render: r => `<span class="font-mono font-bold">${r.room}</span>` },
                { label: 'แขก', key: 'guest' },
                { label: 'รายการ', key: 'items' },
                { label: 'ยอด', render: r => formatMoney(r.total) },
                { label: 'เวลา', key: 'time' },
                { label: 'สถานะ', render: r => statusBadge(r.status) },
            ],
            orders,
            { actions: (r) => {
                let btns = '';
                if (r.status === 'Pending') btns += `<button class="btn btn-sm btn-primary" onclick="Pages.roomservice.accept('${r.id}')">รับออเดอร์</button> `;
                if (r.status === 'InProgress') btns += `<button class="btn btn-sm btn-success" onclick="Pages.roomservice.deliver('${r.id}')">จัดส่งแล้ว</button> `;
                return btns || `<button class="btn btn-sm btn-secondary" onclick="Pages.roomservice.view('${r.id}')">ดู</button>`;
            }}
        );
        return stats + renderCard('ออเดอร์รูมเซอร์วิส', table, '<button class="btn btn-sm btn-primary" onclick="Pages.roomservice.create()">+ สร้างออเดอร์</button>');
    },
    create() {
        formModal('สร้างออเดอร์รูมเซอร์วิส', [
            { name: 'room', label: 'หมายเลขห้อง', required: true },
            { name: 'items', label: 'รายการ', type: 'textarea', required: true, placeholder: 'รายการอาหาร/เครื่องดื่ม' },
            { name: 'notes', label: 'หมายเหตุ', type: 'textarea' },
        ], async (data) => { alert('สร้างออเดอร์สำเร็จ (Demo)'); });
    },
    accept(id) { confirmDialog('รับออเดอร์', `รับออเดอร์ ${id}?`, () => alert('รับออเดอร์แล้ว (Demo)')); },
    deliver(id) { confirmDialog('จัดส่ง', `ยืนยันจัดส่ง ${id}?`, () => alert('จัดส่งแล้ว (Demo)')); },
    view(id) { showModal('ออเดอร์ ' + id, '<p class="text-gray-500">รายละเอียดออเดอร์</p>'); },
};
window.Pages = Pages;
