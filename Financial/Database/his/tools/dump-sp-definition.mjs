// ดึงนิยาม (โค้ด) ของ Stored Procedure / View / Function จาก HIS มาเก็บเป็นไฟล์ .sql เพื่ออ่านตรวจเงื่อนไข
// ใช้ guard ของโปรเจกต์ HO63Database (SELECT อย่างเดียว, rollback เสมอ) — ไม่ EXEC และไม่อ่านข้อมูลผู้ป่วย
//
// วิธีใช้ (ต้องรันในโฟลเดอร์ HO63Database เพื่อให้อ่าน .env ได้):
//   cd D:\AI\HO63Database
//   node D:\MVC\Net7\Financial\Financial\Database\his\tools\dump-sp-definition.mjs pGetQSHCPatientDischargeOPD pGetQSHCPatientDischarge
// ผลลัพธ์: D:\MVC\Net7\Financial\Financial\Database\his\sp\<ชื่อ>.sql
import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const HO63_DB = process.env.HO63_DB_MODULE ?? "D:/AI/HO63Database/tools/db.mjs";
const { query, close } = await import(pathToFileURL(HO63_DB).href);

const names = process.argv.slice(2);
if (names.length === 0) {
  console.error("ระบุชื่อ object อย่างน้อย 1 ชื่อ เช่น pGetQSHCPatientDischargeOPD");
  process.exit(1);
}

const outDir = join(dirname(fileURLToPath(import.meta.url)), "..", "sp");
mkdirSync(outDir, { recursive: true });

try {
  for (const name of names) {
    if (!/^[A-Za-z0-9_]+$/.test(name)) throw new Error(`ชื่อไม่ถูกต้อง: ${name}`);
    const rows = await query(
      "SELECT o.type_desc AS TypeDesc, o.modify_date AS ModifyDate, m.definition AS Definition " +
      "FROM sys.objects o JOIN sys.sql_modules m ON m.object_id = o.object_id WHERE o.name = @name",
      { name });
    if (rows.length === 0) { console.warn(`ไม่พบ ${name}`); continue; }

    const { TypeDesc, ModifyDate, Definition } = rows[0];
    const header = `-- ${name} (${TypeDesc}) แก้ไขล่าสุด ${new Date(ModifyDate).toLocaleString("th-TH")}\n-- ดึงจาก HIS HealthObject ด้วย dump-sp-definition.mjs (อ่านอย่างเดียว)\n\n`;
    const file = join(outDir, `${name}.sql`);
    writeFileSync(file, header + Definition.replace(/\r?\n/g, "\n"));
    console.log(`บันทึก ${file} (${Definition.length.toLocaleString()} ตัวอักษร)`);
  }
} finally {
  await close();
}
