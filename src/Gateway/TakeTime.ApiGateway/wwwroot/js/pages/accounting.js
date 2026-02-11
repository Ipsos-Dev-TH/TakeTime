const Pages = window.Pages || {};
Pages.accounting = {
    title: 'บัญชี & รายงาน',
    async render() {
        const stats = renderStats([
            { label: 'รายได้เดือนนี้', value: '฿687,500', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6"/></svg>' },
            { label: 'ค่าใช้จ่าย', value: '฿312,400', color: 'red', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 17h8m0 0V9m0 8l-8-8-4 4-6-6"/></svg>' },
            { label: 'กำไรสุทธิ', value: '฿375,100', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z"/></svg>' },
            { label: 'อัตราเข้าพัก', value: '72%', color: 'purple', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 3.055A9.001 9.001 0 1020.945 13H11V3.055z"/></svg>' },
        ]);

        const reports = `
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div class="p-4 border border-gray-200 rounded-lg hover:bg-gray-50 cursor-pointer" onclick="Pages.accounting.openReport('revenue')">
                <div class="flex items-center gap-3 mb-2">
                    <div class="w-10 h-10 bg-green-50 rounded-lg flex items-center justify-center"><svg class="w-5 h-5 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"/></svg></div>
                    <div><h4 class="font-semibold">รายงานรายได้</h4><p class="text-sm text-gray-500">รายได้รายวัน/เดือน/ปี แยกตามประเภท</p></div>
                </div>
            </div>
            <div class="p-4 border border-gray-200 rounded-lg hover:bg-gray-50 cursor-pointer" onclick="Pages.accounting.openReport('profitloss')">
                <div class="flex items-center gap-3 mb-2">
                    <div class="w-10 h-10 bg-blue-50 rounded-lg flex items-center justify-center"><svg class="w-5 h-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 8v8m-4-5v5m-4-2v2m-2 4h12a2 2 0 002-2V6a2 2 0 00-2-2H6a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg></div>
                    <div><h4 class="font-semibold">กำไร-ขาดทุน</h4><p class="text-sm text-gray-500">งบกำไรขาดทุนรายเดือน</p></div>
                </div>
            </div>
            <div class="p-4 border border-gray-200 rounded-lg hover:bg-gray-50 cursor-pointer" onclick="Pages.accounting.openReport('occupancy')">
                <div class="flex items-center gap-3 mb-2">
                    <div class="w-10 h-10 bg-purple-50 rounded-lg flex items-center justify-center"><svg class="w-5 h-5 text-purple-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 3.055A9.001 9.001 0 1020.945 13H11V3.055z"/></svg></div>
                    <div><h4 class="font-semibold">อัตราการเข้าพัก</h4><p class="text-sm text-gray-500">Occupancy rate, ADR, RevPAR</p></div>
                </div>
            </div>
            <div class="p-4 border border-gray-200 rounded-lg hover:bg-gray-50 cursor-pointer" onclick="Pages.accounting.openReport('tax')">
                <div class="flex items-center gap-3 mb-2">
                    <div class="w-10 h-10 bg-yellow-50 rounded-lg flex items-center justify-center"><svg class="w-5 h-5 text-yellow-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 14l6-6m-5.5.5h.01m4.99 5h.01M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16l3.5-2 3.5 2 3.5-2 3.5 2z"/></svg></div>
                    <div><h4 class="font-semibold">รายงานภาษี</h4><p class="text-sm text-gray-500">VAT 7%, ภาษีหัก ณ ที่จ่าย</p></div>
                </div>
            </div>
            <div class="p-4 border border-gray-200 rounded-lg hover:bg-gray-50 cursor-pointer" onclick="Pages.accounting.openReport('revenueByType')">
                <div class="flex items-center gap-3 mb-2">
                    <div class="w-10 h-10 bg-indigo-50 rounded-lg flex items-center justify-center"><svg class="w-5 h-5 text-indigo-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5"/></svg></div>
                    <div><h4 class="font-semibold">รายได้ตามประเภทห้อง</h4><p class="text-sm text-gray-500">Standard, Deluxe, Suite, Family</p></div>
                </div>
            </div>
            <div class="p-4 border border-gray-200 rounded-lg hover:bg-gray-50 cursor-pointer" onclick="Pages.accounting.openReport('monthly')">
                <div class="flex items-center gap-3 mb-2">
                    <div class="w-10 h-10 bg-red-50 rounded-lg flex items-center justify-center"><svg class="w-5 h-5 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg></div>
                    <div><h4 class="font-semibold">สรุปรายเดือน</h4><p class="text-sm text-gray-500">สรุปภาพรวมรายเดือน</p></div>
                </div>
            </div>
        </div>`;

        return stats + renderCard('รายงาน', reports, `
            <select class="form-input w-auto text-sm">
                <option>กุมภาพันธ์ 2569</option>
                <option>มกราคม 2569</option>
                <option>ธันวาคม 2568</option>
            </select>
            <button class="btn btn-sm btn-secondary" onclick="Pages.accounting.exportAll()">Export ทั้งหมด</button>
        `);
    },
    openReport(type) {
        const names = { revenue: 'รายงานรายได้', profitloss: 'กำไร-ขาดทุน', occupancy: 'อัตราเข้าพัก', tax: 'ภาษี', revenueByType: 'รายได้ตามประเภทห้อง', monthly: 'สรุปรายเดือน' };
        showModal(names[type] || 'รายงาน', '<p class="text-gray-500 text-center py-8">รอเชื่อมต่อ API เพื่อดึงข้อมูลรายงานจริง</p>');
    },
    exportAll() { alert('Export รายงานสำเร็จ (Demo)'); },
};
window.Pages = Pages;
