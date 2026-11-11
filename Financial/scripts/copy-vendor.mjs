// คัดลอกไลบรารีฝั่ง client จาก node_modules ไปไว้ใน wwwroot/lib เพื่อให้ใช้งานบน Intranet ได้โดยไม่ต้องพึ่ง CDN
// รัน: npm run copy:vendor (ไฟล์ปลายทางถูก commit ไว้แล้ว เครื่องที่ไม่มี Internet ไม่ต้องรัน)
import { cpSync, mkdirSync } from "node:fs";

const files = [
  ["node_modules/chart.js/dist/chart.umd.min.js", "wwwroot/lib/chartjs/chart.umd.min.js"],
  ["node_modules/chart.js/LICENSE.md", "wwwroot/lib/chartjs/LICENSE.md"],
  ["node_modules/three/build/three.module.min.js", "wwwroot/lib/three/three.module.min.js"],
  ["node_modules/three/examples/jsm/controls/OrbitControls.js", "wwwroot/lib/three/addons/OrbitControls.js"],
  ["node_modules/three/LICENSE", "wwwroot/lib/three/LICENSE"],
];

for (const weight of [300, 400, 500, 600, 700]) {
  for (const subset of ["thai", "latin"]) {
    const name = `kanit-${subset}-${weight}-normal.woff2`;
    files.push([`node_modules/@fontsource/kanit/files/${name}`, `wwwroot/fonts/kanit/${name}`]);
  }
}
files.push(["node_modules/@fontsource/kanit/LICENSE", "wwwroot/fonts/kanit/OFL.txt"]);

for (const [from, to] of files) {
  mkdirSync(to.substring(0, to.lastIndexOf("/")), { recursive: true });
  cpSync(from, to);
  console.log(`copied ${to}`);
}
