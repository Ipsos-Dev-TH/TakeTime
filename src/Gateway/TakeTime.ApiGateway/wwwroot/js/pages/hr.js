const Pages = window.Pages || {};
Pages.hr = {
    title: 'บุคลากร',
    async render() {
        const stats = renderStats([
            { label: 'พนักงานทั้งหมด', value: '32', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"/></svg>' },
            { label: 'เข้างานวันนี้', value: '28', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>' },
            { label: 'ลาวันนี้', value: '3', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>' },
            { label: 'OT เดือนนี้', value: '156 ชม.', color: 'purple', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>' },
        ]);
        const employees = [
            { id: 1, name: 'คุณสมชาย ใจดี', position: 'ผู้จัดการ', dept: 'บริหาร', status: 'Active', phone: '081-234-5678', joinDate: '2020-01-15' },
            { id: 2, name: 'คุณสมหญิง ดีมาก', position: 'แม่บ้าน', dept: 'แม่บ้าน', status: 'Active', phone: '082-345-6789', joinDate: '2021-03-01' },
            { id: 3, name: 'คุณนิดา รักงาน', position: 'แม่บ้าน', dept: 'แม่บ้าน', status: 'Active', phone: '083-456-7890', joinDate: '2022-06-15' },
            { id: 4, name: 'ช่างสมบัติ แก้ไข', position: 'ช่างซ่อมบำรุง', dept: 'ซ่อมบำรุง', status: 'Active', phone: '084-567-8901', joinDate: '2021-09-01' },
            { id: 5, name: 'คุณพิมพ์ สวยงาม', position: 'พนักงานต้อนรับ', dept: 'Front Office', status: 'Active', phone: '085-678-9012', joinDate: '2023-01-10' },
        ];
        const table = renderTable(
            [
                { label: 'ชื่อ', render: r => `<div><p class="font-medium">${r.name}</p><p class="text-xs text-gray-400">${r.phone}</p></div>` },
                { label: 'ตำแหน่ง', key: 'position' },
                { label: 'แผนก', render: r => badge(r.dept, 'blue') },
                { label: 'วันเริ่มงาน', render: r => formatDate(r.joinDate) },
                { label: 'สถานะ', render: r => statusBadge(r.status) },
            ],
            employees,
            { actions: (r) => `<button class="btn btn-sm btn-secondary" onclick="Pages.hr.view(${r.id})">ดู</button> <button class="btn btn-sm btn-secondary" onclick="Pages.hr.leave(${r.id})">ลา</button>` }
        );
        return stats + renderCard('รายชื่อพนักงาน', table, `
            <button class="btn btn-sm btn-secondary" onclick="Pages.hr.payroll()">เงินเดือน</button>
            <button class="btn btn-sm btn-secondary" onclick="Pages.hr.leaves()">รายการลา</button>
            <button class="btn btn-sm btn-primary" onclick="Pages.hr.add()">+ เพิ่มพนักงาน</button>
        `);
    },
    add() {
        formModal('เพิ่มพนักงาน', [
            { name: 'name', label: 'ชื่อ-นามสกุล', required: true },
            { name: 'position', label: 'ตำแหน่ง', required: true },
            { name: 'dept', label: 'แผนก', type: 'select', options: [
                { value: 'frontoffice', label: 'Front Office' }, { value: 'housekeeping', label: 'แม่บ้าน' },
                { value: 'maintenance', label: 'ซ่อมบำรุง' }, { value: 'kitchen', label: 'ครัว' },
            ]},
            { name: 'phone', label: 'เบอร์โทร', required: true },
            { name: 'salary', label: 'เงินเดือน (฿)', type: 'number', required: true },
        ], async (data) => { alert('เพิ่มพนักงานสำเร็จ (Demo)'); });
    },
    view(id) { showModal('ข้อมูลพนักงาน', '<p class="text-gray-500">ประวัติพนักงาน, สลิปเงินเดือน, ประวัติลา</p>'); },
    leave(id) { showModal('ขอลา', '<p class="text-gray-500">ฟอร์มขอลาสำหรับพนักงาน</p>'); },
    payroll() { showModal('เงินเดือน', '<p class="text-gray-500">รายงานเงินเดือนประจำเดือน</p>'); },
    leaves() { showModal('รายการลา', '<p class="text-gray-500">รายการขอลาทั้งหมด</p>'); },
};
window.Pages = Pages;
