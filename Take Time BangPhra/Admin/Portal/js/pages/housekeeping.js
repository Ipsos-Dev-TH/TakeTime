const Pages = window.Pages || {};
Pages.housekeeping = {
    title: 'แม่บ้าน',
    async render() {
        const stats = renderStats([
            { label: 'สะอาดแล้ว', value: '12', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>' },
            { label: 'กำลังทำ', value: '5', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"/></svg>' },
            { label: 'รอทำความสะอาด', value: '3', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>' },
            { label: 'รอตรวจ', value: '2', color: 'purple', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-6 9l2 2 4-4"/></svg>' },
        ]);

        const tasks = [
            { id: 'HK-001', room: '102', type: 'Check-Out Clean', priority: 'High', assignee: 'คุณสมหญิง', status: 'InProgress', time: '09:00' },
            { id: 'HK-002', room: '205', type: 'Check-Out Clean', priority: 'Normal', assignee: 'คุณนิดา', status: 'Assigned', time: '09:30' },
            { id: 'HK-003', room: '301', type: 'Stay-Over', priority: 'Normal', assignee: '-', status: 'Pending', time: '10:00' },
            { id: 'HK-004', room: '401', type: 'Deep Clean', priority: 'Low', assignee: 'คุณสมหญิง', status: 'Completed', time: '08:00' },
            { id: 'HK-005', room: '103', type: 'Check-Out Clean', priority: 'Urgent', assignee: '-', status: 'Pending', time: '10:30' },
        ];

        const table = renderTable(
            [
                { label: 'งาน', key: 'id' },
                { label: 'ห้อง', render: r => `<span class="font-mono font-bold">${r.room}</span>` },
                { label: 'ประเภท', key: 'type' },
                { label: 'ความสำคัญ', render: r => {
                    const colors = { Urgent: 'red', High: 'yellow', Normal: 'blue', Low: 'gray' };
                    return badge(r.priority, colors[r.priority] || 'gray');
                }},
                { label: 'ผู้รับผิดชอบ', key: 'assignee' },
                { label: 'เวลา', key: 'time' },
                { label: 'สถานะ', render: r => statusBadge(r.status) },
            ],
            tasks,
            { actions: (r) => {
                let btns = '';
                if (r.status === 'Pending') btns += `<button class="btn btn-sm btn-primary" onclick="Pages.housekeeping.assign('${r.id}')">มอบหมาย</button> `;
                if (r.status === 'Assigned') btns += `<button class="btn btn-sm btn-success" onclick="Pages.housekeeping.start('${r.id}')">เริ่มงาน</button> `;
                if (r.status === 'InProgress') btns += `<button class="btn btn-sm btn-success" onclick="Pages.housekeeping.complete('${r.id}')">เสร็จ</button> `;
                if (r.status === 'Completed') btns += `<button class="btn btn-sm btn-secondary" onclick="Pages.housekeeping.verify('${r.id}')">ตรวจ</button> `;
                return btns;
            }}
        );

        return stats + renderCard('งานแม่บ้านวันนี้', table, '<button class="btn btn-sm btn-primary" onclick="Pages.housekeeping.create()">+ สร้างงาน</button>');
    },
    create() {
        formModal('สร้างงานแม่บ้าน', [
            { name: 'room', label: 'หมายเลขห้อง', required: true },
            { name: 'type', label: 'ประเภท', type: 'select', options: [
                { value: 'CheckOutClean', label: 'ทำความสะอาด (เช็คเอาท์)' },
                { value: 'StayOver', label: 'ทำความสะอาด (พักต่อ)' },
                { value: 'DeepClean', label: 'ทำความสะอาดพิเศษ' },
            ]},
            { name: 'priority', label: 'ความสำคัญ', type: 'select', options: [
                { value: 'Normal', label: 'ปกติ' }, { value: 'High', label: 'สูง' }, { value: 'Urgent', label: 'เร่งด่วน' },
            ]},
            { name: 'notes', label: 'หมายเหตุ', type: 'textarea' },
        ], async (data) => { alert('สร้างงานสำเร็จ (Demo)'); });
    },
    assign(id) {
        formModal('มอบหมายงาน ' + id, [
            { name: 'assignee', label: 'ผู้รับผิดชอบ', type: 'select', options: [
                { value: 'somying', label: 'คุณสมหญิง' }, { value: 'nida', label: 'คุณนิดา' },
                { value: 'pranee', label: 'คุณปราณี' },
            ]},
        ], async (data) => { alert('มอบหมายงานสำเร็จ (Demo)'); });
    },
    start(id) { confirmDialog('เริ่มงาน', `เริ่มทำงาน ${id}?`, () => alert('เริ่มงานแล้ว (Demo)')); },
    complete(id) { confirmDialog('เสร็จงาน', `ยืนยันว่างาน ${id} เสร็จ?`, () => alert('เสร็จงานแล้ว (Demo)')); },
    verify(id) { confirmDialog('ตรวจงาน', `ยืนยันตรวจงาน ${id}?`, () => alert('ตรวจงานสำเร็จ (Demo)')); },
};
window.Pages = Pages;
