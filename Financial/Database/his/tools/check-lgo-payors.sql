-- ตรวจว่าสิทธิ์ อปท. (องค์กรปกครองส่วนท้องถิ่น) อยู่ในกลุ่มผู้จ่าย (PYRACAT) ใดใน HIS — อ่านอย่างเดียว, ตัวเลขรวมเท่านั้น (ไม่มีข้อมูลผู้ป่วย)
-- ใช้เมื่อ: รายงาน อปท. ว่าง ทั้งที่ตั้ง Reports:Fiscal:Schemes ของ LGO เป็น P, W แล้ว (ดึงข้อมูลแล้วจำนวนแถวไม่เพิ่ม)
-- รันด้วยเครื่องมือของ HO63Database: node tools/query.mjs -f <ไฟล์นี้> --param from=2025-10-01 --param to=2026-10-01
-- ผลที่ต้องการ: รหัส PayorCategoryCode ของแถวที่ชื่อผู้จ่าย/เงื่อนไขเป็น อปท. และเป็นสิทธิ์หลัก (PRIMARY) หรือไม่
SELECT TOP 50
    ISNULL(PCAT.ValueCode, '?')                                   AS PayorCategoryCode,
    ISNULL(PCAT.Description, N'(ไม่ระบุ)')                         AS PayorCategory,
    PD.Code                                                       AS PayorCode,
    PD.PayorName                                                  AS PayorName,
    CASE WHEN VP.PAYRTPUID = 13838 THEN 'PRIMARY' ELSE 'OTHER' END AS PayorRank,
    COUNT(DISTINCT PB.PatientVisitUID)                            AS Visits,
    COUNT(DISTINCT PB.UID)                                        AS Bills
FROM PatientBill PB WITH (NOLOCK)
JOIN PatientVisitPayor VP WITH (NOLOCK) ON VP.UID = PB.PatientVisitPayorUID AND VP.StatusFlag = 'A'
JOIN PayorDetail PD WITH (NOLOCK)       ON PD.UID = VP.PayorUID
LEFT JOIN ReferenceValue PCAT WITH (NOLOCK) ON PCAT.UID = PD.PYRACATUID
LEFT JOIN PayorAgreement PA WITH (NOLOCK)   ON PA.UID = VP.PayorAgreementUID
WHERE PB.BillGeneratedDttm >= @from
  AND PB.BillGeneratedDttm <  @to
  AND PB.StatusFlag = 'A'
  AND PB.IsRefund IS NULL
  AND (   PCAT.ValueCode IN ('P', 'W')
       OR PD.PayorName LIKE N'%อปท%'  OR PD.PayorName LIKE N'%ท้องถิ่น%'
       OR PA.Name      LIKE N'%อปท%'  OR PA.Name      LIKE N'%ท้องถิ่น%')
GROUP BY PCAT.ValueCode, PCAT.Description, PD.Code, PD.PayorName,
         CASE WHEN VP.PAYRTPUID = 13838 THEN 'PRIMARY' ELSE 'OTHER' END
ORDER BY Bills DESC
