// Dashboard การเงิน — KPI, กราฟ (Chart.js ในเครื่อง) และรายการผู้ป่วยรอชำระ
(function () {
    "use strict";

    const root = document.getElementById("dashboard");
    if (!root) return;

    const { format, escapeHtml } = window.app;
    const BRAND = "#f4551f";
    const INK = "#292524";
    const PALETTE = ["#f4551f", "#292524", "#0ea5e9", "#10b981", "#f59e0b", "#8b5cf6", "#94a3b8"];
    const REFRESH_MS = 60_000;

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (window.Chart) {
        Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
        Chart.defaults.color = "#78716c";
        Chart.defaults.animation = reduceMotion ? false : { duration: 500 };
        Chart.defaults.plugins.tooltip.callbacks.label = ctx => `${ctx.dataset.label ?? ctx.label}: ${format.thb(ctx.parsed.y ?? ctx.parsed)}`;
    }

    const thDate = new Intl.DateTimeFormat("th-TH", { day: "numeric", month: "short" });
    const thDateTime = new Intl.DateTimeFormat("th-TH", { dateStyle: "medium", timeStyle: "medium" });
    const charts = {};

    function setField(name, text) {
        root.querySelectorAll(`[data-field="${name}"]`).forEach(el => { el.textContent = text; });
    }

    function waitText(m) {
        if (m < 60) return `${m} นาที`;
        if (m < 60 * 24) return `${Math.floor(m / 60)} ชม. ${m % 60} น.`;
        return `${Math.floor(m / 1440)} วัน ${Math.floor((m % 1440) / 60)} ชม.`;
    }

    // สถานะแบบสั้นสำหรับตาราง (tooltip ใน 3D ใช้แบบเต็ม)
    function statusShort(p) {
        if (p.Type === "O") return p.InBilling ? "รับเรื่องแล้ว" : "รอรับเรื่อง";
        return p.InBilling ? "Medical D/C" : "สั่งจำหน่าย";
    }

    function statusText(p) {
        if (p.Type === "O") return p.InBilling ? "การเงินรับเรื่องแล้ว" : "รอการเงินรับเรื่อง";
        return p.InBilling ? "Medical Discharge แล้ว" : "มีคำสั่งจำหน่าย";
    }

    // สีเวลารอ ตามเกณฑ์เดียวกับ 3D (OPD เป้าหมาย 15 นาที, IPD 6/24 ชม.)
    function waitClass(p) {
        const [warn, late] = p.Type === "O" ? [15, 30] : [6 * 60, 24 * 60];
        return p.WaitingMinutes < warn ? "text-emerald-700" : p.WaitingMinutes < late ? "text-amber-700" : "text-red-600 font-medium";
    }

    // ใช้ร่วมกับ 3D Digital Twin (finance-twin.js)
    window.app.pending = { waitText, statusText };

    function renderKpis(d) {
        const pending = d.PendingPayments;
        const o = pending.filter(p => p.Type === "O");
        const i = pending.filter(p => p.Type === "I");
        const sum = list => list.reduce((s, p) => s + p.Amount, 0);
        const avg = list => list.length ? list.reduce((s, p) => s + p.WaitingMinutes, 0) / list.length : 0;
        const prev = d.RevenuePreviousMonthToDate.Total;
        const change = prev ? (d.RevenueMonth.Total - prev) / prev * 100 : 0;

        const warnings = document.getElementById("dashboardWarnings");
        warnings.classList.toggle("hidden", !d.Warnings.length);
        setField("Warnings", d.Warnings.join(" · "));
        setField("DataSource", d.DataSource);
        setField("GeneratedAt", thDateTime.format(new Date(d.GeneratedAt)));
        setField("RevenueToday.Total", format.thb(d.RevenueToday.Total));
        setField("RevenueToday.Opd", format.thb(d.RevenueToday.Opd));
        setField("RevenueToday.Ipd", format.thb(d.RevenueToday.Ipd));
        setField("RevenueMonth.Total", format.thb(d.RevenueMonth.Total));
        setField("MonthChange", `${change >= 0 ? "▲" : "▼"} ${Math.abs(change).toFixed(1)}%`);
        setField("CreditBilledMonth.Total", format.thb(d.CreditBilledMonth.Total));
        setField("CreditBilledMonth.Opd", format.thb(d.CreditBilledMonth.Opd));
        setField("CreditBilledMonth.Ipd", format.thb(d.CreditBilledMonth.Ipd));
        setField("CashBilledMonth.Total", format.thb(d.CashBilledMonth.Total));
        setField("CashBilledMonth.Opd", format.thb(d.CashBilledMonth.Opd));
        setField("CashBilledMonth.Ipd", format.thb(d.CashBilledMonth.Ipd));
        setField("CreditBillsMonth", format.num(d.CreditBillsMonth));
        setField("CreditShare", `${(d.RevenueMonth.Total ? d.CreditBilledMonth.Total / d.RevenueMonth.Total * 100 : 0).toFixed(1)}%`);
        setField("CollectionsTotal", format.thb(d.CollectionsToday.reduce((s, c) => s + c.NetCollected, 0)));
        setField("PendingTotal", format.thb(sum(pending)));
        setField("PendingOCount", format.num(o.length));
        setField("PendingICount", format.num(i.length));
        setField("PendingOCount2", `${format.num(o.length)} ราย`);
        setField("PendingICount2", `${format.num(i.length)} ราย`);
        setField("PendingOAmount", format.thb(sum(o)));
        setField("PendingIAmount", format.thb(sum(i)));
        setField("PendingOWait", format.num(Math.round(avg(o))));
        setField("PendingIWait", format.num(Math.round(avg(i) / 60)));
        setField("Volume.OpdVisitsToday", format.num(d.Volume.OpdVisitsToday));
        setField("Volume.IpdAdmittedNow", format.num(d.Volume.IpdAdmittedNow));
        setField("Volume.IpdDischargesToday", format.num(d.Volume.IpdDischargesToday));
        setField("Volume.AvgBillOpd", format.thb(d.Volume.AvgBillOpd));
        setField("Volume.AvgBillIpd", format.thb(d.Volume.AvgBillIpd));
    }

    const showIdentity = root.dataset.identity === "true";

    function renderPendingTable(d) {
        for (const type of ["O", "I"]) {
            const list = d.PendingPayments.filter(p => p.Type === type).sort((a, b) => b.Amount - a.Amount);
            setField(`Pending${type}Count3`, format.num(list.length));
            setField(`Pending${type}Amount2`, format.thb(list.reduce((s, p) => s + p.Amount, 0)));
            const rows = list.map(p => `
                <tr data-ref="${escapeHtml(p.Reference)}">
                    <td class="tabular-nums">${escapeHtml(p.Reference)}</td>${showIdentity ? `
                    <td class="tabular-nums">${escapeHtml(p.Hn ?? "-")}</td>
                    <td class="whitespace-nowrap">${escapeHtml(p.PatientName ?? "-")}</td>` : ""}
                    <td class="num font-medium text-stone-900">${format.thb(p.Amount)}</td>
                    <td class="num ${waitClass(p)}">${waitText(p.WaitingMinutes)}</td>
                    <td>${escapeHtml(p.PayerName)}</td>
                    <td>${escapeHtml(p.Location)}</td>
                    <td title="${escapeHtml(statusText(p))}">${statusShort(p)}</td>
                </tr>`).join("");
            document.getElementById(`pendingBody${type}`).innerHTML = rows
                || `<tr><td colspan="${showIdentity ? 8 : 6}" class="py-10 text-center text-stone-500">ไม่มีผู้ป่วย${type === "O" ? "นอกรอชำระ" : "ในรอปิดบัญชี"}</td></tr>`;
        }
    }

    function upsertChart(id, config) {
        const canvas = document.getElementById(id);
        if (!canvas || !window.Chart) return;
        if (charts[id]) {
            charts[id].data = config.data;
            charts[id].update();
        } else {
            charts[id] = new Chart(canvas, config);
        }
    }

    function renderCharts(d) {
        const moneyAxis = { ticks: { callback: v => format.compact(v) }, grid: { color: "#e7e3da" } };

        upsertChart("dailyChart", {
            type: "bar",
            data: {
                labels: d.DailyRevenue.map(x => thDate.format(new Date(x.Date))),
                datasets: [
                    { label: "OPD", data: d.DailyRevenue.map(x => x.Opd), backgroundColor: BRAND, borderRadius: 4, stack: "r" },
                    { label: "IPD", data: d.DailyRevenue.map(x => x.Ipd), backgroundColor: INK, borderRadius: 4, stack: "r" },
                ],
            },
            options: {
                maintainAspectRatio: false,
                interaction: { mode: "index", intersect: false },
                scales: { x: { stacked: true, grid: { display: false } }, y: { stacked: true, ...moneyAxis } },
                plugins: { legend: { position: "bottom" } },
            },
        });

        upsertChart("payerChart", {
            type: "doughnut",
            data: {
                labels: d.RevenueByPayer.map(p => p.Name),
                datasets: [{ data: d.RevenueByPayer.map(p => p.Total), backgroundColor: PALETTE, borderWidth: 2, borderColor: "#fff" }],
            },
            options: {
                maintainAspectRatio: false,
                cutout: "62%",
                plugins: {
                    legend: { position: "bottom", labels: { boxWidth: 10 } },
                    tooltip: { callbacks: { label: ctx => `${ctx.label}: ${format.thb(ctx.parsed)}` } },
                },
            },
        });

        upsertChart("collectionChart", {
            type: "bar",
            data: {
                labels: d.CollectionsToday.map(c => c.Name),
                datasets: [{ label: "รับสุทธิ", data: d.CollectionsToday.map(c => c.NetCollected), backgroundColor: PALETTE, borderRadius: 6 }],
            },
            options: {
                maintainAspectRatio: false,
                indexAxis: "y",
                scales: { x: moneyAxis, y: { grid: { display: false } } },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: ctx => {
                                const c = d.CollectionsToday[ctx.dataIndex];
                                return [`รับสุทธิ ${format.thb(c.NetCollected)}`, `${format.num(c.Receipts)} ใบเสร็จ`, `คืนเงิน ${format.thb(c.Refunded)}`];
                            },
                        },
                    },
                },
            },
        });
    }

    function render(d) {
        window.__dashboardData = d;
        renderKpis(d);
        renderPendingTable(d);
        renderCharts(d);
        // แจ้ง 3D Digital Twin (ES module) ให้วาดใหม่
        document.dispatchEvent(new CustomEvent("dashboard:data", { detail: d }));
    }

    async function refresh() {
        const btn = document.getElementById("refreshBtn");
        btn.disabled = true;
        try {
            render(await app.json(root.dataset.source));
        } catch (err) {
            console.warn("Dashboard refresh failed", err);
        } finally {
            btn.disabled = false;
        }
    }

    document.getElementById("refreshBtn").addEventListener("click", refresh);

    // หยุด refresh เมื่อแท็บไม่ได้เปิดดู เพื่อไม่ให้ query ฐานข้อมูลโดยไม่จำเป็น
    let timer = setInterval(refresh, REFRESH_MS);
    document.addEventListener("visibilitychange", () => {
        clearInterval(timer);
        if (!document.hidden) { refresh(); timer = setInterval(refresh, REFRESH_MS); }
    });

    render(JSON.parse(document.getElementById("dashboardInitial").textContent));
})();
