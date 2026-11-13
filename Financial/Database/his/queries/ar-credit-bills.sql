-- ลูกหนี้สิทธิ (บิลเครดิต) ของ 4 สิทธิ์ตามรายงาน — อ่านอย่างเดียว · 1 แถว = 1 บิล
--   บิลเครดิต = PatientBill.IsPaymentComplete = 'N' (finance.md §5 ข้อ 4) · ตัดบิลคืนเงิน, Free bill (23377), ใช้เฉพาะ OPD 4321/64739 และ IPD 4322/64740
--   ผู้จ่ายของบิล = PatientBill.PatientVisitPayorUID → PatientVisitPayor → PayorDetail (ทุกลำดับสิทธิ์ ไม่เฉพาะ PRIMARY)
--   กรองกลุ่มผู้จ่าย/คำในชื่อเหมือนรายงานตามปีงบ แล้วแอปจัดสิทธิ์ด้วย FiscalReportOptions.SchemeOf()
--   ยอด = SUM(PatientBilledItem.NetAmount) ของบิล · VN/AN = PatientVisitID.Identifier แถว Active ล่าสุด · HN = Patient.PASID (PHI)
-- params: @from, @to (datetime, @to exclusive) ตามวันออกบิล
--         /*PAYOR_CATEGORIES*/ → @c0, @c1, ... · /*PAYOR_NAME_FILTER*/ → OR PD.PayorName LIKE @k0 OR PA.Name LIKE @k0 ...
--         /*EXCLUDED_PAYORS*/ → AND ISNULL(PD.Code, '') NOT IN (@x0, ...) (เช่น PDQSHC001 ส่วนเกินสิทธิ์) หรือว่าง
SELECT
    PB.UID                                                          AS BillUid,
    PB.BillNumber                                                   AS BillNumber,
    PB.BillGeneratedDttm                                            AS BillDate,
    CASE WHEN PB.BLCATUID IN (4322, 64740) THEN 'I' ELSE 'O' END   AS BillSide,
    VENT.ValueCode                                                  AS VisitEncounterCode,
    PV.UID                                                          AS VisitUid,
    VID.Identifier                                                  AS VisitNumber,
    P.PASID                                                         AS Hn,
    PV.StartDTTM                                                    AS VisitStart,
    PV.EndDttm                                                      AS VisitEnd,
    PD.Code                                                         AS PayorCode,
    PD.PayorName                                                    AS PayorName,
    PCAT.ValueCode                                                  AS PayorCategoryCode,
    PA.Name                                                         AS AgreementName,
    CAST(ISNULL(ITEMS.NetAmount, 0) AS DECIMAL(18,2))               AS NetAmount
FROM PatientBill PB WITH (NOLOCK)
JOIN PatientVisit PV WITH (NOLOCK)     ON PV.UID = PB.PatientVisitUID
JOIN Patient P WITH (NOLOCK)           ON P.UID = PV.PatientUID
LEFT JOIN ReferenceValue VENT WITH (NOLOCK) ON VENT.UID = PV.ENTYPUID AND VENT.DomainCode = 'ENTYP'
JOIN PatientVisitPayor VP WITH (NOLOCK) ON VP.UID = PB.PatientVisitPayorUID AND VP.StatusFlag = 'A'
JOIN PayorDetail PD WITH (NOLOCK)      ON PD.UID = VP.PayorUID
LEFT JOIN ReferenceValue PCAT WITH (NOLOCK) ON PCAT.UID = PD.PYRACATUID
LEFT JOIN PayorAgreement PA WITH (NOLOCK)   ON PA.UID = VP.PayorAgreementUID
OUTER APPLY (SELECT SUM(PBI.NetAmount) AS NetAmount
             FROM PatientBilledItem PBI WITH (NOLOCK)
             WHERE PBI.PatientBillUID = PB.UID AND PBI.StatusFlag = 'A') ITEMS
OUTER APPLY (SELECT TOP 1 I.Identifier
             FROM PatientVisitID I WITH (NOLOCK)
             WHERE I.PatientVisitUID = PV.UID AND I.StatusFlag = 'A'
             ORDER BY I.UID DESC) VID
WHERE PB.BillGeneratedDttm >= @from
  AND PB.BillGeneratedDttm <  @to
  AND PB.StatusFlag = 'A'
  AND PB.IsRefund IS NULL
  AND PB.IsPaymentComplete = 'N'
  AND PB.BLCATUID IN (4321, 64739, 4322, 64740)
  AND ISNULL(PB.BLTYPUID, 0) <> 23377
  AND (PCAT.ValueCode IN (/*PAYOR_CATEGORIES*/)/*PAYOR_NAME_FILTER*/)
  /*EXCLUDED_PAYORS*/
