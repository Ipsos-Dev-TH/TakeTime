const Pages = window.Pages || {};
Pages.users = {
    title: 'ผู้ใช้ & สิทธิ์',
    async render() {
        const users = [
            { id: 1, name: 'Admin', email: 'admin@taketime.co.th', role: 'SuperAdmin', status: 'Active', lastLogin: '2026-02-11 09:30' },
            { id: 2, name: 'คุณสมชาย ใจดี', email: 'somchai@taketime.co.th', role: 'Manager', status: 'Active', lastLogin: '2026-02-11 08:00' },
            { id: 3, name: 'คุณพิมพ์ สวยงาม', email: 'pim@taketime.co.th', role: 'FrontDesk', status: 'Active', lastLogin: '2026-02-11 07:45' },
            { id: 4, name: 'คุณสมหญิง ดีมาก', email: 'somying@taketime.co.th', role: 'Housekeeper', status: 'Active', lastLogin: '2026-02-10 08:00' },
            { id: 5, name: 'ช่างสมบัติ แก้ไข', email: 'sombat@taketime.co.th', role: 'Maintenance', status: 'Inactive', lastLogin: '2026-02-05 09:00' },
        ];

        const table = renderTable(
            [
                { label: 'ชื่อ', render: r => `<div><p class="font-medium">${r.name}</p><p class="text-xs text-gray-400">${r.email}</p></div>` },
                { label: 'บทบาท', render: r => {
                    const colors = { SuperAdmin: 'red', Manager: 'purple', FrontDesk: 'blue', Housekeeper: 'green', Maintenance: 'yellow' };
                    return badge(r.role, colors[r.role] || 'gray');
                }},
                { label: 'สถานะ', render: r => statusBadge(r.status) },
                { label: 'เข้าใช้ล่าสุด', key: 'lastLogin' },
            ],
            users,
            { actions: (r) => `
                <button class="btn btn-sm btn-secondary" onclick="Pages.users.edit(${r.id})">แก้ไข</button>
                <button class="btn btn-sm btn-secondary" onclick="Pages.users.resetPw(${r.id})">รีเซ็ตรหัส</button>
            `}
        );

        const roles = `
        <div class="space-y-3">
            <div class="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <div><span class="badge badge-red">SuperAdmin</span></div>
                <p class="text-sm text-gray-600">เข้าถึงทุกเมนู, จัดการ tenant, จัดการผู้ใช้</p>
            </div>
            <div class="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <div><span class="badge badge-purple">Manager</span></div>
                <p class="text-sm text-gray-600">จัดการการจอง, ดูรายงาน, จัดการพนักงาน</p>
            </div>
            <div class="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <div><span class="badge badge-blue">FrontDesk</span></div>
                <p class="text-sm text-gray-600">เช็คอิน/เช็คเอาท์, จัดการการจอง, รับชำระ</p>
            </div>
            <div class="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <div><span class="badge badge-green">Housekeeper</span></div>
                <p class="text-sm text-gray-600">ดูงานแม่บ้าน, อัพเดทสถานะห้อง</p>
            </div>
            <div class="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                <div><span class="badge badge-yellow">Maintenance</span></div>
                <p class="text-sm text-gray-600">ดูงานซ่อม, อัพเดทสถานะงาน</p>
            </div>
        </div>`;

        return renderCard('ผู้ใช้ระบบ', table, '<button class="btn btn-sm btn-primary" onclick="Pages.users.add()">+ เพิ่มผู้ใช้</button>') +
            renderCard('บทบาท & สิทธิ์', roles);
    },
    add() {
        formModal('เพิ่มผู้ใช้', [
            { name: 'name', label: 'ชื่อ', required: true },
            { name: 'email', label: 'อีเมล', type: 'email', required: true },
            { name: 'role', label: 'บทบาท', type: 'select', options: [
                { value: 'Manager', label: 'Manager' }, { value: 'FrontDesk', label: 'Front Desk' },
                { value: 'Housekeeper', label: 'Housekeeper' }, { value: 'Maintenance', label: 'Maintenance' },
            ]},
            { name: 'password', label: 'รหัสผ่าน', type: 'password', required: true },
        ], async (data) => { alert('เพิ่มผู้ใช้สำเร็จ (Demo)'); });
    },
    edit(id) { showModal('แก้ไขผู้ใช้', '<p class="text-gray-500">ฟอร์มแก้ไขข้อมูลผู้ใช้</p>'); },
    resetPw(id) { confirmDialog('รีเซ็ตรหัสผ่าน', 'ยืนยันรีเซ็ตรหัสผ่าน?', () => alert('รีเซ็ตรหัสผ่านสำเร็จ (Demo)')); },
};
window.Pages = Pages;
