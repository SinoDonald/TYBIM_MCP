import { rm } from 'node:fs/promises';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
const build = resolve(fileURLToPath(new URL('../build/', import.meta.url)));
// Generated JS only. Never leave a deleted private source's compiled module
// available for the optional runtime loader after the next build.
for (const name of ['private', 'upstream']) {
  const target = resolve(build, name);
  if (dirname(target) !== build) throw new Error('Invalid generated cleanup target');
  await rm(target, { recursive: true, force: true });
}
