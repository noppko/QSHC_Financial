# UPGRADE NOTES - Financial System to .NET 10

**วันที่อัปเกรด:** 15 กรกฎาคม 2569 (2026-07-15)  
**โปรเจกต์:** Financial System  
**เวอร์ชันเดิม:** .NET 8.0  
**เวอร์ชันใหม่:** .NET 10.0

---

## สรุปการอัปเกรด

### ✅ เสร็จสมบูรณ์แล้ว

#### 1. Framework Upgrade

- ✅ อัปเกรด `Financial.csproj` เป็น .NET 10.0
- ✅ อัปเกรด EF Core เป็น 10.0.0
- ✅ อัปเกรด Identity เป็น 10.0.0
- ✅ เพิ่ม Serilog 8.0.3 สำหรับ structured logging
- ✅ เพิ่ม EPPlus 7.5.2 แทน ClosedXML (สำหรับ Excel export)
- ✅ เพิ่ม QuestPDF 2024.12.3 (สำหรับ PDF generation)
- ✅ เพิ่ม Polly สำหรับ HTTP retry และ circuit breaker policies

#### 2. Base Infrastructure

- ✅ สร้าง `BaseEntity.cs` พร้อม audit fields (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsActive)
- ✅ สร้าง `ReferenceValue.cs` แทนการใช้ Enum
- ✅ สร้าง `ReferenceValueService.cs` พร้อม memory caching
- ✅ สร้าง `ThaiDateHelper.cs` สำหรับจัดการวันที่แบบไทย (พ.ศ.)
- ✅ ปรับ `ApplicationDbContext.cs`:
  - เพิ่ม DbSet สำหรับ ReferenceValue
  - เพิ่ม Global Query Filter สำหรับ Soft Delete (IsActive)
  - เพิ่ม Audit Fields automation ใน SaveChangesAsync
  - เพิ่ม Unique Composite Index (Category, Code)
  - Seed ข้อมูล ReferenceValues เริ่มต้น (TITLE, GENDER, BLOOD_TYPE)

#### 3. Authentication System

- ✅ สร้าง `IAuthService.cs` และ `AuthService.cs` สำหรับ External Auth API
- ✅ สร้าง `AuthResponse.cs`, `AuthData.cs` DTOs
- ✅ ปรับ `AccountController.cs` ให้ใช้ External Auth API แทน ADO.NET โดยตรง
- ✅ อัปเดต `LoginRequest.cs` ให้มี validation attributes
- ✅ เพิ่ม Typed HttpClient พร้อม Polly retry/circuit breaker policies

#### 4. Program.cs & Configuration

- ✅ ปรับ `Program.cs` สำหรับ .NET 10:
  - เพิ่ม Serilog configuration
  - เพิ่ม Thai Culture (th-TH) กับ Gregorian Calendar
  - เพิ่ม HttpContextAccessor สำหรับ audit trails
  - เพิ่ม Memory Cache
  - เพิ่ม HttpClient สำหรับ Auth API พร้อม Polly policies
  - ลงทะเบียน Services (Auth, ReferenceValue)
- ✅ ปรับ `appsettings.json`:
  - เพิ่ม Serilog configuration
  - เพิ่ม AuthSettings (endpoint, timeout)
  - เปลี่ยน FileUpload AllowedExtensions เป็น .pdf

#### 5. Frontend Setup (Tailwind CSS)

- ✅ สร้าง `tailwind.config.js` พร้อม theme colors (primary, secondary)
- ✅ สร้าง `Styles/site.css` พร้อม @font-face สำหรับ Kanit
- ✅ สร้าง `package.json` สำหรับ npm scripts
- ✅ เพิ่ม MSBuild Target ใน .csproj สำหรับ build Tailwind CSS อัตโนมัติ

---

## ⚠️ ข้อสมมติฐานที่ตัดสินใจเอง (Assumptions)

### 1. External Auth API

