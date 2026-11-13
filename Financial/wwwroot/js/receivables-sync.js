// ดึงลูกหนี้สิทธิจาก HIS (หน้า ตั้งค่าระบบ, Admin) — เลือกวันที่เริ่ม–สิ้นสุด สั่งงานเบื้องหลังแล้วถามสถานะทุก 2 วินาที
(function () {
    const root = document.getElementById("receivables-sync");
    if (!root) return;
    const button = document.getElementById("arSyncButton");
    const fromInput = document.getElementById("arFrom");
    const toInput = document.getElementById("arTo");
    const box = document.getElementById("arSyncStatus");
    const { escapeHtml } = window.app;
    const STATE_TEXT = { QUEUED: "รอดำเนินการ", RUNNING: "กำลังดึงข้อมูล", DONE: "สำเร็จ", FAILED: "ไม่สำเร็จ" };
    let timer = null;

    // ปุ่มช่วงเร็ว: ตั้งแต่วันที่ที่กำหนด ถึงวันนี้
    root.querySelectorAll("[data-ar-range]").forEach(b => b.addEventListener("click", () => {
        fromInput.value = b.dataset.arRange;
        toInput.value = toInput.max;
    }));

    function render(s) {
        if (!s) { box.innerHTML = ""; button.disabled = false; return; }
        const active = s.State === "QUEUED" || s.State === "RUNNING";
        const color = s.State === "FAILED" ? "text-red-600" : s.State === "DONE" ? "text-emerald-700" : "text-stone-600";
        box.innerHTML = `<span class="${color}">${active ? '<span class="spinner"></span> ' : ""}${escapeHtml(STATE_TEXT[s.State] || s.State)}</span>`
            + ` · ${escapeHtml(s.Message || "")}`
            + (active && s.RowsRead ? ` · อ่านแล้ว ${Number(s.RowsRead).toLocaleString("th-TH")} บิล` : "");
        button.disabled = active;
        if (!active && timer) { clearInterval(timer); timer = null; }
    }

    async function poll() {
        try { render(await app.json(root.dataset.statusUrl)); } catch { /* ลองใหม่รอบถัดไป */ }
    }

    function startPolling() {
        if (!timer) timer = setInterval(poll, 2000);
        poll();
    }

    button.addEventListener("click", async () => {
        if (!fromInput.value || !toInput.value) { box.innerHTML = '<span class="text-red-600">กรุณาเลือกวันที่เริ่มและสิ้นสุด</span>'; return; }
        if (fromInput.value > toInput.value) [fromInput.value, toInput.value] = [toInput.value, fromInput.value];
        button.disabled = true;
        try {
            const body = new FormData();
            body.append("from", fromInput.value);
            body.append("to", toInput.value);
            const r = await app.json(root.dataset.syncUrl, { method: "POST", body });
            render(r.status);
            if (!r.queued) box.insertAdjacentHTML("afterbegin", `<div class="text-amber-700">${escapeHtml(r.message)}</div>`);
            else startPolling();
        } catch (err) {
            box.innerHTML = `<span class="text-red-600">${escapeHtml(err.message)}</span>`;
            button.disabled = false;
        }
    });

    if (root.dataset.active === "true") startPolling();
})();
