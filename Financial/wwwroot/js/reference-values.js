// ค่าอ้างอิง: ลากเรียงลำดับ (HTML5 drag & drop + ปุ่ม ▲▼ สำหรับคีย์บอร์ด/มือถือ) แล้วบันทึกครั้งเดียว
(function () {
    "use strict";

    const body = document.getElementById("refBody");
    if (!body) return;

    const saveBtn = document.getElementById("refSaveOrder");
    const resetBtn = document.getElementById("refReset");
    const status = document.getElementById("refStatus");
    const category = body.dataset.category;
    const rows = () => Array.from(body.querySelectorAll("tr[data-id]"));
    const currentIds = () => rows().map(r => Number(r.dataset.id));
    let savedIds = currentIds();
    let dragging = null;

    function showStatus(text, ok) {
        status.className = ok ? "alert-success mb-3" : "alert-error mb-3";
        status.textContent = text;
    }

    function markDirty() {
        const dirty = currentIds().join() !== savedIds.join();
        saveBtn.disabled = !dirty;
        resetBtn.classList.toggle("hidden", !dirty);
        // ลำดับที่จะได้หลังบันทึก (10, 20, 30, ...)
        rows().forEach((r, i) => {
            const cell = r.querySelector("[data-order]");
            if (dirty) cell.textContent = String((i + 1) * 10);
        });
        if (dirty) status.classList.add("hidden");
    }

    // ---------- ลากด้วยเมาส์ ----------
    body.addEventListener("dragstart", e => {
        dragging = e.target.closest("tr[data-id]");
        if (!dragging) return;
        dragging.classList.add("opacity-40");
        e.dataTransfer.effectAllowed = "move";
        e.dataTransfer.setData("text/plain", dragging.dataset.id);
    });
    body.addEventListener("dragover", e => {
        if (!dragging) return;
        e.preventDefault();
        const over = e.target.closest("tr[data-id]");
        if (!over || over === dragging) return;
        const rect = over.getBoundingClientRect();
        const after = e.clientY > rect.top + rect.height / 2;
        body.insertBefore(dragging, after ? over.nextSibling : over);
    });
    body.addEventListener("drop", e => e.preventDefault());
    body.addEventListener("dragend", () => {
        dragging?.classList.remove("opacity-40");
        dragging = null;
        markDirty();
    });

    // ---------- ปุ่ม ▲▼ ----------
    body.addEventListener("click", e => {
        const btn = e.target.closest("[data-move]");
        if (!btn) return;
        const row = btn.closest("tr[data-id]");
        if (btn.dataset.move === "-1" && row.previousElementSibling) body.insertBefore(row, row.previousElementSibling);
        if (btn.dataset.move === "1" && row.nextElementSibling) body.insertBefore(row.nextElementSibling, row);
        btn.focus();
        markDirty();
    });

    // ยืนยันก่อนลบ: form[data-confirm] จัดการที่ site.js (ทั้งระบบ)

    resetBtn.addEventListener("click", () => window.location.reload());

    saveBtn.addEventListener("click", async () => {
        saveBtn.disabled = true;
        const original = saveBtn.textContent;
        saveBtn.textContent = "กำลังบันทึก…";
        try {
            const response = await window.app.request("/ReferenceValues/Reorder", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ category, ids: currentIds() }),
            });
            const result = await response.json().catch(() => ({}));
            if (!response.ok) throw new Error(result.message || `บันทึกไม่สำเร็จ (${response.status})`);
            const orders = new Map((result.items || []).map(x => [x.Id ?? x.id, x.DisplayOrder ?? x.displayOrder]));
            rows().forEach(r => { const o = orders.get(Number(r.dataset.id)); if (o != null) r.querySelector("[data-order]").textContent = String(o); });
            savedIds = currentIds();
            markDirty();
            showStatus(result.message || "บันทึกลำดับแล้ว", true);
        } catch (err) {
            saveBtn.disabled = false;
            showStatus(err.message, false);
        } finally {
            saveBtn.textContent = original;
        }
    });
})();
