const Pages = window.Pages || {};
Pages.tenants = {
    title: 'Tenant & ตั้งค่า',
    async render() {
        const tenantInfo = `
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
            <div class="bg-white rounded-xl border border-gray-200 p-5">
                <h3 class="font-semibold text-gray-800 mb-4">ข้อมูล Tenant</h3>
                <div class="space-y-3">
                    <div class="flex justify-between"><span class="text-gray-500">รหัส</span><span class="font-medium">taketime-bangphra</span></div>
                    <div class="flex justify-between"><span class="text-gray-500">ชื่อ</span><span class="font-medium">TakeTime BangPhra</span></div>
                    <div class="flex justify-between"><span class="text-gray-500">อีเมล</span><span class="font-medium">info@taketime-bangphra.com</span></div>
                    <div class="flex justify-between"><span class="text-gray-500">โทร</span><span class="font-medium">038-123-456</span></div>
                    <div class="flex justify-between"><span class="text-gray-500">สถานะ</span>${statusBadge('Active')}</div>
                    <div class="flex justify-between"><span class="text-gray-500">แพ็กเกจ</span><span class="badge badge-purple">Professional</span></div>
                </div>
                <button class="btn btn-secondary btn-sm mt-4" onclick="Pages.tenants.editInfo()">แก้ไขข้อมูล</button>
            </div>
            <div class="bg-white rounded-xl border border-gray-200 p-5">
                <h3 class="font-semibold text-gray-800 mb-4">การใช้งาน</h3>
                <div class="space-y-3">
                    <div class="flex justify-between"><span class="text-gray-500">ห้องพักสูงสุด</span><span class="font-medium">50 ห้อง</span></div>
                    <div class="flex justify-between"><span class="text-gray-500">ใช้งานอยู่</span><span class="font-medium">30 ห้อง</span></div>
                    <div class="flex justify-between"><span class="text-gray-500">ผู้ใช้สูงสุด</span><span class="font-medium">20 คน</span></div>
                    <div class="flex justify-between"><span class="text-gray-500">ใช้งานอยู่</span><span class="font-medium">5 คน</span></div>
                    <div class="flex justify-between"><span class="text-gray-500">เริ่มใช้งาน</span><span class="font-medium">15 ม.ค. 2569</span></div>
                    <div class="flex justify-between"><span class="text-gray-500">หมดอายุ</span><span class="font-medium">14 ม.ค. 2570</span></div>
                </div>
            </div>
        </div>`;

        const settings = `
        <div class="space-y-4">
            <div class="p-4 border border-gray-200 rounded-lg">
                <div class="flex items-center justify-between">
                    <div>
                        <h4 class="font-semibold">ภาษีมูลค่าเพิ่ม (VAT)</h4>
                        <p class="text-sm text-gray-500">อัตรา VAT สำหรับใบกำกับภาษี</p>
                    </div>
                    <div class="flex items-center gap-2">
                        <span class="text-lg font-bold text-indigo-600">7%</span>
                        <button class="btn btn-sm btn-secondary" onclick="Pages.tenants.editSetting('vat')">แก้ไข</button>
                    </div>
                </div>
            </div>
            <div class="p-4 border border-gray-200 rounded-lg">
                <div class="flex items-center justify-between">
                    <div>
                        <h4 class="font-semibold">สกุลเงิน</h4>
                        <p class="text-sm text-gray-500">สกุลเงินหลักของระบบ</p>
                    </div>
                    <div class="flex items-center gap-2">
                        <span class="text-lg font-bold text-indigo-600">THB (฿)</span>
                        <button class="btn btn-sm btn-secondary" onclick="Pages.tenants.editSetting('currency')">แก้ไข</button>
                    </div>
                </div>
            </div>
            <div class="p-4 border border-gray-200 rounded-lg">
                <div class="flex items-center justify-between">
                    <div>
                        <h4 class="font-semibold">เวลาเช็คอิน / เช็คเอาท์</h4>
                        <p class="text-sm text-gray-500">เวลามาตรฐานสำหรับการเข้า-ออก</p>
                    </div>
                    <div class="flex items-center gap-2">
                        <span class="font-medium">14:00 / 12:00</span>
                        <button class="btn btn-sm btn-secondary" onclick="Pages.tenants.editSetting('checkinout')">แก้ไข</button>
                    </div>
                </div>
            </div>
            <div class="p-4 border border-gray-200 rounded-lg">
                <div class="flex items-center justify-between">
                    <div>
                        <h4 class="font-semibold">ช่องทางชำระเงิน</h4>
                        <p class="text-sm text-gray-500">วิธีการชำระเงินที่รองรับ</p>
                    </div>
                    <div class="flex items-center gap-2">
                        <span class="badge badge-green">เงินสด</span>
                        <span class="badge badge-green">PromptPay</span>
                        <span class="badge badge-green">บัตรเครดิต</span>
                        <span class="badge badge-gray">โอนเงิน</span>
                        <button class="btn btn-sm btn-secondary" onclick="Pages.tenants.editSetting('payment')">แก้ไข</button>
                    </div>
                </div>
            </div>
            <div class="p-4 border border-gray-200 rounded-lg">
                <div class="flex items-center justify-between">
                    <div>
                        <h4 class="font-semibold">โปรแกรมสะสมแต้ม</h4>
                        <p class="text-sm text-gray-500">Loyalty program สำหรับลูกค้า</p>
                    </div>
                    <div class="flex items-center gap-2">
                        <label class="toggle"><input type="checkbox" checked><span class="toggle-slider"></span></label>
                        <button class="btn btn-sm btn-secondary" onclick="Pages.tenants.editSetting('loyalty')">ตั้งค่า</button>
                    </div>
                </div>
            </div>
        </div>`;

        return tenantInfo + renderCard('ตั้งค่าธุรกิจ', settings);
    },
    editInfo() { showModal('แก้ไขข้อมูล Tenant', '<p class="text-gray-500">ฟอร์มแก้ไขข้อมูลที่พัก</p>'); },
    editSetting(type) {
        const names = { vat: 'VAT', currency: 'สกุลเงิน', checkinout: 'เวลาเช็คอิน/เช็คเอาท์', payment: 'ช่องทางชำระเงิน', loyalty: 'โปรแกรมสะสมแต้ม' };
        showModal('ตั้งค่า ' + (names[type] || type), '<p class="text-gray-500">ฟอร์มตั้งค่า</p>');
    },
};
window.Pages = Pages;
