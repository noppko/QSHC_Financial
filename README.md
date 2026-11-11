# QSHC Financial

ระบบการเงิน ศูนย์หัวใจสิริกิติ์ฯ — ASP.NET Core 10 MVC

- **Dashboard การเงิน**: รายได้ OPD/IPD, สิทธิการรักษา, ลูกหนี้/เคลม, ปริมาณผู้ป่วย และ **3D Digital Twin** ของเงินที่รอชำระ แยก OPD/IPD
- **Discharge IPD/OPD**: ค้นหาตามช่วงวันที่ และส่งออก Excel
- **Interface EDC**: นำเข้าไฟล์ EDC และดูข้อมูลลงทะเบียน

## ใช้งานบน Intranet (ไม่ต้องมี Internet)

ทุก asset อยู่ใน `wwwroot` และถูก commit ไว้แล้ว ไม่มีการโหลดจาก CDN:

| สิ่งที่ใช้ | ตำแหน่ง |
| --- | --- |
| Tailwind CSS (build แล้ว) | `wwwroot/css/site.css` |
| ฟอนต์ Kanit (ไทย+อังกฤษ, 300–700) | `wwwroot/fonts/kanit/` |
| Chart.js 4 | `wwwroot/lib/chartjs/` |
| Three.js (3D) | `wwwroot/lib/three/` |

Integration test `LoginPage_WorksOffline_NoExternalAssetsAndNoTestCredentials` จะล้มเหลวถ้ามีใครเพิ่มลิงก์ CDN เข้ามา

## พัฒนา UI (Tailwind CSS v4 CLI)

ต้องมี Node.js และ Internet เฉพาะตอน `npm install` ครั้งแรก (หรือใช้ npm mirror ภายใน)

```bash
cd Financial
npm install
npm run watch:css      # build CSS อัตโนมัติขณะแก้ไข view
npm run copy:vendor    # คัดลอก Chart.js / Three.js / ฟอนต์ Kanit จาก node_modules ไป wwwroot
```

- ไฟล์ต้นฉบับ CSS: `Styles/site.css` (design tokens อยู่ใน `@theme`)
- `dotnet build` จะรัน `npm run build:css` ให้อัตโนมัติ **เฉพาะเครื่องที่มี `node_modules`** — เครื่องอื่น (build server, Docker) ใช้ `site.css` ที่ commit ไว้
- แก้ view แล้วต้อง build CSS ใหม่และ commit `wwwroot/css/site.css` ด้วย

## Authentication

- Login ผ่าน External Auth API (`AuthSettings:AuthEndpoint`) แล้วเก็บ session ใน cookie `Financial-Auth` (HttpOnly, SameSite=Lax, 8 ชั่วโมง)
- **ทุกหน้าต้อง Login** (fallback policy) ยกเว้นหน้าที่ระบุ `[AllowAnonymous]`
- ทุก POST ตรวจ Antiforgery token — `fetch` ส่งผ่าน header `RequestVerificationToken` (ดู `window.app.request` ใน `wwwroot/js/site.js`)
- จำกัดการ Login 10 ครั้ง/นาที/IP (`Security:LoginAttemptsPerMinute`)
- เรียกแบบ AJAX ขณะ session หมดอายุจะได้ 401 แทนหน้า HTML (ไม่มีสิทธิ์ได้ 403)

### สิทธิ์ตาม Role (`appsettings.json` → `Authorization`)

Role มาจาก `data.roles[]` ของ Auth API (เทียบแบบไม่สนตัวพิมพ์เล็ก/ใหญ่)

| ระดับ | เงื่อนไข | Dashboard | Discharge / Interface EDC | ตั้งค่าระบบ (`/Admin`) |
| --- | --- | --- | --- | --- |
| ผู้ดูแลระบบ | role อยู่ใน `AdminRoles` (ค่าเริ่มต้น `Admin`) | ✔ | ✔ | ✔ |
| เจ้าหน้าที่การเงิน | role ขึ้นต้นด้วย `FinanceRolePrefixes` (ค่าเริ่มต้น `Cashier`, `Cashire`) | ✔ | ✔ | – |
| ผู้ใช้งานทั่วไป | Login ได้ แต่ไม่มี role ข้างต้น | ✔ | – | – |

