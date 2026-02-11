const Pages = window.Pages || {};
Pages.channels = {
    title: 'ช่องทางขาย (OTA)',
    async render() {
        const channels = [
            { name: 'Booking.com', status: 'Active', rooms: 25, lastSync: '2026-02-11 09:30', bookings: 45, revenue: 285000 },
            { name: 'Agoda', status: 'Active', rooms: 20, lastSync: '2026-02-11 09:28', bookings: 32, revenue: 198000 },
            { name: 'Expedia', status: 'Active', rooms: 15, lastSync: '2026-02-11 09:25', bookings: 12, revenue: 87000 },
            { name: 'Direct Website', status: 'Active', rooms: 30, lastSync: '-', bookings: 68, revenue: 425000 },
            { name: 'TripAdvisor', status: 'Inactive', rooms: 0, lastSync: '2026-01-15', bookings: 5, revenue: 35000 },
        ];
        let cardsHtml = '<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 mb-6">';
        channels.forEach(ch => {
            const isActive = ch.status === 'Active';
            cardsHtml += `<div class="stat-card">
                <div class="flex items-center justify-between mb-3">
                    <h4 class="font-semibold text-gray-800">${ch.name}</h4>
                    ${statusBadge(ch.status)}
                </div>
                <div class="grid grid-cols-2 gap-2 text-sm">
                    <div><p class="text-gray-400">ห้องที่เชื่อม</p><p class="font-medium">${ch.rooms}</p></div>
                    <div><p class="text-gray-400">การจอง</p><p class="font-medium">${ch.bookings}</p></div>
                    <div><p class="text-gray-400">รายได้</p><p class="font-medium text-green-600">${formatMoney(ch.revenue)}</p></div>
                    <div><p class="text-gray-400">Sync ล่าสุด</p><p class="font-medium text-xs">${ch.lastSync}</p></div>
                </div>
                <div class="mt-3 flex gap-2">
                    ${isActive ? `<button class="btn btn-sm btn-secondary" onclick="Pages.channels.sync('${ch.name}')">Sync Now</button>` : ''}
                    <button class="btn btn-sm btn-secondary" onclick="Pages.channels.settings('${ch.name}')">ตั้งค่า</button>
                </div>
            </div>`;
        });
        cardsHtml += '</div>';
        return cardsHtml +
            renderCard('ประวัติการ Sync', '<p class="text-gray-400 text-center py-8">ข้อมูล Sync จะแสดงเมื่อเชื่อมต่อ API แล้ว</p>',
                '<button class="btn btn-sm btn-primary" onclick="Pages.channels.connect()">+ เชื่อมต่อช่องทาง</button>');
    },
    connect() { showModal('เชื่อมต่อช่องทางขาย', '<p class="text-gray-500">เลือก OTA ที่ต้องการเชื่อมต่อ</p>'); },
    sync(name) { confirmDialog('Sync ข้อมูล', `Sync กับ ${name}?`, () => alert('Sync สำเร็จ (Demo)')); },
    settings(name) { showModal('ตั้งค่า ' + name, '<p class="text-gray-500">ตั้งค่าการเชื่อมต่อ, Room Mapping, Rate Push</p>'); },
};
window.Pages = Pages;
