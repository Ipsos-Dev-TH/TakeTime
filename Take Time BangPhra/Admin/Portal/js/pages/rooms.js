const Pages = window.Pages || {};
Pages.rooms = {
    title: 'ห้องพัก & ราคา',
    async render() {
        const stats = renderStats([
            { label: 'ห้องทั้งหมด', value: '30', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5"/></svg>' },
            { label: 'ว่าง', value: '15', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>' },
            { label: 'ไม่ว่าง', value: '12', color: 'red', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>' },
            { label: 'ซ่อมบำรุง', value: '3', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01"/></svg>' },
        ]);

        const rooms = [
            { number: '101', type: 'Standard', floor: '1', price: 1500, status: 'Available', beds: '1 Double' },
            { number: '102', type: 'Standard', floor: '1', price: 1500, status: 'Occupied', beds: '2 Single', guest: 'คุณสมศรี' },
            { number: '201', type: 'Deluxe', floor: '2', price: 2500, status: 'Available', beds: '1 King' },
            { number: '205', type: 'Deluxe', floor: '2', price: 2500, status: 'Cleaning', beds: '1 King' },
            { number: '301', type: 'Suite', floor: '3', price: 4500, status: 'Occupied', beds: '1 King + Sofa', guest: 'John Smith' },
            { number: '401', type: 'Family', floor: '4', price: 3500, status: 'Available', beds: '2 Double' },
            { number: '103', type: 'Standard', floor: '1', price: 1500, status: 'Maintenance', beds: '1 Double' },
        ];

        const table = renderTable(
            [
                { label: 'หมายเลข', render: r => `<span class="font-mono font-bold">${r.number}</span>` },
                { label: 'ประเภท', key: 'type' },
                { label: 'ชั้น', key: 'floor' },
                { label: 'เตียง', key: 'beds' },
                { label: 'ราคา/คืน', render: r => formatMoney(r.price) },
                { label: 'สถานะ', render: r => statusBadge(r.status) },
                { label: 'แขก', render: r => r.guest || '-' },
            ],
            rooms,
            { actions: (r) => `<button class="btn btn-sm btn-secondary" onclick="Pages.rooms.edit('${r.number}')">แก้ไข</button>` }
        );

        return stats +
            renderCard('รายการห้องพัก', table, '<button class="btn btn-sm btn-primary" onclick="Pages.rooms.addRoom()">+ เพิ่มห้อง</button>') +
            renderCard('แผนราคา', `
                <div class="space-y-3">
                    <div class="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                        <div><p class="font-medium">Standard Room</p><p class="text-sm text-gray-500">ห้องมาตรฐาน</p></div>
                        <div class="text-right"><p class="font-bold text-indigo-600">฿1,500</p><p class="text-xs text-gray-400">ราคาปกติ/คืน</p></div>
                    </div>
                    <div class="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                        <div><p class="font-medium">Deluxe Room</p><p class="text-sm text-gray-500">ห้องดีลักซ์</p></div>
                        <div class="text-right"><p class="font-bold text-indigo-600">฿2,500</p><p class="text-xs text-gray-400">ราคาปกติ/คืน</p></div>
                    </div>
                    <div class="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                        <div><p class="font-medium">Suite Room</p><p class="text-sm text-gray-500">ห้องสวีท</p></div>
                        <div class="text-right"><p class="font-bold text-indigo-600">฿4,500</p><p class="text-xs text-gray-400">ราคาปกติ/คืน</p></div>
                    </div>
                    <div class="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                        <div><p class="font-medium">Family Room</p><p class="text-sm text-gray-500">ห้องครอบครัว</p></div>
                        <div class="text-right"><p class="font-bold text-indigo-600">฿3,500</p><p class="text-xs text-gray-400">ราคาปกติ/คืน</p></div>
                    </div>
                </div>
            `, '<button class="btn btn-sm btn-secondary" onclick="Pages.rooms.manageRates()">จัดการราคา</button>');
    },
    addRoom() {
        formModal('เพิ่มห้องพักใหม่', [
            { name: 'number', label: 'หมายเลขห้อง', required: true, placeholder: '101' },
            { name: 'type', label: 'ประเภท', type: 'select', options: [
                { value: 'Standard', label: 'Standard' }, { value: 'Deluxe', label: 'Deluxe' },
                { value: 'Suite', label: 'Suite' }, { value: 'Family', label: 'Family' },
            ]},
            { name: 'floor', label: 'ชั้น', placeholder: '1' },
            { name: 'beds', label: 'เตียง', placeholder: '1 Double' },
            { name: 'price', label: 'ราคาต่อคืน (฿)', type: 'number', placeholder: '1500' },
        ], async (data) => { alert('เพิ่มห้องพักสำเร็จ (Demo)'); });
    },
    edit(number) { showModal('แก้ไขห้อง ' + number, '<p class="text-gray-500">ฟอร์มแก้ไขข้อมูลห้องพัก</p>'); },
    manageRates() { showModal('จัดการแผนราคา', '<p class="text-gray-500">จัดการราคาตามฤดูกาล, โปรโมชัน, วันหยุด</p>'); },
};
window.Pages = Pages;
