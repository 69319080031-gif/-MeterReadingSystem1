// Record/Edit reading form: loads meter info + neighbouring readings, computes consumption live.
(function () {
    const cfg = window.readingFormConfig;
    const $ = id => document.getElementById(id);
    const meterSel = $('MeterId');
    const dateInput = $('ReadingDate');
    const valueInput = $('CumulativeReading');
    let info = null;
    let reqSeq = 0;

    async function load() {
        const meterId = meterSel.value;
        if (!meterId) { info = null; render(); return; }
        const seq = ++reqSeq;
        const params = new URLSearchParams({ meterId, date: dateInput.value || '' });
        if (cfg.excludeId) params.set('excludeId', cfg.excludeId);
        try {
            const res = await fetch(`${cfg.infoUrl}?${params}`, { headers: { 'Accept': 'application/json' } });
            if (!res.ok) throw new Error();
            const data = await res.json();
            if (seq !== reqSeq) return; // a newer request is in flight
            info = data;
        } catch {
            info = null;
            $('calcNote').textContent = 'ไม่สามารถโหลดข้อมูลมิเตอร์ได้ กรุณาลองใหม่';
        }
        render();
    }

    function render() {
        const has = !!info;
        $('meterEmpty').classList.toggle('d-none', has);
        $('meterPanel').classList.toggle('d-none', !has);
        document.querySelectorAll('.unit-label').forEach(e => e.textContent = has ? info.unit : 'หน่วย');
        if (!has) {
            ['prevValue', 'nextValue'].forEach(id => $(id).textContent = '-');
            ['prevDate', 'nextDate'].forEach(id => $(id).textContent = '');
            calc();
            return;
        }
        $('mCode').textContent = info.code;
        $('mType').innerHTML = info.type === 'Water'
            ? '<span class="badge-soft badge-water"><i class="bi bi-droplet-fill"></i>' + info.typeName + '</span>'
            : '<span class="badge-soft badge-elec"><i class="bi bi-lightning-charge-fill"></i>' + info.typeName + '</span>';
        $('mLocation').textContent = info.location;
        $('mStatus').textContent = info.status;
        if (dateInput) dateInput.min = info.installationDate;

        $('prevValue').textContent = info.previous ? `${info.previous.display} ${info.unit}` : 'ไม่มี (ครั้งแรก)';
        $('prevDate').textContent = info.previous ? `วันที่ ${info.previous.date}` : '';
        $('nextValue').textContent = info.next ? `${info.next.display} ${info.unit}` : 'ไม่มี';
        $('nextDate').textContent = info.next ? `วันที่ ${info.next.date}` : '';
        calc();
    }

    function calc() {
        const box = $('calcBox'), out = $('consumption'), note = $('calcNote');
        box.classList.remove('bad');
        const raw = valueInput.value.trim();
        if (!info) { out.textContent = '-'; note.textContent = 'เลือกมิเตอร์และกรอกค่าอ่านสะสม'; return; }
        if (raw === '' || isNaN(Number(raw))) { out.textContent = '-'; note.textContent = 'กรอกค่าอ่านสะสมเพื่อคำนวณ'; return; }
        const v = Number(raw);
        if (v < 0) { box.classList.add('bad'); out.textContent = 'ผิดพลาด'; note.textContent = 'ค่าอ่านสะสมต้องไม่ติดลบ'; return; }
        if (info.next && v > info.next.value) {
            box.classList.add('bad');
            out.textContent = 'ผิดพลาด';
            note.textContent = `ค่าอ่านสูงกว่าค่าอ่านครั้งถัดไป (${info.next.display} ${info.unit} วันที่ ${info.next.date}) ระบบจะไม่อนุญาตให้บันทึก`;
            return;
        }
        if (!info.previous) {
            out.textContent = '-';
            note.textContent = 'เป็นค่าอ่านครั้งแรกของมิเตอร์ (ไม่มีปริมาณการใช้)';
            return;
        }
        const diff = Math.round((v - info.previous.value) * 1000) / 1000;
        if (diff < 0) {
            box.classList.add('bad');
            out.textContent = `${fmtNum(diff)} ${info.unit}`;
            note.textContent = `ค่าอ่านต่ำกว่าค่าอ่านครั้งก่อน (${info.previous.display} ${info.unit}) ระบบจะไม่อนุญาตให้บันทึก`;
            return;
        }
        out.textContent = `${fmtNum(diff)} ${info.unit}`;
        note.textContent = `${fmtNum(v)} − ${info.previous.display} = ${fmtNum(diff)} ${info.unit}`;
    }

    meterSel?.addEventListener('change', load);
    dateInput?.addEventListener('change', load);
    valueInput?.addEventListener('input', calc);
    load();
})();
