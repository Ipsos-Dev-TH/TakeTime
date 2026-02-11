// Dashboard page
const Pages = window.Pages || {};
Pages.dashboard = {
    title: 'แดชบอร์ด',
    async render() {
        return renderStats([
            { label: 'การจองวันนี้', value: '12', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7V3m8 4V3m-9 8h10M5 21h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z"/></svg>' },
            { label: 'เช็คอินวันนี้', value: '8', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>' },
            { label: 'เช็คเอาท์วันนี้', value: '5', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1"/></svg>' },
            { label: 'รายได้วันนี้', value: '฿45,200', color: 'indigo', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>' },
        ]) +
        renderStats([
            { label: 'ห้องว่าง', value: '15/30', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-4 0h4"/></svg>' },
            { label: 'งานแม่บ้านค้าง', value: '3', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/></svg>' },
            { label: 'ซ่อมบำรุงรอดำเนินการ', value: '2', color: 'red', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.34 16.5c-.77.833.192 2.5 1.732 2.5z"/></svg>' },
            { label: 'พนักงานเข้างาน', value: '24', color: 'purple', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z"/></svg>' },
        ]) +
        renderCard('การจองล่าสุด', renderTable(
            [
                { label: 'รหัสจอง', key: 'id' },
                { label: 'ชื่อแขก', key: 'guestName' },
                { label: 'ห้อง', key: 'room' },
                { label: 'เช็คอิน', key: 'checkIn', render: r => formatDate(r.checkIn) },
                { label: 'เช็คเอาท์', key: 'checkOut', render: r => formatDate(r.checkOut) },
                { label: 'สถานะ', key: 'status', render: r => statusBadge(r.status) },
            ],
            [
                { id: 'RES-001', guestName: 'คุณสมชาย ใจดี', room: 'Deluxe 201', checkIn: '2026-02-11', checkOut: '2026-02-13', status: 'Confirmed' },
                { id: 'RES-002', guestName: 'John Smith', room: 'Suite 301', checkIn: '2026-02-11', checkOut: '2026-02-14', status: 'CheckedIn' },
                { id: 'RES-003', guestName: 'คุณวิภา แสงทอง', room: 'Standard 105', checkIn: '2026-02-12', checkOut: '2026-02-13', status: 'Pending' },
                { id: 'RES-004', guestName: 'Tanaka Yuki', room: 'Deluxe 205', checkIn: '2026-02-10', checkOut: '2026-02-11', status: 'CheckedOut' },
            ]
        ), '<button class="btn btn-sm btn-secondary" onclick="window.adminApp.navigate(\'reservations\')">ดูทั้งหมด</button>') +
        renderCard('สรุปงานแม่บ้านวันนี้', `
            <div class="grid grid-cols-2 md:grid-cols-5 gap-3">
                <div class="text-center p-3 rounded-lg bg-green-50"><p class="text-2xl font-bold text-green-600">12</p><p class="text-xs text-gray-500">สะอาดแล้ว</p></div>
                <div class="text-center p-3 rounded-lg bg-yellow-50"><p class="text-2xl font-bold text-yellow-600">5</p><p class="text-xs text-gray-500">กำลังทำ</p></div>
                <div class="text-center p-3 rounded-lg bg-red-50"><p class="text-2xl font-bold text-red-600">3</p><p class="text-xs text-gray-500">รอทำความสะอาด</p></div>
                <div class="text-center p-3 rounded-lg bg-blue-50"><p class="text-2xl font-bold text-blue-600">2</p><p class="text-xs text-gray-500">รอตรวจ</p></div>
                <div class="text-center p-3 rounded-lg bg-gray-50"><p class="text-2xl font-bold text-gray-600">8</p><p class="text-xs text-gray-500">ห้องว่าง</p></div>
            </div>
        `);
    }
};
window.Pages = Pages;
