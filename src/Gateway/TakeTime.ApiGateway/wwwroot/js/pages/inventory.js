const Pages = window.Pages || {};
Pages.inventory = {
    title: 'สินค้า & POS',
    async render() {
        const stats = renderStats([
            { label: 'สินค้าทั้งหมด', value: '48', color: 'blue', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4"/></svg>' },
            { label: 'ใกล้หมด', value: '5', color: 'yellow', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01"/></svg>' },
            { label: 'ขายวันนี้', value: '฿8,350', color: 'green', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z"/></svg>' },
            { label: 'หมวดหมู่', value: '6', color: 'purple', icon: '<svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z"/></svg>' },
        ]);

        const products = [
            { id: 1, name: 'น้ำดื่ม 600ml', category: 'เครื่องดื่ม', price: 20, stock: 150, minStock: 50, status: 'Active' },
            { id: 2, name: 'ข้าวผัด', category: 'อาหาร', price: 120, stock: 0, minStock: 0, status: 'Active' },
            { id: 3, name: 'สบู่ห้องพัก', category: 'อเมนิตี้', price: 35, stock: 8, minStock: 20, status: 'Active' },
            { id: 4, name: 'ผ้าขนหนู', category: 'อเมนิตี้', price: 250, stock: 45, minStock: 10, status: 'Active' },
            { id: 5, name: 'กาแฟดริป', category: 'เครื่องดื่ม', price: 80, stock: 5, minStock: 20, status: 'Active' },
        ];

        const table = renderTable(
            [
                { label: 'สินค้า', render: r => `<p class="font-medium">${r.name}</p>` },
                { label: 'หมวดหมู่', render: r => badge(r.category, 'blue') },
                { label: 'ราคา', render: r => formatMoney(r.price) },
                { label: 'คงเหลือ', render: r => {
                    const low = r.stock > 0 && r.stock < r.minStock;
                    return `<span class="${low ? 'text-red-600 font-bold' : ''}">${r.stock}</span>${low ? ' <span class="text-xs text-red-500">ใกล้หมด</span>' : ''}`;
                }},
                { label: 'สถานะ', render: r => statusBadge(r.status) },
            ],
            products,
            { actions: (r) => `<button class="btn btn-sm btn-secondary" onclick="Pages.inventory.edit(${r.id})">แก้ไข</button>` }
        );

        return stats + renderCard('รายการสินค้า', table, '<button class="btn btn-sm btn-primary" onclick="Pages.inventory.add()">+ เพิ่มสินค้า</button>');
    },
    add() {
        formModal('เพิ่มสินค้า', [
            { name: 'name', label: 'ชื่อสินค้า', required: true },
            { name: 'category', label: 'หมวดหมู่', type: 'select', options: [
                { value: 'food', label: 'อาหาร' }, { value: 'drink', label: 'เครื่องดื่ม' },
                { value: 'amenity', label: 'อเมนิตี้' }, { value: 'souvenir', label: 'ของที่ระลึก' },
            ]},
            { name: 'price', label: 'ราคา (฿)', type: 'number', required: true },
            { name: 'stock', label: 'จำนวนในสต็อก', type: 'number' },
        ], async (data) => { alert('เพิ่มสินค้าสำเร็จ (Demo)'); });
    },
    edit(id) { showModal('แก้ไขสินค้า', '<p class="text-gray-500">ฟอร์มแก้ไขข้อมูลสินค้า</p>'); },
};
window.Pages = Pages;
