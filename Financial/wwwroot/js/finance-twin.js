// 3D Digital Twin (isometric) — เงินที่รอชำระ แยก OPD / IPD
// แนวคิดผังเดียวกับ HOFLOW: ห้องกระจก + เคาน์เตอร์ + ที่นั่งรอ + ผู้ป่วยแต่ละคน + การ์ดสถานะลอยเหนือห้อง
//   - ผู้ป่วย 1 คน = 1 ราย · สีเสื้อ = ระยะเวลารอ (เขียว/เหลือง/แดง) · เหรียญเหนือศีรษะ = ค่าใช้จ่ายรวม (สูง = มาก)
//   - ผู้ป่วยที่การเงินรับเรื่องแล้ว (InBilling) ยืนที่เคาน์เตอร์ ที่เหลือนั่งรอ
// ไม่ใช้ไลบรารีภายนอก: three + OrbitControls อยู่ใน wwwroot/lib/three
import * as THREE from "three";
import { OrbitControls } from "three/addons/OrbitControls.js";

const stage = document.getElementById("twinStage");
const canvas = document.getElementById("twinCanvas");
const tooltip = document.getElementById("twinTooltip");
const selectionBox = document.getElementById("twinSelection");
const rotateBtn = document.getElementById("twinRotate");
const { format, escapeHtml } = window.app;

const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const HINT = "คลิกที่ตัวคนเพื่อดูรายละเอียด";

// เกณฑ์ระยะเวลารอ (นาที): [เริ่มนาน, เกินเป้า] — OPD เป้าหมายจุดการเงิน 15 นาที (wait-definitions.md)
const THRESHOLDS = { O: [15, 30], I: [6 * 60, 24 * 60] };
const STATUS = {
  ok: { hex: "#10b981", soft: "#d7f3e8", label: "✓ ปกติ", badge: "bg-emerald-50 text-emerald-700", tone: "text-emerald-700" },
  warn: { hex: "#f59e0b", soft: "#fdecc8", label: "! เริ่มนาน", badge: "bg-amber-50 text-amber-700", tone: "text-amber-700" },
  late: { hex: "#ef4444", soft: "#fbd9d6", label: "✕ เกินเป้า", badge: "bg-red-50 text-red-700", tone: "text-red-600" },
};
const TEAL = "#02a49f";

// ผัง (เมตร): พื้นที่ x -30..30, z -18..18, +z = ด้านหน้า (ทางเข้า)
const FLOOR = [-30, -18, 30, 18];
const MARGIN = [5, 5, 5, 11]; // ซ้าย หลัง ขวา หน้า (หน้ามีที่ว่างสำหรับชื่อศูนย์)
const ENTRANCE = [0, 18];
const ZONES = {
  O: { key: "O", title: "การเงิน OPD", color: TEAL, room: [-28, -18, -2, -9], wait: [-27, -5, -3, 11], counters: 3 },
  I: { key: "I", title: "ปิดบัญชี IPD", color: "#3d6fb6", room: [2, -18, 28, -9], wait: [3, -5, 27, 11], counters: 2 },
};
for (const z of Object.values(ZONES)) {
  z.center = [(z.wait[0] + z.wait[2]) / 2, (z.wait[1] + z.wait[3]) / 2];
  z.roomCenter = [(z.room[0] + z.room[2]) / 2, (z.room[1] + z.room[3]) / 2];
  // ที่นั่งเรียงเป็นแถวหันเข้าห้อง (-z): แถวแรกอยู่ใกล้ห้องที่สุด
  z.seats = [];
  for (let zz = z.wait[1] + 1.4; zz <= z.wait[3] - 1; zz += 2.3) {
    for (let xx = z.wait[0] + 1; xx <= z.wait[2] - 0.8; xx += 1.3) z.seats.push({ x: xx, z: zz });
  }
  // เคาน์เตอร์เรียงตามความกว้างของห้อง: เจ้าหน้าที่อยู่หลังเคาน์เตอร์ ผู้ป่วยที่รับเรื่องแล้วยืนด้านหน้า
  z.counterSlots = Array.from({ length: z.counters }, (_, i) => ({
    x: z.room[0] + ((i + 0.5) * (z.room[2] - z.room[0])) / z.counters, counterZ: -12, staffZ: -13.3, servedZ: -10.1,
  }));
  // ยืนรอด้านหน้าห้องเมื่อที่นั่งเต็ม
  z.standing = Array.from({ length: 80 }, (_, i) => ({ x: z.wait[0] + 1 + (i % 20) * 1.1, z: z.wait[1] - 1.8 - Math.floor(i / 20) * 0.9 }));
}

function supportsWebGL() {
  try { return !!document.createElement("canvas").getContext("webgl2"); } catch { return false; }
}

if (!stage || !supportsWebGL()) {
  document.getElementById("twinFallback")?.classList.replace("hidden", "grid");
} else {
  init();
}

