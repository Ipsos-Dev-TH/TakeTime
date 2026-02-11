// Stats cards component
function renderStats(items) {
    let html = '<div class="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-6">';
    items.forEach(item => {
        const colorMap = {
            blue: 'bg-blue-50 text-blue-600',
            green: 'bg-green-50 text-green-600',
            yellow: 'bg-yellow-50 text-yellow-600',
            red: 'bg-red-50 text-red-600',
            purple: 'bg-purple-50 text-purple-600',
            indigo: 'bg-indigo-50 text-indigo-600',
        };
        const iconColor = colorMap[item.color] || colorMap.blue;

        html += `<div class="stat-card">
            <div class="flex items-center gap-3">
                <div class="w-10 h-10 rounded-lg ${iconColor} flex items-center justify-center">
                    ${item.icon}
                </div>
                <div>
                    <p class="text-2xl font-bold text-gray-800">${item.value}</p>
                    <p class="text-xs text-gray-500">${item.label}</p>
                </div>
            </div>
            ${item.trend ? `<p class="mt-2 text-xs ${item.trend > 0 ? 'text-green-600' : 'text-red-600'}">${item.trend > 0 ? '+' : ''}${item.trend}% จากเดือนที่แล้ว</p>` : ''}
        </div>`;
    });
    html += '</div>';
    return html;
}

function renderSectionHeader(title, actions = '') {
    return `<div class="flex items-center justify-between mb-4">
        <h3 class="text-lg font-semibold text-gray-800">${title}</h3>
        <div class="flex items-center gap-2">${actions}</div>
    </div>`;
}

function renderCard(title, content, headerActions = '') {
    return `<div class="bg-white rounded-xl border border-gray-200 mb-6">
        <div class="flex items-center justify-between px-5 py-4 border-b border-gray-100">
            <h3 class="font-semibold text-gray-800">${title}</h3>
            <div class="flex items-center gap-2">${headerActions}</div>
        </div>
        <div class="p-5">${content}</div>
    </div>`;
}

function loadingSkeleton(rows = 5) {
    let html = '';
    for (let i = 0; i < rows; i++) {
        html += `<div class="flex gap-4 mb-4">
            <div class="skeleton h-4 w-1/4"></div>
            <div class="skeleton h-4 w-1/3"></div>
            <div class="skeleton h-4 w-1/6"></div>
            <div class="skeleton h-4 w-1/5"></div>
        </div>`;
    }
    return html;
}
