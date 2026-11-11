-- Dashboard: รายได้ตามวันออกบิล × OPD/IPD × เงินสด/เครดิต × กลุ่มผู้จ่าย — ตัวเลขรวม ไม่มี PHI
-- อ้างอิง finance.md §5 และ finance-revenue.sql (ทดสอบแล้ว 2026-10-22)
--   ยอดบิล = SUM(PatientBilledItem.NetAmount)  (ห้ามใช้ PatientBill.TotalAmount)
--   ตัดบิลคืนเงิน (IsRefund='Y') และ Free bill (BLTYP 23377 FRBLL ตาม fGetPatientBilledAmount)
--   ใช้เฉพาะ BLCAT OPBILL 4321 / OPMERG 64739 (O) และ IPBILL 4322 / IPMERG 64740 (I)
-- params: @from, @to (datetime, @to exclusive)
-- ตรวจ: node tools/query.mjs -f <ไฟล์นี้> --param from=2026-09-01 --param to=2026-10-01
SELECT
    CAST(PB.BillGeneratedDttm AS date)                                   AS BillDate,
    CASE WHEN PB.BLCATUID IN (4322, 64740) THEN 'I' ELSE 'O' END        AS Side,
    CASE WHEN PB.BLTYPUID = 2160 THEN 1 ELSE 0 END                      AS IsCredit,
    ISNULL(PCAT.ValueCode, '?')                                          AS PayorCategoryCode,
    ISNULL(PCAT.Description, N'(ไม่ระบุ)')                               AS PayorCategory,
    COUNT(DISTINCT PB.UID)                                               AS Bills,
    CAST(SUM(PBI.NetAmount) AS DECIMAL(18,2))                            AS NetAmount
FROM PatientBill PB WITH (NOLOCK)
JOIN PatientBilledItem PBI WITH (NOLOCK)
  ON PBI.PatientBillUID = PB.UID
 AND PBI.StatusFlag = 'A'
LEFT JOIN PayorDetail PD WITH (NOLOCK)      ON PD.UID = PB.PayorUID
LEFT JOIN ReferenceValue PCAT WITH (NOLOCK) ON PCAT.UID = PD.PYRACATUID
WHERE PB.BillGeneratedDttm >= @from
  AND PB.BillGeneratedDttm <  @to
  AND PB.StatusFlag = 'A'
  AND PB.IsRefund IS NULL
  AND PB.BLCATUID IN (4321, 64739, 4322, 64740)
  AND ISNULL(PB.BLTYPUID, 0) <> 23377
GROUP BY CAST(PB.BillGeneratedDttm AS date),
         CASE WHEN PB.BLCATUID IN (4322, 64740) THEN 'I' ELSE 'O' END,
         CASE WHEN PB.BLTYPUID = 2160 THEN 1 ELSE 0 END,
         PCAT.ValueCode, PCAT.Description
