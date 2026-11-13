// QSHC Financial — สคริปต์ส่วนกลาง (ไม่พึ่งไลบรารีภายนอก)
(function () {
    "use strict";

    const csrf = document.querySelector('meta[name="csrf-token"]')?.content ?? "";

    const thb = new Intl.NumberFormat("th-TH", { style: "currency", currency: "THB", maximumFractionDigits: 0 });
    const num = new Intl.NumberFormat("th-TH");
    const compact = new Intl.NumberFormat("th-TH", { notation: "compact", maximumFractionDigits: 1 });

    /**
     * fetch ที่แนบ Antiforgery token และ header สำหรับ AJAX
     * - session หมดอายุ (401) → กลับไปหน้า Login พร้อม returnUrl
     */
    async function request(url, options = {}) {
        const headers = new Headers(options.headers || {});
        headers.set("X-Requested-With", "fetch");
        if (!headers.has("Accept")) headers.set("Accept", "application/json");
        if ((options.method || "GET").toUpperCase() !== "GET") headers.set("RequestVerificationToken", csrf);

        const response = await fetch(url, { credentials: "same-origin", ...options, headers });
        if (response.status === 401) {
            window.location.href = "/Account/Login?returnUrl=" + encodeURIComponent(location.pathname + location.search);
            throw new Error("กรุณาเข้าสู่ระบบใหม่");
        }
        if (response.status === 429) throw new Error("มีการเรียกใช้งานบ่อยเกินไป กรุณารอสักครู่");
        return response;
    }

    async function json(url, options) {
        const response = await request(url, options);
        if (!response.ok) throw new Error(`เกิดข้อผิดพลาด (${response.status})`);
        return response.json();
    }

    function escapeHtml(value) {
        return String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    }

    window.app = { request, json, escapeHtml, format: { thb: v => thb.format(v), num: v => num.format(v), compact: v => compact.format(v) } };

    // ---------- Sidebar (มือถือ) ----------
    const sidebar = document.getElementById("sidebar");
    const backdrop = document.getElementById("sidebar-backdrop");
    function setSidebar(open) {
        if (!sidebar) return;
        sidebar.classList.toggle("-translate-x-full", !open);
        backdrop?.classList.toggle("hidden", !open);
    }
    document.querySelectorAll("[data-sidebar-open]").forEach(b => b.addEventListener("click", () => setSidebar(true)));
    document.querySelectorAll("[data-sidebar-close]").forEach(b => b.addEventListener("click", () => setSidebar(false)));
    document.addEventListener("keydown", e => { if (e.key === "Escape") setSidebar(false); });

    // ---------- ปิดเมนูผู้ใช้เมื่อคลิกนอกเมนู ----------
    document.addEventListener("click", e => {
        document.querySelectorAll("details[data-user-menu][open]").forEach(d => { if (!d.contains(e.target)) d.removeAttribute("open"); });
    });

    // ---------- ยืนยันก่อนส่งฟอร์ม (ข้อความใน data-confirm ไม่ใช้ inline script) ----------
    // capture phase: ถ้าไม่ยืนยัน ฟอร์มไม่ถูกส่งและไม่เข้าสถานะ "กำลังโหลด"
    document.addEventListener("submit", e => {
        const form = e.target.closest("form[data-confirm]");
        if (form && !window.confirm(form.dataset.confirm)) { e.preventDefault(); e.stopPropagation(); }
    }, true);

    // ---------- ฟอร์มที่ใช้เวลานาน: แสดงสถานะกำลังโหลด และกันกดซ้ำ ----------
    document.querySelectorAll("form[data-loading]").forEach(form => {
        form.addEventListener("submit", () => {
            const button = form.querySelector('button[type="submit"]');
            if (!button) return;
            // ปุ่มดาวน์โหลดไฟล์จะไม่เปลี่ยนหน้า จึงคืนค่าปุ่มหลังผ่านไปสักครู่
            const restore = button.innerHTML;
            button.disabled = true;
            button.innerHTML = `<span class="spinner"></span><span>${escapeHtml(form.dataset.loading || "กำลังโหลด...")}</span>`;
            if (form.hasAttribute("data-download")) setTimeout(() => { button.disabled = false; button.innerHTML = restore; }, 4000);
        });
    });
})();
