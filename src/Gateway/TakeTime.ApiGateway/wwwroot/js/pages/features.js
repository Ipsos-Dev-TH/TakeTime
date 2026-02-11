const Pages = window.Pages || {};
Pages.features = {
    title: 'เปิด/ปิด ฟีเจอร์',
    async render() {
        const modules = [
            { key: 'Reservation', name: 'การจองห้องพัก', icon: '📅', enabled: true,
              subs: [
                { key: 'OnlineBooking', name: 'จองออนไลน์', enabled: true },
                { key: 'WalkIn', name: 'Walk-in', enabled: true },
                { key: 'GroupBooking', name: 'จองหมู่คณะ', enabled: false },
                { key: 'DynamicPricing', name: 'ราคาแบบ Dynamic', enabled: true },
              ]},
            { key: 'Payment', name: 'การเงิน & ชำระเงิน', icon: '💳', enabled: true,
              subs: [
                { key: 'PromptPay', name: 'PromptPay', enabled: true },
                { key: 'CreditCard', name: 'บัตรเครดิต', enabled: true },
                { key: 'BankTransfer', name: 'โอนเงิน', enabled: true },
                { key: 'TaxInvoice', name: 'ใบกำกับภาษี', enabled: true },
              ]},
            { key: 'Inventory', name: 'สินค้า & POS', icon: '📦', enabled: true,
              subs: [
                { key: 'POS', name: 'ขายหน้าร้าน', enabled: true },
                { key: 'RoomCharge', name: 'ลงบัญชีห้อง', enabled: true },
                { key: 'StockManagement', name: 'จัดการสต็อก', enabled: true },
              ]},
            { key: 'GuestExperience', name: 'บริการแขก', icon: '🛎️', enabled: true,
              subs: [
                { key: 'Housekeeping', name: 'แม่บ้าน', enabled: true },
                { key: 'RoomService', name: 'รูมเซอร์วิส', enabled: true },
                { key: 'MaintenanceTracking', name: 'ซ่อมบำรุง', enabled: true },
              ]},
            { key: 'CRM', name: 'ลูกค้า & การตลาด', icon: '👥', enabled: true,
              subs: [
                { key: 'LoyaltyProgram', name: 'สะสมแต้ม', enabled: true },
                { key: 'Reviews', name: 'รีวิว', enabled: true },
                { key: 'Marketing', name: 'การตลาด', enabled: false },
              ]},
            { key: 'HumanResources', name: 'บุคลากร', icon: '👤', enabled: true,
              subs: [
                { key: 'LeaveManagement', name: 'จัดการวันลา', enabled: true },
                { key: 'Payroll', name: 'เงินเดือน', enabled: true },
                { key: 'OTManagement', name: 'OT', enabled: true },
              ]},
            { key: 'ChannelManager', name: 'ช่องทางขาย (OTA)', icon: '🌐', enabled: true,
              subs: [
                { key: 'BookingCom', name: 'Booking.com', enabled: true },
                { key: 'Agoda', name: 'Agoda', enabled: true },
                { key: 'Expedia', name: 'Expedia', enabled: true },
              ]},
            { key: 'Accounting', name: 'บัญชี & รายงาน', icon: '📊', enabled: true, subs: [] },
            { key: 'Notification', name: 'การแจ้งเตือน', icon: '🔔', enabled: true,
              subs: [
                { key: 'LINE', name: 'LINE', enabled: true },
                { key: 'Email', name: 'Email', enabled: true },
                { key: 'Telegram', name: 'Telegram', enabled: false },
                { key: 'SMS', name: 'SMS', enabled: false },
              ]},
            { key: 'Affiliate', name: 'พันธมิตร', icon: '🔗', enabled: false, subs: [] },
        ];

        const header = `
        <div class="flex items-center justify-between mb-6">
            <div>
                <p class="text-gray-500">เลือกเปิด/ปิด ฟีเจอร์ที่ต้องการใช้งาน ฟีเจอร์ที่ปิดจะไม่แสดงในเมนูและไม่สามารถเรียกใช้ API ได้</p>
            </div>
            <div class="flex gap-2">
                <button class="btn btn-sm btn-secondary" onclick="Pages.features.applyTemplate('starter')">Starter</button>
                <button class="btn btn-sm btn-secondary" onclick="Pages.features.applyTemplate('thai')">Thai Default</button>
                <button class="btn btn-sm btn-primary" onclick="Pages.features.applyTemplate('full')">Full Access</button>
            </div>
        </div>`;

        let cardsHtml = '<div class="space-y-4">';
        modules.forEach(mod => {
            cardsHtml += `
            <div class="bg-white rounded-xl border border-gray-200 overflow-hidden">
                <div class="flex items-center justify-between px-5 py-4 ${mod.enabled ? '' : 'opacity-60'}">
                    <div class="flex items-center gap-3">
                        <span class="text-2xl">${mod.icon}</span>
                        <div>
                            <h4 class="font-semibold text-gray-800">${mod.name}</h4>
                            <p class="text-xs text-gray-400">${mod.key}</p>
                        </div>
                    </div>
                    <label class="toggle">
                        <input type="checkbox" ${mod.enabled ? 'checked' : ''} onchange="Pages.features.toggleModule('${mod.key}', this.checked)">
                        <span class="toggle-slider"></span>
                    </label>
                </div>`;

            if (mod.subs.length > 0) {
                cardsHtml += `<div class="border-t border-gray-100 px-5 py-3 bg-gray-50 ${mod.enabled ? '' : 'opacity-40 pointer-events-none'}">
                    <div class="grid grid-cols-2 md:grid-cols-4 gap-3">`;
                mod.subs.forEach(sub => {
                    cardsHtml += `
                    <div class="flex items-center justify-between p-2 bg-white rounded-lg border border-gray-100">
                        <span class="text-sm">${sub.name}</span>
                        <label class="toggle" style="transform:scale(0.8)">
                            <input type="checkbox" ${sub.enabled ? 'checked' : ''} onchange="Pages.features.toggleSub('${mod.key}', '${sub.key}', this.checked)">
                            <span class="toggle-slider"></span>
                        </label>
                    </div>`;
                });
                cardsHtml += '</div></div>';
            }
            cardsHtml += '</div>';
        });
        cardsHtml += '</div>';

        return header + cardsHtml;
    },
    toggleModule(key, enabled) {
        console.log(`Module ${key}: ${enabled}`);
        // TODO: API.put('/api/v1/admin/features/modules', { module: key, isEnabled: enabled });
    },
    toggleSub(module, sub, enabled) {
        console.log(`Sub ${module}.${sub}: ${enabled}`);
        // TODO: API.put('/api/v1/admin/features/sub-features', { module, subFeature: sub, isEnabled: enabled });
    },
    applyTemplate(template) {
        const names = { starter: 'Starter', thai: 'Thai Default', full: 'Full Access' };
        confirmDialog('ใช้เทมเพลต', `ต้องการใช้เทมเพลต "${names[template]}"? การตั้งค่าฟีเจอร์ปัจจุบันจะถูกแทนที่`, () => {
            alert(`ใช้เทมเพลต ${names[template]} สำเร็จ (Demo)`);
            // TODO: API.post('/api/v1/admin/features/initialize', { template });
        });
    },
};
window.Pages = Pages;