- เมนูและหน้าแผนผังระบบแสดงเฉพาะหน้าที่มีสิทธิ์ (ใช้ policy เดียวกับ controller)
- เมนูผู้ใช้ (มุมขวาบน) แสดง claim จาก API: ชื่อผู้ใช้, ตำแหน่ง, หน่วยงาน, เพศ, อีเมล, role และระดับสิทธิ์
- หน้า "ตั้งค่าระบบ": ดูการตั้งค่า (ไม่แสดงรหัสผ่าน), ทดสอบการเชื่อมต่อ Auth API / ฐานข้อมูล (เปิด connection อย่างเดียว), ล้าง cache
- **เปิด/ปิดการแสดงกลุ่มเมนู** (หน้า "ตั้งค่าระบบ"): มีผลกับผู้ใช้ทุกคนทันที ไม่ต้อง restart
  - เก็บที่ตาราง `AppSettings` (key `MENU_HIDDEN_GROUPS`) ในฐาน QSHC_Financial — ถ้าฐานใช้งานไม่ได้ จะแสดงเมนูทั้งหมด
  - ซ่อนจากเมนู/แผนผังระบบเท่านั้น สิทธิ์เข้าหน้ายังเป็นไปตาม role
  - กลุ่ม "ผู้ดูแลระบบ" ซ่อนไม่ได้ (กัน Admin หาหน้าตั้งค่าไม่เจอ)

## ฐานข้อมูล

| Connection string | สิทธิ์ | ใช้กับ |
| --- | --- | --- |
| `DefaultConnection` — **QSHC_Financial (10.67.67.162)** | อ่าน/เขียน | ข้อมูลของระบบ Financial เอง: `AppSettings`, `NhsoFiscalSummaries`, `ReferenceValues` |
| `HealthObject` (10.67.67.64) — HIS | **อ่านอย่างเดียว** (ห้ามสร้างตาราง/SP) | Discharge IPD/OPD, Dashboard การเงิน, ดึงตัวเลขรายงาน สปสช. |
| `InterfaceSystem` (10.67.67.161) | อ่าน/เขียน | Interface EDC |

### QSHC_Financial (Entity Framework Core)

- ทุก entity inherit `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`, `IsActive` soft delete — query filter อัตโนมัติ) — เวลาเป็น `DateTime.Now` (เวลาไทย) ห้ามใช้ UtcNow
- ไม่ใช้ enum — ใช้ตาราง `ReferenceValues` (unique `Category + Code`) เช่น `PATIENT_TYPE` (O/I), `ACCESS_LEVEL` (ADMIN/FINANCE/GENERAL), `TITLE`, `GENDER`
- สร้าง/อัปเดตตารางอัตโนมัติเมื่อเปิดแอป (`Database:AutoMigrate = true`) หรือให้ DBA รัน [Financial/Database/financial/QSHC_Financial-migrations.sql](Financial/Database/financial/QSHC_Financial-migrations.sql) (รันซ้ำได้)
- เพิ่มตาราง: แก้ entity → `dotnet ef migrations add <ชื่อ> --configuration Release` (ใช้ design-time factory ไม่เชื่อมต่อฐาน)

> **ความปลอดภัย:** ควรย้ายรหัสผ่านฐานข้อมูลออกจาก `appsettings.json` ไปไว้ใน User Secrets / Environment Variables และใช้ user ที่มีสิทธิ์อ่านอย่างเดียวแทน `sa`

### Dashboard การเงินจาก HIS

`appsettings.json` → `Dashboard:Source`: `"His"` (ค่าเริ่มต้น, ใช้ `ConnectionStrings:HealthObject`) หรือ `"Sample"` (ข้อมูลตัวอย่าง สำหรับทดสอบหน้าจอนอกเครือข่าย)

นิยามข้อมูลอ้างอิง [Financial/Database/his/finance.md](Financial/Database/his/finance.md) — SQL อยู่ใน `Financial/Database/his/queries/` (ฝังใน assembly)

| ส่วนบนหน้าจอ | SQL | นิยาม | cache |
| --- | --- | --- | --- |
| รายได้วันนี้/เดือนนี้/30 วัน, ตามกลุ่มผู้จ่าย, เงินสด/เครดิต | `dashboard-revenue-daily.sql` | วันออกบิล, `SUM(PatientBilledItem.NetAmount)`, ไม่รวมบิลคืนเงินและ Free bill | ย้อนหลัง 30 นาที, วันนี้ 60 วินาที |
| เงินรับวันนี้ตามวิธีชำระ | `dashboard-collections.sql` | วันชำระ, หักคืนเงิน | 60 วินาที |
| ปริมาณผู้ป่วย | `dashboard-volume.sql` | OPD visit วันนี้, IPD ที่ยังนอนอยู่, จำหน่ายวันนี้ | 60 วินาที |
| ผู้ป่วยนอกรอชำระ (3D) | `dashboard-pending-opd.sql` | มี `CHKOUT` วันนี้ ยังไม่มี `FINDIS`/`PHADIS` ภายใน 8 ชม. | 60 วินาที |
| ผู้ป่วยในรอปิดบัญชี (3D) | `dashboard-pending-ipd.sql` | IPD ที่มีคำสั่งจำหน่าย/Medical Discharge แต่ยังไม่ `FINDIS` | 60 วินาที |

