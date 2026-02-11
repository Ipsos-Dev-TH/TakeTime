const Pages = window.Pages || {};
Pages.maintenance = {
    title: 'ซ่อมบำรุง',
    async render() {
        const stats = renderStats([
            { label: 'งานทั้งหมด', value: '18', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.066 2.573c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.573 1.066c-.426 1.756-2.924 1.756-3.35 0"/></svg>' },
            { label: 'รอดำเนินการ', value: '4', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3"/></svg>' },
            { label: 'กำลังซ่อม', value: '2', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581"/></svg>' },
            { label: 'เสร็จเดือนนี้', value: '12', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>' },
        ]);
        const requests = [
            { id: 'MN-001', room: '103', category: 'ไฟฟ้า', issue: 'แอร์ไม่เย็น', priority: 'High', assignee: 'ช่างสมบัติ', status: 'InProgress', date: '2026-02-10' },
            { id: 'MN-002', room: '205', category: 'ประปา', issue: 'ก๊อกน้ำรั่ว', priority: 'Normal', assignee: '-', status: 'Open', date: '2026-02-11' },
            { id: 'MN-003', room: '301', category: 'เฟอร์นิเจอร์', issue: 'เก้าอี้พัง', priority: 'Low', assignee: 'ช่างประเสริฐ', status: 'Assigned', date: '2026-02-11' },
            { id: 'MN-004', room: '102', category: 'ไฟฟ้า', issue: 'ทีวีเสีย', priority: 'Urgent', assignee: 'ช่างสมบัติ', status: 'Open', date: '2026-02-11' },
        ];
        const table = renderTable(
            [
                { label: 'รหัส', key: 'id' },
                { label: 'ห้อง', render: r => `<span class="font-mono font-bold">${r.room}</span>` },
                { label: 'หมวด', render: r => badge(r.category, 'blue') },
                { label: 'ปัญหา', key: 'issue' },
                { label: 'ความสำคัญ', render: r => { const c = { Urgent:'red', High:'yellow', Normal:'blue', Low:'gray' }; return badge(r.priority, c[r.priority]); }},
                { label: 'ช่าง', key: 'assignee' },
                { label: 'สถานะ', render: r => statusBadge(r.status) },
                { label: 'วันที่', render: r => formatDate(r.date) },
            ],
            requests,
            { actions: (r) => {
                let btns = '';
                if (r.status === 'Open') btns += `<button class="btn btn-sm btn-primary" onclick="Pages.maintenance.assign('${r.id}')">มอบหมาย</button> `;
                if (r.status === 'Assigned') btns += `<button class="btn btn-sm btn-success" onclick="Pages.maintenance.start('${r.id}')">เริ่มงาน</button> `;
                if (r.status === 'InProgress') btns += `<button class="btn btn-sm btn-success" onclick="Pages.maintenance.complete('${r.id}')">เสร็จ</button> `;
                return btns;
            }}
        );
        return stats + renderCard('งานซ่อมบำรุง', table, '<button class="btn btn-sm btn-primary" onclick="Pages.maintenance.report()">+ แจ้งซ่อม</button>');
    },
    report() {
        formModal('แจ้งซ่อม', [
            { name: 'room', label: 'หมายเลขห้อง', required: true },
            { name: 'category', label: 'หมวดหมู่', type: 'select', options: [
                { value: 'electrical', label: 'ไฟฟ้า' }, { value: 'plumbing', label: 'ประปา' },
                { value: 'furniture', label: 'เฟอร์นิเจอร์' }, { value: 'other', label: 'อื่นๆ' },
            ]},
            { name: 'issue', label: 'ปัญหา', type: 'textarea', required: true },
            { name: 'priority', label: 'ความสำคัญ', type: 'select', options: [
                { value: 'Normal', label: 'ปกติ' }, { value: 'High', label: 'สูง' }, { value: 'Urgent', label: 'เร่งด่วน' },
            ]},
        ], async (data) => { alert('แจ้งซ่อมสำเร็จ (Demo)'); });
    },
    assign(id) {
        formModal('มอบหมายงาน ' + id, [
            { name: 'technician', label: 'ช่าง', type: 'select', options: [
                { value: 'sombat', label: 'ช่างสมบัติ' }, { value: 'prasert', label: 'ช่างประเสริฐ' },
            ]},
        ], async (data) => { alert('มอบหมายสำเร็จ (Demo)'); });
    },
    start(id) { confirmDialog('เริ่มงาน', `เริ่มซ่อม ${id}?`, () => alert('เริ่มงานแล้ว (Demo)')); },
    complete(id) { confirmDialog('เสร็จงาน', `ซ่อมเสร็จ ${id}?`, () => alert('ซ่อมเสร็จแล้ว (Demo)')); },
};
window.Pages = Pages;
