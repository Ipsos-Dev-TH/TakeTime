const Pages = window.Pages || {};
Pages.notifications = {
    title: 'การแจ้งเตือน',
    async render() {
        const channels = `
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
            <div class="stat-card">
                <div class="flex items-center gap-3 mb-3">
                    <div class="w-10 h-10 bg-green-50 rounded-lg flex items-center justify-center">
                        <svg class="w-5 h-5 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 12h.01M12 12h.01M16 12h.01M21 12c0 4.418-4.03 8-9 8a9.863 9.863 0 01-4.255-.949L3 20l1.395-3.72C3.512 15.042 3 13.574 3 12c0-4.418 4.03-8 9-8s9 3.582 9 8z"/></svg>
                    </div>
                    <div>
                        <h4 class="font-semibold text-sm">LINE</h4>
                        <p class="text-xs text-green-600">เชื่อมต่อแล้ว</p>
                    </div>
                </div>
                <label class="toggle"><input type="checkbox" checked><span class="toggle-slider"></span></label>
                <span class="text-xs text-gray-500 ml-2">เปิดใช้งาน</span>
            </div>
            <div class="stat-card">
                <div class="flex items-center gap-3 mb-3">
                    <div class="w-10 h-10 bg-blue-50 rounded-lg flex items-center justify-center">
                        <svg class="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z"/></svg>
                    </div>
                    <div>
                        <h4 class="font-semibold text-sm">Email</h4>
                        <p class="text-xs text-green-600">เชื่อมต่อแล้ว</p>
                    </div>
                </div>
                <label class="toggle"><input type="checkbox" checked><span class="toggle-slider"></span></label>
                <span class="text-xs text-gray-500 ml-2">เปิดใช้งาน</span>
            </div>
            <div class="stat-card">
                <div class="flex items-center gap-3 mb-3">
                    <div class="w-10 h-10 bg-blue-50 rounded-lg flex items-center justify-center">
                        <svg class="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8"/></svg>
                    </div>
                    <div>
                        <h4 class="font-semibold text-sm">Telegram</h4>
                        <p class="text-xs text-gray-400">ยังไม่ได้เชื่อมต่อ</p>
                    </div>
                </div>
                <label class="toggle"><input type="checkbox"><span class="toggle-slider"></span></label>
                <span class="text-xs text-gray-500 ml-2">ปิดอยู่</span>
            </div>
            <div class="stat-card">
                <div class="flex items-center gap-3 mb-3">
                    <div class="w-10 h-10 bg-purple-50 rounded-lg flex items-center justify-center">
                        <svg class="w-5 h-5 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 18h.01M8 21h8a2 2 0 002-2V5a2 2 0 00-2-2H8a2 2 0 00-2 2v14a2 2 0 002 2z"/></svg>
                    </div>
                    <div>
                        <h4 class="font-semibold text-sm">SMS</h4>
                        <p class="text-xs text-gray-400">ยังไม่ได้เชื่อมต่อ</p>
                    </div>
                </div>
                <label class="toggle"><input type="checkbox"><span class="toggle-slider"></span></label>
                <span class="text-xs text-gray-500 ml-2">ปิดอยู่</span>
            </div>
        </div>`;

        const templates = [
            { name: 'ยืนยันการจอง', channel: 'LINE, Email', trigger: 'เมื่อจองสำเร็จ', status: 'Active' },
            { name: 'เตือนก่อนเช็คอิน', channel: 'LINE, Email', trigger: '1 วันก่อนเช็คอิน', status: 'Active' },
            { name: 'ขอบคุณหลังเช็คเอาท์', channel: 'Email', trigger: 'หลังเช็คเอาท์', status: 'Active' },
            { name: 'แจ้งชำระเงิน', channel: 'LINE', trigger: 'เมื่อชำระเงิน', status: 'Active' },
            { name: 'โปรโมชัน', channel: 'LINE, Email, SMS', trigger: 'ตั้งเวลา', status: 'Inactive' },
        ];

        const table = renderTable(
            [
                { label: 'เทมเพลต', render: r => `<p class="font-medium">${r.name}</p>` },
                { label: 'ช่องทาง', key: 'channel' },
                { label: 'Trigger', key: 'trigger' },
                { label: 'สถานะ', render: r => statusBadge(r.status) },
            ],
            templates,
            { actions: (r) => `<button class="btn btn-sm btn-secondary" onclick="Pages.notifications.editTemplate('${r.name}')">แก้ไข</button>` }
        );

        return channels + renderCard('เทมเพลตการแจ้งเตือน', table, '<button class="btn btn-sm btn-primary" onclick="Pages.notifications.addTemplate()">+ เพิ่มเทมเพลต</button>') +
            renderCard('ส่งแจ้งเตือนด่วน', `
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                    <div>
                        <label class="form-label">ช่องทาง</label>
                        <select class="form-input"><option>LINE</option><option>Email</option><option>SMS</option><option>ทุกช่องทาง</option></select>
                    </div>
                    <div>
                        <label class="form-label">ผู้รับ</label>
                        <select class="form-input"><option>แขกทั้งหมด</option><option>แขกที่เช็คอินอยู่</option><option>สมาชิก</option><option>พนักงาน</option></select>
                    </div>
                    <div>
                        <label class="form-label">ข้อความ</label>
                        <input type="text" class="form-input" placeholder="พิมพ์ข้อความ...">
                    </div>
                </div>
                <button class="btn btn-primary mt-3" onclick="Pages.notifications.sendNow()">ส่งเดี๋ยวนี้</button>
            `);
    },
    addTemplate() { showModal('เพิ่มเทมเพลต', '<p class="text-gray-500">ฟอร์มสร้างเทมเพลตการแจ้งเตือน</p>'); },
    editTemplate(name) { showModal('แก้ไขเทมเพลต: ' + name, '<p class="text-gray-500">ฟอร์มแก้ไขเทมเพลต</p>'); },
    sendNow() { confirmDialog('ส่งแจ้งเตือน', 'ยืนยันส่งแจ้งเตือน?', () => alert('ส่งแจ้งเตือนสำเร็จ (Demo)')); },
};
window.Pages = Pages;
