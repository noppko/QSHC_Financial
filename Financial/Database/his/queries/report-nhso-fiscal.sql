-- รายงานสิทธิ สปสช. ตามปีงบประมาณ แยก OPD/IPD × เงื่อนไขการเรียกเก็บ — ตัวเลขรวม ไม่มี PHI
--   สิทธิ์หลัก = PatientVisitPayor ลำดับ PRIMARY (PAYRTP 13838) ที่บิลเรียกเก็บ (PatientBill.PatientVisitPayorUID)
--   กลุ่มผู้จ่าย สปสช. = PayorDetail.PYRACATUID → A สปสช, E สปสช.(เหมาจ่าย), M สปสช(เฉพาะ) — แอปเลือกกลุ่มที่นับจาก config
--   ประเภทผู้ป่วยของสิทธิ์ = PayorAgreement.ENTYPUID → ReferenceValue (DomainCode ENTYP): OUPAT / INPAT / AEPAT
--     คืนรหัสดิบ — แอปจับคู่เป็น O/I จาก config (Reports:Nhso:AgreementEncounterTypes) และใช้ก่อนประเภทบิล
--   เงื่อนไขการเรียกเก็บ = PayorAgreement.Name (เช่น "ค่าใช้จ่ายปกตินอกจังหวัดขอนแก่น") — แอปจัดเป็นในเขต/นอกเขต จาก config
--   จำนวนเงิน = SUM(PatientBilledItem.NetAmount) ของบิลที่เรียกเก็บกับสิทธิ์นั้น (finance.md §5)
--   ตัดบิลคืนเงินและ Free bill, ใช้เฉพาะบิล OPD (4321/64739) และ IPD (4322/64740)
-- params: @from, @to (datetime, @to exclusive) — 1 ปีงบประมาณ = 1 ต.ค. ถึง 1 ต.ค. ปีถัดไป
-- ตรวจ: node tools/query.mjs -f <ไฟล์นี้> --param from=2024-10-01 --param to=2025-10-01
SELECT
    CASE WHEN PB.BLCATUID IN (4322, 64740) THEN 'I' ELSE 'O' END   AS Side,
    PCAT.ValueCode                                                  AS PayorCategoryCode,
    ISNULL(PA.Name, N'(ไม่ระบุ)')                                    AS AgreementName,
    AENT.ValueCode                                                  AS AgreementEncounterCode,
    COUNT(DISTINCT PB.PatientVisitUID)                              AS Visits,
    COUNT(DISTINCT PB.UID)                                          AS Bills,
    CAST(SUM(PBI.NetAmount) AS DECIMAL(18,2))                       AS NetAmount
FROM PatientBill PB WITH (NOLOCK)
JOIN PatientVisitPayor VP WITH (NOLOCK)
  ON VP.UID = PB.PatientVisitPayorUID
 AND VP.PAYRTPUID = 13838
 AND VP.StatusFlag = 'A'
JOIN PayorDetail PD WITH (NOLOCK)      ON PD.UID = VP.PayorUID
JOIN ReferenceValue PCAT WITH (NOLOCK) ON PCAT.UID = PD.PYRACATUID
LEFT JOIN PayorAgreement PA WITH (NOLOCK) ON PA.UID = VP.PayorAgreementUID
LEFT JOIN ReferenceValue AENT WITH (NOLOCK) ON AENT.UID = PA.ENTYPUID AND AENT.DomainCode = 'ENTYP'
JOIN PatientBilledItem PBI WITH (NOLOCK)
  ON PBI.PatientBillUID = PB.UID
 AND PBI.StatusFlag = 'A'
WHERE PB.BillGeneratedDttm >= @from
  AND PB.BillGeneratedDttm <  @to
  AND PB.StatusFlag = 'A'
  AND PB.IsRefund IS NULL
  AND PB.BLCATUID IN (4321, 64739, 4322, 64740)
  AND ISNULL(PB.BLTYPUID, 0) <> 23377
  AND PCAT.ValueCode IN ('A', 'E', 'M')
GROUP BY CASE WHEN PB.BLCATUID IN (4322, 64740) THEN 'I' ELSE 'O' END,
         PCAT.ValueCode,
         PA.Name,
         AENT.ValueCode
