import { spawnSync } from 'node:child_process';
import { pathToFileURL } from 'node:url';
import { manifest, root, verifyPatch } from './patches.mjs';

const ranks = { info: 0, low: 1, moderate: 2, high: 3, critical: 4 };

export function evaluateAudit(report, verify = verifyPatch) {
  if (report.error || report.auditReportVersion !== 2 || !report.vulnerabilities) {
    throw new Error('npm did not return a valid vulnerability report');
  }
  const vulnerabilities = report.vulnerabilities;
  const patched = [];
  const remaining = [];
  let advisories = 0;
  for (const [name, vulnerability] of Object.entries(vulnerabilities)) {
    if (!Array.isArray(vulnerability.via) || !Array.isArray(vulnerability.nodes)) {
      throw new Error('Invalid audit entry: ' + name);
    }
    for (const advisory of vulnerability.via) {
      if (typeof advisory === 'string') {
        if (!vulnerabilities[advisory]) throw new Error('Audit dependency is missing: ' + advisory);
        continue;
      }
      if (!advisory.url || !(advisory.severity in ranks)) throw new Error('Invalid npm advisory');
      advisories++;
      const patch = manifest.find(item => item.name === name && item.advisory === advisory.url);
      if (patch && vulnerability.nodes.length) {
        for (const node of vulnerability.nodes) {
          if (verify(root, node, patch) !== true) throw new Error('Security backport is not verified');
        }
        patched.push({ name, advisory: advisory.url, version: patch.version });
      } else {
        remaining.push({ name, advisory: advisory.url, severity: advisory.severity });
      }
    }
  }
  if (Object.keys(vulnerabilities).length && !advisories) throw new Error('Audit report has no root advisories');
  return { patched, remaining, blocked: remaining.some(item => ranks[item.severity] >= ranks.high) };
}

function run() {
  const npm = process.env.npm_execpath;
  if (!npm) throw new Error('Run this check with npm run audit:dependencies');
  // Exercise the installed code before treating an advisory as remediated.
  const tests = spawnSync(process.execPath, ['--test', 'scripts/dependency-security/backports.test.mjs'], {
    cwd: root, stdio: 'inherit', timeout: 30000
  });
  if (tests.error || tests.status !== 0) throw new Error('Dependency security regression tests failed');
  const result = spawnSync(process.execPath, [npm, 'audit', '--omit=dev', '--json'], {
    cwd: root, encoding: 'utf8', timeout: 120000, maxBuffer: 16 * 1024 * 1024
  });
  if (result.error || ![0, 1].includes(result.status)) throw new Error('npm audit could not complete');
  const outcome = evaluateAudit(JSON.parse(result.stdout));
  for (const patch of outcome.patched) {
    console.log('Verified source backport: ' + patch.name + '@' + patch.version + ' ' + patch.advisory);
  }
  for (const advisory of outcome.remaining) {
    console.log('Unpatched ' + advisory.severity + ': ' + advisory.name + ' ' + advisory.advisory);
  }
  if (outcome.blocked) process.exitCode = 1;
  else console.log('Dependency audit passed: no unpatched high or critical advisories.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) run();
