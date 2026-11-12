-- รายงานสิทธิ์ตามปีงบประมาณ (สปสช. / กรมบัญชีกลาง / ประกันสังคม / อปท.) — อ่านอย่างเดียว
--   1 แถว = ประเภทบิล × ประเภท visit × กลุ่มผู้จ่าย × เงื่อนไขการเรียกเก็บ × ผู้ป่วย
--   แอปใช้ PatientUid นับ "ราย" แบบคนไม่ซ้ำ ในหน่วยความจำเท่านั้น — บันทึกลง QSHC_Financial เป็นตัวเลขรวม (ไม่มี PHI/HN)
--   ประเภทผู้ป่วย = ประเภท visit: PatientVisit.ENTYPUID → ReferenceValue ENTYP (OUPAT = VN/OPD, INPAT = AN/IPD, AEPAT = ER)
--     ไม่มีประเภท visit → แอปใช้ประเภทบิล (BillSide) แทน
--   สิทธิ์หลัก = PatientVisitPayor ลำดับ PRIMARY (PAYRTP 13838) ที่บิลเรียกเก็บ (PatientBill.PatientVisitPayorUID)
--   กลุ่มผู้จ่าย = PayorDetail.PYRACATUID: A สปสช, B/V กรมบัญชีกลาง, C/N/O ประกันสังคม (แอปส่งรายการจาก Reports:Fiscal:Schemes)
--   + ผู้จ่าย/เงื่อนไขที่ชื่อมีคำตาม PayorNameKeywords (เช่น อปท. = ผู้จ่าย "สปสช(อปท)") — แอปจัดสิทธิ์ตามชื่อก่อนกลุ่มผู้จ่าย
--   เงื่อนไขการเรียกเก็บ = PayorAgreement.Name — แอปจัดเป็นในเขต/นอกเขต จาก config
--   จำนวนเงิน = SUM(PatientBilledItem.NetAmount) ของบิลที่เรียกเก็บกับสิทธิ์นั้น (finance.md §5)
--   ตัดบิลคืนเงินและ Free bill, ใช้เฉพาะบิล OPD (4321/64739) และ IPD (4322/64740)
-- params: @from, @to (datetime, @to exclusive) — 1 ปีงบประมาณ = 1 ต.ค. ถึง 1 ต.ค. ปีถัดไป
--         /*PAYOR_CATEGORIES*/ = แอปแทนด้วย @c0, @c1, ... (รหัส PYRACAT)
--         /*PAYOR_NAME_FILTER*/ = แอปแทนด้วย OR PD.PayorName LIKE @k0 OR PA.Name LIKE @k0 ... (ว่างถ้าไม่มีคำ)
SELECT
    CASE WHEN PB.BLCATUID IN (4322, 64740) THEN 'I' ELSE 'O' END   AS BillSide,
    VENT.ValueCode                                                  AS VisitEncounterCode,
    PCAT.ValueCode                                                  AS PayorCategoryCode,
    ISNULL(PA.Name, N'(ไม่ระบุ)')                                    AS AgreementName,
    PD.PayorName                                                    AS PayorName,
    PV.PatientUID                                                   AS PatientUid,
    COUNT(DISTINCT PB.PatientVisitUID)                              AS Visits,
    COUNT(DISTINCT PB.UID)                                          AS Bills,
    CAST(SUM(PBI.NetAmount) AS DECIMAL(18,2))                       AS NetAmount
FROM PatientBill PB WITH (NOLOCK)
JOIN PatientVisit PV WITH (NOLOCK)     ON PV.UID = PB.PatientVisitUID
LEFT JOIN ReferenceValue VENT WITH (NOLOCK) ON VENT.UID = PV.ENTYPUID AND VENT.DomainCode = 'ENTYP'
JOIN PatientVisitPayor VP WITH (NOLOCK)
  ON VP.UID = PB.PatientVisitPayorUID
 AND VP.PAYRTPUID = 13838
 AND VP.StatusFlag = 'A'
JOIN PayorDetail PD WITH (NOLOCK)      ON PD.UID = VP.PayorUID
LEFT JOIN ReferenceValue PCAT WITH (NOLOCK) ON PCAT.UID = PD.PYRACATUID
LEFT JOIN PayorAgreement PA WITH (NOLOCK) ON PA.UID = VP.PayorAgreementUID
JOIN PatientBilledItem PBI WITH (NOLOCK)
  ON PBI.PatientBillUID = PB.UID
 AND PBI.StatusFlag = 'A'
WHERE PB.BillGeneratedDttm >= @from
  AND PB.BillGeneratedDttm <  @to
  AND PB.StatusFlag = 'A'
  AND PB.IsRefund IS NULL
  AND PB.BLCATUID IN (4321, 64739, 4322, 64740)
  AND ISNULL(PB.BLTYPUID, 0) <> 23377
  AND (PCAT.ValueCode IN (/*PAYOR_CATEGORIES*/)/*PAYOR_NAME_FILTER*/)
GROUP BY CASE WHEN PB.BLCATUID IN (4322, 64740) THEN 'I' ELSE 'O' END,
         VENT.ValueCode,
         PCAT.ValueCode,
         PA.Name,
         PD.PayorName,
         PV.PatientUID
