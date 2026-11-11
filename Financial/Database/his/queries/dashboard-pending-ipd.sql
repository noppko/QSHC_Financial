-- Dashboard: ผู้ป่วยในที่แพทย์ให้กลับบ้านแล้ว แต่ยังไม่ Financial Discharge (สำหรับ 3D Digital Twin)
--   IPD ที่ยังนอนอยู่ = ENTYP 1379, StatusFlag='A', EndDttm IS NULL (ipd.md)
--   Discharge pipeline = DischargeEvent: DischargeOrderDttm → MedicalDischargeDttm → ActualDischargeDttm
--   ยังไม่ปิดบัญชี = PACLSUID <> 44225 (FINDIS)
--   รอตั้งแต่ = MedicalDischargeDttm (ถ้าไม่มีใช้ DischargeOrderDttm)
--   ยอดเงิน = ค่าใช้จ่ายสะสมของ visit SUM(PatientBillableItem.NetAmount)
-- ไม่คืน HN/ชื่อ — คืนเฉพาะ VisitUid ซึ่งแอปจะแสดงเป็นรหัสอ้างอิงแบบย่อ
-- params: ไม่มี
SELECT v.UID                                                     AS VisitUid,
       COALESCE(de.MedicalDischargeDttm, de.DischargeOrderDttm)  AS WaitingSince,
       CASE WHEN de.MedicalDischargeDttm IS NULL THEN 0 ELSE 1 END AS InBilling,
       ISNULL(COALESCE(ward.Name, loc.Name), N'-')               AS LocationName,
       ISNULL(pay.PayorCategoryCode, '?')                        AS PayorCategoryCode,
       ISNULL(pay.PayorCategory, N'(ไม่ระบุ)')                   AS PayorCategory,
       ISNULL(amt.Amount, 0)                                     AS Amount
FROM PatientVisit v WITH (NOLOCK)
CROSS APPLY (
    SELECT TOP 1 x.MedicalDischargeDttm, x.DischargeOrderDttm
    FROM AdmissionEvent a WITH (NOLOCK)
    JOIN DischargeEvent x WITH (NOLOCK) ON x.AdmissionEventUID = a.UID AND x.StatusFlag = 'A'
    WHERE a.PatientVisitUID = v.UID AND a.StatusFlag = 'A'
    ORDER BY x.UID DESC
) de
LEFT JOIN Location bed  WITH (NOLOCK) ON bed.UID = v.BedUID
LEFT JOIN Location ward WITH (NOLOCK) ON ward.UID = bed.ParentLocationUID
LEFT JOIN Location loc  WITH (NOLOCK) ON loc.UID = v.LocationUID
OUTER APPLY (
    SELECT TOP 1 PCAT.ValueCode AS PayorCategoryCode, PCAT.Description AS PayorCategory
    FROM PatientVisitPayor vp WITH (NOLOCK)
    LEFT JOIN PayorDetail PD WITH (NOLOCK)      ON PD.UID = vp.PayorUID
    LEFT JOIN ReferenceValue PCAT WITH (NOLOCK) ON PCAT.UID = PD.PYRACATUID
    WHERE vp.PatientVisitUID = v.UID AND vp.StatusFlag = 'A'
    ORDER BY CASE WHEN vp.PAYRTPUID = 13838 THEN 0 ELSE 1 END, vp.UID
) pay
OUTER APPLY (
    SELECT CAST(SUM(bi.NetAmount) AS DECIMAL(18,2)) AS Amount
    FROM PatientBillableItem bi WITH (NOLOCK)
    WHERE bi.PatientVisitUID = v.UID AND bi.StatusFlag = 'A'
) amt
WHERE v.ENTYPUID = 1379
  AND v.StatusFlag = 'A'
  AND v.EndDttm IS NULL
  AND ISNULL(v.PACLSUID, 0) <> 44225
  AND (de.MedicalDischargeDttm IS NOT NULL OR de.DischargeOrderDttm IS NOT NULL)
