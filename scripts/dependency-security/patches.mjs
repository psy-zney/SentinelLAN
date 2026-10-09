import { createHash } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
export const manifest = JSON.parse(readFileSync(new URL('./patches.json', import.meta.url), 'utf8'));
export const digest = content => createHash('sha256').update(content).digest('hex');

function packageDirectory(workspace, node) {
  if (!node.startsWith('node_modules/') || node.split('/').includes('..')) {
    throw new Error('Invalid dependency location: ' + node);
  }
  const directory = resolve(workspace, node);
  const path = relative(workspace, directory);
  if (isAbsolute(path) || path.startsWith('..')) throw new Error('Dependency escapes the workspace');
  return directory;
}

export function verifyPatch(workspace, node, patch) {
  const directory = packageDirectory(workspace, node);
  const metadata = JSON.parse(readFileSync(resolve(directory, 'package.json'), 'utf8'));
  if (metadata.name !== patch.name || metadata.version !== patch.version) {
    throw new Error('Unsupported dependency version at ' + node);
  }
  for (const file of patch.files) {
    const actual = digest(readFileSync(resolve(directory, file.path)));
    if (actual !== file.afterSha256) throw new Error('Security patch missing or changed: ' + node + '/' + file.path);
  }
  return true;
}

export function applyPatches(workspace = root) {
  const lock = JSON.parse(readFileSync(resolve(workspace, 'package-lock.json'), 'utf8'));
  let applied = 0;
  for (const patch of manifest) {
    for (const node of Object.keys(lock.packages)) {
      if (!node.endsWith('node_modules/' + patch.name)) continue;
      const directory = packageDirectory(workspace, node);
      // A Docker build may install only one web workspace, without the mobile tooling.
      if (!existsSync(directory)) continue;
      const metadata = JSON.parse(readFileSync(resolve(directory, 'package.json'), 'utf8'));
      if (metadata.name !== patch.name || metadata.version !== patch.version) {
        throw new Error('Review the security backport before updating ' + node);
      }
      for (const file of patch.files) {
        const target = resolve(directory, file.path);
        let content = existsSync(target) ? readFileSync(target, 'utf8') : null;
        if (content !== null && digest(content) === file.afterSha256) continue;
        if ((content === null ? null : digest(content)) !== file.beforeSha256) {
          throw new Error('Unexpected upstream source: ' + node + '/' + file.path);
        }
        if (file.source) {
          content = readFileSync(new URL(file.source, import.meta.url), 'utf8');
        } else {
          for (const replacement of file.replacements) {
            if (content.split(replacement.from).length !== 2) {
              throw new Error('Security patch hunk does not match exactly once');
            }
            content = content.replace(replacement.from, replacement.to);
          }
        }
        if (digest(content) !== file.afterSha256) throw new Error('Security patch checksum mismatch');
        writeFileSync(target, content);
        applied++;
      }
      verifyPatch(workspace, node, patch);
    }
  }
  return applied;
}
