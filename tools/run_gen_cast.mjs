// Local runner for docs/designs/design_handoff_cast_sprites/art/gen_cast.js — the
// wardrobe source of truth, which expects (createCanvas, saveFile, log) in scope
// (it was authored for a design-tool harness; see its header). This shim provides
// the minimal 2D-canvas surface the generator actually uses (fillRect and the
// 2-arg / 8-arg drawImage forms, nearest-neighbour) plus a raw RGBA PNG writer,
// so "changing clothes is a spec edit + re-run" works from the repo:
//
//     node tools/run_gen_cast.mjs <out-dir>
//
// Everything the generator saves lands under <out-dir>/art/. Copy ONLY the
// atlas(es) whose pixels changed into assets/sprites/ (and the handoff art dir) —
// recompressing byte-identical-pixel PNGs churns git for nothing. Re-import after:
//     godot-mono --headless --import
import { readFile, writeFile, mkdir } from 'node:fs/promises';
import { deflateSync } from 'node:zlib';
import { dirname, join } from 'node:path';

const outRoot = process.argv[2];
if (!outRoot) {
  console.error('usage: node tools/run_gen_cast.mjs <out-dir>');
  process.exit(1);
}
const srcUrl = new URL('../docs/designs/design_handoff_cast_sprites/art/gen_cast.js', import.meta.url);

function parseColor(style) {
  const hex = style.replace('#', '');
  return [
    parseInt(hex.slice(0, 2), 16),
    parseInt(hex.slice(2, 4), 16),
    parseInt(hex.slice(4, 6), 16),
    255,
  ];
}

function createCanvas(width, height) {
  const data = new Uint8Array(width * height * 4);
  const canvas = { width, height, data };
  const put = (x, y, r, g, b, a) => {
    if (x < 0 || y < 0 || x >= width || y >= height) return;
    const i = (y * width + x) * 4;
    data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = a;
  };
  // Real canvas keeps the previous fillStyle when assigned an invalid value; the
  // generator leans on that (one call passes a number), so the setter mirrors it.
  let fillStyle = '#000000';
  const ctx = {
    imageSmoothingEnabled: false,
    get fillStyle() { return fillStyle; },
    set fillStyle(v) {
      if (typeof v === 'string' && /^#[0-9a-fA-F]{6}$/.test(v)) fillStyle = v;
    },
    fillRect(x, y, w, h) {
      const [r, g, b, a] = parseColor(fillStyle);
      for (let yy = y; yy < y + h; yy++)
        for (let xx = x; xx < x + w; xx++) put(xx, yy, r, g, b, a);
    },
    drawImage(src, ...args) {
      let sx = 0, sy = 0, sw = src.width, sh = src.height, dx, dy, dw, dh;
      if (args.length === 2) {
        [dx, dy] = args; dw = sw; dh = sh;
      } else if (args.length === 8) {
        [sx, sy, sw, sh, dx, dy, dw, dh] = args;
      } else {
        throw new Error(`drawImage arity ${args.length + 1} not supported`);
      }
      for (let y = 0; y < dh; y++) {
        for (let x = 0; x < dw; x++) {
          const px = sx + Math.floor(x * sw / dw);
          const py = sy + Math.floor(y * sh / dh);
          const i = (py * src.width + px) * 4;
          if (src.data[i + 3] === 0) continue;   // source-over: skip transparent
          put(dx + x, dy + y, src.data[i], src.data[i + 1], src.data[i + 2], src.data[i + 3]);
        }
      }
    },
  };
  canvas.getContext = () => ctx;
  return canvas;
}

const CRC_TABLE = new Uint32Array(256).map((_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});
function crc32(buf) {
  let c = 0xffffffff;
  for (const b of buf) c = CRC_TABLE[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}
function chunk(type, payload) {
  const head = Buffer.alloc(8);
  head.writeUInt32BE(payload.length, 0);
  head.write(type, 4, 'ascii');
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(Buffer.concat([Buffer.from(type, 'ascii'), payload])), 0);
  return Buffer.concat([head, payload, crc]);
}

async function saveFile(relPath, canvas) {
  const { width, height, data } = canvas;
  const raw = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y++) {
    const row = y * (width * 4 + 1);
    raw[row] = 0;   // filter: none
    Buffer.from(data.buffer, y * width * 4, width * 4).copy(raw, row + 1);
  }
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;    // bit depth
  ihdr[9] = 6;    // RGBA
  const png = Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
  const outPath = join(outRoot, relPath);
  await mkdir(dirname(outPath), { recursive: true });
  await writeFile(outPath, png);
}

const src = await readFile(srcUrl, 'utf8');
const run = new Function('createCanvas', 'saveFile', 'log',
  `return (async () => { ${src}\n })();`);
await run(createCanvas, saveFile, (msg) => console.log(msg));
