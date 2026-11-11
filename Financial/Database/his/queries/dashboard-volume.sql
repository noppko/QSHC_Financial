-- Dashboard: ปริมาณผู้ป่วย — นับรวม ไม่มี PHI
--   ENTYP 1705 OUPAT = OPD, 1379 INPAT = IPD (codes.md, ipd.md)
--   ผู้ป่วยในขณะนี้ = IPD StatusFlag='A' และ EndDttm IS NULL (ipd.md: ตรวจแล้ว 87 ราย 2026-10-16)
-- params: @today, @tomorrow (datetime)
-- ตรวจ: node tools/query.mjs -f <ไฟล์นี้> --param today=2026-10-22 --param tomorrow=2026-10-23
SELECT
    (SELECT COUNT(*) FROM PatientVisit WITH (NOLOCK)
      WHERE ENTYPUID = 1705 AND StatusFlag = 'A' AND StartDTTM >= @today AND StartDTTM < @tomorrow) AS OpdVisitsToday,
    (SELECT COUNT(*) FROM PatientVisit WITH (NOLOCK)
      WHERE ENTYPUID = 1379 AND StatusFlag = 'A' AND EndDttm IS NULL)                              AS IpdAdmittedNow,
    (SELECT COUNT(*) FROM PatientVisit WITH (NOLOCK)
      WHERE ENTYPUID = 1379 AND StatusFlag = 'A' AND EndDttm >= @today AND EndDttm < @tomorrow)     AS IpdDischargesToday