- ทุกไฟล์เป็น `SELECT`/`WITH` เดียว + `NOLOCK` — ตรวจกับฐานได้ด้วย `node tools/query.mjs -f <ไฟล์> --param ...` ในโปรเจกต์ HO63Database
- Dashboard ไม่แสดง HN/ชื่อผู้ป่วย — ใช้รหัสอ้างอิง 4 หลักท้ายของ VisitUid
- ส่วนใดดึงข้อมูลไม่ได้ จะแสดงแถบแจ้งเตือน ส่วนอื่นยังใช้งานได้
- **ลูกหนี้คงค้าง/อายุลูกหนี้/สถานะเคลม ยังแสดงไม่ได้** เพราะยอดรับชำระจากผู้จ่ายสิทธิไม่อยู่ใน HealthObject (finance.md §8) — แสดง "ยอดตั้งลูกหนี้สิทธิ (บิลเครดิต)" แทน

### รายงานสิทธิ สปสช. ตามปีงบประมาณ (`/Reports/NhsoFiscal`, role Admin/Cashier*)

ตารางแบบ ในเขต / นอกเขต / รวม × จำนวน (ราย), จำนวนเงิน, ร้อยละ แยก OPD/IPD พร้อมส่งออก Excel — ตั้งค่าที่ `Reports:Nhso`

| ค่า | ความหมาย |
| --- | --- |
| `Source` | `Summary` (ค่าเริ่มต้น) = อ่านผลที่ประมวลผลไว้แล้วจาก `NhsoFiscalSummaries` (เร็ว), `Query` = คำนวณจาก HIS ทุกครั้ง (ช้า ~นาที/ปีงบ) |
| `PayorCategoryCodes` | กลุ่มผู้จ่ายที่นับเป็น สปสช. (A สปสช, E สปสช.เหมาจ่าย, M สปสช.เฉพาะ) |
| `OutOfZoneKeywords` | เงื่อนไขการเรียกเก็บ (`PayorAgreement.Name`) ที่มีคำนี้ = นอกเขต (ค่าเริ่มต้น `นอก`); มีคำว่า "ใน" หรือไม่มีทั้งสองคำ = ในเขต — เปลี่ยนแล้ว restart แอป ไม่ต้องประมวลผลใหม่ |
| `OutOfZoneIgnorePhrases` | วลี (Regex) ที่ตัดออกก่อนตรวจคำว่า "นอก" — ค่าเริ่มต้น `ผู้ป่วยนอก` (ที่ไม่ได้ตามด้วย เขต/จังหวัด/ขอนแก่น) และ `นอกเวลา` |
| `PatientTypeFromAgreementName` | `true` = OPD/IPD ตาม **ประเภทของสิทธิ์** (`PayorAgreement.ENTYPUID` → ReferenceValue โดเมน `ENTYP` ใน HIS แล้วจับคู่ด้วย `AgreementEncounterTypes`: OUPAT→O, INPAT→I, AEPAT(ER)→O) → ถ้าไม่ระบุ ดูคำ `IpdAgreementKeywords`/`OpdAgreementKeywords` ในชื่อ → ถ้าไม่ระบุ ใช้ประเภทบิล; `false` = ใช้ประเภทบิลอย่างเดียว |
| `AutoRefreshEnabled` / `AutoRefreshHour` | ประมวลผลอัตโนมัติทุกคืน (ค่าเริ่มต้น 02:00 น.) — ปีงบปัจจุบัน และปีงบก่อนในช่วง ต.ค.–ธ.ค. |

การประมวลผล: แอปดึงตัวเลขรวมจาก HIS (SELECT อย่างเดียว — `report-nhso-fiscal.sql`) แล้วบันทึกลงตาราง `NhsoFiscalSummaries` ใน QSHC_Financial ทำงานเบื้องหลัง ไม่ต้องสร้างอะไรบน HIS
- ปีงบย้อนหลัง: ผู้ดูแลระบบกด "ประมวลผล" ทีละปีที่หน้า **ตั้งค่าระบบ → ประมวลผลรายงาน สปสช.** (ครั้งแรกเท่านั้น)

## Tests

```bash
dotnet test Financial.Tests
```

ครอบคลุม Authentication (controller, claims, open redirect, antiforgery, full login/logout flow), การตรวจช่วงวันที่, การ map ข้อมูลจาก Stored Procedure, การป้องกัน path traversal ตอนอัปโหลด และการใช้งานแบบ offline

> ถ้า Visual Studio/IIS Express ยังรันแอปอยู่ จะ build ไม่ผ่านเพราะ `Financial.dll` ถูกล็อก — หยุด debug ก่อน
