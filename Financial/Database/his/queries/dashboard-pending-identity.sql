-- ADMIN ONLY (มีข้อมูลผู้ป่วย — PHI): HN และชื่อ-สกุลของ visit ที่รอชำระเงินบน Dashboard
-- ใช้เฉพาะผู้ใช้ role Admin (บังคับที่ HomeController) และบันทึกการเข้าถึงใน log ทุกครั้ง
-- ห้ามเรียกจาก endpoint สาธารณะ · คอลัมน์ตรงกับ HO63 admin queries (Patient.PASID = HN)
-- placeholder VISIT_UIDS ในวงเล็บ IN ถูกแทนด้วยพารามิเตอร์ @u0,@u1,... โดยแอป (ไม่ต่อสตริงจากค่าที่รับมา)
-- ผู้ป่วย Patient.IsAnonymous = 'Y' แอปจะแสดงเป็น "ผู้ป่วยปกปิดชื่อ" และไม่แสดง HN
SELECT v.UID                AS VisitUid,
       p.PASID              AS Hn,
       tt.Description       AS Title,
       p.Forename           AS FirstName,
       p.Surname            AS LastName,
       p.IsAnonymous        AS Anon
FROM PatientVisit v WITH (NOLOCK)
JOIN Patient p WITH (NOLOCK) ON p.UID = v.PatientUID
LEFT JOIN ReferenceValue tt WITH (NOLOCK) ON tt.UID = p.TITLEUID
WHERE v.UID IN (/*VISIT_UIDS*/)
