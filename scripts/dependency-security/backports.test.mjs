import assert from 'node:assert/strict';
import { constants, createHash, generateKeyPairSync, privateEncrypt, sign } from 'node:crypto';
import { cpSync, mkdtempSync, readFileSync, realpathSync, rmSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { basename, dirname, join } from 'node:path';
import test from 'node:test';
import { evaluateAudit } from './audit.mjs';
import { applyPatches, manifest, root, verifyPatch } from './patches.mjs';

const require = createRequire(import.meta.url);
const braces = require('braces');
const forge = require('node-forge');
const pattern = '{'.repeat(4000) + 'a,b' + '}'.repeat(4000);
const nestingError = { name: 'SyntaxError', message: /safe nesting limit/ };

for (const method of ['parse', 'compile', 'expand', 'stringify']) {
  test('braces.' + method + ' rejects excessive nesting before recursion', () => {
    assert.throws(() => braces[method](pattern), nestingError);
  });
}

test('braces bounds caller-provided ASTs and cycles', () => {
  let ast = { type: 'text', value: 'x' };
  for (let depth = 0; depth < 1000; depth++) ast = { type: 'root', nodes: [ast] };
  for (const method of ['compile', 'stringify', 'expand']) assert.throws(() => braces[method](ast), nestingError);
  const cycle = { type: 'root', nodes: [] };
  cycle.nodes.push(cycle);
  assert.throws(() => braces.compile(cycle), nestingError);
});

test('braces preserves ordinary ranges, nested alternatives, escapes and wide patterns', () => {
  assert.deepEqual(braces.expand('src/{api,worker}/{a,b}.js'), ['src/api/a.js', 'src/api/b.js', 'src/worker/a.js', 'src/worker/b.js']);
  assert.deepEqual(braces.expand('item-{01..03}'), ['item-01', 'item-02', 'item-03']);
  assert.equal(braces.compile('a/{b,c}/d'), 'a/(b|c)/d');
  assert.equal(braces.stringify(braces.parse('a/{b,c}/d')), 'a/{b,c}/d');
  assert.deepEqual(braces.expand(String.raw`a/\{b,c\}/d`), ['a/{b,c}/d']);
  assert.equal(braces.expand('{' + Array.from({ length: 500 }, (_, i) => 'n' + i).join(',') + '}').length, 500);
});

const { publicKey, privateKey } = generateKeyPairSync('rsa', { modulusLength: 2048 });
const verifier = forge.pki.publicKeyFromPem(publicKey.export({ type: 'spki', format: 'pem' }));
const message = Buffer.from('SentinelLAN dependency signature regression');
const hash = createHash('sha256').update(message).digest();
const oid = Buffer.from('0609608648016503040201', 'hex');
const nullParameter = Buffer.from('0500', 'hex');
function der(tag, value) {
  assert.ok(value.length < 128);
  return Buffer.concat([Buffer.from([tag, value.length]), value]);
}
function signature(parameters, extra = Buffer.alloc(0)) {
  const algorithm = der(0x30, Buffer.concat([oid, parameters, extra]));
  const digestInfo = der(0x30, Buffer.concat([algorithm, der(0x04, hash)]));
  const encoded = Buffer.concat([Buffer.from([0, 1]), Buffer.alloc(256 - digestInfo.length - 3, 0xff), Buffer.from([0]), digestInfo]);
  return privateEncrypt({ key: privateKey, padding: constants.RSA_NO_PADDING }, encoded).toString('binary');
}

test('forge accepts valid Node RSA signatures and SHA-256 with optional NULL parameters', () => {
  assert.equal(verifier.verify(hash.toString('binary'), sign('sha256', message, privateKey).toString('binary')), true);
  assert.equal(verifier.verify(hash.toString('binary'), signature(Buffer.alloc(0))), true);
});

test('forge rejects a signed DigestAlgorithm with an extra nested element', () => {
  assert.throws(() => verifier.verify(hash.toString('binary'), signature(nullParameter, der(0x04, Buffer.from('garbage')))), /DigestInfo/);
});

test('forge rejects nonempty ASN.1 NULL parameters', () => {
  assert.throws(() => verifier.verify(hash.toString('binary'), signature(Buffer.from('0501ff', 'hex'))), /DigestInfo/);
});

test('the audit verifies every affected installed copy before accepting a backport', () => {
  for (const patch of manifest) assert.equal(verifyPatch(root, 'node_modules/' + patch.name, patch), true);
  const temporary = mkdtempSync(join(tmpdir(), 'sentinellan-dependency-test-'));
  try {
    const target = join(temporary, 'node_modules', 'braces');
    cpSync(join(root, 'node_modules', 'braces'), target, { recursive: true });
    writeFileSync(join(temporary, 'package-lock.json'), JSON.stringify({ packages: {
      'node_modules/braces': { version: '3.0.3' }, 'node_modules/node-forge': { version: '1.4.0' }
    } }));
    assert.equal(applyPatches(temporary), 0);
    const file = join(target, 'lib', 'parse.js');
    writeFileSync(file, readFileSync(file, 'utf8') + '\n// altered source\n');
    assert.throws(() => verifyPatch(temporary, 'node_modules/braces', manifest[0]), /missing or changed/);
    assert.throws(() => applyPatches(temporary), /Unexpected upstream source/);
    assert.throws(() => verifyPatch(root, '../braces', manifest[0]), /Invalid dependency location/);
    writeFileSync(file, readFileSync(join(root, 'node_modules/braces/lib/parse.js')));
    writeFileSync(join(target, 'package.json'), JSON.stringify({ name: 'braces', version: '99.0.0' }));
    assert.throws(() => verifyPatch(temporary, 'node_modules/braces', manifest[0]), /Unsupported dependency version/);
  } finally {
    const cleanup = realpathSync(temporary);
    if (dirname(cleanup) !== realpathSync(tmpdir()) || !basename(cleanup).startsWith('sentinellan-dependency-test-')) {
      throw new Error('Refusing to remove a directory outside this test fixture');
    }
    rmSync(cleanup, { recursive: true, force: true });
  }
});

function report(via, nodes = ['node_modules/braces']) {
  return { auditReportVersion: 2, vulnerabilities: { braces: { via, nodes } } };
}

test('the audit still blocks a new high advisory in a backported package', () => {
  let verified = 0;
  const known = { url: manifest[0].advisory, severity: 'high' };
  const fresh = { url: 'https://example.test/new-advisory', severity: 'high' };
  const outcome = evaluateAudit(report([known, fresh]), () => { verified++; return true; });
  assert.equal(verified, 1);
  assert.equal(outcome.blocked, true);
  assert.deepEqual(outcome.remaining, [{ name: 'braces', advisory: fresh.url, severity: 'high' }]);
});

test('the audit refuses incomplete reports and unverified backports', () => {
  assert.throws(() => evaluateAudit({ error: { code: 'network-error' } }), /valid vulnerability report/);
  const known = { url: manifest[0].advisory, severity: 'high' };
  assert.throws(() => evaluateAudit(report([known]), () => { throw new Error('missing patch'); }), /missing patch/);
  assert.throws(() => evaluateAudit(report([known]), () => false), /not verified/);
  assert.equal(evaluateAudit(report([known], [])).blocked, true);
  assert.throws(() => evaluateAudit(report(['missing-dependency'])), /Audit dependency is missing/);
  assert.throws(() => evaluateAudit(report(['braces'])), /no root advisories/);
});
