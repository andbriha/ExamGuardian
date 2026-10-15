import { readdir, readFile } from 'node:fs/promises';
import { JSDOM } from 'jsdom';

const { window } = new JSDOM('<!doctype html><html><body></body></html>');
globalThis.window = window;
globalThis.document = window.document;
const { default: mermaid } = await import('mermaid');

mermaid.initialize({ startOnLoad: false, securityLevel: 'strict' });
const pages = new URL('../src/pages/docs/', import.meta.url);
let count = 0;
let failures = 0;
for (const name of await readdir(pages)) {
  if (!name.endsWith('.mdx')) continue;
  const source = await readFile(new URL(name, pages), 'utf8');
  for (const match of source.matchAll(/<Mermaid source=\{("(?:\\.|[^"\\])*")\} \/>/g)) {
    count++;
    try { await mermaid.parse(JSON.parse(match[1])); }
    catch (error) { failures++; console.error(`${name}, diagram ${count}: ${error.message}`); }
  }
}
console.log(`${count} diagrams checked; ${failures} failures.`);
if (failures || !count) process.exitCode = 1;
