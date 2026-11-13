// ดึงลูกหนี้สิทธิจาก HIS (Admin) — สั่งงานเบื้องหลังแล้วถามสถานะทุก 2 วินาที
(function () {
    const root = document.getElementById("arSync");
    if (!root) return;
    const button = document.getElementById("syncButton");
    const days = document.getElementById("syncDays");
    const box = document.getElementById("syncStatus");
    const { escapeHtml } = window.app;
    const STATE_TEXT = { QUEUED: "รอดำเนินการ", RUNNING: "กำลังดึงข้อมูล", DONE: "สำเร็จ", FAILED: "ไม่สำเร็จ" };
    let timer = null;

    function render(s) {
        if (!s) { box.innerHTML = ""; return; }
        const active = s.State === "QUEUED" || s.State === "RUNNING";
        const color = s.State === "FAILED" ? "text-red-600" : s.State === "DONE" ? "text-emerald-700" : "text-stone-600";
        box.innerHTML = `<span class="${color}">${active ? '<span class="spinner"></span> ' : ""}${escapeHtml(STATE_TEXT[s.State] || s.State)}</span>`
            + ` · ${escapeHtml(s.Message || "")}`
            + (s.RowsRead ? ` · อ่านแล้ว ${Number(s.RowsRead).toLocaleString("th-TH")} บิล` : "")
            + (s.State === "DONE" ? ' · <a href="" class="text-brand-600 hover:underline">โหลดหน้าใหม่</a>' : "");
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
        button.disabled = true;
        try {
            const body = new FormData();
            body.append("days", days.value);
            const r = await app.json(root.dataset.syncUrl, { method: "POST", body });
            render(r.status);
            startPolling();
        } catch (err) {
            box.innerHTML = `<span class="text-red-600">${escapeHtml(err.message)}</span>`;
            button.disabled = false;
        }
    });

    if (root.dataset.active === "true") startPolling();
})();
