// สร้างไอคอนของระบบ (เครื่องคิดเลข = งานบัญชี/การเงิน) โดยไม่ต้องใช้เครื่องมือภายนอก
// รัน: npm run build:icons  → wwwroot/favicon.ico (16/32/48), favicon.svg, apple-touch-icon.png (180), icon-192.png
import { writeFileSync } from "node:fs";
import { deflateSync } from "node:zlib";

const BRAND = [0xf4, 0x55, 0x1f];      // brand-500
const BRAND_DARK = [0xbb, 0x2c, 0x11]; // brand-700
const WHITE = [0xff, 0xff, 0xff];
const DISPLAY = [0x29, 0x25, 0x24];    // stone-800
const DISPLAY_TEXT = [0xff, 0xc9, 0xa8]; // brand-200

// รูปร่างในพิกัด 0..1 (วาดตามลำดับ ทับกันได้)
const SHAPES = [
  { kind: "rrect", x0: 0, y0: 0, x1: 1, y1: 1, r: 0.22, color: BRAND },
  { kind: "rrect", x0: 0.22, y0: 0.12, x1: 0.78, y1: 0.88, r: 0.09, color: WHITE },          // ตัวเครื่อง
  { kind: "rrect", x0: 0.3, y0: 0.2, x1: 0.7, y1: 0.39, r: 0.04, color: DISPLAY },          // จอ
  { kind: "rrect", x0: 0.5, y0: 0.27, x1: 0.64, y1: 0.32, r: 0.01, color: DISPLAY_TEXT },   // ตัวเลขบนจอ
  ...[0, 1, 2].flatMap(row => [0, 1, 2].map(col => ({                                       // ปุ่ม 3x3
    kind: "rrect",
    x0: 0.3 + col * 0.14, y0: 0.46 + row * 0.13,
    x1: 0.3 + col * 0.14 + 0.11, y1: 0.46 + row * 0.13 + 0.1,
    r: 0.025,
    color: col === 2 && row === 2 ? BRAND_DARK : BRAND,                                     // ปุ่ม "=" สีเข้ม
  }))),
];

function insideRoundedRect(px, py, s) {
  if (px < s.x0 || px > s.x1 || py < s.y0 || py > s.y1) return false;
  const cx = Math.min(Math.max(px, s.x0 + s.r), s.x1 - s.r);
  const cy = Math.min(Math.max(py, s.y0 + s.r), s.y1 - s.r);
  return (px - cx) ** 2 + (py - cy) ** 2 <= s.r ** 2;
}

/** วาดเป็น RGBA ด้วย supersampling 4x4 ต่อพิกเซล เพื่อขอบเรียบ */
function render(size) {
  const ss = 4;
  const rgba = Buffer.alloc(size * size * 4);
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      let r = 0, g = 0, b = 0, a = 0;
      for (let sy = 0; sy < ss; sy++) {
        for (let sx = 0; sx < ss; sx++) {
          const px = (x + (sx + 0.5) / ss) / size;
          const py = (y + (sy + 0.5) / ss) / size;
          let color = null;
          for (const s of SHAPES) if (insideRoundedRect(px, py, s)) color = s.color;
          if (color) { r += color[0]; g += color[1]; b += color[2]; a += 1; }
        }
      }
      const n = ss * ss, i = (y * size + x) * 4;
      rgba[i] = a ? Math.round(r / a) : 0;
      rgba[i + 1] = a ? Math.round(g / a) : 0;
      rgba[i + 2] = a ? Math.round(b / a) : 0;
      rgba[i + 3] = Math.round((a / n) * 255);
    }
  }
  return rgba;
}

const CRC_TABLE = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});
const crc32 = buf => { let c = 0xffffffff; for (const byte of buf) c = CRC_TABLE[(c ^ byte) & 0xff] ^ (c >>> 8); return (c ^ 0xffffffff) >>> 0; };

function chunk(type, data) {
  const len = Buffer.alloc(4); len.writeUInt32BE(data.length);
  const body = Buffer.concat([Buffer.from(type, "ascii"), data]);
  const crc = Buffer.alloc(4); crc.writeUInt32BE(crc32(body));
  return Buffer.concat([len, body, crc]);
}

function png(size) {
  const rgba = render(size);
  const raw = Buffer.alloc(size * (size * 4 + 1));
  for (let y = 0; y < size; y++) {
    raw[y * (size * 4 + 1)] = 0; // filter: none
    rgba.copy(raw, y * (size * 4 + 1) + 1, y * size * 4, (y + 1) * size * 4);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0); ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0; // 8-bit RGBA
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk("IHDR", ihdr), chunk("IDAT", deflateSync(raw, { level: 9 })), chunk("IEND", Buffer.alloc(0)),
  ]);
}

/** ICO ที่เก็บภาพ PNG หลายขนาด (รองรับตั้งแต่ Windows Vista / ทุกเบราว์เซอร์) */
function ico(sizes) {
  const images = sizes.map(png);
  const header = Buffer.alloc(6); header.writeUInt16LE(0, 0); header.writeUInt16LE(1, 2); header.writeUInt16LE(images.length, 4);
  let offset = 6 + 16 * images.length;
  const entries = images.map((img, i) => {
    const e = Buffer.alloc(16), s = sizes[i];
    e[0] = s >= 256 ? 0 : s; e[1] = s >= 256 ? 0 : s; e[2] = 0; e[3] = 0;
    e.writeUInt16LE(1, 4); e.writeUInt16LE(32, 6); e.writeUInt32LE(img.length, 8); e.writeUInt32LE(offset, 12);
    offset += img.length;
    return e;
  });
  return Buffer.concat([header, ...entries, ...images]);
}

const hex = c => "#" + c.map(v => v.toString(16).padStart(2, "0")).join("");
const svgShapes = SHAPES.map(s => {
  const k = 64, w = (s.x1 - s.x0) * k, h = (s.y1 - s.y0) * k;
  return `<rect x="${+(s.x0 * k).toFixed(2)}" y="${+(s.y0 * k).toFixed(2)}" width="${+w.toFixed(2)}" height="${+h.toFixed(2)}" rx="${+(s.r * k).toFixed(2)}" fill="${hex(s.color)}"/>`;
}).join("");
const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><title>QSHC Financial</title>${svgShapes}</svg>\n`;

writeFileSync("wwwroot/favicon.ico", ico([16, 32, 48]));
writeFileSync("wwwroot/favicon.svg", svg);
writeFileSync("wwwroot/apple-touch-icon.png", png(180));
writeFileSync("wwwroot/icon-192.png", png(192));
console.log("icons written: favicon.ico, favicon.svg, apple-touch-icon.png, icon-192.png");
