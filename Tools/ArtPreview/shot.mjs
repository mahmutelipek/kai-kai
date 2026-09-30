// Usage: node shot.mjs <scene.json> <out.png> [--width 1280] [--height 720] [--analyze out.json] [--masks tagPrefix]
// Renders an ArtPreview scene in headless Chromium (SwiftShader WebGL) and optionally measures per-object pixels.
import { chromium } from 'playwright-core';
import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const opt = (name, def) => { const i = args.indexOf('--' + name); return i >= 0 ? args[i + 1] : def; };
const [scenePath, outPath] = args;
const width = +opt('width', 1280), height = +opt('height', 720);
const analyzePath = opt('analyze', null), masks = opt('masks', null);
const refColor = opt('ref', null) ? opt('ref').split(',').map(Number) : null;

const exe = process.env.CHROMIUM || '/opt/pw-browsers/chromium-1194/chrome-linux/chrome';
const browser = await chromium.launch({ executablePath: exe, args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--allow-file-access-from-files'] });
const page = await browser.newPage({ viewport: { width, height } });
page.on('console', m => console.log('page:', m.text()));
page.on('pageerror', e => console.log('page error:', e.message));
await page.goto('file://' + path.resolve(path.dirname(new URL(import.meta.url).pathname), 'render.html'));
await page.waitForFunction('window.ready === true', null, { timeout: 30000 });
const scene = JSON.parse(fs.readFileSync(scenePath, 'utf8'));
const result = await page.evaluate(([s, o]) => window.renderScene(s, o), [scene, { width, height, analyze: !!analyzePath, masks, refColor }]);
if (result.png) fs.writeFileSync(outPath, Buffer.from(result.png.split(',')[1], 'base64'));
else await page.screenshot({ path: outPath });
if (analyzePath) { delete result.png; fs.writeFileSync(analyzePath, JSON.stringify(result, null, 1)); }
await browser.close();
console.log('wrote', outPath);