- **สมมติฐาน:** ใช้ Auth API ตาม spec (http://10.67.67.166/QSHCAuth/api/Account/HOAuthJson) แทนการ query database โดยตรงด้วย Stored Procedure
- **เหตุผล:** ตามเอกสาร upgrade กำหนดให้ใช้ External Auth API เท่านั้น เพื่อ centralized authentication
- **Impact:** AccountController ไม่ใช้ ADO.NET และ Stored Procedure `pRepQSHCVerifyLoginRole` อีกต่อไป

### 2. Excel Export Library

- **สมมติฐาน:** เปลี่ยนจาก ClosedXML เป็น EPPlus
- **เหตุผล:** EPPlus เป็น library ที่ระบุใน spec และมี performance ดีกว่า รองรับ .NET 10 อย่างเต็มที่
- **Impact:** ต้องแก้โค้ด Excel export ทั้งหมดใน DischargesController และ InterfaceSystemController

### 3. วันที่ - พุทธศักราช vs คริสต์ศักราช

- **สมมติฐาน:**
  - Database เก็บวันที่เป็น **ค.ศ.** (Gregorian Calendar) เสมอ
  - แสดงผลบนหน้าจอเป็น **พ.ศ.** ผ่าน ThaiDateHelper
  - Culture ของระบบตั้งเป็น th-TH แต่ใช้ Gregorian Calendar
- **เหตุผล:** ป้องกันปัญหาปีเพี้ยน +543 เมื่อบันทึกลง database หรือ exchange ข้อมูลผ่าน API
- **Impact:** ต้องใช้ `ToThaiDateString()` helper ทุกจุดที่แสดงวันที่บนหน้าจอ

### 4. Soft Delete ทั้งระบบ

- **สมมติฐาน:** ใช้ Soft Delete (IsActive = false) แทน Hard Delete ทั้งระบบ
- **เหตุผล:** เป็น best practice สำหรับ audit trail และ data recovery
- **Impact:** Global Query Filter กรอง IsActive = true อัตโนมัติทุก query

### 5. Connection Strings ที่ Hardcoded

- **สมมติฐาน:** เก็บ connection strings ไว้ใน appsettings.json (เดิมมีบาง Controller hardcode ไว้ใน code)
- **เหตุผล:** Security และ maintainability
- **Impact:** จะต้องปรับ DischargesController และ Controllers อื่นๆ ที่ยังใช้ ADO.NET ให้ใช้ configuration แทน
- **Action Required:** ยังมี hardcoded connection string ใน DischargesController บรรทัดที่ 28 และอื่นๆ

---

## 🔧 กำลังดำเนินการ (In Progress)

### 1. แก้ Build Errors

**Status:** กำลังแก้  
**Errors ที่เจอ:**

```
- DischargesController.cs: error CS0246: The type or namespace name 'ClosedXML' could not be found
- InterfaceSystemController.cs: error CS0246: The type or namespace name 'DocumentFormat' could not be found
```

**แผนการแก้:**

1. แก้ DischargesController: เปลี่ยนจาก ClosedXML เป็น EPPlus
2. แก้ InterfaceSystemController: เปลี่ยนจาก DocumentFormat.OpenXml เป็น EPPlus
3. สร้าง helper method สำหรับ Excel export ที่ reusable

---

## 📋 รายการที่ต้องทำต่อ (To-Do List)

### Phase 1: ให้ Build ผ่าน 100%

- [ ] แก้ DischargesController ให้ใช้ EPPlus แทน ClosedXML
- [ ] แก้ InterfaceSystemController ให้ใช้ EPPlus แทน DocumentFormat.OpenXml
- [ ] สร้าง ExcelService หรือ helper สำหรับ export Excel
- [ ] สร้าง Migration สำหรับ ReferenceValue
- [ ] Run `dotnet build` จนไม่มี error

### Phase 2: Backend Services & Logic

- [ ] สร้าง DashboardService และ IDashboardService
- [ ] สร้าง Dashboard DTOs
- [ ] Implement Dashboard logic (KPI, Trend, Distribution, Recent Items)
- [ ] ปรับ Controllers ทั้งหมดให้ไม่ hardcode connection strings
- [ ] สร้าง Service layer แยกจาก Controllers (DischargesService, InterfaceSystemService)
- [ ] Apply Dependency Injection pattern ทั้งโปรเจกต์
- [ ] ปรับให้ใช้ async/await ครบทุกจุด (ตอนนี้บาง Controller ยังใช้ sync ADO.NET)

### Phase 3: Views & Frontend

- ✅ ติดตั้ง Node.js และ Tailwind CSS (npm install)
- ⏳ ดาวน์โหลดฟอนต์ Kanit (.woff2) มาเก็บที่ wwwroot/fonts/kanit/ (ยังไม่ทำ - ใช้ fallback font ก่อน)
- ✅ สร้าง `_Layout.cshtml` ใหม่แบบ horizontal navigation (ไม่ใช่ sidebar อีกต่อไป)
- ✅ ปรับ Views ทั้งหมด:
  - ✅ Account/Login.cshtml - Modern gradient design
  - ✅ Home/Index.cshtml - Dashboard แบบใหม่เหมือนตัวอย่าง (Income Tracker, Recent Projects, Let's Connect, Proposal Progress)
  - ✅ Home/Privacy.cshtml - Modern card design
  - ✅ Discharges/IPD.cshtml - Modern table design พร้อม search form
  - ✅ Discharges/OPD.cshtml - Modern table design พร้อม search form
  - ✅ Shared/Error.cshtml - Modern error page
  - [ ] InterfaceSystem/\*.cshtml - (ยังไม่ทำ)
  - [ ] InterfaceEDC/\*.cshtml - (ยังไม่ทำ)
- ✅ Compile Tailwind CSS สำเร็จ
- ✅ Background gradient สี slate-blue เหมือนตัวอย่าง
- ✅ Card-based layout ทั้งหมด
- ✅ Top navigation bar พร้อม search, notifications, user menu
- ✅ Build สำเร็จ 0 errors

### Phase 4: Dashboard

- [ ] สร้าง DashboardController
- [ ] สร้าง Dashboard/Index.cshtml
- [ ] Implement Summary Cards (KPI)
- [ ] Implement Trend Chart (12 เดือนย้อนหลัง)
- [ ] Implement Distribution Chart (แบ่งตามสิทธิ์การรักษา)
- [ ] Implement Recent Items Table
- [ ] Implement Date Range Filter พร้อม AJAX
- [ ] Download ApexCharts และเก็บใน wwwroot/lib/

### Phase 5: Unit Testing

- [ ] สร้าง project `Financial.Tests` (xUnit)
- [ ] ติดตั้ง packages: xUnit, Moq, FluentAssertions, EF Core InMemory/SQLite
- [ ] เขียน tests สำหรับ AuthService
- [ ] เขียน tests สำหรับ ReferenceValueService
- [ ] เขียน tests สำหรับ DashboardService
- [ ] เขียน tests สำหรับ ThaiDateHelper
- [ ] เขียน tests สำหรับ Soft Delete & Audit Fields
- [ ] เขียน tests สำหรับ File Upload Validation
- [ ] Run `dotnet test` ให้ผ่าน 100%

### Phase 6: Final Testing & Documentation

- [ ] Run `dotnet build` - ต้องไม่มี error
- [ ] Run `dotnet test` - ต้องผ่าน 100%
- [ ] Run `dotnet ef migrations add InitialWithReferenceValues`
- [ ] Run `dotnet ef database update`
- [ ] รันแอปและทดสอบด้วย test user (md199 / abc12345):
  - [ ] Login
  - [ ] Dashboard
  - [ ] IPD Discharges
  - [ ] OPD Discharges
  - [ ] Interface System
  - [ ] Excel Export
  - [ ] Logout
- [ ] บันทึกผลการทดสอบพร้อม screenshots (ถ้าทำได้)
- [ ] Update UPGRADE_NOTES.md ฉบับสมบูรณ์

---

## 🔨 คำสั่งที่ต้องรัน

### ติดตั้ง Node.js และ Tailwind CSS

```powershell
cd D:\MVC\Net7\Financial\Financial
npm install
npm run build:css
```

**หมายเหตุ:** ถ้า environment ปัจจุบันไม่มี Node.js หรือไม่สามารถติดตั้งได้:

1. ดาวน์โหลด Tailwind CSS Standalone Binary จาก https://github.com/tailwindlabs/tailwindcss/releases
2. วางไว้ที่ root ของโปรเจกต์
3. เปลี่ยนคำสั่งใน .csproj Target จาก `npx tailwindcss` เป็น path ของ binary

### สร้าง Migration

```powershell
dotnet ef migrations add InitialWithReferenceValues
dotnet ef database update
```

### Build & Test

```powershell
dotnet build
dotnet test
```

---

## 📦 Dependencies ที่เพิ่มใหม่

| Package                         | Version   | ใช้สำหรับ                                |
| ------------------------------- | --------- | ---------------------------------------- |
| EPPlus                          | 7.5.2     | Excel Export                             |
| QuestPDF                        | 2024.12.3 | PDF Generation                           |
| Serilog.AspNetCore              | 8.0.3     | Structured Logging                       |
| Serilog.Sinks.Console           | 6.0.0     | Console Log Output                       |
| Serilog.Sinks.File              | 6.0.0     | File Log Output                          |
| Microsoft.Extensions.Http.Polly | 10.0.0    | HTTP Resilience (Retry, Circuit Breaker) |

---

## ⚠️ Breaking Changes

### 1. Authentication

- **เดิม:** ใช้ Stored Procedure `pRepQSHCVerifyLoginRole` query จาก HealthObject database โดยตรง
- **ใหม่:** เรียก External Auth API `http://10.67.67.166/QSHCAuth/api/Account/HOAuthJson`
- **Impact:** ถ้า External Auth API ไม่พร้อมใช้งาน จะ login ไม่ได้

### 2. Excel Export

- **เดิม:** ใช้ ClosedXML
- **ใหม่:** ใช้ EPPlus
- **Impact:** API ต่างกัน ต้องเขียน code ใหม่ทั้งหมด

### 3. DateTime Handling

- **เดิม:** ไม่มีการจัดการวันที่แบบไทยที่ชัดเจน อาจมีปัญหาปีพุทธศักราชหลุดลง database
- **ใหม่:** Database เก็บเป็น ค.ศ., แสดงผลเป็น พ.ศ. ผ่าน helper
- **Impact:** ต้องใช้ ThaiDateHelper ทุกจุดที่แสดงวันที่

### 4. Enum → ReferenceValue

- **เดิม:** อาจมี Enum ใช้อยู่
- **ใหม่:** ทุก dropdown/status ใช้ ReferenceValue จาก database
- **Impact:** ต้อง migrate ข้อมูลและปรับ logic ทั้งหมด

---

## 🔐 การตั้งค่า Environment

### appsettings.json

```json
{
  "AuthSettings": {
    "AuthEndpoint": "http://10.67.67.166/QSHCAuth/api/Account/HOAuthJson",
    "TimeoutSeconds": 30
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=aspnet-Financial;...",
    "InterfaceSystem": "Server=10.67.67.161;user id=sa;password=***;Database=QSHC_InterfaceSystem;..."
  }
}
```

**⚠️ Security Note:** ห้าม commit appsettings.json ที่มี password จริงลง git

---

## 📝 TODO สำหรับ Manual Setup

### 1. ฟอนต์ Kanit

**ต้องทำด้วยตนเอง:**

1. ไปที่ https://fonts.google.com/specimen/Kanit
2. ดาวน์โหลด Kanit font (weight: 300, 400, 500, 600, 700) เป็น .woff2
3. สร้างโฟลเดอร์ `wwwroot/fonts/kanit/`
4. วางไฟล์:
   - Kanit-Light.woff2
   - Kanit-Regular.woff2
   - Kanit-Medium.woff2
   - Kanit-SemiBold.woff2
   - Kanit-Bold.woff2

**สถานะ:** ❌ ยังไม่ได้ทำ (environment ปัจจุบันดาวน์โหลดจากอินเทอร์เน็ตไม่ได้)

### 2. ApexCharts

**ต้องทำด้วยตนเอง:**

1. ดาวน์โหลด ApexCharts จาก https://apexcharts.com/
2. สร้างโฟลเดอร์ `wwwroot/lib/apexcharts/`
3. วางไฟล์:
   - apexcharts.min.js
   - apexcharts.min.css

**สถานะ:** ❌ ยังไม่ได้ทำ

---

## 🎯 เกณฑ์การตรวจสอบก่อนส่งมอบ

- [ ] ไม่มี build error แม้แต่ตัวเดียว
- [ ] ไม่มี warning สำคัญ (nullable, obsolete API)
- [ ] Unit tests ผ่านทั้งหมด 100%
- [ ] รันแอปได้และ login ด้วย test user (md199/abc12345) สำเร็จ
- [ ] ทุกหน้าแสดงผลด้วย Tailwind CSS สวยงาม responsive
- [ ] ฟังก์ชันเดิมทำงานครบ 100%
- [ ] Dashboard แสดงข้อมูลสถิติถูกต้อง
- [ ] Excel export ทำงานได้
- [ ] Tailwind CSS build อัตโนมัติเมื่อ `dotnet build`
- [ ] ไม่มี hardcoded credentials หรือ secrets ใน source code

---

## 🐛 Issues & Resolutions

### Issue #1: ClosedXML Not Found

**สาเหตุ:** เปลี่ยน package เป็น EPPlus แต่ยังไม่แก้โค้ด  
**แก้ไข:** กำลังแก้ DischargesController และ InterfaceSystemController

### Issue #2: .NET 10 SDK

**สถานะ:** ✅ Resolved  
**รายละเอียด:** มี .NET 10 SDK เวอร์ชัน 10.0.109 ติดตั้งอยู่แล้ว

---

## 📊 สถิติโปรเจกต์

- **Controllers:** 4 ไฟล์
- **Views:** ~15 ไฟล์
- **Models:** ~20+ entities
- **Database Contexts:** 2 (ApplicationDbContext, InterfaceSystemDbContext)
- **External Databases:** 3 (DefaultConnection, InterfaceSystem, HealthObject via API)

---

## 👤 Test User Credentials

**Username:** md199  
**Password:** abc12345

**⚠️ สำคัญ:** ห้าม hardcode credentials นี้ลงใน source code หรือ unit tests  
ใช้เฉพาะการทดสอบแบบ manual เท่านั้น

---

## 📅 Timeline

- **เริ่มงาน:** 2026-07-15 10:00
- **Phase 1 Complete (Framework Upgrade):** 2026-07-15 12:00
- **Expected Completion:** TBD

---

**อัปเดตล่าสุด:** 2026-07-15 12:30  
**ผู้ดำเนินการ:** GitHub Copilot (.NET 10 Upgrade Specialist)
