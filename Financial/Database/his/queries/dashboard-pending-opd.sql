-- Dashboard: ผู้ป่วยนอกที่รอชำระเงินขณะนี้ (สำหรับ 3D Digital Twin)
-- จุดการเงิน (service-points.md): arrive = CHKOUT 28029, call = BLINP 44764, finish = FINDIS 44225 (PHADIS 44226 = จบแล้ว)
--   รอชำระ = visit OPD วันนี้ที่มี CHKOUT แล้ว แต่ยังไม่มี FINDIS/PHADIS และ CHKOUT ไม่เกิน 8 ชม. (wait-definitions.md)
--   ยอดเงิน = ค่าใช้จ่ายรวมของ visit SUM(PatientBillableItem.NetAmount)
--   สิทธิ = PatientVisitPayor ลำดับแรก (PAYRTP 13838 PRIMARY) → กลุ่มผู้จ่าย PYRACAT
-- ไม่คืน HN/ชื่อ — คืนเฉพาะ VisitUid ซึ่งแอปจะแสดงเป็นรหัสอ้างอิงแบบย่อ
-- params: @today, @tomorrow, @since (datetime; @since = ขณะนี้ − 8 ชม.)
WITH ev AS (
    SELECT e.PatientVisitUID AS pv,
           MIN(CASE WHEN e.DepartmentUID = 28029 THEN e.CWhen END)            AS chkAt,
           MIN(CASE WHEN e.DepartmentUID = 44764 THEN e.CWhen END)            AS billingAt,
           MAX(CASE WHEN e.DepartmentUID IN (44225, 44226) THEN 1 ELSE 0 END) AS done
    FROM PatientServiceEvent e WITH (NOLOCK)
    JOIN PatientVisit v WITH (NOLOCK)
      ON v.UID = e.PatientVisitUID AND v.ENTYPUID = 1705 AND v.StatusFlag = 'A'
    WHERE e.CWhen >= @today AND e.CWhen < @tomorrow
      AND e.StatusFlag = 'A'
      AND e.DepartmentUID IN (28029, 44764, 44225, 44226)
    GROUP BY e.PatientVisitUID
)
SELECT ev.pv                                     AS VisitUid,
       ev.chkAt                                  AS WaitingSince,
       CASE WHEN ev.billingAt IS NULL THEN 0 ELSE 1 END AS InBilling,
       ISNULL(loc.Name, N'-')                    AS LocationName,
       ISNULL(pay.PayorCategoryCode, '?')        AS PayorCategoryCode,
       ISNULL(pay.PayorCategory, N'(ไม่ระบุ)')   AS PayorCategory,
       ISNULL(amt.Amount, 0)                     AS Amount
FROM ev
JOIN PatientVisit v WITH (NOLOCK) ON v.UID = ev.pv
LEFT JOIN Location loc WITH (NOLOCK) ON loc.UID = v.LocationUID
OUTER APPLY (
    SELECT TOP 1 PCAT.ValueCode AS PayorCategoryCode, PCAT.Description AS PayorCategory
    FROM PatientVisitPayor vp WITH (NOLOCK)
    LEFT JOIN PayorDetail PD WITH (NOLOCK)      ON PD.UID = vp.PayorUID
    LEFT JOIN ReferenceValue PCAT WITH (NOLOCK) ON PCAT.UID = PD.PYRACATUID
    WHERE vp.PatientVisitUID = ev.pv AND vp.StatusFlag = 'A'
    ORDER BY CASE WHEN vp.PAYRTPUID = 13838 THEN 0 ELSE 1 END, vp.UID
) pay
OUTER APPLY (
    SELECT CAST(SUM(bi.NetAmount) AS DECIMAL(18,2)) AS Amount
    FROM PatientBillableItem bi WITH (NOLOCK)
    WHERE bi.PatientVisitUID = ev.pv AND bi.StatusFlag = 'A'
) amt
WHERE ev.done = 0
  AND ev.chkAt IS NOT NULL
  AND ev.chkAt >= @since
