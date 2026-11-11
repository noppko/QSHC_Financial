-- Dashboard: เงินรับตามวิธีชำระ (ตามวันชำระ) — ตัวเลขรวม ไม่มี PHI
-- อ้างอิง finance-cash-collection.sql (ทดสอบแล้ว 2026-10-22)
--   แถวคืนเงิน IsRefund='Y' มียอดเป็นบวก → ต้องลบเอง; มัดจำ IsAdvance='Y' แยกคอลัมน์
-- params: @from, @to (datetime, @to exclusive)
-- ตรวจ: node tools/query.mjs -f <ไฟล์นี้> --param from=2026-10-21 --param to=2026-10-22
SELECT
    ISNULL(PAYMD.ValueCode, '?')                                                                                   AS PayMode,
    SUM(CASE WHEN ISNULL(PPD.IsRefund, 'N') <> 'Y' AND PPD.IsAdvance IS NULL THEN 1 ELSE 0 END)                    AS Receipts,
    CAST(SUM(CASE WHEN ISNULL(PPD.IsRefund, 'N') <> 'Y' AND PPD.IsAdvance IS NULL THEN PPD.Amount ELSE 0 END) AS DECIMAL(18,2)) AS Received,
    CAST(SUM(CASE WHEN PPD.IsRefund = 'Y' THEN PPD.Amount ELSE 0 END) AS DECIMAL(18,2))                             AS Refunded,
    CAST(SUM(CASE WHEN PPD.IsAdvance = 'Y' AND ISNULL(PPD.IsRefund, 'N') <> 'Y' THEN PPD.Amount ELSE 0 END) AS DECIMAL(18,2)) AS Deposit,
    CAST(SUM(CASE WHEN PPD.IsRefund = 'Y' THEN -PPD.Amount ELSE PPD.Amount END) AS DECIMAL(18,2))                  AS NetCollected
FROM PatientPaymentDetail PPD WITH (NOLOCK)
LEFT JOIN ReferenceValue PAYMD WITH (NOLOCK) ON PAYMD.UID = PPD.PAYMDUID
WHERE PPD.PaidDttm >= @from
  AND PPD.PaidDttm <  @to
  AND PPD.StatusFlag = 'A'
GROUP BY PAYMD.ValueCode
