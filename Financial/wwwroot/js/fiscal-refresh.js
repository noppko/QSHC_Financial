// รายงานสิทธิ์ตามปีงบ: ปุ่ม "ดึงข้อมูลจาก HIS" หลายปี + progress bar (ถามสถานะทุก 2 วินาที)
(function () {
    "use strict";

    const button = document.getElementById("fiscalRefresh");
    if (!button) return;

    const box = document.getElementById("fiscalProgress");
    const title = document.getElementById("fiscalProgressTitle");
    const count = document.getElementById("fiscalProgressCount");
    const bar = document.getElementById("fiscalProgressBar");
    const fill = document.getElementById("fiscalProgressFill");
    const list = document.getElementById("fiscalProgressYears");
    const done = document.getElementById("fiscalProgressDone");
    const { request, escapeHtml, format } = window.app;
    const POLL_MS = 2000;
    let timer = null;

    const range = () => {
        const a = Number(document.getElementById("from").value), b = Number(document.getElementById("to").value);
        return [Math.min(a, b), Math.max(a, b)];
    };

    const duration = s => s >= 60 ? `${Math.floor(s / 60)} นาที ${s % 60} วินาที` : `${s} วินาที`;

    const STATE = {
        QUEUED: { text: "รอคิว", cls: "bg-sky-50 text-sky-700" },
        RUNNING: { text: "กำลังดึง", cls: "bg-amber-50 text-amber-700 animate-pulse" },
        DONE: { text: "สำเร็จ", cls: "bg-emerald-50 text-emerald-700" },
        FAILED: { text: "ไม่สำเร็จ", cls: "bg-red-50 text-red-700" },
    };

    function render(status) {
        if (!status || status.total === 0) return false;
        box.classList.remove("hidden");

        const finished = status.done + status.failed;
        const percent = Math.round((finished / status.total) * 100);
        fill.style.width = `${status.active ? Math.max(percent, 3) : percent}%`;
        fill.classList.toggle("bg-red-500", !status.active && status.failed > 0);
        bar.setAttribute("aria-valuenow", String(percent));
        count.textContent = `${finished} / ${status.total} ปีงบ (${percent}%)`;

        const running = status.years.find(y => y.state === "RUNNING");
        title.textContent = status.active
            ? (running ? `กำลังดึงข้อมูลปีงบ ${running.year} จาก HIS — อ่านแล้ว ${format.num(running.rows)} แถว · ${duration(running.seconds)}` : "รอคิวประมวลผล")
            : status.failed > 0 ? `ดึงข้อมูลเสร็จ — ไม่สำเร็จ ${status.failed} ปี (ดูรายละเอียดใน log)` : "ดึงข้อมูลจาก HIS เสร็จแล้ว";

        list.innerHTML = status.years.map(y => {
            const st = STATE[y.state] ?? STATE.QUEUED;
            const extra = y.state === "DONE" ? ` · ${duration(y.seconds)}` : y.state === "FAILED" ? ` · ${escapeHtml(y.message ?? "")}` : "";
            return `<li class="badge ${st.cls}">${y.year} ${st.text}${extra}</li>`;
        }).join("");

        done.classList.toggle("hidden", status.active);
        button.disabled = status.active;
        return status.active;
    }

    async function poll() {
        clearTimeout(timer);
        const [from, to] = range();
        try {
            const response = await request(`${button.dataset.statusUrl}?from=${from}&to=${to}`);
            if (!response.ok) throw new Error(`(${response.status})`);
            if (render(await response.json())) timer = setTimeout(poll, POLL_MS);
        } catch {
            timer = setTimeout(poll, POLL_MS * 3); // เครือข่ายสะดุด: ลองใหม่ช้าลง
        }
    }

    button.addEventListener("click", async () => {
        const [from, to] = range();
        if (!window.confirm(`ดึงข้อมูลปีงบ ${from}–${to} จาก HIS ใหม่ (ทุกสิทธิ์)?\nใช้เวลาปีละหลายนาที ทำงานเบื้องหลัง ปิดหน้านี้ได้`)) return;
        button.disabled = true;
        try {
            const body = new URLSearchParams({ from: String(from), to: String(to) });
            const response = await request(button.dataset.url, { method: "POST", body });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.message || `เริ่มดึงข้อมูลไม่สำเร็จ (${response.status})`);
            render(result.status);
            timer = setTimeout(poll, POLL_MS);
        } catch (err) {
            button.disabled = false;
            box.classList.remove("hidden");
            title.textContent = err.message;
        }
    });

    // เปิดหน้ามาขณะกำลังประมวลผลอยู่ (สั่งไว้ก่อนหน้า/คนอื่นสั่ง) → แสดง progress ต่อ
    ["from", "to"].forEach(id => document.getElementById(id).addEventListener("change", poll));
    poll();
})();
