// Chart.js helpers shared by the dashboard and reports.
window.MeterCharts = (function () {
    const colors = {
        water: { line: '#0EA5E9', fill: 'rgba(14,165,233,.75)', soft: 'rgba(14,165,233,.12)' },
        elec: { line: '#F59E0B', fill: 'rgba(245,158,11,.8)', soft: 'rgba(245,158,11,.12)' },
        primary: { line: '#2563EB', fill: 'rgba(37,99,235,.8)', soft: 'rgba(37,99,235,.12)' }
    };

    if (window.Chart) {
        Chart.defaults.font.family = '"Sarabun", "Segoe UI", sans-serif';
        Chart.defaults.font.size = 13;
        Chart.defaults.color = '#64748B';
        Chart.defaults.plugins.legend.labels.usePointStyle = true;
        Chart.defaults.maintainAspectRatio = false;
    }

    const fmt = v => Number(v).toLocaleString('en-US', { maximumFractionDigits: 2 });
    const grid = { color: '#EEF2F7' };
    const tooltip = { callbacks: { label: c => ` ${c.dataset.label}: ${fmt(c.parsed.y)}` } };

    function bar(id, labels, data, label, c) {
        const el = document.getElementById(id);
        if (!el) return null;
        return new Chart(el, {
            type: 'bar',
            data: { labels, datasets: [{ label, data, backgroundColor: c.fill, hoverBackgroundColor: c.line, borderRadius: 6, maxBarThickness: 34 }] },
            options: {
                plugins: { legend: { display: false }, tooltip },
                scales: { x: { grid: { display: false } }, y: { beginAtZero: true, grid, ticks: { callback: fmt } } }
            }
        });
    }

    function compare(id, labels, water, elec) {
        const el = document.getElementById(id);
        if (!el) return null;
        return new Chart(el, {
            type: 'line',
            data: {
                labels,
                datasets: [
                    { label: 'น้ำ (m³)', data: water, borderColor: colors.water.line, backgroundColor: colors.water.soft, fill: true, tension: .35, yAxisID: 'y', pointRadius: 3 },
                    { label: 'ไฟฟ้า (kWh)', data: elec, borderColor: colors.elec.line, backgroundColor: colors.elec.soft, fill: true, tension: .35, yAxisID: 'y1', pointRadius: 3 }
                ]
            },
            options: {
                interaction: { mode: 'index', intersect: false },
                plugins: { legend: { position: 'bottom' }, tooltip },
                scales: {
                    x: { grid: { display: false } },
                    y: { beginAtZero: true, position: 'left', grid, title: { display: true, text: 'm³' }, ticks: { callback: fmt } },
                    y1: { beginAtZero: true, position: 'right', grid: { drawOnChartArea: false }, title: { display: true, text: 'kWh' }, ticks: { callback: fmt } }
                }
            }
        });
    }

    // Grouped bars per period: water on the left axis (m³), electricity on the right axis (kWh). Either may be null.
    function grouped(id, labels, water, elec) {
        const el = document.getElementById(id);
        if (!el) return null;
        const datasets = [];
        if (water) datasets.push({ label: 'น้ำ (m³)', data: water, backgroundColor: colors.water.fill, borderRadius: 6, maxBarThickness: 30, yAxisID: 'y' });
        if (elec) datasets.push({ label: 'ไฟฟ้า (kWh)', data: elec, backgroundColor: colors.elec.fill, borderRadius: 6, maxBarThickness: 30, yAxisID: water ? 'y1' : 'y' });
        const scales = {
            x: { grid: { display: false } },
            y: { beginAtZero: true, grid, title: { display: true, text: water ? 'm³' : 'kWh' }, ticks: { callback: fmt } }
        };
        if (water && elec) scales.y1 = { beginAtZero: true, position: 'right', grid: { drawOnChartArea: false }, title: { display: true, text: 'kWh' }, ticks: { callback: fmt } };
        return new Chart(el, {
            type: 'bar',
            data: { labels, datasets },
            options: { interaction: { mode: 'index', intersect: false }, plugins: { legend: { position: 'bottom' }, tooltip }, scales }
        });
    }

    return { colors, bar, compare, grouped };
})();