function init() {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;

  const scene = new THREE.Scene();
  const HOME_DIR = new THREE.Vector3(1, 1.05, 1).normalize();
  const HOME_DIST = 120;
  const camera = new THREE.OrthographicCamera(-100, 100, 100, -100, -500, 1000);
  camera.position.copy(HOME_DIR).multiplyScalar(HOME_DIST);

  const controls = new OrbitControls(camera, canvas);
  controls.enableDamping = true;
  controls.screenSpacePanning = false;
  // คลิกซ้ายลาก = เลื่อน, คลิกขวาลาก = หมุน, ล้อเมาส์ = ซูม (มือถือ: 1 นิ้วเลื่อน, 2 นิ้วซูม/หมุน)
  controls.mouseButtons = { LEFT: THREE.MOUSE.PAN, MIDDLE: THREE.MOUSE.DOLLY, RIGHT: THREE.MOUSE.ROTATE };
  controls.touches = { ONE: THREE.TOUCH.PAN, TWO: THREE.TOUCH.DOLLY_ROTATE };
  // ไม่ให้ก้มจนเป็นมุมมอง 2D จากด้านบน (ต่ำสุด 30° จากแนวดิ่ง) และไม่ต่ำจนเห็นแต่ขอบ
  controls.minPolarAngle = THREE.MathUtils.degToRad(30);
  controls.maxPolarAngle = Math.PI / 2.4;
  controls.minZoom = 3;
  controls.maxZoom = 60;
  controls.autoRotate = false;
  controls.autoRotateSpeed = 0.6;
  rotateBtn?.setAttribute("aria-pressed", "false");

  // ---------- แสง ----------
  scene.add(new THREE.HemisphereLight(0xffffff, 0xdfe8ee, 1.55));
  const sun = new THREE.DirectionalLight(0xffffff, 1.7);
  sun.position.set(30, 60, 25);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  Object.assign(sun.shadow.camera, { left: -48, right: 48, top: 40, bottom: -40, near: 1, far: 160 });
  scene.add(sun);

  // ---------- วัสดุ/เรขาคณิตที่ใช้ร่วมกัน ----------
  const matCache = new Map();
  const mat = (hex, opts = {}) => {
    const key = hex + JSON.stringify(opts);
    if (!matCache.has(key)) matCache.set(key, new THREE.MeshStandardMaterial({ color: hex, roughness: 0.65, ...opts }));
    return matCache.get(key);
  };
  const box = (w, h, d, material, x, y, z, parent, shadow = true) => {
    const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
    m.position.set(x, y, z);
    m.castShadow = shadow; m.receiveShadow = true;
    parent.add(m);
    return m;
  };

  const world = new THREE.Group();
  scene.add(world);

  // ---------- แท่น + พื้น + ชื่อศูนย์ ----------
  const sx0 = FLOOR[0] - MARGIN[0], sz0 = FLOOR[1] - MARGIN[1], sx1 = FLOOR[2] + MARGIN[2], sz1 = FLOOR[3] + MARGIN[3];
  box(sx1 - sx0, 2, sz1 - sz0, mat("#f9fbfc", { roughness: 0.9 }), (sx0 + sx1) / 2, -1, (sz0 + sz1) / 2, world);
  const slabEdge = new THREE.LineSegments(
    new THREE.EdgesGeometry(new THREE.PlaneGeometry(sx1 - sx0 - 0.6, sz1 - sz0 - 0.6)),
    new THREE.LineBasicMaterial({ color: TEAL, transparent: true, opacity: 0.45 }));
  slabEdge.rotation.x = -Math.PI / 2; slabEdge.position.set((sx0 + sx1) / 2, 0.02, (sz0 + sz1) / 2);
  world.add(slabEdge);
  const interior = new THREE.Mesh(new THREE.PlaneGeometry(FLOOR[2] - FLOOR[0], FLOOR[3] - FLOOR[1]), mat("#f1f5f8", { roughness: 0.95 }));
  interior.rotation.x = -Math.PI / 2; interior.position.y = 0.005; interior.receiveShadow = true;
  world.add(interior);

  document.fonts.load("600 96px Kanit").catch(() => { }).finally(() => {
    const c = document.createElement("canvas");
    c.width = 2048; c.height = 160;
    const g = c.getContext("2d");
    g.fillStyle = "rgba(12, 89, 143, 0.78)";
    g.font = "600 96px Kanit, sans-serif";
    g.textAlign = "center"; g.textBaseline = "middle";
    g.fillText("ศูนย์หัวใจสิริกิติ์ ภาคตะวันออกเฉียงเหนือ", c.width / 2, c.height / 2 + 6);
    const t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace; t.anisotropy = 8;
    const m = new THREE.Mesh(new THREE.PlaneGeometry(56, 56 * (160 / 2048)), new THREE.MeshBasicMaterial({ map: t, transparent: true, depthWrite: false }));
    m.rotation.x = -Math.PI / 2; m.position.set(-6, 0.03, FLOOR[3] + MARGIN[3] / 2 + 1);
    world.add(m);
  });

  // ทางเข้า
  const entrance = new THREE.Group();
  entrance.position.set(ENTRANCE[0], 0, ENTRANCE[1]);
  const arch = new THREE.Mesh(new THREE.TorusGeometry(3.4, 0.32, 14, 40, Math.PI), mat(TEAL, { roughness: 0.35 }));
  arch.castShadow = true;
  entrance.add(arch);
  const doormat = new THREE.Mesh(new THREE.PlaneGeometry(7.5, 4), mat("#c8ece8"));
  doormat.rotation.x = -Math.PI / 2; doormat.position.set(0, 0.03, 1.6);
  entrance.add(doormat);
  world.add(entrance);

  // ---------- ห้อง/ที่นั่ง/เคาน์เตอร์ของแต่ละโซน ----------
  const zoneObjects = {};

  function wall(ax, az, bx, bz, glass, rim, parent) {
    const len = Math.hypot(bx - ax, bz - az);
    if (len < 0.05) return;
    const g = new THREE.Group();
    g.position.set((ax + bx) / 2, 0, (az + bz) / 2);
    g.rotation.y = -Math.atan2(bz - az, bx - ax);
    const pane = new THREE.Mesh(new THREE.BoxGeometry(len, 3, 0.14), glass);
    pane.position.y = 1.5; pane.renderOrder = 2;
    g.add(pane);
    box(len + 0.14, 0.12, 0.2, rim, 0, 3, 0, g);
    box(0.16, 3, 0.16, rim, -len / 2, 1.5, 0, g, false);
    box(0.16, 3, 0.16, rim, len / 2, 1.5, 0, g, false);
    parent.add(g);
  }

  for (const zone of Object.values(ZONES)) {
    const group = new THREE.Group();
    const [rx0, rz0, rx1, rz1] = zone.room;
    const glass = new THREE.MeshStandardMaterial({ color: zone.color, transparent: true, opacity: 0.34, depthWrite: false, roughness: 0.15, metalness: 0.05 });
    const rim = mat(zone.color, { roughness: 0.4 });

    // พื้นห้อง
    const tint = new THREE.Color("#ffffff").lerp(new THREE.Color(zone.color), 0.2);
    const roomFloor = new THREE.Mesh(new THREE.PlaneGeometry(rx1 - rx0, rz1 - rz0), mat("#" + tint.getHexString(), { roughness: 0.85 }));
    roomFloor.rotation.x = -Math.PI / 2; roomFloor.position.set((rx0 + rx1) / 2, 0.015, (rz0 + rz1) / 2); roomFloor.receiveShadow = true;
    group.add(roomFloor);

    // ผนังกระจก: หลัง/ซ้าย/ขวา + ด้านหน้าเว้นประตูกลาง
    const doorW = 6, mx = (rx0 + rx1) / 2;
    wall(rx0, rz0, rx1, rz0, glass, rim, group);
    wall(rx0, rz0, rx0, rz1, glass, rim, group);
    wall(rx1, rz0, rx1, rz1, glass, rim, group);
    wall(rx0, rz1, mx - doorW / 2, rz1, glass, rim, group);
    wall(mx + doorW / 2, rz1, rx1, rz1, glass, rim, group);

    // เคาน์เตอร์ + จอ + เก้าอี้
    for (const s of zone.counterSlots) {
      box(2.2, 1.06, 0.6, mat("#ffffff"), s.x, 0.53, s.counterZ, group);
      box(2.2, 0.75, 0.04, mat(zone.color), s.x, 0.45, s.counterZ + 0.32, group, false);
      box(2.3, 0.05, 0.7, mat("#eef3f6"), s.x, 1.08, s.counterZ, group);
      box(0.62, 0.4, 0.05, mat("#24384d"), s.x - 0.4, 1.35, s.counterZ - 0.1, group);
      box(0.55, 0.08, 0.55, mat("#55657a"), s.x, 0.45, s.staffZ, group);
      box(0.55, 0.6, 0.07, mat("#55657a"), s.x, 0.78, s.staffZ - 0.27, group);
    }

    // พื้นที่รอ (สีตามสถานะ) + ขอบ
    const [wx0, wz0, wx1, wz1] = zone.wait;
    const w = wx1 - wx0 + 1.6, d = wz1 - wz0 + 1.6;
    const waitMat = new THREE.MeshStandardMaterial({ color: STATUS.ok.soft, emissive: STATUS.ok.hex, emissiveIntensity: 0, roughness: 0.9 });
    const waitFloor = new THREE.Mesh(new THREE.PlaneGeometry(w, d), waitMat);
    waitFloor.rotation.x = -Math.PI / 2; waitFloor.position.set(zone.center[0], 0.02, zone.center[1]); waitFloor.receiveShadow = true;
    group.add(waitFloor);
    const borderMat = new THREE.LineBasicMaterial({ color: STATUS.ok.hex, transparent: true, opacity: 0.55 });
    const border = new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.PlaneGeometry(w, d)), borderMat);
    border.rotation.x = -Math.PI / 2; border.position.set(zone.center[0], 0.035, zone.center[1]);
    group.add(border);

    // เก้าอี้นั่งรอ (instanced)
    const seatMesh = new THREE.InstancedMesh(new THREE.BoxGeometry(0.92, 0.1, 0.8), mat("#7d9cc8"), zone.seats.length);
    const backMesh = new THREE.InstancedMesh(new THREE.BoxGeometry(0.92, 0.6, 0.08), mat("#6a8bbb"), zone.seats.length);
    const m4 = new THREE.Matrix4();
    zone.seats.forEach((s, i) => {
      seatMesh.setMatrixAt(i, m4.makeTranslation(s.x, 0.44, s.z));
      backMesh.setMatrixAt(i, m4.makeTranslation(s.x, 0.78, s.z + 0.38));
    });
    seatMesh.castShadow = backMesh.castShadow = true;
    group.add(seatMesh, backMesh);

    world.add(group);
    zoneObjects[zone.key] = { group, waitMat, borderMat, status: "ok" };
  }

  // ---------- ตัวคน (low-poly) ----------
  const legGeo = new THREE.CapsuleGeometry(0.11, 0.52, 4, 8).translate(0, -0.37, 0);
  const armGeo = new THREE.CapsuleGeometry(0.075, 0.42, 4, 8).translate(0, -0.29, 0);
  const torsoGeo = new THREE.CapsuleGeometry(0.26, 0.4, 4, 12);
  const headGeo = new THREE.SphereGeometry(0.2, 14, 12);
  const hairGeo = new THREE.SphereGeometry(0.215, 14, 10, 0, Math.PI * 2, 0, Math.PI * 0.55);
  const coinGeo = new THREE.CylinderGeometry(0.28, 0.28, 1, 18);
  const coinMat = new THREE.MeshStandardMaterial({ color: "#f5b301", metalness: 0.5, roughness: 0.3, emissive: "#f5b301", emissiveIntensity: 0.15 });
  const PANTS = ["#3d4f6b", "#5a6b7d", "#2f3b4c", "#6b5a4a", "#44607a", "#7a8794"];
  const HAIR = ["#2b211c", "#3d2b22", "#1f1f24", "#5a4636", "#9a9a9a", "#c8c8c8"];
  const SKIN = ["#f1c9a5", "#e3b48c", "#d39f78"];
  const hash = str => { let h = 2166136261; for (let i = 0; i < str.length; i++) h = Math.imul(h ^ str.charCodeAt(i), 16777619); return h >>> 0; };

  function makePerson(shirt, seed, { coin = false } = {}) {
    const root = new THREE.Group();
    const body = new THREE.Group();
    root.add(body);
    const part = (geo, color, x, y, z, rough = 0.7) => {
      const m = new THREE.Mesh(geo, mat(color, { roughness: rough }));
      m.position.set(x, y, z); m.castShadow = true;
      body.add(m);
      return m;
    };
    const parts = {
      torso: part(torsoGeo, shirt, 0, 1.24, 0),
      head: part(headGeo, SKIN[seed % 3], 0, 1.76, 0, 0.6),
      hair: part(hairGeo, HAIR[(seed * 7) % HAIR.length], 0, 1.78, -0.02, 0.9),
      legL: part(legGeo, PANTS[seed % PANTS.length], 0.12, 0.84, 0),
      legR: part(legGeo, PANTS[seed % PANTS.length], -0.12, 0.84, 0),
      armL: part(armGeo, shirt, 0.33, 1.44, 0),
      armR: part(armGeo, shirt, -0.33, 1.44, 0),
    };
    let coinMesh = null;
    if (coin) {
      coinMesh = new THREE.Mesh(coinGeo, coinMat);
      coinMesh.castShadow = true;
      root.add(coinMesh);
    }
    return { root, body, parts, coinMesh };
  }

  function setShirt(person, color) {
    person.parts.torso.material = mat(color, { roughness: 0.7 });
    person.parts.armL.material = person.parts.armR.material = mat(color, { roughness: 0.7 });
  }

  function setCoin(person, amount) {
    if (!person.coinMesh) return;
    const h = 0.12 + (Math.log10(Math.max(amount, 100)) - 2) * 0.22;
    person.coinMesh.scale.y = h;
    person.coinMesh.position.y = 2.1 + h / 2;
  }

  // ผู้ใช้บริการแต่ละคน (key = ประเภท+รหัสอ้างอิง) — เดินจากทางเข้าไปนั่งรอ/ไปเคาน์เตอร์
  const figures = new Map();
  const staff = [];
  const pickables = [];

  // เจ้าหน้าที่การเงิน (นั่งหลังเคาน์เตอร์)
  for (const zone of Object.values(ZONES)) {
    zone.counterSlots.forEach((s, i) => {
      const p = makePerson("#02a49f", i + (zone.key === "I" ? 5 : 0));
      p.root.position.set(s.x, 0, s.staffZ);
      p.body.position.y = -0.32;
      p.parts.legL.rotation.x = p.parts.legR.rotation.x = -1.35;
      p.parts.armL.rotation.x = p.parts.armR.rotation.x = -0.6;
      world.add(p.root);
      staff.push({ person: p, zone: zone.key });
    });
  }

  // ---------- การ์ดสถานะ (DOM) ลอยเหนือห้อง ----------
  const labelLayer = document.createElement("div");
  labelLayer.className = "pointer-events-none absolute inset-0 overflow-hidden";
  stage.appendChild(labelLayer);
  const cards = {};
  for (const zone of Object.values(ZONES)) {
    const el = document.createElement("button");
    el.type = "button";
    el.className = "pointer-events-auto absolute left-0 top-0 whitespace-nowrap rounded-xl border-l-4 border-stone-200 bg-white/95 px-3 py-2 text-left shadow-lg shadow-stone-900/10 transition hover:shadow-xl";
    el.style.borderLeftColor = zone.color;
    el.setAttribute("aria-label", `${zone.title} — คลิกเพื่อดูเฉพาะโซนนี้`);
    el.addEventListener("click", () => document.querySelector(`[data-twin-filter="${zone.key}"]`)?.click());
    labelLayer.appendChild(el);
    cards[zone.key] = el;
  }
  const entranceLabel = document.createElement("span");
  entranceLabel.className = "pointer-events-none absolute left-0 top-0 whitespace-nowrap rounded-md px-2 py-0.5 text-[10px] font-medium text-white shadow";
  entranceLabel.style.backgroundColor = TEAL;
  entranceLabel.textContent = "ทางเข้า";
  labelLayer.appendChild(entranceLabel);

  // ---------- ข้อมูล ----------
  let filter = "all";
  let hovered = null, selected = null;

  function statusOf(type, avgMinutes, count) {
    if (count === 0) return "ok";
    const [warn, late] = THRESHOLDS[type];
    return avgMinutes < warn ? "ok" : avgMinutes < late ? "warn" : "late";
  }

  function build(data) {
    const pending = data.PendingPayments ?? [];
    const firstBuild = figures.size === 0; // เปิดหน้าครั้งแรก: ทุกคนอยู่ที่ของตัวเองแล้ว (ไม่ต้องเดินเข้าพร้อมกันทั้งหมด)
    const seen = new Set();
    const byZone = { O: [], I: [] };

    // key ไม่ซ้ำภายในรอบเดียวกัน (รหัสอ้างอิง 4 หลักอาจชนกันได้)
    for (const item of pending) {
      let key = `${item.Type}${item.Reference}`;
      for (let n = 2; seen.has(key); n++) key = `${item.Type}${item.Reference}#${n}`;
      seen.add(key);
      byZone[item.Type]?.push({ key, item });
    }

    for (const zone of Object.values(ZONES)) {
      const list = byZone[zone.key].sort((a, b) => b.item.WaitingMinutes - a.item.WaitingMinutes);
      const taken = new Set();        // ที่นั่ง/ที่ยืนที่มีคนอยู่แล้ว (คนเดิมที่ยังอยู่)
      const counterTaken = new Set();

      // 1) คนเดิมที่ยังอยู่: คงตำแหน่งเดิมถ้าสถานะการให้บริการไม่เปลี่ยน
      for (const { key, item } of list) {
        const f = figures.get(key);
        if (!f?.slot) continue;
        if (item.InBilling && f.slot.kind === "counter") counterTaken.add(f.slot.index);
        if (!item.InBilling && f.slot.kind === "seat") taken.add(f.slot.index);
        if (!item.InBilling && f.slot.kind === "stand") taken.add("s" + f.slot.index);
      }

      // 2) จัดที่ใหม่: รับเรื่องแล้ว → เคาน์เตอร์ที่ว่าง, ที่เหลือ → ที่นั่งว่างใกล้ห้องที่สุด
      let seatCursor = 0, standCursor = 0;
      const nextSeat = () => { while (taken.has(seatCursor)) seatCursor++; return seatCursor++; };
      const nextStand = () => { while (taken.has("s" + standCursor)) standCursor++; return standCursor++; };
      const nextCounter = () => { for (let i = 0; i < zone.counterSlots.length; i++) if (!counterTaken.has(i)) return i; return -1; };

      for (const { key, item } of list) {
        let f = figures.get(key);
        const shirt = STATUS[statusOf(item.Type, item.WaitingMinutes, 1)].hex;
        if (!f) {
          const seed = hash(key);
          const person = makePerson(shirt, seed, { coin: true });
          person.root.position.set(ENTRANCE[0] + (seed % 5) - 2, 0, ENTRANCE[1] + 3);
          world.add(person.root);
          person.root.traverse(o => { if (o.isMesh) o.userData.fig = key; });
          f = { key, person, x: person.root.position.x, z: person.root.position.z, tx: 0, tz: 0, tyaw: Math.PI, sitting: false, slot: null, item, zone, phase: seed % 7, leaving: false };
          figures.set(key, f);
          pickables.push(person.root);
        }
        f.item = item;
        f.leaving = false;
        setShirt(f.person, shirt);
        setCoin(f.person, item.Amount);

        const needsCounter = item.InBilling && f.slot?.kind !== "counter";
        const ci = needsCounter ? nextCounter() : -1;
        if (ci >= 0) {
          counterTaken.add(ci);
          if (f.slot?.kind === "seat") taken.delete(f.slot.index);
          f.slot = { kind: "counter", index: ci };
        } else if (!f.slot || (f.slot.kind === "counter" && !item.InBilling)) {
          const si = nextSeat();
          if (si < zone.seats.length) {
            f.slot = { kind: "seat", index: si };
            taken.add(si);
          } else {
            f.slot = { kind: "stand", index: nextStand() };
            taken.add("s" + f.slot.index);
          }
        }

        if (f.slot.kind === "counter") {
          const s = zone.counterSlots[f.slot.index];
          f.tx = s.x; f.tz = s.servedZ; f.sitting = false;
        } else if (f.slot.kind === "seat") {
          const s = zone.seats[f.slot.index];
          f.tx = s.x; f.tz = s.z; f.sitting = true;
        } else {
          const s = zone.standing[Math.min(f.slot.index, zone.standing.length - 1)];
          f.tx = s.x; f.tz = s.z; f.sitting = false;
        }
        if (firstBuild) { f.x = f.tx; f.z = f.tz; f.person.root.position.set(f.x, 0, f.z); f.person.root.rotation.y = Math.PI; }
      }
    }

    // คนที่ไม่อยู่ในรายการแล้ว (ชำระเสร็จ/จำหน่ายแล้ว) → เดินออกทางเข้า
    for (const f of figures.values()) {
      if (!seen.has(f.key) && !f.leaving) { f.leaving = true; f.sitting = false; f.tx = ENTRANCE[0]; f.tz = ENTRANCE[1] + 3; }
    }

    // การ์ดและสีพื้นที่รอ
    for (const zone of Object.values(ZONES)) {
      const list = byZone[zone.key];
      const total = list.reduce((s, x) => s + x.item.Amount, 0);
      const avg = list.length ? list.reduce((s, x) => s + x.item.WaitingMinutes, 0) / list.length : 0;
      const status = statusOf(zone.key, avg, list.length);
      const st = STATUS[status];
      const zo = zoneObjects[zone.key];
      zo.status = status;
      zo.waitMat.color.set(st.soft); zo.waitMat.emissive.set(st.hex);
      zo.borderMat.color.set(st.hex);
      const waitText = zone.key === "O" ? `${Math.round(avg)} นาที` : `${(avg / 60).toFixed(1)} ชม.`;
      cards[zone.key].innerHTML = `
        <span class="flex items-center gap-1.5 text-[11px] font-medium text-stone-700">
          <i class="size-2.5 rounded-[3px]" style="background:${zone.color}"></i>${zone.title}
          <span class="ml-auto rounded-full px-1.5 text-[9px] font-normal ${st.badge}">${st.label}</span>
        </span>
        <span class="mt-0.5 flex items-baseline gap-1 text-[10px] text-stone-500">
          <b class="text-[22px] font-semibold leading-none text-stone-900">${format.num(list.length)}</b> คนรออยู่
          <span class="ml-1 text-[9px] text-stone-400">${format.thb(total)}</span>
        </span>
        <span class="block text-[9.5px] text-stone-500">${list.length ? `รอเฉลี่ย <b class="font-semibold ${st.tone}">${waitText}</b>` : "ไม่มีผู้รอชำระ"}</span>`;
    }
  }

  // ---------- การโต้ตอบ ----------
  const raycaster = new THREE.Raycaster();
  const pointer = new THREE.Vector2();

  function pick(event) {
    const rect = canvas.getBoundingClientRect();
    pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
    raycaster.setFromCamera(pointer, camera);
    const hit = raycaster.intersectObjects(pickables.filter(p => p.visible), true)[0];
    const key = hit?.object.userData.fig;
    const f = key ? figures.get(key) : null;
    return f && !f.leaving ? f : null;
  }

  function describe(item) {
    const { waitText, statusText } = window.app.pending;
    return `
      <div class="mb-1 font-semibold text-stone-900">${item.Type === "O" ? "OPD" : "IPD"} · ${escapeHtml(item.Reference)}</div>
      <div class="grid grid-cols-[auto_1fr] gap-x-3 text-stone-600">
        ${item.PatientName || item.Hn ? `
        <span>ผู้ป่วย</span><b class="text-right text-stone-900">${escapeHtml(item.PatientName ?? "-")}</b>
        <span>HN</span><span class="text-right tabular-nums">${escapeHtml(item.Hn ?? "-")}</span>` : ""}
        <span>ค่าใช้จ่ายรวม</span><b class="text-right text-stone-900">${format.thb(item.Amount)}</b>
        <span>สถานะ</span><span class="text-right">${statusText(item)}</span>
        <span>กลุ่มผู้จ่าย</span><span class="text-right">${escapeHtml(item.PayerName)}</span>
        <span>${item.Type === "O" ? "คลินิก" : "หอผู้ป่วย"}</span><span class="text-right">${escapeHtml(item.Location)}</span>
        <span>รอมาแล้ว</span><span class="text-right">${waitText(item.WaitingMinutes)}</span>
      </div>`;
  }

  let downAt = null;
  canvas.addEventListener("contextmenu", e => e.preventDefault()); // คลิกขวาใช้หมุน ไม่เปิดเมนู browser
  canvas.addEventListener("pointerdown", e => { downAt = [e.clientX, e.clientY]; });
  canvas.addEventListener("pointermove", e => {
    const f = pick(e);
    hovered = f;
    canvas.style.cursor = f ? "pointer" : "grab";
    if (f) {
      const rect = stage.getBoundingClientRect();
      tooltip.innerHTML = describe(f.item);
      tooltip.classList.remove("hidden");
      const x = Math.min(e.clientX - rect.left + 14, rect.width - tooltip.offsetWidth - 8);
      const y = Math.min(e.clientY - rect.top + 14, rect.height - tooltip.offsetHeight - 8);
      tooltip.style.transform = `translate(${x}px, ${y}px)`;
      tooltip.style.left = tooltip.style.top = "0";
    } else {
      tooltip.classList.add("hidden");
    }
  });
  canvas.addEventListener("pointerleave", () => { tooltip.classList.add("hidden"); hovered = null; });
  canvas.addEventListener("click", e => {
    if (downAt && Math.hypot(e.clientX - downAt[0], e.clientY - downAt[1]) > 5) return; // ลากกล้อง ไม่ใช่คลิก
    const f = pick(e);
    selected = f;
    selectionBox.innerHTML = f ? describe(f.item) : HINT;
    document.querySelectorAll("[data-pending-body] tr").forEach(tr => tr.classList.toggle("bg-brand-50", !!f && tr.dataset.ref === f.item.Reference));
    if (f) { controls.autoRotate = false; rotateBtn?.setAttribute("aria-pressed", "false"); }
  });

  // วงแหวนล้อมคนที่เลือก
  const ring = new THREE.Mesh(new THREE.RingGeometry(0.55, 0.7, 36), new THREE.MeshBasicMaterial({ color: TEAL, transparent: true, opacity: 0.9, side: THREE.DoubleSide }));
  ring.rotation.x = -Math.PI / 2; ring.visible = false;
  world.add(ring);

  // ---------- กล้อง ----------
  let goal = null;
  const fitZoom = () => Math.max(5, Math.min(stage.clientWidth / 74, stage.clientHeight / 52));

  function setGoal(target, zoomFactor, immediate = false) {
    const g = { target: new THREE.Vector3(...target), zoom: fitZoom() * zoomFactor, dir: HOME_DIR.clone() };
    if (immediate) {
      controls.target.copy(g.target);
      camera.position.copy(g.target).addScaledVector(HOME_DIR, HOME_DIST);
      camera.zoom = g.zoom; camera.updateProjectionMatrix(); controls.update();
    } else goal = g;
  }

  function applyVisibility() {
    for (const [key, card] of Object.entries(cards)) card.classList.toggle("opacity-50", filter !== "all" && filter !== key);
  }

  function applyFilter(immediate = false) {
    applyVisibility();
    if (filter === "all") setGoal([-1, 0, 1], 1, immediate);
    else setGoal([ZONES[filter].center[0], 0, -6], 1.7, immediate);
  }

  document.querySelectorAll("[data-twin-filter]").forEach(btn => btn.addEventListener("click", () => {
    filter = btn.dataset.twinFilter;
    document.querySelectorAll("[data-twin-filter]").forEach(b => b.setAttribute("aria-pressed", String(b === btn)));
    applyFilter();
  }));
  rotateBtn?.addEventListener("click", () => {
    controls.autoRotate = !controls.autoRotate;
    rotateBtn.setAttribute("aria-pressed", String(controls.autoRotate));
  });
  const zoomBy = k => { goal = { target: controls.target.clone(), zoom: Math.min(controls.maxZoom, Math.max(controls.minZoom, camera.zoom * k)), dir: null }; };
  document.getElementById("twinZoomIn")?.addEventListener("click", () => zoomBy(1.35));
  document.getElementById("twinZoomOut")?.addEventListener("click", () => zoomBy(1 / 1.35));
  document.getElementById("twinHome")?.addEventListener("click", () => { controls.autoRotate = false; rotateBtn?.setAttribute("aria-pressed", "false"); applyFilter(); });
  controls.addEventListener("start", () => { goal = null; });

  // ---------- ปรับขนาด / render loop ----------
  let sized = false;
  function resize() {
    const w = stage.clientWidth, h = stage.clientHeight;
    if (!w || !h) return;
    renderer.setSize(w, h, false);
    camera.left = -w / 2; camera.right = w / 2; camera.top = h / 2; camera.bottom = -h / 2;
    if (!sized) { sized = true; applyFilter(true); } else camera.updateProjectionMatrix();
  }
  new ResizeObserver(resize).observe(stage);
  resize();

  let visible = true;
  new IntersectionObserver(([entry]) => { visible = entry.isIntersecting; }).observe(stage);

  const clock = new THREE.Clock();
  const v = new THREE.Vector3();
  const SPEED = 9;

  function placeLabel(el, x, y, z, show) {
    el.style.display = show ? "" : "none";
    if (!show) return;
    v.set(x, y, z).project(camera);
    const px = (v.x * 0.5 + 0.5) * stage.clientWidth, py = (-v.y * 0.5 + 0.5) * stage.clientHeight;
    el.style.transform = `translate(${px}px, ${py}px) translate(-50%, -100%)`;
  }

  renderer.setAnimationLoop(() => {
    if (!visible || document.hidden) return; // ไม่เรนเดอร์เมื่อมองไม่เห็น ประหยัด CPU/GPU
    const dt = Math.min(0.1, clock.getDelta()), t = clock.elapsedTime;

    if (goal) {
      const k = 1 - Math.exp(-dt * 5);
      controls.target.lerp(goal.target, k);
      const dir = camera.position.clone().sub(controls.target).normalize();
      if (goal.dir) dir.lerp(goal.dir, k).normalize();
      camera.position.copy(controls.target).addScaledVector(dir, HOME_DIST);
      camera.zoom += (goal.zoom - camera.zoom) * k;
      camera.updateProjectionMatrix();
      if (controls.target.distanceTo(goal.target) < 0.05 && Math.abs(camera.zoom - goal.zoom) < 0.01 && (!goal.dir || dir.distanceTo(goal.dir) < 0.002)) goal = null;
    }

    for (const f of [...figures.values()]) {
      const dx = f.tx - f.x, dz = f.tz - f.z, d = Math.hypot(dx, dz);
      const walking = d > 0.05;
      if (walking) { const s = Math.min(d, SPEED * dt); f.x += (dx / d) * s; f.z += (dz / d) * s; }
      const r = f.person.root;
      r.position.set(f.x, walking ? Math.abs(Math.sin(t * 11 + f.phase)) * 0.05 : 0, f.z);
      const yaw = walking ? Math.atan2(dx, dz) : f.tyaw;
      r.rotation.y += Math.atan2(Math.sin(yaw - r.rotation.y), Math.cos(yaw - r.rotation.y)) * Math.min(1, dt * 12);
      const sit = f.sitting && !walking;
      f.person.body.position.y = sit ? -0.32 : 0;
      const swing = walking ? Math.sin(t * 11 + f.phase) * 0.55 : 0;
      f.person.parts.legL.rotation.x = sit ? -1.35 : swing;
      f.person.parts.legR.rotation.x = sit ? -1.35 : -swing;
      f.person.parts.armL.rotation.x = sit ? -0.6 : -swing * 0.8;
      f.person.parts.armR.rotation.x = sit ? -0.6 : swing * 0.8;
      if (f.person.coinMesh && !reduceMotion) f.person.coinMesh.rotation.y = t * 1.5 + f.phase;
      const s = f === hovered || f === selected ? 1.18 : 1;
      r.scale.set(s, s, s);

      // ออกจากฉากเมื่อเดินถึงทางเข้า
      if (f.leaving && !walking) {
        world.remove(r);
        pickables.splice(pickables.indexOf(r), 1);
        figures.delete(f.key);
        if (selected === f) { selected = null; selectionBox.textContent = HINT; }
      }
    }

    if (selected && figures.has(selected.key)) {
      ring.visible = selected.person.root.visible;
      ring.position.set(selected.x, 0.05, selected.z);
    } else ring.visible = false;

    // พื้นที่รอที่ "เกินเป้า" กะพริบเบาๆ
    for (const zo of Object.values(zoneObjects)) {
      zo.waitMat.emissiveIntensity = zo.status === "late" && !reduceMotion ? 0.22 + 0.22 * Math.sin(t * 3.2) : 0;
    }

    controls.update();
    renderer.render(scene, camera);

    for (const zone of Object.values(ZONES)) {
      placeLabel(cards[zone.key], zone.roomCenter[0], 5.2, zone.roomCenter[1], true);
    }
    placeLabel(entranceLabel, ENTRANCE[0], 4.4, ENTRANCE[1], true);
  });

  // ---------- ข้อมูล ----------
  const start = data => document.fonts.load("600 24px Kanit").catch(() => { }).finally(() => { build(data); applyVisibility(); });
  document.addEventListener("dashboard:data", e => start(e.detail));
  if (window.__dashboardData) start(window.__dashboardData);
}
